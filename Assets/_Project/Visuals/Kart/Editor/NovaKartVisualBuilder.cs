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
            foreach (string path in new[] { KartFbx, TyreFbx, CharFbx })
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
            foreach (string path in new[] { KartFbx, TyreFbx, CharFbx })
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
            foreach (string w in Wheels)
            {
                Transform anchor = Find(kart, w);
                if (anchor.GetComponentsInChildren<Renderer>(true).Length == 0)
                {
                    var tyre = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TyreFbx));
                    tyre.transform.SetParent(anchor, false);
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
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
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
            UnityEngine.Object.DestroyImmediate(go);
            report.AppendLine(failures == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + failures + ")");
            Debug.Log(report.ToString());
            return failures;
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
            foreach (var v in views)
            {
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
