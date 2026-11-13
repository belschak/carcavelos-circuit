using CarcavelosCircuit.Audio;
using CarcavelosCircuit.Driving;
using CarcavelosCircuit.Shared;
using CarcavelosCircuit.Shared.Editor;
using CarcavelosCircuit.Track;
using Unity.Cinemachine;
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
        public const string SettingsPath = Root + "RaceFlow/Settings/RaceSettings.asset";
        private const string ItemSettingsPath = Root + "RaceFlow/Settings/ItemSettings.asset"; // item boxes (task R7)
        public const string LeaderboardConfigPath = Root + "RaceFlow/Settings/LeaderboardConfig.asset"; // task R10, EMPTY in the repo
        private const string ThemePath = Root + "RaceFlow/UI/RaceHudTheme.tss";
        private const string PanelPath = Root + "RaceFlow/UI/RaceHudPanelSettings.asset";
        private const string HudLayoutPath = Root + "RaceFlow/UI/RaceHud.uxml";
        private const string KartPrefabPath = Root + "RaceFlow/Prefabs/PlaceholderKart.prefab";
        private const string KartAudioPrefabPath = Root + "Audio/Prefabs/KartAudio.prefab";
        private const string ChaseCameraPrefabPath = Root + "Driving/ChaseCamera.prefab"; // Role 1 (D2), used since Q13
        private const string ClipsFolder = Root + "Audio/Clips/";
        private const string MaterialsFolder = Root + "RaceFlow/Materials/";
        private const string MainScenePath = Root + "RaceFlow/Scenes/Main.unity";
        private const string TrackScenePath = Root + "RaceFlow/Scenes/RaceFlow_PlaceholderTrack.unity";
        private const string VolumeProfilePath = Root + "RaceFlow/Settings/RaceCameraVolume.asset";
        private const string DeansGhostPath = Root + "RaceFlow/Settings/DeansGhost.txt"; // shipped team ghost (DeansGhostExporter)
        private const string NameTagFontPath = Root + "RaceFlow/UI/Fonts/BarlowCondensed/BarlowCondensed-ExtraBold.ttf"; // HUD font (R15)
        private const string NameTagsRootName = "NameTags"; // Role 1's name tags (D9), request Q22

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
            CreateLeaderboardConfig();
            PanelSettings panel = CreatePanelSettings();
            GameObject kartAudio = CreateKartAudioPrefab();
            GameObject kart = FindRealKartPrefab();
            if (kart == null)
            {
                kart = CreatePlaceholderKartPrefab(); // until Role 1's Kart prefab exists
            }
            BuildPlaceholderTrackScene();
            BuildMainScene(settings, panel, kart, kartAudio);

            MergeBuildScenes(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("Role4SceneBuilder: done.");
        }

        // Build order: Main first (the game starts there), then every track scene RaceSettings may race on that exists
        // in the project (wherever its owner keeps it), then every other scene someone added to File > Build Profiles
        // (kept with its tick box). Merged, never replaced. The Web build calls this too, so a track scene that was
        // merged into main is in the build without anyone touching the build list.
        public static void MergeBuildScenes(RaceSettings settings)
        {
            var ours = new System.Collections.Generic.List<string>();
            ours.Add(MainScenePath);
            string[] trackNames = settings != null ? settings.TrackScenesInOrder() : new string[0];
            for (int i = 0; i < trackNames.Length; i++)
            {
                string path = FindScenePath(trackNames[i]);
                if (path != "" && !ours.Contains(path))
                {
                    ours.Add(path);
                }
            }
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (string path in ours)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (!ours.Contains(existing.path))
                {
                    scenes.Add(existing);
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // The project path of the scene file with exactly this name, e.g. "Track_CampusWorld" ->
        // "Assets/_Project/Visuals/Scenes/Track_CampusWorld.unity". "" if there is none (not merged yet).
        public static string FindScenePath(string sceneName)
        {
            string[] guids = AssetDatabase.FindAssets(sceneName + " t:Scene", new[] { "Assets/_Project" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                {
                    return path;
                }
            }
            return "";
        }

        // ---------- guard for Main.unity ----------

        // The builder's own root objects in Main.unity and the only components it puts on each of them.
        // null = not a builder object: someone else's, kept on every rebuild.
        private static System.Type[] BuilderComponents(string rootName)
        {
            switch (rootName)
            {
                case "Main Camera":
                    return new[] { typeof(Transform), typeof(Camera), typeof(AudioListener), typeof(CinemachineBrain), typeof(ChaseCameraRig), typeof(MapFlyover), typeof(UniversalAdditionalCameraData) };
                case "ChaseCamera":
                    // Role 1's prefab instance (D2). Role 1 tunes it in the prefab, never here, so a rebuild loses nothing.
                    return new[] { typeof(Transform), typeof(CinemachineCamera), typeof(CinemachineFollow), typeof(CinemachineRotationComposer) };
                case "PostProcessing":
                    return new[] { typeof(Transform), typeof(Volume) };
                case "CameraLook": // AAA camera look: three extra volumes + RaceCameraLook (help by Luc's Claude)
                    return new[] { typeof(Transform), typeof(Volume), typeof(RaceCameraLook) };
                case "RaceSystems":
                    return new[] { typeof(Transform), typeof(RaceManager), typeof(RaceBootstrap), typeof(AudioSource), typeof(RaceSounds) };
                case "HUD":
                    return new[] { typeof(Transform), typeof(UIDocument), typeof(RaceHud), typeof(TitleScreen), typeof(LeaderboardHud) };
                case "Music":
                    return new[] { typeof(Transform), typeof(AudioSource), typeof(RaceMusic) };
                case "Ghost":
                    return new[] { typeof(Transform), typeof(GhostRecorder), typeof(GhostPlayer) };
                case Role4RacersBuilder.RootName: // tasks R8 + R9
                    return Role4RacersBuilder.Components();
                case "Items":
                    return new[] { typeof(Transform), typeof(ItemSystem), typeof(AudioSource), typeof(UIDocument), typeof(ItemSlotHud) };
                case "Ambience":
                    return new[] { typeof(Transform), typeof(CarcavelosCircuit.Ambience.SeasideAmbience) }; // waves + surfers (R12)
                case NameTagsRootName:
                    return new[] { typeof(Transform), typeof(NameTagManager) }; // Role 1's name tags (D9, Q22)
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

        // Item box tuning (task R7). Kept if it exists, like RaceSettings.
        private static ItemSettings CreateItemSettings()
        {
            ItemSettings settings = AssetDatabase.LoadAssetAtPath<ItemSettings>(ItemSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ItemSettings>();
                AssetDatabase.CreateAsset(settings, ItemSettingsPath);
            }
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // The item boxes' material. Emission is switched on in the asset, so the Web build keeps the emissive shader
        // variant (ItemSystem adds the tile picture and the glow colour at runtime).
        private static Material CreateItemBoxMaterial()
        {
            Material material = ColourMaterial("ItemBox", "#FFFFFF", 0.7f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetColor("_EmissionColor", new Color(0.45f, 0.45f, 0.45f, 1f));
            EditorUtility.SetDirty(material);
            return material;
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
            panel.match = 1f; // match the screen height: the HUD is always the same share of the height, extra width goes to the sides
            EditorUtility.SetDirty(panel);
            return panel;
        }

        // Bloom + vignette for the race camera. Since the "AAA camera look" the numbers live in code
        // (RaceCameraLookBuilder), so every rebuild and every Web build gets the same look.
        private static VolumeProfile CreateVolumeProfile()
        {
            return RaceCameraLookBuilder.BuildBaseProfile();
        }

        // A global volume with a profile (the camera look's extra layers). Higher priority = blended on top.
        private static Volume AddGlobalVolume(GameObject go, VolumeProfile profile, float priority)
        {
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.sharedProfile = profile;
            return volume;
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
            so.FindProperty("bumpClip").objectReferenceValue = Clip("sfx_wall_bump");          // audit 11
            so.FindProperty("takeOffClip").objectReferenceValue = Clip("sfx_takeoff_whoosh");
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
        // Role 3's kart model (Visuals/Kart/Prefabs/KartVisual_*.prefab), the first one by name. null until it is merged.
        private static GameObject FindKartVisualPrefab()
        {
            string[] guids = AssetDatabase.FindAssets("KartVisual t:Prefab", new[] { Root + "Visuals" });
            string best = "";
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith("KartVisual") && (best == "" || string.CompareOrdinal(path, best) < 0))
                {
                    best = path;
                }
            }
            if (best == "")
            {
                return null;
            }
            Debug.Log("Role4SceneBuilder: placeholder kart shows Role 3's model " + best);
            return AssetDatabase.LoadAssetAtPath<GameObject>(best);
        }

        // Role 3's kart effects (V5: drift smoke, sparks, boost flame; Luc's request Q10): the prefab named exactly
        // "KartEffects" anywhere under Visuals/ (it lives in Visuals/KartEffects/Prefabs/). null until it is merged,
        // and then the race simply has no effects. Found by name like FindKartVisualPrefab, so the day Luc's PR
        // lands, one builder run puts it on the kart with nobody touching this file.
        private static GameObject FindKartEffectsPrefab()
        {
            string[] guids = AssetDatabase.FindAssets("KartEffects t:Prefab", new[] { Root + "Visuals" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == "KartEffects")
                {
                    Debug.Log("Role4SceneBuilder: the kart gets Role 3's effects " + path);
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }
            return null;
        }

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

            // "Visual" child = the part Role 3 swaps for the real model (docs/interfaces.md). Once Luc's KartVisual prefab
            // is on main it sits here, so the race shows the real kart even before Role 1's Kart prefab exists.
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(kart.transform, false);
            GameObject kartVisual = FindKartVisualPrefab();
            if (kartVisual != null)
            {
                PrefabUtility.InstantiatePrefab(kartVisual, visual.transform);
                GameObject withModel = PrefabUtility.SaveAsPrefabAsset(kart, KartPrefabPath);
                Object.DestroyImmediate(kart);
                return withModel;
            }
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

        // A small rectangular loop (about 230 m, roughly 20 s per lap): the last fallback if no campus scene has a track,
        // and a quick test bed for the race flow. Built with Role 2's real track parts (TrackInfo, Checkpoint, SpawnPoint,
        // RespawnPoint). Drive direction: anticlockwise seen from above, starting east on the south straight.
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
            TrackInfo track = root.AddComponent<TrackInfo>();

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
            Checkpoint[] checkpoints = new Checkpoint[positions.Length];
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
                respawn.AddComponent<RespawnPoint>(); // only draws the marker in the Scene view

                Checkpoint checkpoint = cp.AddComponent<Checkpoint>();
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
            spawn.AddComponent<SpawnPoint>(); // only draws the marker in the Scene view

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

            // Camera with the only AudioListener. Role 1's Cinemachine chase camera (Q13) drives it through a
            // CinemachineBrain; ChaseCameraRig connects that camera to the race.
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera cam = cameraObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 2000f; // the campus world (#33) has sea, backdrop and fort beyond 500 m
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 6f, -40f);
            cameraObject.AddComponent<CinemachineBrain>();
            ChaseCameraRig chase = cameraObject.AddComponent<ChaseCameraRig>();
            GameObject chasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChaseCameraPrefabPath);
            if (chasePrefab == null)
            {
                throw new System.Exception("Role4SceneBuilder: " + ChaseCameraPrefabPath + " not found (Role 1, task D2). Pull main first.");
            }
            GameObject chaseObject = (GameObject)PrefabUtility.InstantiatePrefab(chasePrefab, scene);
            chaseObject.name = "ChaseCamera";
            SerializedObject cso = new SerializedObject(chase);
            cso.FindProperty("chaseCamera").objectReferenceValue = chaseObject.GetComponent<CinemachineCamera>();
            cso.ApplyModifiedPropertiesWithoutUndo();

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
            AudioSource menuSource = AddAudioSource(systems, null, false);    // menu sounds (audit 10), RaceSounds.Awake lets it ignore the pause
            AudioSource chimeSource = AddAudioSource(systems, null, false);   // checkpoint chime, pitched ahead/behind (audit 19)
            RaceSounds sounds = systems.AddComponent<RaceSounds>();

            // Music: race loop + results loop + seaside ambience, and RaceMusic, which follows the race and ducks under the beeps.
            GameObject music = new GameObject("Music");
            AudioSource raceMusicSource = AddAudioSource(music, Clip("music_campus_loop"), true);
            AudioSource resultsMusicSource = AddAudioSource(music, Clip("music_results_loop"), true);
            AudioSource ambienceSource = AddAudioSource(music, Clip("amb_seaside_loop"), true); // waves + gulls (audit 12)
            RaceMusic raceMusic = music.AddComponent<RaceMusic>();
            SerializedObject muso = new SerializedObject(raceMusic);
            muso.FindProperty("raceManager").objectReferenceValue = manager;
            muso.FindProperty("raceMusic").objectReferenceValue = raceMusicSource;
            muso.FindProperty("resultsMusic").objectReferenceValue = resultsMusicSource;
            muso.FindProperty("ambience").objectReferenceValue = ambienceSource;
            muso.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject mso = new SerializedObject(manager);
            mso.FindProperty("settings").objectReferenceValue = settings;
            mso.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject bso = new SerializedObject(bootstrap);
            bso.FindProperty("settings").objectReferenceValue = settings;
            bso.FindProperty("kartPrefab").objectReferenceValue = kartPrefab;
            bso.FindProperty("kartModel").objectReferenceValue = FindKartVisualPrefab(); // Q14 bridge, null until Role 3's model is merged
            // Spawned as children of the kart: our engine sounds, and Role 3's effects once they exist (Q10).
            SerializedProperty attachments = bso.FindProperty("kartAttachments");
            GameObject kartEffectsPrefab = FindKartEffectsPrefab();
            attachments.arraySize = kartEffectsPrefab != null ? 2 : 1;
            attachments.GetArrayElementAtIndex(0).objectReferenceValue = kartAudioPrefab;
            if (kartEffectsPrefab != null)
            {
                attachments.GetArrayElementAtIndex(1).objectReferenceValue = kartEffectsPrefab;
            }
            bso.FindProperty("raceManager").objectReferenceValue = manager;
            bso.FindProperty("chaseCamera").objectReferenceValue = chase;
            bso.ApplyModifiedPropertiesWithoutUndo();

            // Start screen and finish: the camera flies over the whole map (MapFlyover); while driving the chase camera.
            MapFlyover flyover = cameraObject.AddComponent<MapFlyover>();
            SerializedObject fso = new SerializedObject(flyover);
            fso.FindProperty("raceManager").objectReferenceValue = manager;
            fso.FindProperty("chaseCamera").objectReferenceValue = chase;
            fso.ApplyModifiedPropertiesWithoutUndo();

            // AAA camera look (help by Luc's Claude): speed rush, high-end extras (PC/Mac) and the soft background
            // over the map, three global volumes on one object; RaceCameraLook drives their weights.
            RaceCameraLookBuilder.TunePcPipeline();
            GameObject lookObject = new GameObject("CameraLook");
            Volume speedVolume = AddGlobalVolume(lookObject, RaceCameraLookBuilder.BuildSpeedProfile(), 2f);
            Volume highEndVolume = AddGlobalVolume(lookObject, RaceCameraLookBuilder.BuildHighEndProfile(), 2f);
            Volume overviewVolume = AddGlobalVolume(lookObject, RaceCameraLookBuilder.BuildOverviewProfile(), 3f);
            RaceCameraLook cameraLook = lookObject.AddComponent<RaceCameraLook>();
            SerializedObject lso = new SerializedObject(cameraLook);
            lso.FindProperty("raceManager").objectReferenceValue = manager;
            lso.FindProperty("flyover").objectReferenceValue = flyover;
            lso.FindProperty("raceCamera").objectReferenceValue = cam;
            lso.FindProperty("speedVolume").objectReferenceValue = speedVolume;
            lso.FindProperty("highEndVolume").objectReferenceValue = highEndVolume;
            lso.FindProperty("overviewVolume").objectReferenceValue = overviewVolume;
            lso.ApplyModifiedPropertiesWithoutUndo();

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
            sso.FindProperty("medalStamp").objectReferenceValue = Clip("sfx_landing_thud");        // task P6b
            sso.FindProperty("newMedalSparkle").objectReferenceValue = Clip("sfx_drift_stage2");   // task P6b
            sso.FindProperty("retryClick").objectReferenceValue = Clip("ui_click");
            sso.FindProperty("respawnWhoosh").objectReferenceValue = Clip("sfx_respawn");
            sso.FindProperty("menuSource").objectReferenceValue = menuSource;
            sso.FindProperty("chimeSource").objectReferenceValue = chimeSource;
            sso.FindProperty("menuOpen").objectReferenceValue = Clip("ui_open");     // audit 10
            sso.FindProperty("menuClose").objectReferenceValue = Clip("ui_close");
            sso.FindProperty("pageTurn").objectReferenceValue = Clip("ui_page");
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
            AddStartScreenAndLeaderboard(hud, manager); // tasks R10 + R11

            // The HUD's menus ask RaceSounds for their sounds (audit 10). The HUD exists only now, so it is wired here.
            SerializedObject soundsToHud = new SerializedObject(sounds);
            soundsToHud.FindProperty("hud").objectReferenceValue = raceHud;
            soundsToHud.ApplyModifiedPropertiesWithoutUndo();

            // The ghost chip on the HUD reads the GhostPlayer (audit 13).
            GhostPlayer ghostPlayer = BuildGhost(manager);
            SerializedObject hudToGhost = new SerializedObject(raceHud);
            hudToGhost.FindProperty("ghostPlayer").objectReferenceValue = ghostPlayer;
            hudToGhost.ApplyModifiedPropertiesWithoutUndo();

            BuildItems(manager, panel);
            Role4RacersBuilder.Build(manager, settings, panel); // every racer on the grid, positions + minimap (R8, R9)
            CarcavelosCircuit.Ambience.Editor.AmbienceBuilder.AddToScene(); // waves + surfers on the beach (R12)
            BuildNameTags(cam); // a name above every other racer's kart (Role 1's D9, request Q22)

            EditorSceneManager.SaveScene(scene, MainScenePath);

            // Fail loudly instead of shipping a Main.unity with empty references (the Web build hides null errors).
            if (settings == null || panel == null || kartPrefab == null || kartAudioPrefab == null)
            {
                throw new System.Exception("Role4SceneBuilder: a Main.unity reference is missing (settings, panel or a prefab).");
            }
        }

        // ---------- start screen title + leaderboard (tasks R11 + R10) ----------

        // The connection asset of the online leaderboard. Always saved EMPTY: the CI Web build fills it from its
        // secrets after this runs and empties it again (Role4WebBuild), so a key never ends up in a file in the repo,
        // even if an earlier build was killed half-way.
        private static void CreateLeaderboardConfig()
        {
            LeaderboardConfig config = AssetDatabase.LoadAssetAtPath<LeaderboardConfig>(LeaderboardConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<LeaderboardConfig>();
                AssetDatabase.CreateAsset(config, LeaderboardConfigPath);
            }
            config.SetConnection("", "");
            EditorUtility.SetDirty(config);
        }

        // On the HUD object, next to RaceHud and on its UIDocument: the title opener and the leaderboard cards.
        // Called while Main.unity is open, so the config is loaded here (live reference).
        private static void AddStartScreenAndLeaderboard(GameObject hud, RaceManager manager)
        {
            TitleScreen title = hud.AddComponent<TitleScreen>();
            SerializedObject tso = new SerializedObject(title);
            tso.FindProperty("raceManager").objectReferenceValue = manager;
            tso.ApplyModifiedPropertiesWithoutUndo();

            LeaderboardConfig config = AssetDatabase.LoadAssetAtPath<LeaderboardConfig>(LeaderboardConfigPath);
            if (config == null)
            {
                throw new System.Exception("Role4SceneBuilder: " + LeaderboardConfigPath + " is missing.");
            }
            LeaderboardHud leaderboard = hud.AddComponent<LeaderboardHud>();
            SerializedObject lso = new SerializedObject(leaderboard);
            lso.FindProperty("raceManager").objectReferenceValue = manager;
            lso.FindProperty("config").objectReferenceValue = config;
            lso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- item boxes (task R7, decision 2026-11-10) ----------

        // The "Items" root object: ItemSystem puts the boxes on the track at race start, ItemSlotHud shows the slot.
        // Called while Main.unity is open, so the assets are created and loaded here (live references).
        private static void BuildItems(RaceManager manager, PanelSettings panel)
        {
            ItemSettings itemSettings = CreateItemSettings();
            Material boxMaterial = CreateItemBoxMaterial();

            GameObject itemsObject = new GameObject("Items");
            ItemSystem itemSystem = itemsObject.AddComponent<ItemSystem>();
            AudioSource itemSource = AddAudioSource(itemsObject, null, false);
            SerializedObject iso = new SerializedObject(itemSystem);
            iso.FindProperty("raceManager").objectReferenceValue = manager;
            iso.FindProperty("settings").objectReferenceValue = itemSettings;
            iso.FindProperty("boxMaterial").objectReferenceValue = boxMaterial;
            iso.FindProperty("source").objectReferenceValue = itemSource;
            iso.FindProperty("boxBreak").objectReferenceValue = Clip("el_item_box_break");
            iso.FindProperty("rollTick").objectReferenceValue = Clip("sfx_item_roll_tick");
            iso.FindProperty("itemReady").objectReferenceValue = Clip("el_item_ready");
            iso.FindProperty("ringSource").objectReferenceValue = AddAudioSource(itemsObject, null, false);
            // Bica = Lisbon's word for an espresso, onda = Portuguese for wave: the clips kept their first names.
            iso.FindProperty("espressoUse").objectReferenceValue = Clip("el_item_bica");
            iso.FindProperty("waveUse").objectReferenceValue = Clip("el_item_onda");
            iso.FindProperty("seagullCall").objectReferenceValue = Clip("el_item_seagull");
            iso.FindProperty("ringCollect").objectReferenceValue = Clip("el_item_ring");
            iso.FindProperty("deansOn").objectReferenceValue = Clip("el_item_deans_on");
            iso.FindProperty("shieldBlock").objectReferenceValue = Clip("el_item_shield_block");
            iso.FindProperty("shieldPop").objectReferenceValue = Clip("el_item_shield_pop");
            iso.ApplyModifiedPropertiesWithoutUndo();

            // Own UIDocument on the HUD's PanelSettings, drawn under the race HUD (menus and the pause card cover it).
            UIDocument document = itemsObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.sortingOrder = -1f;
            ItemSlotHud slotHud = itemsObject.AddComponent<ItemSlotHud>();
            SerializedObject sho = new SerializedObject(slotHud);
            sho.FindProperty("raceManager").objectReferenceValue = manager;
            sho.FindProperty("items").objectReferenceValue = itemSystem;
            sho.ApplyModifiedPropertiesWithoutUndo();

            if (itemSettings == null || boxMaterial == null)
            {
                throw new System.Exception("Role4SceneBuilder: ItemSettings or the ItemBox material is missing.");
            }
        }

        // ---------- name tags (Role 1's task D9, request Q22) ----------

        // The "NameTags" root object: Role 1's NameTagManager finds every racer through RacerRegistry by itself and
        // floats a name above each kart except the player's. We only add it once, point it at the race camera and
        // give it the HUD font, so the names look like the rest of the arcade UI. Role 1 tunes it in NameTagManager.
        private static void BuildNameTags(Camera raceCamera)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(NameTagFontPath);
            if (font == null)
            {
                throw new System.Exception("Role4SceneBuilder: " + NameTagFontPath + " is missing (HUD font, task R15).");
            }
            GameObject tagsObject = new GameObject(NameTagsRootName);
            NameTagManager tags = tagsObject.AddComponent<NameTagManager>();
            SerializedObject nso = new SerializedObject(tags);
            nso.FindProperty("viewCamera").objectReferenceValue = raceCamera;
            nso.FindProperty("tagFont").objectReferenceValue = font;
            nso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- ghost (stretch item, docs/decisions.md) ----------

        // The "Ghost" root object: records every lap, replays the best one as a see-through kart. Called while
        // Main.unity is open, so the material and the text file are loaded after the scene switch (live references).
        private static GhostPlayer BuildGhost(RaceManager manager)
        {
            Material ghostMaterial = CreateGhostMaterial();
            TextAsset deansGhost = CreateDeansGhostFile();

            GameObject ghostObject = new GameObject("Ghost");
            GhostRecorder recorder = ghostObject.AddComponent<GhostRecorder>();
            GhostPlayer player = ghostObject.AddComponent<GhostPlayer>();

            SerializedObject rso = new SerializedObject(recorder);
            rso.FindProperty("raceManager").objectReferenceValue = manager;
            rso.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject pso = new SerializedObject(player);
            pso.FindProperty("raceManager").objectReferenceValue = manager;
            pso.FindProperty("ghostMaterial").objectReferenceValue = ghostMaterial;
            pso.FindProperty("shippedGhost").objectReferenceValue = deansGhost;
            pso.ApplyModifiedPropertiesWithoutUndo();

            if (ghostMaterial == null || deansGhost == null)
            {
                throw new System.Exception("Role4SceneBuilder: the ghost material or DeansGhost.txt is missing.");
            }
            return player;
        }

        // URP Lit, "Surface Type: Transparent, Blending: Alpha", set the same way URP's material Inspector does.
        // The Main scene points to this material, so the Web build includes it together with its transparent
        // shader variant (a material looked up by name at runtime would be missing there).
        private static Material CreateGhostMaterial()
        {
            string path = MaterialsFolder + "Ghost.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetFloat("_Surface", 1f); // 1 = transparent
            material.SetFloat("_Blend", 0f);   // 0 = alpha blending
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_ReceiveShadows", 0f);
            material.SetFloat("_Smoothness", 0.8f);
            material.SetColor("_BaseColor", new Color(0.55f, 0.8f, 1f, 0.4f)); // GhostPlayer sets colour + alpha at runtime
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("DepthOnly", false);   // see-through: never hide what is behind it
            material.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(material);
            return material;
        }

        // The shipped Dean's ghost. A placeholder text until the team records one (menu Carcavelos Circuit >
        // Save My Best Ghost As Dean's Ghost); the game ignores text that is not a valid ghost. Never overwritten here.
        private static TextAsset CreateDeansGhostFile()
        {
            if (!System.IO.File.Exists(DeansGhostPath))
            {
                System.IO.File.WriteAllText(DeansGhostPath, "no ghost recorded yet\n");
                AssetDatabase.ImportAsset(DeansGhostPath);
            }
            return AssetDatabase.LoadAssetAtPath<TextAsset>(DeansGhostPath);
        }
    }
}
