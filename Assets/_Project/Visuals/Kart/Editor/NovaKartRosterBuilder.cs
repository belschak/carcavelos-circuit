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
    // No far version (LOD) yet: these karts are drawn in full until they get too small on screen.
    // Menu Carcavelos Circuit > Build Roster Karts (Role 3), or batch mode:
    //   -executeMethod CarcavelosCircuit.Visuals.Kart.Editor.NovaKartRosterBuilder.BuildAndCheck [-kartShots <folder>]
    public static class NovaKartRosterBuilder
    {
        private const string Root = "Assets/_Project/Visuals/Kart";
        private const string MatDir = Root + "/Materials";
        private const float CullBelow = 0.006f; // like the Classic: below 0.6 % of the screen height the kart is hidden
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
            foreach (string path in new[] { c.KartFbx, c.TyreFbx, c.CharFbx })
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
            foreach (string path in new[] { c.KartFbx, c.TyreFbx, c.CharFbx })
            {
                string folder = path == c.KartFbx ? c.KartMatDir : MatDir;
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
            foreach (string w in Wheels)
            {
                Transform anchor = NovaKartVisualBuilder.Find(kart, w);
                if (anchor.GetComponentsInChildren<Renderer>(true).Length == 0)
                {
                    var tyre = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(c.TyreFbx));
                    tyre.transform.SetParent(anchor, false);
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
            var group = root.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(CullBelow, root.GetComponentsInChildren<Renderer>(true)) });
            group.RecalculateBounds();
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
            foreach (var v in new[] { ("front", new Vector3(-3f, 2.6f, 8.5f)), ("back", new Vector3(3f, 2.6f, -8.5f)) })
            {
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
