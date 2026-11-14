using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarcavelosCircuit.Visuals.Kart.Editor
{
    // Builds the KartVisual prefab (docs/interfaces.md "Visuals provides"): Luc's Nova Classic kart with Slick tyres
    // and the Rookie driver, from the FBX files exported by the NovaKart roster tools (Art/Export, outside the repo).
    // Quick version (TASKS V3): no Animation Rigging package, so the driver keeps the seated pose from the FBX and the
    // hands do not follow the steering wheel yet. Kart forward = +Z, pivot at ground level centre, 1 unit = 1 m.
    // Materials follow the roster builder's look table (NovaKartRosterBuilder.SurfaceLook, v12).
    public static class NovaKartVisualBuilder
    {
        private const string Root = "Assets/_Project/Visuals/Kart";
        private const string KartFbx = Root + "/Models/Kart_Classic.fbx";
        private const string TyreFbx = Root + "/Models/Tyre_Slick.fbx";
        private const string CharFbx = Root + "/Models/Char_Rookie.fbx";
        private const string Tex = Root + "/Textures/Char_Rookie_";
        private const string MatDir = Root + "/Materials";
        private const string PrefabPath = Root + "/Prefabs/KartVisual_Classic_Rookie.prefab";
        // Task V32: the light far version (LOD1) made by tools/kart_lod/make_kart_lod.py, and its colour palette.
        private const string KartLodFbx = Root + "/Models/Kart_Classic_Far.fbx";
        private const string TyreLodFbx = Root + "/Models/Tyre_Slick_Far.fbx";
        private const string PaletteJson = "tools/kart_lod/palette.json";   // relative to the project folder
        private const string LodMaterial = "NK_KartLOD";
        private const string PaletteTex = Root + "/Textures/KartLOD_Palette_";
        private const int PaletteCellPixels = 4;    // 4 x 4 pixels per cell = one compression block, so no colour bleeds
        private const string TyreIdMap = "tools/kart_lod/tyre_far_id.png";  // baked by make_kart_lod.py: which material where
        private const string TyreFarTex = Root + "/Textures/TyreFar_";     // Normal.png from Blender, Colour/MetalSmooth from here
        private const string TyreFarMaterial = "NT_TyreFar";
        private const string TyreAoMap = "tools/kart_lod/tyre_far_ao.png";   // baked ambient occlusion
        private const float AoStrength = 0.8f;      // how much of the baked shade darkens the colour (1 = all)
        private const float NearUntil = 0.12f;      // full kart while it fills at least 12 % of the screen height
        private const float CullBelow = 0.006f;     // below 0.6 % of the screen height the kart is not drawn at all
        private const float TyreRadius = 0.21f;                         // Slick, from Art/Export/Tyres/tyres.json
        private static readonly Color GloveRookie = new Color(0.035f, 0.035f, 0.04f);
        private static readonly string[] Wheels = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };

        [MenuItem("Carcavelos Circuit/Build Kart Visual (Role 3)")]
        public static void Build()
        {
            ImportSettings();
            MakeMaterials();
            BuildPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("NovaKartVisualBuilder: " + PrefabPath + " built.");
        }

        // Batch mode: -executeMethod CarcavelosCircuit.Visuals.Kart.Editor.NovaKartVisualBuilder.BuildAndCheck
        // [-kartShots <folder>]: builds, checks, renders a few views, exits 0 (pass) or 1.
        public static void BuildAndCheck()
        {
            int failures = 0;
            try
            {
                Build();
                failures = Check();
                string shots = Argument("-kartShots");
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

        // ---------- import ----------
        private static void ImportSettings()
        {
            foreach (string path in new[] { KartFbx, TyreFbx, CharFbx, KartLodFbx, TyreLodFbx })
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(path);
                mi.globalScale = 1f;
                mi.useFileScale = true;
                mi.bakeAxisConversion = true;
                mi.importCameras = false;
                mi.importLights = false;
                mi.importAnimation = false;
                mi.animationType = path == CharFbx ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                mi.SaveAndReimport();
            }
            var normal = (TextureImporter)AssetImporter.GetAtPath(Tex + "Normal.png");
            normal.textureType = TextureImporterType.NormalMap;
            normal.SaveAndReimport();
            var mask = (TextureImporter)AssetImporter.GetAtPath(Tex + "MetallicSmoothness.png");
            mask.sRGBTexture = false;
            mask.alphaSource = TextureImporterAlphaSource.FromInput;
            mask.SaveAndReimport();
            var albedo = (TextureImporter)AssetImporter.GetAtPath(Tex + "BaseColor.png");
            albedo.maxTextureSize = 2048;
            albedo.SaveAndReimport();
        }

        // ---------- materials: editable copies of the FBX materials, then the roster look ----------
        private static void MakeMaterials()
        {
            Directory.CreateDirectory(MatDir);
            AssetDatabase.Refresh();
            // the LOD files come last: their NK_Paint_Body must find the material the full kart already made
            foreach (string path in new[] { KartFbx, TyreFbx, CharFbx, KartLodFbx, TyreLodFbx })
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(path);
                var names = new HashSet<string>(mi.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name));
                var embedded = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Material>().ToList();
                foreach (Material e in embedded)
                {
                    names.Add(e.name);
                }
                foreach (string name in names)
                {
                    string matPath = MatDir + "/" + name + ".mat";
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat == null)
                    {
                        Material source = embedded.FirstOrDefault(m => m.name == name);
                        mat = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        AssetDatabase.CreateAsset(mat, matPath);
                    }
                    Look(mat);
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
                }
                mi.SaveAndReimport();
            }
            MakePalette();
            MakeFarTyre();
        }

        // Task V32: the far tyre is a round low-poly shell with the full tyre baked onto it. Blender bakes an ID map
        // (each tyre material in its own flat colour, listed in palette.json) and a normal map. Here every ID colour
        // becomes that material's real colour and metal/smoothness, so the far tyre matches the full one exactly.
        private static void MakeFarTyre()
        {
            Palette palette = JsonUtility.FromJson<Palette>(File.ReadAllText(PaletteJson));
            var ids = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ids.LoadImage(File.ReadAllBytes(TyreIdMap));
            var colour = new Texture2D(ids.width, ids.height, TextureFormat.RGBA32, true);
            var mask = new Texture2D(ids.width, ids.height, TextureFormat.RGBA32, true);
            var looks = new Material[palette.tyre_bake.ids.Length];
            for (int i = 0; i < looks.Length; i++)
            {
                looks[i] = PaletteSource(palette.tyre_bake.ids[i].name);
            }
            Material rubber = PaletteSource("NT_Tyre");
            // ambient occlusion baked in Blender: grooves and the deep brake disc get darker, like the full tyre's
            // real shade; same size as the ID map
            var occlusion = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            occlusion.LoadImage(File.ReadAllBytes(TyreAoMap));
            for (int y = 0; y < ids.height; y++)
            {
                for (int x = 0; x < ids.width; x++)
                {
                    Color id = ids.GetPixel(x, y);
                    // unused texture space (black) gets rubber, so mipmaps of the tyre edge do not turn grey
                    Material m = id.maxColorComponent < 0.5f ? rubber : looks[NearestId(palette.tyre_bake.ids, id)];
                    // near-black shade is a bake ray that grazed the tyre's inside at the tread edge, not real shade
                    // (real grooves stay around 50 % grey), so it counts as no shade
                    float shade = occlusion.GetPixel(x, y).r;
                    if (shade < 0.15f)
                    {
                        shade = 1f;
                    }
                    Color c = m.GetColor("_BaseColor") * Mathf.Lerp(1f, shade, AoStrength);
                    colour.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
                    mask.SetPixel(x, y, new Color(m.GetFloat("_Metallic"), 0f, 0f, m.GetFloat("_Smoothness")));
                }
            }
            UnityEngine.Object.DestroyImmediate(ids);
            UnityEngine.Object.DestroyImmediate(occlusion);
            Texture2D colourTex = SaveTyreTexture(colour, "Colour", true);
            Texture2D maskTex = SaveTyreTexture(mask, "MetalSmooth", false);
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(TyreFarTex + "Normal.png");
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.maxTextureSize = 512;
            normalImporter.SaveAndReimport();

            Material tyre = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + TyreFarMaterial + ".mat");
            tyre.shader = Shader.Find("Universal Render Pipeline/Lit");
            tyre.SetTexture("_BaseMap", colourTex);
            tyre.SetColor("_BaseColor", Color.white);
            tyre.SetTexture("_MetallicGlossMap", maskTex);
            tyre.EnableKeyword("_METALLICSPECGLOSSMAP");
            Set(tyre, "_Smoothness", 1f);     // with a mask texture this only scales the mask's smoothness
            tyre.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TyreFarTex + "Normal.png"));
            tyre.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(tyre);
        }

        // The ID colour closest to c (the bake uses one sample per pixel, so c is normally an exact ID colour).
        private static int NearestId(TyreId[] ids, Color c)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < ids.Length; i++)
            {
                float d = Mathf.Abs(c.r - ids[i].r) + Mathf.Abs(c.g - ids[i].g) + Mathf.Abs(c.b - ids[i].b);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        // Saves a far-tyre texture: smooth filtering and mipmaps (it is a real picture of the tyre, not a palette).
        private static Texture2D SaveTyreTexture(Texture2D tex, string kind, bool srgb)
        {
            string path = TyreFarTex + kind + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.sRGBTexture = srgb;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 512;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Task V32: the far kart has one material for everything except the body paint. Its three tiny textures hold
        // one 4 x 4 pixel cell per original material (order from tools/kart_lod/palette.json): colour, metal +
        // smoothness, glow. The cells are read from the real materials, so a colour change there reaches the far kart
        // on the next build.
        private static void MakePalette()
        {
            Palette palette = JsonUtility.FromJson<Palette>(File.ReadAllText(PaletteJson));
            int size = palette.grid * PaletteCellPixels;
            var colour = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var glow = new Texture2D(size, size, TextureFormat.RGBA32, false);
            // the brightest glow decides the material's emission strength; the cells keep each glow's colour
            float glowStrength = 1f;
            foreach (string name in palette.materials)
            {
                Material m = PaletteSource(name);
                if (m.IsKeywordEnabled("_EMISSION"))
                {
                    Color e = m.GetColor("_EmissionColor");
                    glowStrength = Mathf.Max(glowStrength, e.maxColorComponent);
                }
            }
            for (int i = 0; i < size * size; i++)
            {
                colour.SetPixel(i % size, i / size, Color.magenta);   // unused cells: easy to spot if a UV goes wrong
                mask.SetPixel(i % size, i / size, Color.black);
                glow.SetPixel(i % size, i / size, Color.black);
            }
            for (int i = 0; i < palette.materials.Length; i++)
            {
                Material m = PaletteSource(palette.materials[i]);
                Color c = m.GetColor("_BaseColor");
                Color e = m.IsKeywordEnabled("_EMISSION") ? m.GetColor("_EmissionColor") / glowStrength : Color.black;
                var metal = new Color(m.GetFloat("_Metallic"), 0f, 0f, m.GetFloat("_Smoothness"));
                FillCell(colour, i, palette.grid, new Color(c.r, c.g, c.b, 1f));
                FillCell(mask, i, palette.grid, metal);
                FillCell(glow, i, palette.grid, new Color(e.r, e.g, e.b, 1f));
            }
            Texture2D colourTex = SavePalette(colour, "Colour", true);
            Texture2D maskTex = SavePalette(mask, "MetalSmooth", false);
            Texture2D glowTex = SavePalette(glow, "Glow", true);

            Material lod = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + LodMaterial + ".mat");
            lod.shader = Shader.Find("Universal Render Pipeline/Lit");
            lod.SetTexture("_BaseMap", colourTex);
            lod.SetColor("_BaseColor", Color.white);
            lod.SetTexture("_MetallicGlossMap", maskTex);
            lod.EnableKeyword("_METALLICSPECGLOSSMAP");
            Set(lod, "_Smoothness", 1f);      // with a mask texture this only scales the mask's smoothness
            lod.SetTexture("_EmissionMap", glowTex);
            lod.SetColor("_EmissionColor", Color.white * glowStrength);
            lod.EnableKeyword("_EMISSION");
            lod.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(lod);
        }

        private static Material PaletteSource(string name)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + name + ".mat");
            if (m == null)
            {
                throw new InvalidOperationException("NovaKartVisualBuilder: palette.json names " + name + ", but " + MatDir + " has no such material.");
            }
            return m;
        }

        // Cell 0 is bottom left, then row by row, like the UVs make_kart_lod.py writes.
        private static void FillCell(Texture2D tex, int index, int grid, Color c)
        {
            int x0 = index % grid * PaletteCellPixels, y0 = index / grid * PaletteCellPixels;
            for (int y = 0; y < PaletteCellPixels; y++)
            {
                for (int x = 0; x < PaletteCellPixels; x++)
                {
                    tex.SetPixel(x0 + x, y0 + y, c);
                }
            }
        }

        // Saves a palette PNG and imports it sharp: no filtering, no mipmaps, no compression (it is 16 x 16 pixels).
        private static Texture2D SavePalette(Texture2D tex, string kind, bool srgb)
        {
            string path = PaletteTex + kind + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.sRGBTexture = srgb;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Shape of tools/kart_lod/palette.json (fields starting with "_" are comments and ignored).
        [Serializable]
        private class Palette
        {
            public int grid;
            public string paint;
            public string[] materials;
            public TyreBake tyre_bake;
        }

        [Serializable]
        private class TyreBake
        {
            public int size;
            public TyreId[] ids;
        }

        [Serializable]
        private class TyreId
        {
            public string name;
            public float r, g, b;
        }

        // Metallic / smoothness / glow per material name (roster builder v12), the driver's textures and gloves.
        private static void Look(Material m)
        {
            string n = m.name;
            float metal = 0f, smooth = 0.5f;
            if (n == "NK_Paint_Body" || n == "NK_Trim_Black") { smooth = 0.8f; }
            else if (n == "NK_Accent_Silver") { metal = 0.9f; smooth = 0.8f; }
            else if (n == "NK_Steel") { metal = 1f; smooth = 0.5f; }
            else if (n == "NK_Steel_Bright") { metal = 1f; smooth = 0.75f; }
            else if (n.EndsWith("_Metal", StringComparison.Ordinal)) { metal = 1f; smooth = 0.75f; }
            else if (n == "NK_Chassis_Dark") { smooth = 0.3f; }
            else if (n == "NK_Metal") { metal = 0.7f; smooth = 0.6f; }
            else if (n.StartsWith("NK_Number", StringComparison.Ordinal)) { smooth = 0.6f; }
            else if (n == "NT_Tyre") { smooth = 0.12f; }
            else if (n.EndsWith("_Rim", StringComparison.Ordinal)) { metal = 0.9f; smooth = 0.8f; }
            else if (n.EndsWith("_Hub", StringComparison.Ordinal)) { metal = 0.7f; smooth = 0.6f; }
            else if (n.EndsWith("_Letter", StringComparison.Ordinal) || n.EndsWith("_Wall", StringComparison.Ordinal)) { smooth = 0.45f; }
            Set(m, "_Metallic", metal);
            Set(m, "_Smoothness", smooth);
            if (n == "NK_Glow" || n.EndsWith("_Glow", StringComparison.Ordinal))
            {
                if (n == "NK_Glow")
                {
                    m.SetColor("_BaseColor", new Color(0.1f, 0.5f, 1f));
                }
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.3f, 0.75f, 1f) * 4f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (n.StartsWith("NK_Char_Rookie", StringComparison.Ordinal))
            {
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "BaseColor.png"));
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "Normal.png"));
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "MetallicSmoothness.png"));
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                Set(m, "_Smoothness", 1f);
            }
            if (n.StartsWith("NK_Glove", StringComparison.Ordinal))
            {
                m.SetColor("_BaseColor", GloveRookie);
                Set(m, "_Smoothness", 0.55f);
            }
            EditorUtility.SetDirty(m);
        }

        // ---------- prefab ----------
        private static void BuildPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();
            var root = new GameObject("KartVisual_Classic_Rookie");
            var kart = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KartFbx));
            kart.transform.SetParent(root.transform, false);
            // forward must be +Z: the front axle (Wheel_FL) in front of the rear axle (Wheel_RL)
            if (Find(kart, "Wheel_FL").position.z < Find(kart, "Wheel_RL").position.z)
            {
                kart.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            // V32: the far kart must sit exactly on the full one, or it jumps when Unity swaps them: same place and
            // turn as the full kart (CheckLevelsOfDetail measures it)
            var kartFar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KartLodFbx));
            kartFar.transform.SetParent(root.transform, false);
            kartFar.transform.localRotation = kart.transform.localRotation;
            var far = new List<Renderer>(kartFar.GetComponentsInChildren<Renderer>(true));
            foreach (string w in Wheels)
            {
                Transform anchor = Find(kart, w);
                if (anchor.GetComponentsInChildren<Renderer>(true).Length == 0)
                {
                    var tyre = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TyreFbx));
                    tyre.transform.SetParent(anchor, false);
                    // the far tyre sits on the same anchor, so it still rolls and steers with KartVisualSteering
                    var tyreFar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TyreLodFbx));
                    tyreFar.transform.SetParent(anchor, false);
                    far.AddRange(tyreFar.GetComponentsInChildren<Renderer>(true));
                }
            }
            var driver = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CharFbx));
            driver.name = "Driver_Rookie";
            driver.transform.SetParent(root.transform, false);
            driver.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);  // armature FBX arrives facing -Z
            // the driver sits behind the steering wheel: if the body ends up in front of it, turn it round
            Bounds body = Bounds(driver);
            if (body.center.z > Find(kart, "Steering_Wheel").position.z)
            {
                driver.transform.localRotation = Quaternion.identity;
            }
            var steering = root.AddComponent<KartVisualSteering>();
            steering.wheelRadius = TyreRadius;
            root.AddComponent<KartVisualFromState>();
            root.AddComponent<KartDriverArms>();      // hands stay on the rim while the wheel turns
            AddLevelsOfDetail(root, far, driver);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        // V32: Unity's LODGroup picks what to draw from how tall the kart is on screen. Near: the full kart.
        // Far: the light kart and tyres, plus the same driver (the arms' IK needs the full rig). Very far: nothing.
        private static void AddLevelsOfDetail(GameObject root, List<Renderer> far, GameObject driver)
        {
            var near = new List<Renderer>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!far.Contains(r))
                {
                    near.Add(r);
                }
            }
            var farWithDriver = new List<Renderer>(far);
            farWithDriver.AddRange(driver.GetComponentsInChildren<Renderer>(true));
            var group = root.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(NearUntil, near.ToArray()), new LOD(CullBelow, farWithDriver.ToArray()) });
            // blend the two versions over a short stretch instead of popping (URP's LOD cross-fade is on in both
            // pipeline assets), so the swap is not visible
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            group.RecalculateBounds();
        }

        // ---------- checks ----------
        private static int Check()
        {
            var report = new StringBuilder("KartVisual check\n");
            int failures = 0;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Transform fl = Find(go, "Wheel_FL"), fr = Find(go, "Wheel_FR"), rl = Find(go, "Wheel_RL"), rr = Find(go, "Wheel_RR");
            Transform wheel = Find(go, "Steering_Wheel");
            bool forward = fl.position.z > rl.position.z && fr.position.z > rr.position.z;
            bool leftSide = fl.position.x < 0f && rl.position.x < 0f;
            report.AppendLine("front axle ahead (+Z): " + forward + ", left wheels at -X: " + leftSide);
            if (!forward || !leftSide)
            {
                failures++;
            }
            float lowest = float.MaxValue;
            foreach (Transform w in new[] { fl, fr, rl, rr })
            {
                lowest = Mathf.Min(lowest, Bounds(w.gameObject).min.y);
            }
            report.AppendLine("tyres touch the ground: lowest point y = " + lowest.ToString("0.000") + " m");
            if (Mathf.Abs(lowest) > 0.03f)
            {
                failures++;
            }
            Bounds driver = Bounds(Find(go, "Driver_Rookie").gameObject);
            bool seated = Mathf.Abs(driver.center.x) < 0.15f && driver.center.z < wheel.position.z && driver.min.y > -0.02f;
            report.AppendLine("driver behind the wheel and centred: " + seated + " (centre " + driver.center.ToString("0.00") +
                              ", wheel z " + wheel.position.z.ToString("0.00") + ")");
            if (!seated)
            {
                failures++;
            }
            int tris = 0, bad = 0;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                tris += mesh != null ? mesh.triangles.Length / 3 : 0;
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null || !AssetDatabase.GetAssetPath(m).StartsWith(MatDir, StringComparison.Ordinal))
                    {
                        bad++;
                        report.AppendLine("  material problem on " + r.name + ": " + (m == null ? "none" : m.name));
                    }
                }
            }
            report.AppendLine("triangles: " + tris + ", renderers with missing/unconverted material: " + bad);
            if (bad > 0)
            {
                failures++;
            }
            Bounds all = Bounds(go);
            report.AppendLine("size (m): " + all.size.ToString("0.00"));

            // Hands on the rim: turn the wheel from full left to full right, the hands must stay where they hold it.
            var steering = go.GetComponent<KartVisualSteering>();
            var arms = go.GetComponent<KartDriverArms>();
            float worstGrip = 0f;
            arms.SolveNow();   // first call records the grips, so the wheel must still be straight here
            foreach (float steer in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
            {
                steering.steer = steer;
                steering.ShowNow();
                arms.SolveNow();
                float error = arms.WorstGripError();
                worstGrip = Mathf.Max(worstGrip, error);
                report.AppendLine("  steer " + steer.ToString("+0.0;-0.0;0.0") + ": hand furthest from its grip " + (error * 100f).ToString("0.0") + " cm");
            }
            report.AppendLine("hands stay on the rim: worst " + (worstGrip * 100f).ToString("0.0") + " cm (limit 1.0 cm)");
            if (worstGrip > 0.01f)
            {
                failures++;
            }
            failures += CheckLevelsOfDetail(go, report);
            UnityEngine.Object.DestroyImmediate(go);
            report.AppendLine(failures == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + failures + ")");
            Debug.Log(report.ToString());
            return failures;
        }

        // V32: the far kart must be much cheaper, sit exactly where the full one sits, and keep the paint slot that
        // BotKartPaint recolours. Returns the number of failed checks.
        private static int CheckLevelsOfDetail(GameObject go, StringBuilder report)
        {
            int failures = 0;
            var group = go.GetComponent<LODGroup>();
            if (group == null || group.lodCount != 2)
            {
                report.AppendLine("LOD: missing LODGroup with 2 levels");
                return 1;
            }
            LOD[] lods = group.GetLODs();
            int nearTris = Triangles(lods[0].renderers), farTris = Triangles(lods[1].renderers);
            int nearDraws = DrawCalls(lods[0].renderers), farDraws = DrawCalls(lods[1].renderers);
            report.AppendLine("LOD near: " + nearTris + " triangles, " + nearDraws + " draw calls; far: " + farTris +
                              " triangles, " + farDraws + " draw calls");
            if (farTris > nearTris * 0.35f || farDraws > 10)
            {
                report.AppendLine("  FAIL: far kart should be at most 35 % of the triangles and 10 draw calls");
                failures++;
            }
            // same place and size: compare the full and far body, then each full and far tyre
            // the far body is one mesh made from these four parts of the full kart
            Bounds bodyNear = Find(go, "Kart_Body").GetComponent<Renderer>().bounds;
            foreach (string part in new[] { "Kart_Details", "Seat", "Steering_Wheel" })
            {
                bodyNear.Encapsulate(Find(go, part).GetComponent<Renderer>().bounds);
            }
            Transform kartFar = Find(go, "Kart_Classic_Far");   // a one-mesh FBX: Unity names the object after the file
            failures += SameSpot(report, "body", bodyNear, kartFar.GetComponent<Renderer>().bounds);
            foreach (string w in Wheels)
            {
                Transform anchor = Find(go, w);
                Renderer tyreNear = null, tyreFar = null;
                foreach (Renderer r in anchor.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name.EndsWith("_Far", StringComparison.Ordinal)) { tyreFar = r; } else { tyreNear = r; }
                }
                if (tyreNear == null || tyreFar == null)
                {
                    report.AppendLine("  FAIL: " + w + " is missing its full or far tyre");
                    failures++;
                    continue;
                }
                failures += SameSpot(report, w, tyreNear.bounds, tyreFar.bounds);
            }
            // the bots' colour lands on the far kart too
            bool paintSlot = kartFar.GetComponent<Renderer>().sharedMaterials.Any(m => m != null && m.name == "NK_Paint_Body");
            report.AppendLine("far kart keeps NK_Paint_Body for BotKartPaint: " + paintSlot);
            if (!paintSlot)
            {
                failures++;
            }
            // at which distance the swap happens with the race camera (60 degree field of view, LOD bias 1)
            float swap = group.size / (2f * Mathf.Tan(30f * Mathf.Deg2Rad) * NearUntil);
            float cull = group.size / (2f * Mathf.Tan(30f * Mathf.Deg2Rad) * CullBelow);
            report.AppendLine("LOD switch: far kart from about " + swap.ToString("0") + " m, hidden from about " + cull.ToString("0") + " m");
            return failures;
        }

        // Centres within 3 cm and sizes within 5 cm: otherwise the kart would jump when Unity swaps the versions.
        private static int SameSpot(StringBuilder report, string what, Bounds near, Bounds far)
        {
            float centre = Vector3.Distance(near.center, far.center);
            float size = (near.size - far.size).magnitude;
            bool ok = centre < 0.03f && size < 0.05f;
            report.AppendLine("  " + what + " near vs far: centre off " + (centre * 100f).ToString("0.0") + " cm, size off " +
                              (size * 100f).ToString("0.0") + " cm" + (ok ? "" : "  FAIL"));
            return ok ? 0 : 1;
        }

        private static int Triangles(Renderer[] renderers)
        {
            int tris = 0;
            foreach (Renderer r in renderers)
            {
                Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                tris += mesh != null ? mesh.triangles.Length / 3 : 0;
            }
            return tris;
        }

        // One draw call per material slot of every renderer (before shadows).
        private static int DrawCalls(Renderer[] renderers)
        {
            int draws = 0;
            foreach (Renderer r in renderers)
            {
                draws += r.sharedMaterials.Length;
            }
            return draws;
        }

        // Turntable stills in a plain scene with a sun (art style: bright late-morning light).
        private static void RenderShots(string folder)
        {
            Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40f, 135f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.62f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.38f, 0.36f, 0.32f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.SetColor("_BaseColor", new Color(0.85f, 0.79f, 0.66f));
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;
            var kart = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var steering = kart.GetComponent<KartVisualSteering>();
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.66f, 0.8f, 0.95f);
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.05f;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            var views = new (string name, Vector3 pos, float steer)[]
            {
                ("front_left", new Vector3(-2.2f, 1.3f, 2.6f), 0f), ("side", new Vector3(-3.4f, 0.9f, 0f), 0f),
                ("chase", new Vector3(0f, 1.6f, -3.6f), 0f), ("steer_right", new Vector3(-1.6f, 1.6f, 2.8f), 1f),
            };
            // V32: every view twice, once with the full kart (lod0) and once forced to the far kart (lod1), plus a view
            // from 16 m, about where the swap happens in the race
            var group = kart.GetComponent<LODGroup>();
            var shots = new List<(string name, Vector3 pos, float steer, int lod)>();
            foreach (var v in views)
            {
                shots.Add(("lod0_" + v.name, v.pos, v.steer, 0));
                shots.Add(("lod1_" + v.name, v.pos, v.steer, 1));
            }
            shots.Add(("lod0_16m", new Vector3(-9f, 3f, 13f), 0f, 0));
            shots.Add(("lod1_16m", new Vector3(-9f, 3f, 13f), 0f, 1));
            foreach (var v in shots)
            {
                group.ForceLOD(v.lod);
                cam.fieldOfView = v.name.EndsWith("16m", StringComparison.Ordinal) ? 60f : 35f;
                steering.steer = v.steer;
                steering.ShowNow();
                kart.GetComponent<KartDriverArms>().SolveNow();
                cam.transform.position = v.pos;
                cam.transform.LookAt(new Vector3(0f, 0.45f, 0f));
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(folder, "kart_" + v.name + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            RenderTexture.active = null;
            cam.targetTexture = null;
        }

        // ---------- helpers ----------
        private static Transform Find(GameObject root, string name)
        {
            Transform t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            if (t == null)
            {
                throw new InvalidOperationException("NovaKartVisualBuilder: part '" + name + "' not found in " + root.name);
            }
            return t;
        }

        private static Bounds Bounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs)
            {
                b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static void Set(Material m, string property, float value)
        {
            if (m.HasProperty(property))
            {
                m.SetFloat(property, value);
            }
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
