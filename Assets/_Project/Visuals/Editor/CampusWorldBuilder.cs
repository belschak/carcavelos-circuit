using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CarcavelosCircuit.RaceFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarcavelosCircuit.Visuals.Editor
{
    // Builds Luc's Carcavelos campus world into Track_CampusWorld.unity (Role 3, task V4).
    // Input: Assets/_Project/Visuals/CampusWorld/{Models,Textures,Data}, exported by the world generator
    // (world-gen/export_unity_bundle.py, outside this repo). Output, all made through the Unity API:
    // URP Lit materials, one prefab per tree/prop kind, and the scene with world, track surface, 904 trees and
    // props, a sun, and the race placeholders (checkpoints, spawn) that RaceFlow already understands.
    // Axes: x = east, y = up, z = north, 1 unit = 1 m. Run it again after every new export.
    public static class CampusWorldBuilder
    {
        private const string Root = "Assets/_Project/Visuals/CampusWorld";
        private const string ModelsDir = Root + "/Models";
        private const string TexDir = Root + "/Textures";
        private const string DataDir = Root + "/Data";
        private const string MatDir = Root + "/Materials";
        private const string PrefabDir = Root + "/Prefabs";
        private const string ScenePath = "Assets/_Project/Visuals/Scenes/Track_CampusWorld.unity";
        private static readonly string[] Models = { "CampusWorld", "TrackSurface", "RouteGuide", "Dressing_Prototypes" };

        [Serializable] private class Emission { public float[] colour; public float strength; }
        [Serializable] private class MatInfo
        {
            public string name; public float[] colour; public float smoothness; public float metallic; public float alpha;
            public string albedo; public string metallic_smoothness; public string normal; public float normal_strength;
            public Emission emission;
        }
        [Serializable] private class MatList { public MatInfo[] materials; }
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
                bool normal = file.EndsWith("_normal.png", StringComparison.Ordinal);
                bool mask = file.EndsWith("_metallic_smoothness.png", StringComparison.Ordinal);
                bool big = file.StartsWith("ground_colour", StringComparison.Ordinal) || file.StartsWith("mural", StringComparison.Ordinal);
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !(normal || mask);
                ti.alphaSource = mask ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                ti.maxTextureSize = big ? 2048 : 1024;
                ti.wrapMode = file.StartsWith("ground_colour", StringComparison.Ordinal) ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                ti.mipmapEnabled = true;
                ti.anisoLevel = 4;
                ti.SaveAndReimport();
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
                if (mat == null)
                {
                    mat = new Material(lit);
                    AssetDatabase.CreateAsset(mat, path);
                }
                else
                {
                    mat.shader = lit;
                }
                Texture2D albedo = Tex(m.albedo), normal = Tex(m.normal), mask = Tex(m.metallic_smoothness);
                // with a texture the tint stays white: the texture carries the colour (its average = the palette)
                Color colour = albedo != null ? Color.white : new Color(m.colour[0], m.colour[1], m.colour[2], 1f);
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
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                result[m.name] = mat;
            }
            AssetDatabase.SaveAssets();
            return result;
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

        // ---------- scene ----------
        private static void BuildScene(Dictionary<string, GameObject> prefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("CampusWorld (Role 3)");

            GameObject world = Instance("CampusWorld", root.transform);
            foreach (MeshFilter mf in world.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mf.gameObject.name;
                if (!n.StartsWith("Backdrop_", StringComparison.Ordinal) && !n.StartsWith("Tunnel_Lights", StringComparison.Ordinal))
                {
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
                MarkStatic(mf.gameObject);
            }
            GameObject track = Instance("TrackSurface", root.transform);
            foreach (MeshFilter mf in track.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.gameObject.name.StartsWith("Track_EdgeLines", StringComparison.Ordinal))  // paint, not a bump
                {
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
                MarkStatic(mf.gameObject);
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

            TrackData data = JsonUtility.FromJson<TrackData>(File.ReadAllText(DataDir + "/track.json"));
            AddRaceParts(root, data);
            AddSunAndSky(root);
            AddPreviewCamera(root, data);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void AddRaceParts(GameObject root, TrackData data)
        {
            var race = new GameObject("Race (placeholders)");
            race.transform.SetParent(root.transform, false);
            PlaceholderTrack track = race.AddComponent<PlaceholderTrack>();          // swap to Role 2's TrackInfo with T4
            int count = data.checkpoints.Length;
            var checkpoints = new PlaceholderCheckpoint[count];
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
                PlaceholderCheckpoint checkpoint = cp.AddComponent<PlaceholderCheckpoint>(); // swap to Role 2's Checkpoint with T4
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
        }

        // docs/art-style.md: bright late-morning sun from one side, soft shadows, light-blue sky, no fog.
        // The race makes this scene the active one, so these settings apply in the race.
        private static void AddSunAndSky(GameObject root)
        {
            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(root.transform, false);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            float az = 135f * Mathf.Deg2Rad, el = 40f * Mathf.Deg2Rad;              // from the south-east, 40 degrees up
            var towardsSun = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
            sunObject.transform.rotation = Quaternion.LookRotation(-towardsSun);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;                         // no baking needed
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.62f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.52f, 0.49f, 0.43f);                // light bounced off the pale paving: ceilings and overhangs stay light, as on campus
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.fog = false;
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
            GameObject dressingRoot = GameObject.Find("CampusWorld (Role 3)/Dressing");
            int placed = dressingRoot != null ? dressingRoot.GetComponentsInChildren<MeshRenderer>(true).Length : 0;
            report.AppendLine("dressing: " + placed + " of " + dressing.items.Length + " placed");
            if (placed != dressing.items.Length)
            {
                failures++;
            }

            int bad = 0;
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null || m.shader.name.StartsWith("Hidden/InternalError", StringComparison.Ordinal) ||
                        !AssetDatabase.GetAssetPath(m).StartsWith(MatDir, StringComparison.Ordinal))
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
            int miss = 0, tested = 0;
            for (int i = 0; i < data.points.Length; i += 3)
            {
                if (Mathf.Abs(i - data.jump_index) < 3)
                {
                    continue;
                }
                tested++;
                Vector3 p = Pos(data.points[i]);
                if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f) || Mathf.Abs(hit.point.y - p.y) > 0.2f)
                {
                    if (miss < 8)
                    {
                        report.AppendLine("  road not found under s = " + data.points[i].s + " (" + (hit.collider ? hit.collider.name + " at " + hit.point.y.ToString("0.00") : "nothing") + ")");
                    }
                    miss++;
                }
            }
            report.AppendLine("road collider under the route: " + (tested - miss) + " of " + tested + " points");
            if (miss > 0)
            {
                failures++;
            }
            report.AppendLine(failures == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + failures + ")");
            Debug.Log(report.ToString());
            return failures;
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
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            foreach (float s in new[] { 30f, 120f, 240f, 400f, 490f, 560f, 700f, 980f, 1048f, 1150f })
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
