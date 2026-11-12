using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CarcavelosCircuit.Visuals.Editor
{
    // Builds Luc's Carcavelos campus world (Role 3, tasks V4 and V8).
    // Input: Assets/_Project/Visuals/CampusWorld/{Models,Textures,Data}, exported by the world generator
    // (tools/world_gen/export_unity_bundle.py). Output, all made through the Unity API:
    // - URP Lit materials, the painted sky and the look profile, and one prefab per tree/prop kind;
    // - CampusWorld.prefab: everything you see and drive on (world with colliders, track surface, about 1,070 trees,
    //   props and furniture, the golden-hour sun, the look volume, the sky light). Role 2 puts this one prefab into
    //   Track_Campus; a new export updates it there by itself;
    // - Track_CampusWorld.unity: Role 3's preview = the prefab plus the race placeholders (checkpoints, spawn) that
    //   RaceFlow already understands, and a preview camera. The real checkpoints are Role 2's (from Data/track.json).
    // Axes: x = east, y = up, z = north, 1 unit = 1 m. Run it again after every new export.
    public static class CampusWorldBuilder
    {
        private const string Root = "Assets/_Project/Visuals/CampusWorld";
        private const string ModelsDir = Root + "/Models";
        private const string TexDir = Root + "/Textures";
        private const string DataDir = Root + "/Data";
        private const string MatDir = Root + "/Materials";
        private const string PrefabDir = Root + "/Prefabs";
        private const string SettingsDir = Root + "/Settings";
        private const string SkyTexture = TexDir + "/sky_panorama.png";       // world-gen/make_sky.py
        private const string LookProfile = SettingsDir + "/CampusWorld_Look.asset";
        // Golden hour (design pass 2026-11-09): low warm sun from the west-south-west. Must match make_sky.py.
        // V18l (Luc, 12 Nov: "summer at the beach", much harder shadows with contrast): a high summer-afternoon sun
        // from the south-west instead of golden hour. Must match make_sky.py (and so make_shadowmap.py).
        private const float SunAzimuth = 225f, SunElevation = 42f;
        // V17: warm sea haze = the painted sky's colour just above the horizon (sky_panorama.png, about #E0D3BC),
        // a little darker so far walls do not glow. 0.0008 per metre: 21 % at 300 m, 55 % at 1 km.
        private static readonly Color HazeColour = new Color(0.80f, 0.86f, 0.92f);   // V18l: the summer sky's pale blue horizon
        private const float HazeDensity = 0.0008f;
        private const string ScenePath = "Assets/_Project/Visuals/Scenes/Track_CampusWorld.unity";
        private const string WorldPrefabPath = Root + "/CampusWorld.prefab";
        private const string WorldRootName = "CampusWorld";                  // root of the prefab and of its instance
        private static readonly string[] Models = { "CampusWorld", "TrackSurface", "RouteGuide", "Dressing_Prototypes" };

        [Serializable] private class Emission { public float[] colour; public float strength; }
        [Serializable] private class MatInfo
        {
            public string name; public float[] colour; public float smoothness; public float metallic; public float alpha;
            public string albedo; public string metallic_smoothness; public string normal; public float normal_strength;
            public Emission emission;
            public float alpha_clip;      // > 0: leaf cutout, pixels with albedo alpha below this are not drawn
            public bool double_sided;     // draw both faces (single-sheet leaves)
            public Water water;           // the moving sea (Sea.shader); without it or its shader: plain URP Lit
            public Wind wind;             // swaying trees (Foliage.shader); without it or its shader: plain URP Lit
            public Ground ground;         // detailed lawn and sand (Ground.shader); without it or its shader: plain URP Lit
            // V16h begin
            public WorldLit world_lit;    // vertex ambient occlusion + baked sun shadow (WorldLit.shader); else plain URP Lit
            public bool tint;             // the texture is multiplied by the colour (one neutral texture, walls of many colours)
            // V16h end
        }
        // V16h begin: the two ground colour maps whose alpha is the baked sun shadow, and the world rectangles they cover
        [Serializable] private class WorldLit
        {
            public string shader; public bool ground_shadow; public bool always_shaded;
            public float[] world_uv;      // meshes without UVs: tile metres across, up, roof scale (texture read by world position)
        }
        [Serializable] private class Shade { public string world_map; public string near_map; public float[] world_rect; public float[] near_rect; }
        // V16h end
        [Serializable] private class Ground { public string shader; public string mask; public string detail; public string detail_normal; }
        [Serializable] private class Wind
        {
            public string shader; public float bend; public float flutter;
            public bool upright;          // grass cards: both faces lit like the lawn they stand on
            public float shrink_from, shrink_to;   // grass cards sink into the ground between these distances (m); 0 = never
        }
        [Serializable] private class Tuft { public float x, z, yaw, s; public int r = 255, g = 255, b = 255, big, far; }
        [Serializable] private class TuftData { public Tuft[] tufts; }
        [Serializable] private class Water
        {
            public string shader; public string normal; public string shore_map;
            public float[] shore_rect;    // world x0, z0, x1, z1 covered by the shore map
            public float shore_range; public float coast;
            public float[] coast_west;    // far coast west of the shore map: world x, its z there, metres north per metre west
        }
        [Serializable] private class MatList { public MatInfo[] materials; public Shade shade; }
        [Serializable] private class Item { public string name; public string proto; public float x, y, z, yaw, roll, scale; }
        [Serializable] private class Dressing { public Item[] items; }
        [Serializable] private class TrackPoint { public float x, y, z, width, s; public string zone; public bool tunnel; }
        [Serializable] private class TrackData
        {
            public float length_m; public int jump_index, start_index, spawn_index; public int[] checkpoints; public TrackPoint[] points;
        }
        [Serializable] private class BoxCheck { public string name; public float[] min; public float[] max; }
        [Serializable] private class CheckData { public BoxCheck[] instances; public BoxCheck[] objects; }

        [MenuItem("Carcavelos Circuit/Build Campus World (Role 3)")]
        public static void Build()
        {
            ConfigureImporters();
            Dictionary<string, Material> materials = BuildMaterials();
            RemapModelMaterials(materials);
            Dictionary<string, GameObject> prefabs = BuildPrefabs();
            BuildScene(prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log("CampusWorldBuilder: " + ScenePath + " built.");
        }

        // Batch mode: -executeMethod CarcavelosCircuit.Visuals.Editor.CampusWorldBuilder.BuildAndCheck
        // [-campusWorldShots <folder>] builds, runs the self-test, optionally renders a few views, exits 0/1.
        public static void BuildAndCheck()
        {
            int failures = 0;
            try
            {
                Build();
                failures = SelfTest();
                string shots = Argument("-campusWorldShots");
                if (!string.IsNullOrEmpty(shots))
                {
                    RenderShots(shots);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
            }
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        // ---------- importers ----------
        private static void ConfigureImporters()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                {
                    continue;
                }
                string file = Path.GetFileName(path);
                if (path == SkyTexture)
                {
                    continue;                                                   // a cubemap, see below
                }
                bool normal = file.EndsWith("_normal.png", StringComparison.Ordinal);
                bool mask = file.EndsWith("_metallic_smoothness.png", StringComparison.Ordinal);
                bool big = file.StartsWith("ground_colour", StringComparison.Ordinal) || file.StartsWith("mural", StringComparison.Ordinal);
                bool cutout = file.EndsWith("_cutout_albedo.png", StringComparison.Ordinal);   // alpha = leaf outline
                bool data = file.EndsWith("_data.png", StringComparison.Ordinal);              // numbers, not colour
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !(normal || mask || data);
                bool shadeAlpha = file.StartsWith("ground_colour", StringComparison.Ordinal);   // V16h: alpha = baked sun shadow
                ti.alphaSource = mask || cutout || data || shadeAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                ti.alphaIsTransparency = cutout;                                 // no dark fringe around the leaflets
                ti.mipMapsPreserveCoverage = cutout;                             // thin leaflets do not vanish far away
                ti.alphaTestReferenceValue = 0.5f;
                ti.maxTextureSize = big ? 2048 : 1024;
                bool tiles = !data || file.Contains("_detail_");                               // data maps cover the world once; detail repeats
                ti.wrapMode = file.StartsWith("ground_colour", StringComparison.Ordinal) || cutout || !tiles ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                ti.mipmapEnabled = true;
                ti.anisoLevel = file.Contains("_detail_") ? 8 : 4;               // ground detail is seen at very flat angles
                ti.SaveAndReimport();
            }
            // The painted sky (2:1 lat-long picture) becomes a cubemap: the skybox and the reflections on glass
            // and gloss use it, so no lighting bake is needed.
            if (AssetImporter.GetAtPath(SkyTexture) is TextureImporter sky)
            {
                sky.textureType = TextureImporterType.Default;
                sky.textureShape = TextureImporterShape.TextureCube;
                var cube = new TextureImporterSettings();
                sky.ReadTextureSettings(cube);
                cube.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                cube.cubemapConvolution = TextureImporterCubemapConvolution.Specular;   // blurred mips: glossy reflections
                sky.SetTextureSettings(cube);
                sky.sRGBTexture = true;
                sky.mipmapEnabled = true;
                sky.wrapMode = TextureWrapMode.Clamp;
                sky.maxTextureSize = 1024;                                      // per cube face
                sky.SaveAndReimport();
            }
            foreach (string name in Models)
            {
                ModelImporter mi = AssetImporter.GetAtPath(ModelsDir + "/" + name + ".fbx") as ModelImporter;
                if (mi == null)
                {
                    continue;
                }
                mi.globalScale = 1f;
                mi.useFileScale = true;
                mi.importAnimation = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.importBlendShapes = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                mi.SaveAndReimport();
            }
        }

        // ---------- materials ----------
        private static Dictionary<string, Material> BuildMaterials()
        {
            MatList list = JsonUtility.FromJson<MatList>(File.ReadAllText(DataDir + "/materials.json"));
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogWarning("CampusWorldBuilder: URP Lit not found, using Standard.");
                lit = Shader.Find("Standard");
            }
            EnsureFolder(MatDir);
            var result = new Dictionary<string, Material>();
            foreach (MatInfo m in list.materials)
            {
                string path = MatDir + "/" + m.name + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool wantsWater = m.water != null && !string.IsNullOrEmpty(m.water.shader);
                Shader water = wantsWater ? Shader.Find(m.water.shader) : null;
                if (wantsWater && water == null)
                {
                    Debug.LogWarning("CampusWorldBuilder: shader " + m.water.shader + " not found, " + m.name + " stays flat URP Lit.");
                }
                bool wantsWind = m.wind != null && !string.IsNullOrEmpty(m.wind.shader);
                Shader foliage = wantsWind ? Shader.Find(m.wind.shader) : null;
                if (wantsWind && foliage == null)
                {
                    Debug.LogWarning("CampusWorldBuilder: shader " + m.wind.shader + " not found, " + m.name + " stays URP Lit without wind.");
                }
                bool wantsGround = m.ground != null && !string.IsNullOrEmpty(m.ground.shader);
                Shader ground = wantsGround ? Shader.Find(m.ground.shader) : null;
                if (wantsGround && ground == null)
                {
                    Debug.LogWarning("CampusWorldBuilder: shader " + m.ground.shader + " not found, " + m.name + " stays URP Lit without detail.");
                }
                // V16h begin
                bool wantsWorldLit = m.world_lit != null && !string.IsNullOrEmpty(m.world_lit.shader);
                Shader worldLit = wantsWorldLit ? Shader.Find(m.world_lit.shader) : null;
                if (wantsWorldLit && worldLit == null)
                {
                    Debug.LogWarning("CampusWorldBuilder: shader " + m.world_lit.shader + " not found, " + m.name + " stays URP Lit.");
                }
                // V16h end
                Shader shader = water != null ? water : foliage != null ? foliage : ground != null ? ground : worldLit != null ? worldLit : lit;
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                else
                {
                    mat.shader = shader;
                }
                if (shader != lit)
                {
                    ResetToShaderDefaults(mat);          // our own shaders: the .shader file is where the look is tuned
                }
                Texture2D albedo = Tex(m.albedo), normal = Tex(m.normal), mask = Tex(m.metallic_smoothness);
                // with a texture the tint stays white: the texture carries the colour (its average = the palette)
                // (V16h: unless the entry says "tint": then the neutral texture is multiplied by the colour)
                Color colour = albedo != null && !m.tint ? Color.white : new Color(m.colour[0], m.colour[1], m.colour[2], 1f);
                SetColor(mat, colour, "_BaseColor", "_Color");
                SetTexture(mat, albedo, "_BaseMap", "_MainTex");
                SetTexture(mat, normal, "_BumpMap");
                SetFloat(mat, normal != null ? m.normal_strength : 1f, "_BumpScale");
                Keyword(mat, "_NORMALMAP", normal != null);
                SetTexture(mat, mask, "_MetallicGlossMap");
                Keyword(mat, "_METALLICSPECGLOSSMAP", mask != null);
                SetFloat(mat, 0f, "_SmoothnessTextureChannel");                  // smoothness from the mask's alpha
                SetFloat(mat, mask != null ? 1f : Mathf.Clamp01(m.smoothness), "_Smoothness", "_Glossiness");
                SetFloat(mat, Mathf.Clamp01(m.metallic), "_Metallic");
                bool glow = m.emission != null && m.emission.strength > 0f && m.emission.colour != null && m.emission.colour.Length >= 3;
                Keyword(mat, "_EMISSION", glow);
                if (glow)
                {
                    float k = Mathf.Min(m.emission.strength, 3f);
                    SetColor(mat, new Color(m.emission.colour[0] * k, m.emission.colour[1] * k, m.emission.colour[2] * k, 1f), "_EmissionColor");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                // Leaf cutouts: URP Lit alpha clipping (opaque queue order, no blending, shadows follow the outline),
                // optionally both faces. Other materials are left as they are, unless they were a cutout before.
                bool clip = m.alpha_clip > 0f;
                if (clip || (mat.HasProperty("_AlphaClip") && mat.GetFloat("_AlphaClip") > 0.5f))
                {
                    SetFloat(mat, clip ? 1f : 0f, "_AlphaClip");
                    SetFloat(mat, clip ? m.alpha_clip : 0.5f, "_Cutoff");
                    Keyword(mat, "_ALPHATEST_ON", clip);
                    mat.SetOverrideTag("RenderType", clip ? "TransparentCutout" : "Opaque");
                    mat.renderQueue = clip ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : -1;
                }
                if (m.double_sided || (mat.HasProperty("_Cull") && mat.GetFloat("_Cull") < 1.5f))
                {
                    SetFloat(mat, m.double_sided ? 0f : 2f, "_Cull");           // 0 = both faces, 2 = back faces culled
                    mat.doubleSidedGI = m.double_sided;
                }
                if (foliage != null && mat.HasProperty("_Wind"))
                {
                    // Foliage.shader: metres of bend (tree top) and flutter (leaf tips); the direction stays the shader's.
                    Vector4 wind = mat.GetVector("_Wind");
                    mat.SetVector("_Wind", new Vector4(wind.x, wind.y, m.wind.bend, m.wind.flutter));
                    if (m.wind.upright)
                    {
                        SetFloat(mat, 0f, "_FlipBack");
                        SetFloat(mat, 1f, "_BackShade");
                    }
                    if (m.wind.shrink_to > m.wind.shrink_from && mat.HasProperty("_Shrink"))
                    {
                        mat.SetVector("_Shrink", new Vector4(m.wind.shrink_from, m.wind.shrink_to, 1f / (m.wind.shrink_to - m.wind.shrink_from), 0f));
                    }
                }
                if (ground != null)
                {
                    // Ground.shader: lawn / sand mask (same UVs as the colour map) and the tiling detail + its normal.
                    SetTexture(mat, Tex(m.ground.mask), "_GroundMask");
                    SetTexture(mat, Tex(m.ground.detail), "_DetailMap");
                    SetTexture(mat, Tex(m.ground.detail_normal), "_DetailNormal");
                    // V16h: the fine colour map over the lap (alpha of both maps = baked sun shadow)
                    if (list.shade != null && mat.HasProperty("_NearMap"))
                    {
                        SetTexture(mat, Tex(list.shade.near_map), "_NearMap");
                        mat.SetVector("_NearRect", RectVector(Tex(list.shade.near_map) != null ? list.shade.near_rect : null));
                    }
                }
                // V16h begin: WorldLit.shader reads the baked sun shadow from the ground maps by world position
                if (worldLit != null && list.shade != null)
                {
                    SetTexture(mat, Tex(list.shade.world_map), "_GroundWorldMap");
                    SetTexture(mat, Tex(list.shade.near_map), "_GroundNearMap");
                    mat.SetVector("_WorldRect", RectVector(list.shade.world_rect));
                    mat.SetVector("_NearRect", RectVector(Tex(list.shade.near_map) != null ? list.shade.near_rect : null));
                    bool takes = m.world_lit.ground_shadow && Tex(list.shade.world_map) != null;
                    mat.SetVector("_Shade", new Vector4(m.world_lit.always_shaded ? 1f : 0f, takes ? 1f : 0f, 0f, 0f));
                }
                if (worldLit != null)
                {
                    float[] wuv = m.world_lit.world_uv;
                    bool byWorld = wuv != null && wuv.Length >= 3 && wuv[0] > 0f && wuv[1] > 0f;
                    Keyword(mat, "_WORLD_UV", byWorld);
                    if (byWorld)
                    {
                        mat.SetVector("_WorldUV", new Vector4(1f / wuv[0], 1f / wuv[1], wuv[2], 0f));
                    }
                }
                // V16h end
                if (water != null)
                {
                    // Sea.shader: wave normal map, shore-distance map and the world rectangle that map covers.
                    SetTexture(mat, Tex(m.water.normal), "_WaveNormal");
                    SetTexture(mat, Tex(m.water.shore_map), "_ShoreMap");
                    float[] r = m.water.shore_rect;
                    if (r != null && r.Length >= 4)
                    {
                        mat.SetVector("_ShoreRect", new Vector4(r[0], r[1], 1f / (r[2] - r[0]), 1f / (r[3] - r[1])));
                    }
                    SetFloat(mat, m.water.shore_range, "_ShoreRange");
                    SetFloat(mat, m.water.coast, "_CoastZ");
                    float[] cw = m.water.coast_west;
                    if (cw != null && cw.Length >= 3 && mat.HasProperty("_CoastWest"))
                    {
                        mat.SetVector("_CoastWest", new Vector4(cw[0], cw[1], cw[2], 0f));
                    }
                }
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                result[m.name] = mat;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        // V16h: world rectangle (x0, z0, x1, z1) -> (x0, z0, 1 / width, 1 / depth) for the shaders; zero = no map
        private static Vector4 RectVector(float[] r)
        {
            return r != null && r.Length >= 4 && r[2] > r[0] && r[3] > r[1]
                ? new Vector4(r[0], r[1], 1f / (r[2] - r[0]), 1f / (r[3] - r[1])) : Vector4.zero;
        }

        // A saved material keeps its old numbers when a shader's defaults change. For our own shaders (sea, foliage,
        // ground) every number, vector and colour goes back to the shader's default first; the values from
        // materials.json are set afterwards.
        private static void ResetToShaderDefaults(Material mat)
        {
            Shader s = mat.shader;
            for (int i = 0; i < s.GetPropertyCount(); i++)
            {
                int id = s.GetPropertyNameId(i);
                switch (s.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        mat.SetFloat(id, s.GetPropertyDefaultFloatValue(i));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        mat.SetVector(id, s.GetPropertyDefaultVectorValue(i));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        mat.SetColor(id, s.GetPropertyDefaultVectorValue(i));
                        break;
                }
            }
        }

        private static void RemapModelMaterials(Dictionary<string, Material> materials)
        {
            foreach (string name in Models)
            {
                string path = ModelsDir + "/" + name + ".fbx";
                ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null)
                {
                    continue;
                }
                bool changed = false;
                foreach (Material embedded in AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Material>())
                {
                    if (materials.TryGetValue(embedded.name, out Material ours))
                    {
                        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), ours);
                        changed = true;
                    }
                    else
                    {
                        Debug.LogWarning("CampusWorldBuilder: no material data for " + embedded.name + " in " + name);
                    }
                }
                if (changed)
                {
                    mi.SaveAndReimport();
                }
            }
        }

        // ---------- prefabs: one per tree / prop kind ----------
        private static Dictionary<string, GameObject> BuildPrefabs()
        {
            EnsureFolder(PrefabDir);
            var result = new Dictionary<string, GameObject>();
            GameObject model = Model("Dressing_Prototypes");
            foreach (MeshFilter source in model.GetComponentsInChildren<MeshFilter>(true))
            {
                string kind = source.gameObject.name;
                var root = new GameObject(kind);
                var visual = new GameObject("Mesh");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localRotation = source.transform.rotation;      // the FBX axis change of this mesh
                visual.transform.localScale = source.transform.lossyScale;
                visual.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                visual.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
                AddDressingCollider(root, visual.GetComponent<MeshRenderer>().bounds, kind);
                result[kind] = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + kind + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
            }
            return result;
        }

        // Trees: a trunk-sized capsule. Lamps and parasols: a pole. Benches, bins, bollards, chairs, railings: a box.
        // Shrubs and grass: nothing (the kart drives through them).
        private static void AddDressingCollider(GameObject root, Bounds b, string kind)
        {
            if (kind.StartsWith("Veg_Shrub", StringComparison.Ordinal) || kind.StartsWith("Veg_Grass", StringComparison.Ordinal))
            {
                return;
            }
            if (kind.StartsWith("Veg_", StringComparison.Ordinal) || kind.StartsWith("Prop_Lamp", StringComparison.Ordinal) ||
                kind.StartsWith("Prop_Parasol", StringComparison.Ordinal))
            {
                CapsuleCollider c = root.AddComponent<CapsuleCollider>();
                c.radius = kind.StartsWith("Veg_", StringComparison.Ordinal) ? 0.35f : 0.15f;
                c.height = Mathf.Max(b.size.y, 1f);
                c.center = new Vector3(0f, c.height / 2f, 0f);
                return;
            }
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
        }

        // ---------- world prefab + preview scene ----------
        private static void BuildScene(Dictionary<string, GameObject> prefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(WorldRootName);

            GameObject world = Instance("CampusWorld", root.transform);
            world.name = "World";                                     // buildings, terrain, beach, tunnel (CampusWorld.fbx)
            foreach (MeshFilter mf in world.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mf.gameObject.name;
                // V10f: "Street_NoCollide_*" = road markings and low kerbs of the street section: to look at, not to hit.
                if (!n.StartsWith("Backdrop_", StringComparison.Ordinal) && !n.StartsWith("Tunnel_Lights", StringComparison.Ordinal)
                    && !n.StartsWith("Street_NoCollide", StringComparison.Ordinal) && !n.Contains("NoCollide"))
                {
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
                MarkStatic(mf.gameObject);
                HideIfColliderOnly(mf);
            }
            GameObject track = Instance("TrackSurface", root.transform);
            foreach (MeshFilter mf in track.GetComponentsInChildren<MeshFilter>(true))
            {
                // Edge lines and kerbs are paint, not bumps: a collider on the kerbs' ramp faces launched karts.
                // V10i: "...NoCollide" = to look at only (the stairs' visible steps); "...ColliderOnly" = to drive on or
                // to stop a kart, never drawn (the stairs' smooth driving surface, the guard strip behind the railings).
                if (!mf.gameObject.name.StartsWith("Track_EdgeLines", StringComparison.Ordinal)
                    && !mf.gameObject.name.StartsWith("Track_Kerbs", StringComparison.Ordinal)
                    && !mf.gameObject.name.Contains("NoCollide"))
                {
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
                MarkStatic(mf.gameObject);
                HideIfColliderOnly(mf);
            }
            GameObject guide = Instance("RouteGuide", root.transform);
            guide.SetActive(false);                                   // data for Role 2, never visible in the race

            var dressingRoot = new GameObject("Dressing");
            dressingRoot.transform.SetParent(root.transform, false);
            Dressing dressing = JsonUtility.FromJson<Dressing>(File.ReadAllText(DataDir + "/dressing.json"));
            var groups = new Dictionary<string, Transform>();
            foreach (Item it in dressing.items)
            {
                if (!prefabs.TryGetValue(it.proto, out GameObject prefab))
                {
                    Debug.LogWarning("CampusWorldBuilder: no prefab for " + it.proto);
                    continue;
                }
                if (!groups.TryGetValue(it.proto, out Transform group))
                {
                    group = new GameObject(it.proto).transform;
                    group.SetParent(dressingRoot.transform, false);
                    groups[it.proto] = group;
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                go.name = it.name;
                go.transform.SetPositionAndRotation(new Vector3(it.x, it.y, it.z),
                    Quaternion.Euler(0f, it.yaw, 0f) * Quaternion.Euler(0f, 0f, it.roll));
                go.transform.localScale = Vector3.one * it.scale;
                MarkStatic(go);
            }

            // Noah's Nova hall (task V12, help PR #45): replaces the old building block, white fence (Q19) with it.
            // Before the tunnel guards and the grass tufts, so both see the world as it is raced (no tufts in the hall).
            string hall = NovaHallInsert.Apply(root);
            if (hall != null)
            {
                Debug.Log("CampusWorldBuilder: " + hall);
            }
            // V14: the white fence stays out where the world has its own edge or the way must be open (fence_free.json).
            string fenceFree = FenceFreeZones.Apply(root);
            if (fenceFree != null)
            {
                Debug.Log("CampusWorldBuilder: " + fenceFree);
            }

            AddTunnelGuards(root, JsonUtility.FromJson<TrackData>(File.ReadAllText(DataDir + "/track.json")));
            AddGrassTufts(root);
            AddSunAndSky(root);
            // One prefab for everything you see and drive on. Saving over the old file keeps its GUID, so the
            // instance in Role 2's Track_Campus stays linked and shows the new world after every rebuild.
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, WorldPrefabPath, InteractionMode.AutomatedAction);

            // Preview only (never in the prefab): placeholders so the scene races on its own, and a camera.
            TrackData data = JsonUtility.FromJson<TrackData>(File.ReadAllText(DataDir + "/track.json"));
            var preview = new GameObject("Preview (Role 3)");
            AddRaceParts(preview, data);
            AddPreviewCamera(preview, data);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // The tunnel walls lean outwards at 45 degrees. A kart that touches such a wall is thrown up it like a ramp.
        // So along the tunnel an invisible upright wall (colliders only, nothing to see) stands at each wall foot,
        // 0.6 m outside the road edge: a kart now slides along it instead of climbing the mural.
        private static void AddTunnelGuards(GameObject root, TrackData data)
        {
            // The walls reach 1 m below the road as well, so on the sloping tunnel floor no wall ends above the ground
            // (a kart could wedge under such an end).
            const float outside = 0.6f, thickness = 0.3f, height = 3f, below = 1f;
            var guards = new GameObject("TunnelGuards");
            guards.transform.SetParent(root.transform, false);
            int n = data.points.Length;
            for (int i = 0; i < n; i++)
            {
                TrackPoint a = data.points[i], b = data.points[(i + 1) % n];
                if (!a.tunnel || !b.tunnel)
                {
                    continue;
                }
                Vector3 from = Pos(a), to = Pos(b);
                Vector3 along = to - from;
                Vector3 flat = new Vector3(along.x, 0f, along.z).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, flat);
                float half = Mathf.Max(a.width, b.width) * 0.5f + outside + thickness * 0.5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    var wall = new GameObject("Guard");
                    wall.transform.SetParent(guards.transform, false);
                    wall.transform.SetPositionAndRotation((from + to) * 0.5f + right * (side * half) + Vector3.up * (height * 0.5f - below),
                        Quaternion.LookRotation(along.normalized));
                    BoxCollider box = wall.AddComponent<BoxCollider>();
                    box.size = new Vector3(thickness, height, along.magnitude + 0.3f);
                    MarkStatic(wall);
                }
            }
        }

        private static void AddRaceParts(GameObject root, TrackData data)
        {
            var race = new GameObject("Race");
            race.transform.SetParent(root.transform, false);
            TrackInfo track = race.AddComponent<TrackInfo>();          // Role 2's track parts (T4)
            // Role 2's racing line (T8, request Q20): the road's centre points; start and direction come from checkpoint 0.
            var roadPoints = new List<Vector3>();
            for (int i = 0; i < data.points.Length; i++)
            {
                roadPoints.Add(Pos(data.points[i]));
            }
            race.AddComponent<TrackProgress>().SetRoadPoints(roadPoints);
            int count = data.checkpoints.Length;
            var checkpoints = new Checkpoint[count];
            for (int c = 0; c < count; c++)
            {
                int i = data.checkpoints[c];
                TrackPoint p = data.points[i];
                var cp = new GameObject(c == 0 ? "Checkpoint 0 (start-finish)" : "Checkpoint " + c);
                cp.transform.SetParent(race.transform, false);
                cp.transform.SetPositionAndRotation(Pos(p) + Vector3.up * 2f, Quaternion.LookRotation(Forward(data, i)));
                BoxCollider trigger = cp.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(p.width + 4f, 6f, 1f);
                int after = (i + 2) % data.points.Length;                             // about 4 m further on
                var respawn = new GameObject("RespawnPoint");
                respawn.transform.SetParent(cp.transform, false);
                respawn.transform.SetPositionAndRotation(Pos(data.points[after]) + Vector3.up * 0.5f,
                    Quaternion.LookRotation(Forward(data, after)));
                Checkpoint checkpoint = cp.AddComponent<Checkpoint>();
                var so = new SerializedObject(checkpoint);
                so.FindProperty("index").intValue = c;
                so.FindProperty("respawnPoint").objectReferenceValue = respawn.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
                checkpoints[c] = checkpoint;
            }
            var spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(race.transform, false);
            spawn.transform.SetPositionAndRotation(Pos(data.points[data.spawn_index]) + Vector3.up * 0.3f,
                Quaternion.LookRotation(Forward(data, data.spawn_index)));
            var tso = new SerializedObject(track);
            SerializedProperty list = tso.FindProperty("checkpoints");
            list.arraySize = count;
            for (int c = 0; c < count; c++)
            {
                list.GetArrayElementAtIndex(c).objectReferenceValue = checkpoints[c];
            }
            tso.FindProperty("spawnPoint").objectReferenceValue = spawn.transform;
            tso.ApplyModifiedPropertiesWithoutUndo();

            AddBoostPads(race, data);
        }

        // Boost pads (task V11, BoostPadPlan): two before the jump so the kart flies far, a chain in the tunnel.
        // Each pad sits on the road surface found by a ray and leans with it (the tunnel floor climbs a little).
        private static void AddBoostPads(GameObject race, TrackData data)
        {
            Material material = BoostPadMaterial();
            var group = new GameObject("BoostPads");
            group.transform.SetParent(race.transform, false);

            float[] s = data.points.Select(p => p.s).ToArray();
            var distances = new List<float>(BoostPadPlan.JumpPads(data.points[data.jump_index].s));
            int jumpPads = distances.Count;   // the first ones are the jump pads, they get the stronger boost
            bool[] covered = data.points.Select(p => p.tunnel).ToArray();
            foreach (int[] run in BoostPadPlan.CoveredRuns(covered))
            {
                distances.AddRange(BoostPadPlan.TunnelPads(s[run[0]], s[run[1]]));
            }

            Physics.SyncTransforms();
            for (int n = 0; n < distances.Count; n++)
            {
                int i = BoostPadPlan.ClosestPoint(s, data.length_m, distances[n]);
                bool jump = n < jumpPads;
                TrackPoint p = data.points[i];
                Vector3 position = Pos(p);
                Vector3 up = Vector3.up;
                if (Physics.Raycast(position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                {
                    position = hit.point;
                    up = hit.normal;
                }
                var go = new GameObject((jump ? "BoostPad jump s=" : "BoostPad s=") + p.s.ToString("0"));
                go.transform.SetParent(group.transform, false);
                go.transform.SetPositionAndRotation(position,
                    Quaternion.LookRotation(Vector3.ProjectOnPlane(Forward(data, i), up).normalized, up));
                go.AddComponent<BoostPad>().Configure(new Vector2(Mathf.Min(4.5f, p.width - 1f), 3f), material,
                    jump ? BoostPadPlan.JumpBoostSeconds : BoostPadPlan.TunnelBoostSeconds,
                    jump ? BoostPadPlan.JumpSpeedMultiplier : BoostPadPlan.TunnelSpeedMultiplier);
            }
            Debug.Log("CampusWorldBuilder: " + distances.Count + " boost pads.");
        }

        // URP Lit with emission on, so the Web build keeps the glowing shader variant. The chevrons come in code.
        private static Material BoostPadMaterial()
        {
            const string path = "Assets/_Project/Visuals/BoostPads/BoostPad.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(lit != null ? lit : Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.35f);
            material.SetColor("_EmissionColor", new Color(0.8f, 0.8f, 0.8f, 1f));
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }

        // 3D grass tufts (Data/grass_tufts.json from make_grass_tufts.py; nothing happens without that file or the
        // W_grass_tuft material). Each tuft is put on the ground with a ray; a tuft whose ray does not land on a plain
        // ground mesh (road, deck, roof, prop) is left out. A tuft = 3 small crosses of 2 upright cards (12 triangles),
        // or 4 crosses of 3 cards (24 triangles) for the "big" ones in the fringe at the track edge, with the grass
        // cutout, shifted 35 % towards the lawn colour under it (vertex colour; alpha = card height for the
        // shader's sink-away). All tufts of a 16 m cell are merged into one mesh, and a LODGroup hides the cell
        // beyond about 65 m (at LOD bias 1), so only the cells near the camera cost anything. No shadows cast.
        // V16h: tufts marked "far" are the far LOD: one cross of two cards (4 triangles), larger, in 32 m cells that stay
        // visible to 150 m (material W_grass_tuft_far: sinks away just before), so lawns keep texture further out.
        private const float TuftCell = 16f, TuftCullDistance = 65f, TuftFarCell = 32f, TuftFarCullDistance = 150f;
        private static readonly Color TuftLawn = new Color(143f / 255f, 179f / 255f, 122f / 255f).linear;   // the lawn the cutout was painted for
        private static readonly List<Vector3> tuftFeet = new List<Vector3>();
        private static int tuftsPlaced, tuftsLeftOut, tuftTriangles, tuftChunks;

        private static void AddGrassTufts(GameObject root)
        {
            tuftsPlaced = tuftsLeftOut = tuftTriangles = tuftChunks = 0;
            tuftFeet.Clear();
            string file = DataDir + "/grass_tufts.json", meshPath = ModelsDir + "/GrassTufts.asset";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/W_grass_tuft.mat");
            Material farMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/W_grass_tuft_far.mat");
            AssetDatabase.DeleteAsset(meshPath);
            if (!File.Exists(file) || mat == null)
            {
                return;
            }
            TuftData data = JsonUtility.FromJson<TuftData>(File.ReadAllText(file));
            Physics.SyncTransforms();
            var cells = new Dictionary<Vector2Int, List<Vector4>>();        // x, y, z, index
            for (int i = 0; i < data.tufts.Length; i++)
            {
                Tuft t = data.tufts[i];
                bool onGround = Physics.Raycast(new Vector3(t.x, 300f, t.z), Vector3.down, out RaycastHit hit, 600f, ~0, QueryTriggerInteraction.Ignore) &&
                                IsPlainGround(hit);
                if (!onGround)
                {
                    tuftsLeftOut++;
                    continue;
                }
                bool farTuft = t.far > 0;
                if (farTuft && farMat == null)
                {
                    tuftsLeftOut++;
                    continue;
                }
                float cellSize = farTuft ? TuftFarCell : TuftCell;
                // far cells get their own keys (x offset by 100000), so the two kinds never share a mesh
                var key = new Vector2Int(Mathf.FloorToInt(t.x / cellSize) + (farTuft ? 100000 : 0), Mathf.FloorToInt(t.z / cellSize));
                if (!cells.TryGetValue(key, out List<Vector4> list))
                {
                    cells[key] = list = new List<Vector4>();
                }
                list.Add(new Vector4(t.x, hit.point.y, t.z, i));
                tuftFeet.Add(hit.point);
                tuftsPlaced++;
            }
            var tuftRoot = new GameObject("GrassTufts");
            tuftRoot.transform.SetParent(root.transform, false);
            foreach (KeyValuePair<Vector2Int, List<Vector4>> cell in cells)
            {
                bool farCell = cell.Key.x > 50000;
                float size = farCell ? TuftFarCell : TuftCell;
                var origin = new Vector3((cell.Key.x - (farCell ? 100000 : 0) + 0.5f) * size, 0f, (cell.Key.y + 0.5f) * size);
                var verts = new List<Vector3>(); var normals = new List<Vector3>();
                var uv = new List<Vector2>(); var sway = new List<Vector2>(); var tris = new List<int>();
                var colours = new List<Color>();
                foreach (Vector4 p in cell.Value)
                {
                    Tuft t = data.tufts[(int)p.w];
                    var rng = new System.Random(7919 * (int)p.w + 17);
                    float R() => (float)rng.NextDouble();
                    Color here = new Color(t.r / 255f, t.g / 255f, t.b / 255f).linear;       // lawn colour under the tuft
                    var lawn = new Color(Mathf.Lerp(1f, Mathf.Clamp(here.r / TuftLawn.r, 0.6f, 1.5f), 0.35f),
                                         Mathf.Lerp(1f, Mathf.Clamp(here.g / TuftLawn.g, 0.6f, 1.5f), 0.35f),
                                         Mathf.Lerp(1f, Mathf.Clamp(here.b / TuftLawn.b, 0.6f, 1.5f), 0.35f));
                    int crosses = farCell ? 1 : t.big > 0 ? 4 : 3, cards = t.big > 0 && !farCell ? 3 : 2;
                    for (int cross = 0; cross < crosses; cross++)
                    {
                        float away = cross == 0 ? 0f : (0.1f + 0.14f * R()) * t.s, around = (t.yaw + cross * 120f + 60f * R()) * Mathf.Deg2Rad;
                        Vector3 foot = new Vector3(p.x, p.y, p.z) - origin + new Vector3(Mathf.Cos(around), 0f, Mathf.Sin(around)) * away;
                        float height = (0.15f + 0.12f * R()) * t.s, half = (0.15f + 0.07f * R()) * t.s, turn = R() * Mathf.PI;
                        float half_u = R() < 0.5f ? 0f : 0.5f;                          // which of the two tufts in the texture
                        for (int card = 0; card < cards; card++)
                        {
                            float a = turn + card * Mathf.PI / cards;
                            var side = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * half;
                            var lean = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * (0.06f * (R() - 0.5f));
                            int n = verts.Count;
                            verts.Add(foot - side + Vector3.down * 0.04f); verts.Add(foot + side + Vector3.down * 0.04f);
                            verts.Add(foot + side + lean + Vector3.up * height); verts.Add(foot - side + lean + Vector3.up * height);
                            uv.Add(new Vector2(half_u, 0f)); uv.Add(new Vector2(half_u + 0.5f, 0f));
                            uv.Add(new Vector2(half_u + 0.5f, 1f)); uv.Add(new Vector2(half_u, 1f));
                            sway.Add(Vector2.zero); sway.Add(Vector2.zero); sway.Add(new Vector2(0f, 1f)); sway.Add(new Vector2(0f, 1f));
                            for (int k = 0; k < 4; k++) normals.Add(Vector3.up);        // lit like the lawn under it
                            for (int k = 0; k < 4; k++) colours.Add(new Color(lawn.r, lawn.g, lawn.b, height + 0.04f));
                            tris.AddRange(new[] { n, n + 2, n + 1, n, n + 3, n + 2 });
                        }
                    }
                }
                var mesh = new Mesh { name = (farCell ? "GrassTuftsFar_" : "GrassTufts_") + (cell.Key.x - (farCell ? 100000 : 0)) + "_" + cell.Key.y };
                mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetUVs(1, sway);
                mesh.SetColors(colours);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                if (tuftChunks == 0)
                {
                    AssetDatabase.CreateAsset(mesh, meshPath);
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(mesh, meshPath);
                }
                var go = new GameObject(mesh.name);
                go.transform.SetParent(tuftRoot.transform, false);
                go.transform.position = origin;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = farCell ? farMat : mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                LODGroup lod = go.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(0.5f, new Renderer[] { r }) });
                lod.RecalculateBounds();
                // relative screen height of the cell at the cull distance, for a 60 degree camera
                float shown = Mathf.Clamp(lod.size / (2f * (farCell ? TuftFarCullDistance : TuftCullDistance) * Mathf.Tan(30f * Mathf.Deg2Rad)), 0.01f, 0.9f);
                lod.SetLODs(new[] { new LOD(shown, new Renderer[] { r }) });
                tuftChunks++;
                tuftTriangles += tris.Count / 3;
            }
            AssetDatabase.SaveAssets();
        }

        // Terrain, track corridor and shoulders: the triangle the ray hit is drawn with the ground material
        // (the corridor mesh also carries cut walls and other parts, so the whole mesh is not enough to go by).
        private static bool IsPlainGround(RaycastHit hit)
        {
            var mc = hit.collider as MeshCollider;
            MeshRenderer r = mc != null ? mc.GetComponent<MeshRenderer>() : null;
            if (r == null || mc.sharedMesh == null || hit.triangleIndex < 0)
            {
                return false;
            }
            Mesh mesh = mc.sharedMesh;
            Material[] mats = r.sharedMaterials;
            for (int sub = 0; sub < mesh.subMeshCount && sub < mats.Length; sub++)
            {
                UnityEngine.Rendering.SubMeshDescriptor d = mesh.GetSubMesh(sub);
                if (hit.triangleIndex * 3 >= d.indexStart && hit.triangleIndex * 3 < d.indexStart + d.indexCount)
                {
                    return mats[sub] != null && mats[sub].name == "W_ground_map";
                }
            }
            return false;
        }

        // Golden hour (design reference, 2026-11-09): a low warm sun from the west-south-west, long soft shadows,
        // cool blue fill, a painted sky with cumulus clouds (also the reflection source), and a gentle warm grade.
        // V17 "AAA" pass: a stronger sun against a darker, cooler fill (more contrast between sun and shade, like a
        // real late afternoon), a filmic tone curve, and a light warm sea haze for depth. No bake. Sun and look volume are part of the prefab. Sky light, sky and reflections are scene
        // settings, so CampusWorldLighting (on the prefab root) writes them into whichever scene holds the prefab while
        // that scene is the active one (the race makes the track scene active).
        private static void AddSunAndSky(GameObject root)
        {
            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(root.transform, false);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 2.1f;                                                    // V18l: hot summer sun (V17: 1.9)
            sun.color = new Color(1f, 0.95f, 0.86f);                                 // #FFF2DB, nearly white, a little warm
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;                                                 // V18l: full shadows, the fill lights the shade
            sun.shadowBias = 0.04f;                                                  // V17: no shadow acne on the long, flat paving
            sun.shadowNormalBias = 0.35f;
            float az = SunAzimuth * Mathf.Deg2Rad, el = SunElevation * Mathf.Deg2Rad;
            var towardsSun = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
            sunObject.transform.rotation = Quaternion.LookRotation(-towardsSun);

            Material skybox;
            Cubemap cube = AssetDatabase.LoadAssetAtPath<Cubemap>(SkyTexture);
            if (cube != null)
            {
                EnsureFolder(MatDir);
                string path = MatDir + "/Sky_Painted.mat";
                skybox = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (skybox == null)
                {
                    skybox = new Material(Shader.Find("Skybox/Cubemap"));
                    AssetDatabase.CreateAsset(skybox, path);
                }
                skybox.SetTexture("_Tex", cube);
                skybox.SetFloat("_Exposure", 1f);
                skybox.SetFloat("_Rotation", 0f);
                EditorUtility.SetDirty(skybox);
            }
            else
            {
                Debug.LogWarning("CampusWorldBuilder: no " + SkyTexture + ", using Unity's default sky.");
                skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            }
            CampusWorldLighting lighting = root.AddComponent<CampusWorldLighting>();
            var so = new SerializedObject(lighting);
            so.FindProperty("sun").objectReferenceValue = sun;
            // V17: the fill is darker than V8's (#B3C2DE / grey / #9E8C75), so shade reads as shade and the sun as sun;
            // only a little bluer (bluer still turned the tunnel and the hall cold); the ground bounce stays warm.
            // V18l: the shade is lit by a clear blue summer sky: darker and bluer than V17, so sun and shade stand
            // apart hard (cool blue shadows against hot, nearly white sunlight); the ground bounce stays warm.
            so.FindProperty("skyColour").colorValue = new Color(0.40f, 0.55f, 0.80f);       // #668CCC sky fill
            so.FindProperty("horizonColour").colorValue = new Color(0.52f, 0.58f, 0.66f);
            so.FindProperty("groundColour").colorValue = new Color(0.55f, 0.45f, 0.33f);    // warm bounce from the paving
            so.FindProperty("hazeColour").colorValue = HazeColour;                          // the sky's horizon colour
            so.FindProperty("hazeDensity").floatValue = HazeDensity;
            so.FindProperty("skybox").objectReferenceValue = skybox;
            so.FindProperty("reflection").objectReferenceValue = cube;                     // glass and gloss reflect the sky
            so.ApplyModifiedPropertiesWithoutUndo();
            lighting.ApplyIfActiveScene();                         // the preview scene is active here: save it with the look
            var look = new GameObject("Look (global volume)");
            look.transform.SetParent(root.transform, false);
            Volume volume = look.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = BuildLookProfile();
        }

        // Tonemapping + a grade, made by code so every laptop gets the same look. Bloom stays in Role 4's
        // RaceCameraVolume (Main.unity); the overrides do not overlap.
        // V17: ACES (the filmic curve of film and most console games) instead of Neutral: highlights roll off softly
        // instead of clipping, mid-tones get more contrast. ACES darkens the mid-tones, so the exposure goes up.
        // Shadows a little cooler and highlights a little warmer (golden hour), blue and orange stay clean.
        // V18l summer: more contrast and colour, no golden cast; shade pushed towards blue, light towards warm white.
        private const float LookExposure = 0.1f, LookContrast = 30f, LookSaturation = 22f;

        private static VolumeProfile BuildLookProfile()
        {
            EnsureFolder(SettingsDir);
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(LookProfile) != null)
            {
                AssetDatabase.DeleteAsset(LookProfile);
            }
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, LookProfile);
            Tonemapping tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            ColorAdjustments grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(LookExposure);
            grade.contrast.Override(LookContrast);
            grade.saturation.Override(LookSaturation);
            WhiteBalance warm = profile.Add<WhiteBalance>(true);
            warm.temperature.Override(2f);
            ShadowsMidtonesHighlights split = profile.Add<ShadowsMidtonesHighlights>(true);
            split.shadows.Override(new Vector4(0.93f, 0.98f, 1.10f, -0.07f));       // V18l: blue, deeper shade
            split.highlights.Override(new Vector4(1.03f, 1.0f, 0.96f, 0.02f));       // warm, bright sunlit tops
            foreach (VolumeComponent c in profile.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // For looking at the scene alone; RaceBootstrap switches track-scene cameras off in the race.
        private static void AddPreviewCamera(GameObject root, TrackData data)
        {
            int i = data.spawn_index;
            Vector3 at = Pos(data.points[i]);
            Vector3 f = Forward(data, i);
            var go = new GameObject("Preview Camera");
            go.transform.SetParent(root.transform, false);
            go.transform.position = at - f * 6.5f + Vector3.up * 2.3f;
            go.transform.rotation = Quaternion.LookRotation(at + f * 10f + Vector3.up - go.transform.position);
            Camera cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 3000f;
            cam.fieldOfView = 60f;
        }

        // ---------- self-test (batch mode) ----------
        private static int SelfTest()
        {
            var report = new StringBuilder("CampusWorld self-test\n");
            int failures = 0;
            Dressing dressing = JsonUtility.FromJson<Dressing>(File.ReadAllText(DataDir + "/dressing.json"));
            GameObject dressingRoot = GameObject.Find(WorldRootName + "/Dressing");
            int placed = dressingRoot != null ? dressingRoot.GetComponentsInChildren<MeshRenderer>(true).Length : 0;
            report.AppendLine("dressing: " + placed + " of " + dressing.items.Length + " placed");
            if (placed != dressing.items.Length)
            {
                failures++;
            }
            failures += PrefabTest(report);
            if (tuftsPlaced + tuftsLeftOut > 0)
            {
                // every tuft was put down by a ray that landed on a plain ground mesh; check that again from the meshes
                int offGround = 0, checkedTufts = 0;
                Physics.SyncTransforms();
                foreach (Vector3 foot in tuftFeet)                       // where every tuft was put down
                {
                    checkedTufts++;
                    if (!Physics.Raycast(new Vector3(foot.x, 300f, foot.z), Vector3.down, out RaycastHit hit, 600f, ~0, QueryTriggerInteraction.Ignore) || !IsPlainGround(hit))
                    {
                        offGround++;
                    }
                }
                report.AppendLine("grass tufts: " + tuftsPlaced + " placed in " + tuftChunks + " cells, " + tuftTriangles +
                                  " triangles, " + tuftsLeftOut + " left out (ray not on plain ground); " + offGround + " of " +
                                  checkedTufts + " checked stand off the ground mesh");
                if (offGround > 0)
                {
                    failures++;
                }
            }

            int bad = 0;
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null || m.shader.name.StartsWith("Hidden/InternalError", StringComparison.Ordinal) ||
                        !KnownMaterial(AssetDatabase.GetAssetPath(m)))
                    {
                        if (bad < 10)
                        {
                            report.AppendLine("  material problem on " + r.name + ": " + (m == null ? "none" : m.name));
                        }
                        bad++;
                    }
                }
            }
            report.AppendLine("renderers with a missing or default material: " + bad);
            if (bad > 0)
            {
                failures++;
            }

            CheckData check = JsonUtility.FromJson<CheckData>(File.ReadAllText(DataDir + "/check.json"));
            float worst = 0f;
            int off = 0;
            foreach (BoxCheck c in check.instances.Concat(check.objects))
            {
                GameObject go = FindByName(c.name);
                if (go == null)
                {
                    report.AppendLine("  not found: " + c.name);
                    off++;
                    continue;
                }
                Bounds b = VertexBounds(go);                    // Renderer.bounds is a loose box for rotated meshes
                float err = Mathf.Max(MaxDiff(b.min, c.min), MaxDiff(b.max, c.max));
                worst = Mathf.Max(worst, err);
                if (err > 0.08f)
                {
                    report.AppendLine("  bounds off by " + err.ToString("0.00") + " m: " + c.name);
                    off++;
                }
            }
            report.AppendLine("bounds vs Blender: " + (check.instances.Length + check.objects.Length) + " checked, " + off +
                              " off, worst " + worst.ToString("0.000") + " m");
            if (off > 0)
            {
                failures++;
            }

            TrackData data = JsonUtility.FromJson<TrackData>(File.ReadAllText(DataDir + "/track.json"));
            Physics.SyncTransforms();
            // The road the kart drives: track.json, lined up with the Nova hall doors where the hall is in (V12).
            var centre = new Vector3[data.points.Length];
            var width = new float[data.points.Length];
            for (int i = 0; i < centre.Length; i++)
            {
                centre[i] = Pos(data.points[i]);
                width[i] = data.points[i].width;
            }
            var original = (Vector3[])centre.Clone();
            if (NovaHallInsert.HasHall())
            {
                CarcavelosCircuit.Track.Editor.HallDoorApproach.Apply(centre, width, null);
            }
            int miss = 0, tested = 0, inHall = 0;
            for (int i = 0; i < data.points.Length; i++)
            {
                // Every third point, plus every point moved to a hall door (the new door roads).
                if ((i % 3 != 0 && centre[i] == original[i]) || Mathf.Abs(i - data.jump_index) < 3)
                {
                    continue;
                }
                // Inside the hall track.json follows Noah's lanes (tools/world_gen/hall_route.py), on the hall floor.
                if (NovaHallInsert.HasHall() && NovaHallInsert.InsideHall(centre[i]))
                {
                    inHall++;
                }
                tested++;
                Vector3 p = centre[i];
                if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f) || Mathf.Abs(hit.point.y - p.y) > 0.2f)
                {
                    if (miss < 8)
                    {
                        report.AppendLine("  road not found under s = " + data.points[i].s + " (" + (hit.collider ? hit.collider.name + " at " + hit.point.y.ToString("0.00") : "nothing") + ")");
                    }
                    miss++;
                }
            }
            report.AppendLine("road collider under the route: " + (tested - miss) + " of " + tested + " points" +
                              (inHall > 0 ? " (" + inHall + " of them in the Nova hall)" : ""));
            if (miss > 0)
            {
                failures++;
            }
            failures += LightingTest(report);
            report.AppendLine(failures == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + failures + ")");
            Debug.Log(report.ToString());
            return failures;
        }

        // CampusWorld.prefab is what Role 2 uses. The scene root must be a clean instance of it (no overrides), and the
        // prefab must hold the world with its colliders and light but nothing race-specific (no placeholders, no
        // camera), so Role 2's scene never ends up with two sets of checkpoints.
        private static int PrefabTest(StringBuilder report)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(WorldPrefabPath);
            GameObject instance = GameObject.Find(WorldRootName);
            bool linked = instance != null && PrefabUtility.IsOutermostPrefabInstanceRoot(instance) &&
                          PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance) == WorldPrefabPath;
            bool clean = linked && !PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false);
            int raceParts = asset == null ? -1 : asset.GetComponentsInChildren<PlaceholderTrack>(true).Length +
                asset.GetComponentsInChildren<PlaceholderCheckpoint>(true).Length + asset.GetComponentsInChildren<Camera>(true).Length;
            CampusWorldLighting lighting = asset != null ? asset.GetComponent<CampusWorldLighting>() : null;
            bool lit = lighting != null && new SerializedObject(lighting).FindProperty("sun").objectReferenceValue != null;
            int volumes = asset != null ? asset.GetComponentsInChildren<Volume>(true)
                .Count(v => v.isGlobal && v.sharedProfile != null) : 0;              // the look comes along
            int colliders = asset != null ? asset.GetComponentsInChildren<MeshCollider>(true).Length : 0;
            report.AppendLine("prefab: " + (asset != null ? "saved" : "MISSING") + ", scene instance linked " + linked +
                              ", without overrides " + clean + ", race parts or cameras inside " + raceParts +
                              ", light with sun " + lit + ", look volumes " + volumes +
                              ", mesh colliders " + colliders);
            return asset != null && clean && raceParts == 0 && lit && volumes == 1 && colliders > 0 ? 0 : 1;
        }

        // Role 2's case: a scene holding only the prefab gets the campus sky light once it is the active scene, and
        // while another scene is active (Main.unity while the race loads the track) that scene's light is left alone.
        private static int LightingTest(StringBuilder report)
        {
            Scene preview = SceneManager.GetActiveScene();
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(WorldPrefabPath);
            Scene holder = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            SceneManager.SetActiveScene(preview);
            RenderSettings.ambientMode = AmbientMode.Flat;                 // stands in for another scene's own setting
            var first = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder);
            bool leftAlone = RenderSettings.ambientMode == AmbientMode.Flat;
            RenderSettings.ambientMode = AmbientMode.Trilight;            // the preview's own value again
            UnityEngine.Object.DestroyImmediate(first);

            SceneManager.SetActiveScene(holder);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.fog = false;
            var second = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder);
            bool painted = AssetDatabase.LoadAssetAtPath<Cubemap>(SkyTexture) != null;
            bool applied = RenderSettings.ambientMode == AmbientMode.Trilight && RenderSettings.fog &&
                           Mathf.Approximately(RenderSettings.fogDensity, HazeDensity) &&
                           RenderSettings.sun != null && RenderSettings.sun.transform.IsChildOf(second.transform) &&
                           (!painted || (RenderSettings.skybox != null && RenderSettings.skybox.name == "Sky_Painted" &&
                                         RenderSettings.defaultReflectionMode == DefaultReflectionMode.Custom &&
                                         RenderSettings.customReflectionTexture != null));

            SceneManager.SetActiveScene(preview);
            EditorSceneManager.CloseScene(holder, true);
            report.AppendLine("light in a scene with only the prefab: applied " + applied + ", other active scene left alone " + leftAlone);
            return applied && leftAlone ? 0 : 1;
        }

        // Kart's-eye views at a few places of the lap, for comparing with the Blender renders.
        private static void RenderShots(string folder)
        {
            Directory.CreateDirectory(folder);
            TrackData data = JsonUtility.FromJson<TrackData>(File.ReadAllText(DataDir + "/track.json"));
            var go = new GameObject("ShotCamera");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 3000f;
            cam.allowHDR = true;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;    // the grade, as in the race
            var rt = new RenderTexture(1280, 720, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            string extra = Argument("-campusWorldShotS");                          // e.g. "60,575" (metres)
            float[] stations = string.IsNullOrEmpty(extra)
                ? new[] { 16f, 60f, 100f, 150f, 200f, 330f, 470f, 575f, 610f, 700f, 860f, 965f, 1060f, 1100f, 1150f, 1260f }
                : extra.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            foreach (float s in stations)
            {
                int i = Mathf.RoundToInt(s / 2f) % data.points.Length;
                int back = (i - 3 + data.points.Length) % data.points.Length;
                Vector3 kart = Pos(data.points[i]);
                go.transform.position = new Vector3(data.points[back].x, Mathf.Max(data.points[back].y, kart.y) + 2.3f, data.points[back].z);
                Vector3 ahead = Pos(data.points[(i + 5) % data.points.Length]) + Vector3.up;
                go.transform.rotation = Quaternion.LookRotation(ahead - go.transform.position);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(folder, "unity_s" + ((int)s).ToString("0000") + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            RenderTexture.active = null;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(go);
        }

        // ---------- helpers ----------
        private static Vector3 Pos(TrackPoint p)
        {
            return new Vector3(p.x, p.y, p.z);
        }

        private static Vector3 Forward(TrackData data, int i)
        {
            int n = data.points.Length;
            Vector3 d = Pos(data.points[(i + 2) % n]) - Pos(data.points[(i - 2 + n) % n]);
            d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }

        private static GameObject Model(string name)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/" + name + ".fbx");
        }

        private static GameObject Instance(string model, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Model(model), parent);
            return go;
        }

        // V10i: meshes named "...ColliderOnly" keep their collider but are not drawn.
        private static void HideIfColliderOnly(MeshFilter mf)
        {
            if (!mf.gameObject.name.Contains("ColliderOnly"))
            {
                return;
            }
            MeshRenderer renderer = mf.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
            GameObjectUtility.SetStaticEditorFlags(mf.gameObject, 0);
        }

        // Materials the self-test accepts: Luc's material folder, the ones inside Noah's hall model (#45) and
        // Role 2's track materials (the white fence).
        private static bool KnownMaterial(string path)
        {
            return path.StartsWith(MatDir, StringComparison.Ordinal) ||
                   path == Root + "/Models/NovaHallWorld.fbx" ||
                   path.StartsWith("Assets/_Project/Track/Materials/", StringComparison.Ordinal);
        }

        private static void MarkStatic(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        private static GameObject FindByName(string name)
        {
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == name)
                {
                    return t.gameObject;
                }
            }
            return null;
        }

        // Exact world-space box around the vertices (what Blender measures), not the renderer's rotated local box.
        private static Bounds VertexBounds(GameObject go)
        {
            bool first = true;
            var b = new Bounds();
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                foreach (Vector3 v in mf.sharedMesh.vertices)
                {
                    Vector3 w = m.MultiplyPoint3x4(v);
                    if (first)
                    {
                        b = new Bounds(w, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        b.Encapsulate(w);
                    }
                }
            }
            return b;
        }

        private static float MaxDiff(Vector3 a, float[] b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b[0]), Mathf.Max(Mathf.Abs(a.y - b[1]), Mathf.Abs(a.z - b[2])));
        }

        private static Texture2D Tex(string file)
        {
            return string.IsNullOrEmpty(file) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + file);
        }

        private static void SetColor(Material m, Color c, params string[] names)
        {
            foreach (string n in names)
            {
                if (m.HasProperty(n))
                {
                    m.SetColor(n, c);
                }
            }
        }

        private static void SetTexture(Material m, Texture t, params string[] names)
        {
            foreach (string n in names)
            {
                if (m.HasProperty(n))
                {
                    m.SetTexture(n, t);
                }
            }
        }

        private static void SetFloat(Material m, float v, params string[] names)
        {
            foreach (string n in names)
            {
                if (m.HasProperty(n))
                {
                    m.SetFloat(n, v);
                }
            }
        }

        private static void Keyword(Material m, string keyword, bool on)
        {
            if (on)
            {
                m.EnableKeyword(keyword);
            }
            else
            {
                m.DisableKeyword(keyword);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}
