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
    // Driver select (Luc, 17 Nov 2026): the other three drivers of Luc's NovaKart roster (Art/Export, v12), each in
    // their own kart, like the Rookie in the Nova Classic (NovaKartVisualBuilder, which this builder leaves alone):
    //   Tiago (Carcavelos surfer)  + Beach Buggy + Offroad tyres
    //   Nova  (space cadet)        + Orbit       + Balloon tyres
    //   Amara (kart mechanic)      + Street GT   + Retro tyres
    // Same prefab layout as KartVisual_Classic_Rookie (kart forward +Z, pivot on the ground, tyres on Wheel_*,
    // driver behind Steering_Wheel, KartVisualSteering + KartVisualFromState + KartDriverArms), so the race, the
    // bots and BotKartPaint treat every kart alike.
    // All four karts share material NAMES but not colours (NK_Paint_Body is white on the Classic, turquoise on the
    // Buggy), so each kart's materials live in their own folder Materials/<Kart>/. Driver and tyre materials have
    // unique names (NT_Tyre is the same black rubber everywhere and is shared).
    // Far version (V32, like the Classic): tools/kart_lod/make_kart_lod.py -- <Kart> <Tyre> makes Kart_<Kart>_Far and
    // Tyre_<Tyre>_Far plus the tyre's ID / AO / normal maps; this builder paints the palette and the far tyre from the
    // real materials and adds a cross-fading LODGroup (full kart near, light kart + same driver far, nothing very far).
    // Menu Carcavelos Circuit > Build Roster Karts (Role 3), or batch mode:
    //   -executeMethod CarcavelosCircuit.Visuals.Kart.Editor.NovaKartRosterBuilder.BuildAndCheck [-kartShots <folder>]
    public static class NovaKartRosterBuilder
    {
        private const string Root = "Assets/_Project/Visuals/Kart";
        private const string MatDir = Root + "/Materials";
        private const float NearUntil = 0.12f;  // like the Classic: full kart while it fills at least 12 % of the screen
        private const float CullBelow = 0.006f; // like the Classic: below 0.6 % of the screen height the kart is hidden
        private const int PaletteCellPixels = 4;
        private const float AoStrength = 0.8f;
        private static readonly string[] Wheels = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };

        public class Combo
        {
            public string Driver, Kart, Tyre;
            public float TyreRadius;   // Art/Export/Tyres/tyres.json
            public Color Glove;        // the glove colour from the driver's FBX
            public string PrefabPath { get { return Root + "/Prefabs/KartVisual_" + Kart + "_" + Driver + ".prefab"; } }
            public string KartFbx { get { return Root + "/Models/Kart_" + Kart + ".fbx"; } }
            public string TyreFbx { get { return Root + "/Models/Tyre_" + Tyre + ".fbx"; } }
            public string CharFbx { get { return Root + "/Models/Char_" + Driver + ".fbx"; } }
            public string Tex { get { return Root + "/Textures/Char_" + Driver + "_"; } }
            public string KartMatDir { get { return MatDir + "/" + Kart; } }
            public string KartLodFbx { get { return Root + "/Models/Kart_" + Kart + "_Far.fbx"; } }
            public string TyreLodFbx { get { return Root + "/Models/Tyre_" + Tyre + "_Far.fbx"; } }
            public string PaletteJson { get { return "tools/kart_lod/palette_" + Kart + ".json"; } }
            public string PaletteTex { get { return Root + "/Textures/KartLOD_" + Kart + "_Palette_"; } }
            public string TyreIdMap { get { return "tools/kart_lod/tyre_far_" + Tyre + "_id.png"; } }
            public string TyreAoMap { get { return "tools/kart_lod/tyre_far_" + Tyre + "_ao.png"; } }
            public string TyreFarTex { get { return Root + "/Textures/TyreFar_" + Tyre + "_"; } }
            public string TyreFarMaterial { get { return "NT_TyreFar_" + Tyre; } }
        }

        public static readonly Combo[] Combos =
        {
            new Combo { Driver = "Tiago", Kart = "Buggy", Tyre = "Offroad", TyreRadius = 0.2205f, Glove = new Color(0.01f, 0.42f, 0.38f) },
            new Combo { Driver = "Nova", Kart = "Orbit", Tyre = "Balloon", TyreRadius = 0.2352f, Glove = new Color(0.8f, 0.82f, 0.86f) },
            new Combo { Driver = "Amara", Kart = "StreetGT", Tyre = "Retro", TyreRadius = 0.21f, Glove = new Color(0.035f, 0.035f, 0.04f) }
        };

        [MenuItem("Carcavelos Circuit/Build Roster Karts (Role 3)")]
        public static void Build()
        {
            foreach (Combo c in Combos)
            {
                ImportSettings(c);
                MakeMaterials(c);
                BuildPrefab(c);
            }
            RegisterKarts();
            AssetDatabase.SaveAssets();
            Debug.Log("NovaKartRosterBuilder: " + Combos.Length + " roster karts built.");
        }

        public static void BuildAndCheck()
        {
            int failures = 0;
            try
            {
                Build();
                foreach (Combo c in Combos)
                {
                    failures += Check(c);
                }
                string shots = Argument("-kartShots");
                if (!string.IsNullOrEmpty(shots))
                {
                    RenderLineup(shots);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
            }
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        // The race finds the four karts through BotSettings (BotSquad swaps the player's and the bots' models).
        private const string BotSettingsPath = "Assets/_Project/Visuals/Bots/BotSettings.asset";

        private static void RegisterKarts()
        {
            ScriptableObject settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(BotSettingsPath);
            if (settings == null)
            {
                Debug.LogWarning("NovaKartRosterBuilder: no " + BotSettingsPath + ", so the race cannot swap karts yet.");
                return;
            }
            var paths = new List<string> { Root + "/Prefabs/KartVisual_Classic_Rookie.prefab" };
            paths.AddRange(Combos.Select(c => c.PrefabPath));
            var so = new SerializedObject(settings);
            SerializedProperty list = so.FindProperty("kartVisuals");
            list.arraySize = paths.Count;
            for (int i = 0; i < paths.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        // ---------- import ----------
        private static void ImportSettings(Combo c)
        {
            foreach (string path in new[] { c.KartFbx, c.TyreFbx, c.CharFbx, c.KartLodFbx, c.TyreLodFbx })
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(path);
                mi.globalScale = 1f;
                mi.useFileScale = true;
                mi.bakeAxisConversion = true;
                mi.importCameras = false;
                mi.importLights = false;
                mi.importAnimation = false;
                mi.animationType = path == c.CharFbx ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                mi.SaveAndReimport();
            }
            var normal = (TextureImporter)AssetImporter.GetAtPath(c.Tex + "Normal.png");
            normal.textureType = TextureImporterType.NormalMap;
            normal.maxTextureSize = 2048;
            normal.SaveAndReimport();
            var mask = (TextureImporter)AssetImporter.GetAtPath(c.Tex + "MetallicSmoothness.png");
            mask.sRGBTexture = false;
            mask.alphaSource = TextureImporterAlphaSource.FromInput;
            mask.maxTextureSize = 1024;
            mask.SaveAndReimport();
            var albedo = (TextureImporter)AssetImporter.GetAtPath(c.Tex + "BaseColor.png");
            albedo.maxTextureSize = 2048;
            albedo.SaveAndReimport();
        }

        // ---------- materials ----------
        private static void MakeMaterials(Combo c)
        {
            Directory.CreateDirectory(c.KartMatDir);
            AssetDatabase.Refresh();
            // the far files come last: their NK_Paint_Body must find the material the full kart already made
            foreach (string path in new[] { c.KartFbx, c.TyreFbx, c.CharFbx, c.KartLodFbx, c.TyreLodFbx })
            {
                string folder = path == c.KartFbx || path == c.KartLodFbx ? c.KartMatDir : MatDir;
                var mi = (ModelImporter)AssetImporter.GetAtPath(path);
                var names = new HashSet<string>(mi.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name));
                var embedded = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Material>().ToList();
                foreach (Material e in embedded)
                {
                    names.Add(e.name);
                }
                foreach (string name in names)
                {
                    string matPath = folder + "/" + name + ".mat";
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat == null)
                    {
                        Material source = embedded.FirstOrDefault(m => m.name == name);
                        mat = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        AssetDatabase.CreateAsset(mat, matPath);
                    }
                    if (folder == MatDir && name == "NT_Tyre")
                    {
                        // the shared rubber: the Classic's builder owns its look, leave it as it is
                    }
                    else
                    {
                        NovaKartVisualBuilder.Look(mat);
                        ExtraLook(mat, c);
                    }
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
                }
                mi.SaveAndReimport();
            }
            MakePalette(c);
            MakeFarTyre(c);
        }

        // ---------- far version (V32): palette for the body, baked look for the tyre ----------
        [Serializable] private class Palette { public int grid; public string paint; public string[] materials; public TyreBake tyre_bake; }
        [Serializable] private class TyreBake { public int size; public TyreId[] ids; }
        [Serializable] private class TyreId { public string name; public float r, g, b; }

        // One 4 x 4 pixel cell per kart material (colour, metal + smoothness, glow), read from the real materials.
        private static void MakePalette(Combo c)
        {
            Palette palette = JsonUtility.FromJson<Palette>(File.ReadAllText(c.PaletteJson));
            int size = palette.grid * PaletteCellPixels;
            var colour = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var glow = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float glowStrength = 1f;
            foreach (string name in palette.materials)
            {
                Material m = Source(c.KartMatDir, name);
                if (m.IsKeywordEnabled("_EMISSION"))
                {
                    glowStrength = Mathf.Max(glowStrength, m.GetColor("_EmissionColor").maxColorComponent);
                }
            }
            for (int i = 0; i < size * size; i++)
            {
                colour.SetPixel(i % size, i / size, Color.magenta); // unused cells: easy to spot if a UV goes wrong
                mask.SetPixel(i % size, i / size, Color.black);
                glow.SetPixel(i % size, i / size, Color.black);
            }
            for (int i = 0; i < palette.materials.Length; i++)
            {
                Material m = Source(c.KartMatDir, palette.materials[i]);
                Color col = m.GetColor("_BaseColor");
                Color e = m.IsKeywordEnabled("_EMISSION") ? m.GetColor("_EmissionColor") / glowStrength : Color.black;
                FillCell(colour, i, palette.grid, new Color(col.r, col.g, col.b, 1f));
                FillCell(mask, i, palette.grid, new Color(m.GetFloat("_Metallic"), 0f, 0f, m.GetFloat("_Smoothness")));
                FillCell(glow, i, palette.grid, new Color(e.r, e.g, e.b, 1f));
            }
            Material lod = Source(c.KartMatDir, "NK_KartLOD");
            lod.shader = Shader.Find("Universal Render Pipeline/Lit");
            lod.SetTexture("_BaseMap", SavePalette(colour, c.PaletteTex + "Colour.png", true));
            lod.SetColor("_BaseColor", Color.white);
            lod.SetTexture("_MetallicGlossMap", SavePalette(mask, c.PaletteTex + "MetalSmooth.png", false));
            lod.EnableKeyword("_METALLICSPECGLOSSMAP");
            NovaKartVisualBuilder.Set(lod, "_Smoothness", 1f);
            lod.SetTexture("_EmissionMap", SavePalette(glow, c.PaletteTex + "Glow.png", true));
            lod.SetColor("_EmissionColor", Color.white * glowStrength);
            lod.EnableKeyword("_EMISSION");
            lod.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(lod);
        }

        // The far tyre: Blender baked which tyre material is where (ID colours from the palette file) and the shade;
        // here every ID colour becomes that material's real colour and metal value.
        private static void MakeFarTyre(Combo c)
        {
            Palette palette = JsonUtility.FromJson<Palette>(File.ReadAllText(c.PaletteJson));
            var ids = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ids.LoadImage(File.ReadAllBytes(c.TyreIdMap));
            var occlusion = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            occlusion.LoadImage(File.ReadAllBytes(c.TyreAoMap));
            var colour = new Texture2D(ids.width, ids.height, TextureFormat.RGBA32, true);
            var mask = new Texture2D(ids.width, ids.height, TextureFormat.RGBA32, true);
            Material rubber = Source(MatDir, "NT_Tyre");
            // a tyre material without faces (NT_Balloon_Hub) is not imported by Unity and never shows in the ID map
            var looks = palette.tyre_bake.ids
                .Select(id => AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + id.name + ".mat") ?? rubber).ToArray();
            for (int y = 0; y < ids.height; y++)
            {
                for (int x = 0; x < ids.width; x++)
                {
                    Color id = ids.GetPixel(x, y);
                    Material m = id.maxColorComponent < 0.5f ? rubber : looks[NearestId(palette.tyre_bake.ids, id)];
                    float shade = occlusion.GetPixel(x, y).r;
                    if (shade < 0.15f)
                    {
                        shade = 1f; // a bake ray that grazed the inside at the tread edge, not real shade
                    }
                    Color col = m.GetColor("_BaseColor") * Mathf.Lerp(1f, shade, AoStrength);
                    colour.SetPixel(x, y, new Color(col.r, col.g, col.b, 1f));
                    mask.SetPixel(x, y, new Color(m.GetFloat("_Metallic"), 0f, 0f, m.GetFloat("_Smoothness")));
                }
            }
            UnityEngine.Object.DestroyImmediate(ids);
            UnityEngine.Object.DestroyImmediate(occlusion);
            Texture2D colourTex = SaveTyreTexture(colour, c.TyreFarTex + "Colour.png", true);
            Texture2D maskTex = SaveTyreTexture(mask, c.TyreFarTex + "MetalSmooth.png", false);
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(c.TyreFarTex + "Normal.png");
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.maxTextureSize = 512;
            normalImporter.SaveAndReimport();
            Material tyre = Source(MatDir, c.TyreFarMaterial);
            tyre.shader = Shader.Find("Universal Render Pipeline/Lit");
            tyre.SetTexture("_BaseMap", colourTex);
            tyre.SetColor("_BaseColor", Color.white);
            tyre.SetTexture("_MetallicGlossMap", maskTex);
            tyre.EnableKeyword("_METALLICSPECGLOSSMAP");
            NovaKartVisualBuilder.Set(tyre, "_Smoothness", 1f);
            tyre.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(c.TyreFarTex + "Normal.png"));
            tyre.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(tyre);
        }

        private static Material Source(string folder, string name)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + name + ".mat");
            if (m == null)
            {
                throw new InvalidOperationException("NovaKartRosterBuilder: the palette names " + name + ", but " + folder + " has no such material.");
            }
            return m;
        }

        private static int NearestId(TyreId[] ids, Color col)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < ids.Length; i++)
            {
                float d = Mathf.Abs(col.r - ids[i].r) + Mathf.Abs(col.g - ids[i].g) + Mathf.Abs(col.b - ids[i].b);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        private static void FillCell(Texture2D tex, int index, int grid, Color col)
        {
            int x0 = index % grid * PaletteCellPixels, y0 = index / grid * PaletteCellPixels;
            for (int y = 0; y < PaletteCellPixels; y++)
            {
                for (int x = 0; x < PaletteCellPixels; x++)
                {
                    tex.SetPixel(x0 + x, y0 + y, col);
                }
            }
        }

        // A palette PNG imported sharp: no filtering, no mipmaps, no compression (it is 16 x 16 pixels).
        private static Texture2D SavePalette(Texture2D tex, string path, bool srgb)
        {
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

        private static Texture2D SaveTyreTexture(Texture2D tex, string path, bool srgb)
        {
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

        // What the Classic's look table does not know: the driver's own textures and gloves, glass, chrome.
        private static void ExtraLook(Material m, Combo c)
        {
            string n = m.name;
            if (n == "NK_Char_" + c.Driver)
            {
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(c.Tex + "BaseColor.png"));
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(c.Tex + "Normal.png"));
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(c.Tex + "MetallicSmoothness.png"));
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                NovaKartVisualBuilder.Set(m, "_Smoothness", 1f);
            }
            else if (n == "NK_Glove_" + c.Driver)
            {
                m.SetColor("_BaseColor", c.Glove);
            }
            else if (n.Contains("Glass"))
            {
                NovaKartVisualBuilder.Set(m, "_Metallic", 0.2f);
                NovaKartVisualBuilder.Set(m, "_Smoothness", 0.92f);
            }
            else if (n.Contains("Chrome"))
            {
                NovaKartVisualBuilder.Set(m, "_Metallic", 1f);
                NovaKartVisualBuilder.Set(m, "_Smoothness", 0.85f);
            }
            EditorUtility.SetDirty(m);
        }

        // ---------- prefab ----------
        private static void BuildPrefab(Combo c)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(c.PrefabPath));
            AssetDatabase.Refresh();
            var root = new GameObject(Path.GetFileNameWithoutExtension(c.PrefabPath));
            var kart = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(c.KartFbx));
            kart.transform.SetParent(root.transform, false);
            if (NovaKartVisualBuilder.Find(kart, "Wheel_FL").position.z < NovaKartVisualBuilder.Find(kart, "Wheel_RL").position.z)
            {
                kart.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // forward must be +Z
            }
            // the far kart sits exactly on the full one (same place and turn), or it jumps when Unity swaps them
            var kartFar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(c.KartLodFbx));
            kartFar.transform.SetParent(root.transform, false);
            kartFar.transform.localRotation = kart.transform.localRotation;
            var far = new List<Renderer>(kartFar.GetComponentsInChildren<Renderer>(true));
            foreach (string w in Wheels)
            {
                Transform anchor = NovaKartVisualBuilder.Find(kart, w);
                if (anchor.GetComponentsInChildren<Renderer>(true).Length == 0)
                {
                    var tyre = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(c.TyreFbx));
                    tyre.transform.SetParent(anchor, false);
                    var tyreFar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(c.TyreLodFbx));
                    tyreFar.transform.SetParent(anchor, false); // on the same anchor: it still rolls and steers
                    far.AddRange(tyreFar.GetComponentsInChildren<Renderer>(true));
                }
            }
            var driver = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(c.CharFbx));
            driver.name = "Driver_" + c.Driver;
            driver.transform.SetParent(root.transform, false);
            driver.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);  // armature FBX arrives facing -Z
            Bounds body = NovaKartVisualBuilder.Bounds(driver);
            if (body.center.z > NovaKartVisualBuilder.Find(kart, "Steering_Wheel").position.z)
            {
                driver.transform.localRotation = Quaternion.identity;
            }
            var steering = root.AddComponent<KartVisualSteering>();
            steering.wheelRadius = c.TyreRadius;
            root.AddComponent<KartVisualFromState>();
            root.AddComponent<KartDriverArms>();
            // near: the full kart; far: the light kart and tyres plus the same driver (the arms' IK needs the full rig)
            var near = root.GetComponentsInChildren<Renderer>(true).Where(r => !far.Contains(r)).ToList();
            var farWithDriver = new List<Renderer>(far);
            farWithDriver.AddRange(driver.GetComponentsInChildren<Renderer>(true));
            var group = root.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(NearUntil, near.ToArray()), new LOD(CullBelow, farWithDriver.ToArray()) });
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            group.RecalculateBounds();
            root.AddComponent<KartLodWarmup>(); // compiles the far materials and the fade before the race starts
            PrefabUtility.SaveAsPrefabAsset(root, c.PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        // ---------- checks ----------
        private static int Check(Combo c)
        {
            var report = new StringBuilder("Roster kart check: " + c.Driver + " in the " + c.Kart + "\n");
            int failures = 0;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(c.PrefabPath));
            Transform fl = NovaKartVisualBuilder.Find(go, "Wheel_FL"), fr = NovaKartVisualBuilder.Find(go, "Wheel_FR");
            Transform rl = NovaKartVisualBuilder.Find(go, "Wheel_RL"), rr = NovaKartVisualBuilder.Find(go, "Wheel_RR");
            Transform wheel = NovaKartVisualBuilder.Find(go, "Steering_Wheel");
            bool forward = fl.position.z > rl.position.z && fr.position.z > rr.position.z && fl.position.x < 0f;
            report.AppendLine("front ahead (+Z), left at -X: " + forward);
            if (!forward) failures++;
            float lowest = float.MaxValue;
            foreach (Transform w in new[] { fl, fr, rl, rr })
            {
                lowest = Mathf.Min(lowest, NovaKartVisualBuilder.Bounds(w.gameObject).min.y);
            }
            report.AppendLine("tyres touch the ground: lowest y = " + lowest.ToString("0.000") + " m");
            if (Mathf.Abs(lowest) > 0.03f) failures++;
            Bounds driver = PosedBounds(NovaKartVisualBuilder.Find(go, "Driver_" + c.Driver).gameObject);
            bool seated = Mathf.Abs(driver.center.x) < 0.15f && driver.center.z < wheel.position.z && driver.min.y > -0.02f;
            report.AppendLine("driver behind the wheel and centred: " + seated + " (centre " + driver.center.ToString("0.00") +
                              ", lowest y " + driver.min.y.ToString("0.000") + ", wheel z " + wheel.position.z.ToString("0.00") + ")");
            if (!seated) failures++;
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
            report.AppendLine("triangles: " + tris + ", material problems: " + bad);
            if (bad > 0) failures++;
            var steering = go.GetComponent<KartVisualSteering>();
            var arms = go.GetComponent<KartDriverArms>();
            float worstGrip = 0f;
            arms.SolveNow();
            foreach (float steer in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
            {
                steering.steer = steer;
                steering.ShowNow();
                arms.SolveNow();
                worstGrip = Mathf.Max(worstGrip, arms.WorstGripError());
            }
            report.AppendLine("hands stay on the rim: worst " + (worstGrip * 100f).ToString("0.0") + " cm (limit 1.5 cm)");
            if (worstGrip > 0.015f) failures++;
            steering.steer = 0f; // wheel straight again: a turned deep-dish wheel sticks out of the body's box
            steering.ShowNow();
            failures += CheckLevelsOfDetail(go, c, report);
            UnityEngine.Object.DestroyImmediate(go);
            report.AppendLine(failures == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + failures + ")");
            Debug.Log(report.ToString());
            return failures;
        }

        // The driver as posed in the seat: a skinned mesh's renderer bounds are only a rough box (they reached 14 cm
        // below the ground), so measure the baked vertices instead.
        private static Bounds PosedBounds(GameObject driver)
        {
            bool first = true;
            Bounds bounds = new Bounds(driver.transform.position, Vector3.zero);
            foreach (SkinnedMeshRenderer skin in driver.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh baked = new Mesh();
                skin.BakeMesh(baked, true);
                foreach (Vector3 v in baked.vertices)
                {
                    Vector3 p = skin.transform.TransformPoint(v);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else { bounds.Encapsulate(p); }
                }
                UnityEngine.Object.DestroyImmediate(baked);
            }
            return bounds;
        }

        // V32 rules: the far kart is much cheaper, sits where the full one sits, keeps the paint slot for BotKartPaint.
        private static int CheckLevelsOfDetail(GameObject go, Combo c, StringBuilder report)
        {
            var group = go.GetComponent<LODGroup>();
            if (group == null || group.lodCount != 2)
            {
                report.AppendLine("LOD: missing LODGroup with 2 levels");
                return 1;
            }
            int failures = 0;
            LOD[] lods = group.GetLODs();
            int nearTris = Triangles(lods[0].renderers), farTris = Triangles(lods[1].renderers);
            int nearDraws = lods[0].renderers.Sum(r => r.sharedMaterials.Length), farDraws = lods[1].renderers.Sum(r => r.sharedMaterials.Length);
            report.AppendLine("LOD near: " + nearTris + " triangles, " + nearDraws + " draw calls; far: " + farTris + " triangles, " + farDraws + " draw calls");
            if (farTris > nearTris * 0.4f || farDraws > 10)
            {
                report.AppendLine("  FAIL: far kart should be at most 40 % of the triangles and 10 draw calls");
                failures++;
            }
            Bounds bodyNear = NovaKartVisualBuilder.Find(go, "Kart_Body").GetComponent<Renderer>().bounds;
            foreach (string part in new[] { "Kart_Details", "Seat", "Steering_Wheel" })
            {
                bodyNear.Encapsulate(NovaKartVisualBuilder.Find(go, part).GetComponent<Renderer>().bounds);
            }
            Transform kartFar = NovaKartVisualBuilder.Find(go, "Kart_" + c.Kart + "_Far");
            failures += SameSpot(report, "body", bodyNear, kartFar.GetComponent<Renderer>().bounds);
            foreach (string w in Wheels)
            {
                Renderer tyreNear = null, tyreFar = null;
                foreach (Renderer r in NovaKartVisualBuilder.Find(go, w).GetComponentsInChildren<Renderer>(true))
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
            bool paintSlot = kartFar.GetComponent<Renderer>().sharedMaterials.Any(m => m != null && m.name == "NK_Paint_Body");
            report.AppendLine("far kart keeps NK_Paint_Body for BotKartPaint: " + paintSlot);
            if (!paintSlot) failures++;
            return failures;
        }

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

        // Batch mode: only the lineup pictures. -executeMethod ...NovaKartRosterBuilder.LineupOnly -kartShots <folder>
        public static void LineupOnly()
        {
            RenderLineup(Argument("-kartShots"));
            EditorApplication.Exit(0);
        }

        // All four karts side by side, front left and from behind, for a quick look.
        private static void RenderLineup(string folder)
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
            ground.transform.localScale = Vector3.one * 3f;
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.SetColor("_BaseColor", new Color(0.85f, 0.79f, 0.66f));
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;
            var paths = new List<string> { Root + "/Prefabs/KartVisual_Classic_Rookie.prefab" };
            paths.AddRange(Combos.Select(c => c.PrefabPath));
            for (int i = 0; i < paths.Count; i++)
            {
                var kart = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]));
                kart.transform.position = new Vector3((i - 1.5f) * 2.2f, 0f, 0f);
                kart.GetComponent<KartDriverArms>().SolveNow();
                // off the SRP Batcher, which draws wrong in batch mode (see DriverPortraitBuilder)
                var own = new MaterialPropertyBlock();
                own.SetFloat("_LineupShot", 1f);
                foreach (Renderer part in kart.GetComponentsInChildren<Renderer>(true))
                {
                    part.SetPropertyBlock(own);
                }
            }
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.66f, 0.8f, 0.95f);
            cam.fieldOfView = 35f;
            var rt = new RenderTexture(1600, 700, 24);
            cam.targetTexture = rt;
            var groups = UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
            foreach (var v in new[] { ("front", new Vector3(-3f, 2.6f, 8.5f), 0), ("back", new Vector3(3f, 2.6f, -8.5f), 0),
                                      ("front_far", new Vector3(-3f, 2.6f, 8.5f), 1) })
            {
                foreach (LODGroup g in groups)
                {
                    g.ForceLOD(v.Item3); // 1 = the light far kart, to compare it with the full one
                }
                cam.transform.position = v.Item2;
                cam.transform.LookAt(new Vector3(0f, 0.45f, 0f));
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1600, 700, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1600, 700), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(folder, "roster_" + v.Item1 + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            RenderTexture.active = null;
            cam.targetTexture = null;
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
