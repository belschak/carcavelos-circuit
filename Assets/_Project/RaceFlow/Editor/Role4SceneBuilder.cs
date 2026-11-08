using CarcavelosCircuit.Audio;
using CarcavelosCircuit.Shared;
using CarcavelosCircuit.Shared.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow.Editor
{
    // Builds everything Role 4 needs from code, so nobody hand-edits scene files and the result is the same
    // on every laptop: settings, HUD panel, kart + kart-audio prefabs, the placeholder test track and Main.unity.
    // Menu: Carcavelos Circuit > Build Role 4 Scenes. Command line (Unity closed):
    //   Unity.exe -batchmode -projectPath . -executeMethod CarcavelosCircuit.RaceFlow.Editor.Role4SceneBuilder.BuildAll -quit
    // Safe to run again: the placeholder track is rebuilt, RaceSettings keeps its values. In Main.unity only the
    // builder's own root objects are rebuilt; root objects someone else added are kept. If someone added components
    // or children to the builder's own objects, the builder stops (batch mode: error, exit code 1; menu: asks first).
    public static class Role4SceneBuilder
    {
        private const string Root = "Assets/_Project/";
        private const string SettingsPath = Root + "RaceFlow/Settings/RaceSettings.asset";
        private const string ThemePath = Root + "RaceFlow/UI/RaceHudTheme.tss";
        private const string PanelPath = Root + "RaceFlow/UI/RaceHudPanelSettings.asset";
        private const string HudLayoutPath = Root + "RaceFlow/UI/RaceHud.uxml";
        private const string KartPrefabPath = Root + "RaceFlow/Prefabs/PlaceholderKart.prefab";
        private const string KartAudioPrefabPath = Root + "Audio/Prefabs/KartAudio.prefab";
        private const string ClipsFolder = Root + "Audio/Clips/";
        private const string MaterialsFolder = Root + "RaceFlow/Materials/";
        private const string MainScenePath = Root + "RaceFlow/Scenes/Main.unity";
        private const string TrackScenePath = Root + "RaceFlow/Scenes/RaceFlow_PlaceholderTrack.unity";
        private const string VolumeProfilePath = Root + "RaceFlow/Settings/RaceCameraVolume.asset";
        private const string CampusScenePath = Root + "Track/Scenes/Track_Campus.unity";

        [MenuItem("Carcavelos Circuit/Build Role 4 Scenes")]
        public static void BuildAll()
        {
            // The builder opens other scenes, so unsaved edits in the open scene would be lost without asking.
            // (In batch mode nothing is open and this simply returns true.)
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("Role4SceneBuilder: cancelled, nothing was changed.");
                return;
            }
            if (!MayRebuildMain())
            {
                return;
            }

            foreach (string folder in new[] { "RaceFlow/Settings", "RaceFlow/UI", "RaceFlow/Prefabs", "RaceFlow/Materials", "RaceFlow/Scenes", "Audio/Prefabs", "Audio/Clips" })
            {
                ProjectSetup.EnsureFolder(Root + folder);
            }

            SetUpAudioImport();
            RaceSettings settings = CreateSettings();
            PanelSettings panel = CreatePanelSettings();
            GameObject kartAudio = CreateKartAudioPrefab();
            GameObject kart = FindRealKartPrefab();
            if (kart == null)
            {
                kart = CreatePlaceholderKartPrefab(); // until Role 1's Kart prefab exists
            }
            BuildPlaceholderTrackScene();
            BuildMainScene(settings, panel, kart, kartAudio);

            MergeBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("Role4SceneBuilder: done.");
        }

        // Build order: Main first (the game starts there), then the tracks it can load, then every other scene that
        // someone added to File > Build Profiles (kept with its tick box). Merged, never replaced.
        private static void MergeBuildScenes()
        {
            string[] ours = { MainScenePath, CampusScenePath, TrackScenePath };
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (string path in ours)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (System.Array.IndexOf(ours, existing.path) < 0)
                {
                    scenes.Add(existing);
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------- guard for Main.unity ----------

        // The builder's own root objects in Main.unity and the only components it puts on each of them.
        // null = not a builder object: someone else's, kept on every rebuild.
        private static System.Type[] BuilderComponents(string rootName)
        {
            switch (rootName)
            {
                case "Main Camera":
                    return new[] { typeof(Transform), typeof(Camera), typeof(AudioListener), typeof(PlaceholderChaseCamera), typeof(UniversalAdditionalCameraData) };
                case "PostProcessing":
                    return new[] { typeof(Transform), typeof(Volume) };
                case "RaceSystems":
                    return new[] { typeof(Transform), typeof(RaceManager), typeof(RaceBootstrap), typeof(AudioSource), typeof(RaceSounds) };
                case "HUD":
                    return new[] { typeof(Transform), typeof(UIDocument), typeof(RaceHud) };
                case "Music":
                    // MusicPlayer = the old music script (before P4), still allowed so an old Main.unity can be rebuilt.
                    return new[] { typeof(Transform), typeof(AudioSource), typeof(RaceMusic), typeof(MusicPlayer) };
                default:
                    return null;
            }
        }

        // Rebuilding Main recreates the builder's own root objects. Hand-added children or components on them would
        // be lost, so we look first. Returns "" if there are none, otherwise one line per problem.
        private static string FindHandEditsInMain()
        {
            if (!System.IO.File.Exists(MainScenePath))
            {
                return "";
            }
            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            string report = "";
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                System.Type[] allowed = BuilderComponents(root.name);
                if (allowed == null)
                {
                    continue; // someone else's object, it is kept anyway
                }
                if (root.transform.childCount > 0)
                {
                    report += "\n- " + root.name + " has child objects";
                }
                foreach (Component component in root.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        report += "\n- " + root.name + " has a missing script";
                    }
                    else if (System.Array.IndexOf(allowed, component.GetType()) < 0)
                    {
                        report += "\n- " + root.name + " has an extra " + component.GetType().Name;
                    }
                }
            }
            return report;
        }

        // False = stop the whole build. Batch mode never overwrites hand edits (error + exception = exit code 1),
        // the menu asks first.
        private static bool MayRebuildMain()
        {
            string handEdits = FindHandEditsInMain();
            if (handEdits == "")
            {
                return true;
            }
            string message = "Main.unity has hand edits on objects the builder recreates, a rebuild would lose them:" + handEdits
                + "\nMove them to a root object of your own (those are kept) or add them to Role4SceneBuilder.";
            if (Application.isBatchMode)
            {
                Debug.LogError("Role4SceneBuilder: refused, nothing was changed. " + message);
                throw new System.Exception("Role4SceneBuilder: refused to overwrite hand edits in Main.unity.");
            }
            if (EditorUtility.DisplayDialog("Build Role 4 Scenes", message, "Rebuild anyway", "Cancel"))
            {
                return true;
            }
            Debug.LogWarning("Role4SceneBuilder: cancelled, nothing was changed.");
            return false;
        }

        // ---------- assets ----------

        private static RaceSettings CreateSettings()
        {
            RaceSettings settings = AssetDatabase.LoadAssetAtPath<RaceSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<RaceSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            EditorUtility.SetDirty(settings); // writes settings added later to the file, with their default values
            return settings;
        }

        // UI Toolkit needs a PanelSettings asset (screen scaling) and a theme (default fonts and controls).
        private static PanelSettings CreatePanelSettings()
        {
            if (!System.IO.File.Exists(ThemePath))
            {
                System.IO.File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(ThemePath);
            }
            ThemeStyleSheet theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);

            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        // Kept if it already exists, so Role 3's tuning survives a rebuild.
        private static VolumeProfile CreateVolumeProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile != null)
            {
                return profile;
            }
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);   // only very bright pixels glow
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.6f);
            AssetDatabase.AddObjectToAsset(bloom, profile); // the effect is stored inside the profile file
            AssetDatabase.SaveAssets();
            return profile;
        }

        // Web build rules for all clips (Decompress On Load, Preload, Normalize off, AAC on Web): see AudioImportSetup.
        private static void SetUpAudioImport()
        {
            AudioImportSetup.ApplyAll();
        }

        private static AudioClip Clip(string name)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipsFolder + name + ".wav");
            if (clip == null)
            {
                Debug.LogWarning("Role4SceneBuilder: missing sound " + name + ".wav");
            }
            return clip;
        }

        private static AudioSource AddAudioSource(GameObject target, AudioClip clip, bool loop)
        {
            AudioSource source = target.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D: the camera always follows our own kart
            source.dopplerLevel = 0f; // no pitch wobble, even if someone switches a source to 3D later
            return source;
        }

        private static GameObject CreateKartAudioPrefab()
        {
            // Engine and drift sounds are Luc's (Role 3) kart set, wind is our own synth.
            GameObject go = new GameObject("KartAudio");
            AudioSource engineLow = AddAudioSource(go, Clip("kart_engine_low"), true);
            AudioSource engineHigh = AddAudioSource(go, Clip("kart_engine_high"), true);
            AudioSource squeal = AddAudioSource(go, Clip("kart_drift_squeal"), true);
            AudioSource sparks = AddAudioSource(go, Clip("kart_drift_sparks"), true);
            AudioSource wind = AddAudioSource(go, Clip("amb_wind_loop"), true);
            AudioSource stage = AddAudioSource(go, null, false);
            AudioSource secondStage = AddAudioSource(go, null, false); // the two chime sources take turns
            AudioSource effects = AddAudioSource(go, null, false);
            AudioSource shift = AddAudioSource(go, null, false); // gear-change pop, own source for its random pitch
            KartAudio kartAudio = go.AddComponent<KartAudio>();

            SerializedObject so = new SerializedObject(kartAudio);
            so.FindProperty("engineLowSource").objectReferenceValue = engineLow;
            so.FindProperty("engineHighSource").objectReferenceValue = engineHigh;
            so.FindProperty("squealSource").objectReferenceValue = squeal;
            so.FindProperty("sparksSource").objectReferenceValue = sparks;
            so.FindProperty("windSource").objectReferenceValue = wind;
            so.FindProperty("stageSource").objectReferenceValue = stage;
            so.FindProperty("secondStageSource").objectReferenceValue = secondStage;
            so.FindProperty("effectSource").objectReferenceValue = effects;
            so.FindProperty("shiftSource").objectReferenceValue = shift;
            so.FindProperty("driftStartClip").objectReferenceValue = Clip("kart_drift_start");
            so.FindProperty("stageUpClip").objectReferenceValue = Clip("kart_drift_stage_up");
            so.FindProperty("boostClip").objectReferenceValue = Clip("el_boost_whoosh");
            so.FindProperty("landClip").objectReferenceValue = Clip("sfx_landing_thud");
            so.FindProperty("shiftClip").objectReferenceValue = Clip("sfx_shift_pop");
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, KartAudioPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // Colours from docs/art-style.md (Role 3's palette).
        private static Material ColourMaterial(string name, string hex, float smoothness)
        {
            string path = MaterialsFolder + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            Color colour;
            ColorUtility.TryParseHtmlString(hex, out colour);
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        // A primitive shape without its collider (the kart has one box collider on the root instead).
        private static GameObject VisualPart(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = euler;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        // Swap day for the kart is automatic: as soon as Role 1's prefab "Kart" exists in Driving/ and its root has
        // a component implementing IKartState, the builder puts it into Main instead of the placeholder.
        private static GameObject FindRealKartPrefab()
        {
            string[] guids = AssetDatabase.FindAssets("Kart t:Prefab", new[] { Root + "Driving" });
            foreach (string guid in guids)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.name == "Kart" && prefab.GetComponent<IKartState>() != null)
                {
                    Debug.Log("Role4SceneBuilder: using Role 1's kart " + AssetDatabase.GUIDToAssetPath(guid));
                    return prefab;
                }
            }
            return null;
        }

        private static GameObject CreatePlaceholderKartPrefab()
        {
            Material white = ColourMaterial("KartWhite", "#EAEAEC", 0.6f);
            Material black = ColourMaterial("GlossBlack", "#1D1D1F", 0.8f);
            Material orange = ColourMaterial("BoostOrange", "#FF8A1F", 0.5f);

            GameObject kart = new GameObject("PlaceholderKart");
            Rigidbody body = kart.AddComponent<Rigidbody>();
            body.mass = 150f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            // Slippery box: the kart script sets the speed itself, friction would only fight it.
            PhysicsMaterial slippery = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MaterialsFolder + "KartNoFriction.physicMaterial");
            if (slippery == null)
            {
                slippery = new PhysicsMaterial("KartNoFriction");
                slippery.dynamicFriction = 0f;
                slippery.staticFriction = 0f;
                slippery.frictionCombine = PhysicsMaterialCombine.Minimum;
                AssetDatabase.CreateAsset(slippery, MaterialsFolder + "KartNoFriction.physicMaterial");
            }
            BoxCollider box = kart.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.3f, 0f);
            box.size = new Vector3(1.2f, 0.6f, 1.8f);
            box.sharedMaterial = slippery;
            kart.AddComponent<PlaceholderKart>();

            // "Visual" child = the part Role 3 swaps for the real model (docs/interfaces.md).
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(kart.transform, false);
            VisualPart(PrimitiveType.Cube, "Body", visual.transform, new Vector3(0f, 0.35f, 0f), Vector3.zero, new Vector3(1.1f, 0.3f, 1.7f), white);
            VisualPart(PrimitiveType.Cube, "Seat", visual.transform, new Vector3(0f, 0.6f, -0.3f), Vector3.zero, new Vector3(0.6f, 0.3f, 0.5f), black);
            VisualPart(PrimitiveType.Cube, "Nose", visual.transform, new Vector3(0f, 0.4f, 0.85f), Vector3.zero, new Vector3(0.9f, 0.18f, 0.2f), orange);
            float[] xs = { -0.6f, 0.6f };
            float[] zs = { -0.6f, 0.6f };
            foreach (float x in xs)
            {
                foreach (float z in zs)
                {
                    VisualPart(PrimitiveType.Cylinder, "Wheel", visual.transform, new Vector3(x, 0.2f, z), new Vector3(0f, 0f, 90f), new Vector3(0.4f, 0.1f, 0.4f), black);
                }
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(kart, KartPrefabPath);
            Object.DestroyImmediate(kart);
            return prefab;
        }

        // ---------- placeholder track ----------

        private static GameObject Block(string name, Transform parent, Vector3 position, Vector3 size, Material material, float yaw = 0f, float pitch = 0f)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localEulerAngles = new Vector3(pitch, yaw, 0f);
            block.transform.localScale = size;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            return block;
        }

        // A small rectangular loop (about 230 m, roughly 20 s per lap) to test the race flow until Role 2's campus
        // track has a TrackInfo. Drive direction: anticlockwise seen from above, starting east on the south straight.
        private static void BuildPlaceholderTrackScene()
        {
            Material asphalt = ColourMaterial("Asphalt", "#55585E", 0.2f);
            Material sand = ColourMaterial("Sand", "#E3C48D", 0.1f);
            Material white = ColourMaterial("KartWhite", "#EAEAEC", 0.6f);
            Material blue = ColourMaterial("AtlanticBlue", "#2D8CD6", 0.4f);
            Material orange = ColourMaterial("BoostOrange", "#FF8A1F", 0.5f);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Object.DestroyImmediate(GameObject.Find("Main Camera")); // Main.unity brings the camera
            GameObject sun = GameObject.Find("Directional Light");
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject root = new GameObject("PlaceholderTrack");
            PlaceholderTrack track = root.AddComponent<PlaceholderTrack>();

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localScale = new Vector3(14f, 1f, 12f); // 140 m x 120 m
            ground.GetComponent<MeshRenderer>().sharedMaterial = asphalt;

            // Outer walls (low, blue like the sea) and a sand-coloured block in the middle as a "building".
            Block("Wall North", root.transform, new Vector3(0f, 0.5f, 30.5f), new Vector3(82f, 1f, 1f), blue);
            Block("Wall South", root.transform, new Vector3(0f, 0.5f, -30.5f), new Vector3(82f, 1f, 1f), blue);
            Block("Wall East", root.transform, new Vector3(40.5f, 0.5f, 0f), new Vector3(1f, 1f, 60f), blue);
            Block("Wall West", root.transform, new Vector3(-40.5f, 0.5f, 0f), new Vector3(1f, 1f, 60f), blue);
            Block("Centre Building", root.transform, new Vector3(0f, 2f, 0f), new Vector3(50f, 4f, 30f), sand);

            // Corner cones so the turns are easy to read.
            Vector3[] cones = { new Vector3(26f, 0f, 16f), new Vector3(-26f, 0f, 16f), new Vector3(26f, 0f, -16f), new Vector3(-26f, 0f, -16f) };
            foreach (Vector3 c in cones)
            {
                GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cone.name = "Cone";
                cone.transform.SetParent(root.transform, false);
                cone.transform.localPosition = c + new Vector3(0f, 0.6f, 0f);
                cone.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
                cone.GetComponent<MeshRenderer>().sharedMaterial = orange;
            }

            // A low kicker ramp on the north straight to test air time and the landing sound.
            Block("Ramp", root.transform, new Vector3(0f, 0.25f, 22.5f), new Vector3(6f, 0.3f, 8f), white, 90f, 6f); // rises towards -x, the driving direction here

            // Start/finish line (visual only).
            GameObject line = Block("Start Line", root.transform, new Vector3(0f, 0.01f, -22.5f), new Vector3(1f, 0.02f, 15f), white);
            Object.DestroyImmediate(line.GetComponent<Collider>());

            // Checkpoints: 0 = start/finish, then east, north, west straights. Each gets a respawn pose a bit after it.
            Vector3[] positions = { new Vector3(0f, 2f, -22.5f), new Vector3(32.5f, 2f, 0f), new Vector3(0f, 2f, 22.5f), new Vector3(-32.5f, 2f, 0f) };
            float[] yaws = { 90f, 0f, -90f, 180f };
            PlaceholderCheckpoint[] checkpoints = new PlaceholderCheckpoint[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject cp = new GameObject("Checkpoint " + i);
                cp.transform.SetParent(root.transform, false);
                cp.transform.localPosition = positions[i];
                cp.transform.localRotation = Quaternion.Euler(0f, yaws[i], 0f);
                BoxCollider trigger = cp.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(15f, 4f, 1f); // across the 15 m wide road

                GameObject respawn = new GameObject("RespawnPoint");
                respawn.transform.SetParent(cp.transform, false);
                respawn.transform.localPosition = new Vector3(0f, -1.5f, 4f); // 4 m after the line, 0.5 m above ground

                PlaceholderCheckpoint checkpoint = cp.AddComponent<PlaceholderCheckpoint>();
                SerializedObject cso = new SerializedObject(checkpoint);
                cso.FindProperty("index").intValue = i;
                cso.FindProperty("respawnPoint").objectReferenceValue = respawn.transform;
                cso.ApplyModifiedPropertiesWithoutUndo();
                checkpoints[i] = checkpoint;
            }

            GameObject spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.localPosition = new Vector3(4f, 0.3f, -22.5f); // just after the start line, facing east
            spawn.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            SerializedObject tso = new SerializedObject(track);
            SerializedProperty list = tso.FindProperty("checkpoints");
            list.arraySize = checkpoints.Length;
            for (int i = 0; i < checkpoints.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = checkpoints[i];
            }
            tso.FindProperty("spawnPoint").objectReferenceValue = spawn.transform;
            tso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, TrackScenePath);
        }

        // ---------- Main.unity ----------

        private static void BuildMainScene(RaceSettings settings, PanelSettings panel, GameObject kartPrefab, GameObject kartAudioPrefab)
        {
            // Rebuild in place: open the existing Main, remove only the builder's own root objects and keep every
            // other root object (for example Role 1's camera rig). MayRebuildMain checked them before.
            Scene scene;
            if (System.IO.File.Exists(MainScenePath))
            {
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (BuilderComponents(root.name) != null)
                    {
                        Object.DestroyImmediate(root);
                    }
                    else
                    {
                        Debug.Log("Role4SceneBuilder: kept " + root.name + " in Main.unity (not created by the builder).");
                    }
                }
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            // Opening a scene unloads assets nothing points to yet (our settings and panel), which would leave
            // empty references in Main. Loading them again from disk gives live objects.
            settings = AssetDatabase.LoadAssetAtPath<RaceSettings>(SettingsPath);
            panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            kartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(kartPrefab));
            kartAudioPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KartAudioPrefabPath);

            // Camera with the placeholder chase script and the only AudioListener.
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera cam = cameraObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 6f, -40f);
            PlaceholderChaseCamera chase = cameraObject.AddComponent<PlaceholderChaseCamera>();

            // Bloom makes the drift sparks and boost flames glow (Role 3's request). The camera must allow
            // post-processing, and one global Volume applies the profile. Role 3 tunes the values in the asset.
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            GameObject volumeObject = new GameObject("PostProcessing");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = CreateVolumeProfile();

            // Race logic: bootstrap (loads track, spawns kart), manager (states, timer), sounds.
            GameObject systems = new GameObject("RaceSystems");
            RaceManager manager = systems.AddComponent<RaceManager>();
            RaceBootstrap bootstrap = systems.AddComponent<RaceBootstrap>();
            AudioSource uiSource = AddAudioSource(systems, null, false);
            AudioSource delayedSource = AddAudioSource(systems, null, false); // record sting, retry beep
            RaceSounds sounds = systems.AddComponent<RaceSounds>();

            // Music: race loop + results loop, and RaceMusic, which follows the race and ducks under the beeps.
            GameObject music = new GameObject("Music");
            AudioSource raceMusicSource = AddAudioSource(music, Clip("music_campus_loop"), true);
            AudioSource resultsMusicSource = AddAudioSource(music, Clip("music_results_loop"), true);
            RaceMusic raceMusic = music.AddComponent<RaceMusic>();
            SerializedObject muso = new SerializedObject(raceMusic);
            muso.FindProperty("raceManager").objectReferenceValue = manager;
            muso.FindProperty("raceMusic").objectReferenceValue = raceMusicSource;
            muso.FindProperty("resultsMusic").objectReferenceValue = resultsMusicSource;
            muso.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject mso = new SerializedObject(manager);
            mso.FindProperty("settings").objectReferenceValue = settings;
            mso.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject bso = new SerializedObject(bootstrap);
            bso.FindProperty("settings").objectReferenceValue = settings;
            bso.FindProperty("kartPrefab").objectReferenceValue = kartPrefab;
            SerializedProperty attachments = bso.FindProperty("kartAttachments");
            attachments.arraySize = 1;
            attachments.GetArrayElementAtIndex(0).objectReferenceValue = kartAudioPrefab;
            bso.FindProperty("raceManager").objectReferenceValue = manager;
            bso.FindProperty("chaseCamera").objectReferenceValue = chase;
            bso.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject sso = new SerializedObject(sounds);
            sso.FindProperty("raceManager").objectReferenceValue = manager;
            sso.FindProperty("source").objectReferenceValue = uiSource;
            sso.FindProperty("delayedSource").objectReferenceValue = delayedSource;
            sso.FindProperty("music").objectReferenceValue = raceMusic;
            sso.FindProperty("hintWarning").objectReferenceValue = Clip("sfx_hint_warning");
            sso.FindProperty("countdownBeep").objectReferenceValue = Clip("sfx_countdown_beep");
            sso.FindProperty("goBeep").objectReferenceValue = Clip("sfx_go_beep");
            sso.FindProperty("checkpointChime").objectReferenceValue = Clip("sfx_checkpoint_chime");
            sso.FindProperty("finishJingle").objectReferenceValue = Clip("sfx_finish_jingle");
            sso.FindProperty("newRecordSting").objectReferenceValue = Clip("sfx_new_record");
            sso.FindProperty("retryClick").objectReferenceValue = Clip("ui_click");
            sso.FindProperty("respawnWhoosh").objectReferenceValue = Clip("sfx_respawn");
            sso.ApplyModifiedPropertiesWithoutUndo();

            // HUD (UI Toolkit).
            GameObject hud = new GameObject("HUD");
            UIDocument document = hud.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudLayoutPath);
            RaceHud raceHud = hud.AddComponent<RaceHud>();
            SerializedObject hso = new SerializedObject(raceHud);
            hso.FindProperty("raceManager").objectReferenceValue = manager;
            hso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MainScenePath);

            // Fail loudly instead of shipping a Main.unity with empty references (the Web build hides null errors).
            if (settings == null || panel == null || kartPrefab == null || kartAudioPrefab == null)
            {
                throw new System.Exception("Role4SceneBuilder: a Main.unity reference is missing (settings, panel or a prefab).");
            }
        }
    }
}
