using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarcavelosCircuit.Visuals.Editor
{
    // Builds the kart effects (task V5): particle materials, the KartEffects_Wheel, KartEffects_Exhaust and
    // KartEffects prefabs, and the KartEffects_Demo scene. Ported from Luc's art sandbox (NovaKartFXBuilder,
    // KartDriftFX); point lights, glow pools and wheel flames are left out to keep the Web build light.
    // The textures come from our own script fx_textures.py (art source, outside the repo): no third-party assets.
    // Rebuilding overwrites Inspector changes on the prefabs, so tune the defaults in KartEffects.cs instead.
    public static class KartEffectsBuilder
    {
        private const string Root = "Assets/_Project/Visuals/KartEffects/";
        private const string TextureFolder = Root + "Textures/";
        private const string MaterialFolder = Root + "Materials/";
        private const string PrefabFolder = Root + "Prefabs/";
        private const string EffectsPrefabPath = PrefabFolder + "KartEffects.prefab";
        private const string DemoScenePath = "Assets/_Project/Visuals/Scenes/KartEffects_Demo.unity";
        private const string KartVisualPath = "Assets/_Project/Visuals/Kart/Prefabs/KartVisual_Classic_Rookie.prefab"; // V3, used if it exists
        private const string BloomProfilePath = "Assets/_Project/RaceFlow/Settings/RaceCameraVolume.asset";      // Role 4's race camera bloom

        [MenuItem("Carcavelos Circuit/Visuals/Build Kart Effects")]
        public static void Build()
        {
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();

            Material smokeMaterial = ParticleMaterial("KartFX_Smoke", "FX_Smoke_4x4", false, 1f);
            Material sparkMaterial = ParticleMaterial("KartFX_Spark", "FX_Spark", true, 3.2f);
            Material flameMaterial = ParticleMaterial("KartFX_Flame", "FX_Glow", true, 2.4f);

            GameObject wheel = BuildWheelPrefab(smokeMaterial, sparkMaterial);
            GameObject exhaust = BuildExhaustPrefab(flameMaterial, sparkMaterial);
            GameObject effects = BuildEffectsPrefab(wheel, exhaust);
            BuildDemoScene(effects);
            AssetDatabase.SaveAssets();
            Debug.Log("KartEffects: built. In the race scene add " + EffectsPrefabPath + " to RaceBootstrap > Kart Attachments.");
        }

        // ---------- prefabs ----------

        // One rear wheel: tyre smoke, sparks and the stage-up burst. Local +Z = kart forward, y = 0 is the road.
        private static GameObject BuildWheelPrefab(Material smokeMaterial, Material sparkMaterial)
        {
            GameObject root = new GameObject("KartEffects_Wheel");
            Transform ground = new GameObject("GroundPlane").transform;   // the sparks bounce off this plane
            ground.SetParent(root.transform, false);
            ground.localPosition = new Vector3(0f, -0.012f, 0f);

            // Tyre smoke: soft puffs thrown back and up; they slow down, rise, grow and fade.
            ParticleSystem smoke = NewSystem(root, "Smoke", smokeMaterial, ParticleSystemRenderMode.Billboard);
            smoke.transform.localRotation = Quaternion.Euler(-28f, 180f, 0f);
            ParticleSystem.MainModule main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.55f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.88f, 0.88f, 0.9f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.06f;
            main.maxParticles = 200;
            ParticleSystem.ShapeModule shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.07f;
            ParticleSystem.InheritVelocityModule inherit = smoke.inheritVelocity;
            inherit.enabled = true;
            inherit.mode = ParticleSystemInheritVelocityMode.Initial;
            inherit.curve = 0.45f;
            ParticleSystem.LimitVelocityOverLifetimeModule drag = smoke.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 10f;
            drag.drag = 1.6f;
            ParticleSystem.SizeOverLifetimeModule size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1.4f), new Keyframe(1f, 2.6f)));
            ParticleSystem.RotationOverLifetimeModule spin = smoke.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);
            ParticleSystem.ColorOverLifetimeModule fade = smoke.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(new Color(0.88f, 0.88f, 0.9f), new[] { 0f, 0.06f, 0.45f, 1f }, new[] { 0f, 0.36f, 0.2f, 0f });
            ParticleSystem.TextureSheetAnimationModule sheet = smoke.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 0.999f));
            ParticleSystemRenderer smokeRenderer = smoke.GetComponent<ParticleSystemRenderer>();
            smokeRenderer.sortMode = ParticleSystemSortMode.Distance;
            smokeRenderer.maxParticleSize = 3f;
            smokeRenderer.pivot = new Vector3(0f, 0.45f, 0f);   // lift each puff so big puffs do not cut into the road

            // Sparks: hot streaks thrown back and out; they bounce on the road.
            ParticleSystem sparks = NewSystem(root, "Sparks", sparkMaterial, ParticleSystemRenderMode.Stretch);
            sparks.transform.localRotation = Quaternion.Euler(-18f, 180f, 0f);
            SparkSetup(sparks, ground, 0.15f, 0.38f, 240);
            ParticleSystem.ShapeModule sparkShape = sparks.shape;
            sparkShape.shapeType = ParticleSystemShapeType.Cone;
            sparkShape.angle = 24f;
            sparkShape.radius = 0.06f;
            sparkShape.position = new Vector3(0f, 0.03f, 0f);

            // Stage-up burst: a short fountain of sparks. KartEffects emits it by code, so its own emission is off.
            ParticleSystem burst = NewSystem(root, "Burst", sparkMaterial, ParticleSystemRenderMode.Stretch);
            SparkSetup(burst, ground, 0.3f, 0.65f, 120);
            ParticleSystem.EmissionModule burstEmission = burst.emission;
            burstEmission.enabled = false;
            ParticleSystem.ShapeModule burstShape = burst.shape;
            burstShape.shapeType = ParticleSystemShapeType.Hemisphere;
            burstShape.radius = 0.08f;
            burstShape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.MainModule burstMain = burst.main;
            burstMain.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);

            return SavePrefab(root, PrefabFolder + "KartEffects_Wheel.prefab");
        }

        // One exhaust: the boost flame (outer + core) and the ignition sparks. Local +Z = out of the pipe.
        private static GameObject BuildExhaustPrefab(Material flameMaterial, Material sparkMaterial)
        {
            GameObject root = new GameObject("KartEffects_Exhaust");

            // Flame: short-lived glowing blobs shot out of the pipe that shrink as they fly. Plain billboards, because the
            // chase camera looks straight down the jet, where stretched particles would shrink to dots.
            ParticleSystem flame = NewSystem(root, "Flame", flameMaterial, ParticleSystemRenderMode.Billboard);
            FlameSetup(flame, 0.08f, 0.16f, 2.2f, 3.4f, 0.2f, 0.28f, 60);
            ParticleSystem core = NewSystem(root, "FlameCore", flameMaterial, ParticleSystemRenderMode.Billboard);
            FlameSetup(core, 0.06f, 0.1f, 1.4f, 2.2f, 0.11f, 0.15f, 40);

            // Ignition: sparks out of the pipe when a boost starts. KartEffects emits them by code.
            ParticleSystem ignition = NewSystem(root, "Ignition", sparkMaterial, ParticleSystemRenderMode.Stretch);
            ParticleSystem.MainModule main = ignition.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.7f;
            main.maxParticles = 80;
            ParticleSystem.EmissionModule emission = ignition.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ignition.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.02f;
            ParticleSystem.ColorOverLifetimeModule fade = ignition.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(Color.white, new[] { 0f, 0.6f, 1f }, new[] { 1f, 0.9f, 0f });
            ParticleSystemRenderer renderer = ignition.GetComponent<ParticleSystemRenderer>();
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 1f;

            return SavePrefab(root, PrefabFolder + "KartEffects_Exhaust.prefab");
        }

        // The prefab RaceBootstrap spawns under the kart: only the KartEffects component, which adds the parts at Start.
        private static GameObject BuildEffectsPrefab(GameObject wheel, GameObject exhaust)
        {
            GameObject root = new GameObject("KartEffects");
            KartEffects effects = root.AddComponent<KartEffects>();
            SerializedObject fields = new SerializedObject(effects);
            fields.FindProperty("wheelEffectsPrefab").objectReferenceValue = wheel;
            fields.FindProperty("exhaustEffectsPrefab").objectReferenceValue = exhaust;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, EffectsPrefabPath);
        }

        // ---------- demo scene ----------

        // A kart driving a circle on an empty asphalt plane, camera behind it, Role 4's bloom. Press Play to watch.
        private static void BuildDemoScene(GameObject effectsPrefab)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(8f, 1f, 8f);
            ground.GetComponent<Renderer>().sharedMaterial = LitMaterial("KartFX_DemoAsphalt", new Color(0.22f, 0.23f, 0.25f), 0.3f);

            Light sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.9f, 0.76f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(35f, 245f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.7f);

            GameObject kart = new GameObject("DemoKart");
            kart.AddComponent<KartEffectsDemoKart>();
            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KartVisualPath);
            if (visualPrefab != null)
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, kart.transform);
                visual.name = "Visual";
            }
            else
            {
                BuildPlaceholderVisual(kart.transform);
            }
            PrefabUtility.InstantiatePrefab(effectsPrefab, kart.transform);

            Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetParent(kart.transform, false);
            camera.transform.localPosition = new Vector3(0f, 1.7f, -4.6f);
            camera.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 0.35f, 1.5f) - camera.transform.localPosition);
            camera.fieldOfView = 55f;
            camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.72f, 0.92f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            VolumeProfile bloom = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BloomProfilePath);
            if (bloom != null)
            {
                Volume volume = new GameObject("Bloom (race camera profile)").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = bloom;
            }
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), DemoScenePath);
        }

        // Same box kart as the race placeholder (Role4SceneBuilder): body, seat, nose, four wheels without anchor names,
        // so the demo also shows the fallback positions in KartEffects.
        private static void BuildPlaceholderVisual(Transform kart)
        {
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(kart, false);
            Material white = LitMaterial("KartFX_DemoWhite", new Color(0.92f, 0.92f, 0.93f), 0.4f);
            Material dark = LitMaterial("KartFX_DemoDark", new Color(0.12f, 0.12f, 0.13f), 0.3f);
            Box(PrimitiveType.Cube, "Body", visual, new Vector3(0f, 0.35f, 0f), Vector3.zero, new Vector3(1.1f, 0.3f, 1.7f), white);
            Box(PrimitiveType.Cube, "Seat", visual, new Vector3(0f, 0.6f, -0.3f), Vector3.zero, new Vector3(0.6f, 0.3f, 0.5f), dark);
            float[] sides = { -0.6f, 0.6f };
            foreach (float x in sides)
            {
                foreach (float z in sides)
                {
                    Box(PrimitiveType.Cylinder, "Wheel", visual, new Vector3(x, 0.2f, z), new Vector3(0f, 0f, 90f), new Vector3(0.4f, 0.1f, 0.4f), dark);
                }
            }
        }

        // ---------- check (batch mode) ----------

        // Builds everything, drives the demo kart for 8 s in Edit Mode and checks which effects are on when.
        // Command line (Unity closed): Unity -batchmode -projectPath . -executeMethod
        //   CarcavelosCircuit.Visuals.Editor.KartEffectsBuilder.BuildAndCheck -quit -logFile Logs/kartfx.log
        // With the environment variable KARTFX_SHOTS=<folder> it also saves five stills there.
        public static void BuildAndCheck()
        {
            Build();
            EditorSceneManager.OpenScene(DemoScenePath);
            KartEffectsDemoKart kart = Object.FindFirstObjectByType<KartEffectsDemoKart>();
            KartEffects effects = kart.GetComponentInChildren<KartEffects>();
            effects.Setup();

            ParticleSystem[] systems = kart.GetComponentsInChildren<ParticleSystem>(true);
            int broken = 0;
            foreach (ParticleSystem system in systems)
            {
                Material material = system.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                if (material == null || !material.shader.isSupported)
                {
                    broken++;
                }
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.useAutoRandomSeed = false;
                system.randomSeed = 1234;
                system.Play(true);
            }

            string shots = System.Environment.GetEnvironmentVariable("KARTFX_SHOTS");
            Camera camera = kart.GetComponentInChildren<Camera>();
            Dictionary<int, string> stills = new Dictionary<int, string>
            {
                { 40, "1_smoke" }, { 84, "2_blue_sparks" }, { 138, "3_orange_sparks" }, { 150, "4_boost_orange" }, { 372, "5_boost_blue" },
            };
            List<string> problems = new List<string>();
            if (broken > 0)
            {
                problems.Add(broken + " particle systems with a missing or broken material");
            }

            const float dt = 1f / 60f;
            for (int frame = 1; frame <= 480; frame++)
            {
                kart.Step(dt);
                effects.Tick();
                foreach (ParticleSystem system in systems)
                {
                    system.Simulate(dt, false, false, false);
                }
                Expect(frame, 36, "Smoke", true, systems, problems);       // 0.6 s: drifting, no stage yet
                Expect(frame, 36, "Sparks", false, systems, problems);
                Expect(frame, 84, "Sparks", true, systems, problems);      // 1.4 s: blue sparks
                Expect(frame, 180, "Flame", true, systems, problems);      // 3.0 s: boost after the long drift
                Expect(frame, 290, "Flame", false, systems, problems);     // 4.8 s: coasting
                Expect(frame, 290, "Sparks", false, systems, problems);
                Expect(frame, 372, "Flame", true, systems, problems);      // 6.2 s: boost after the short drift
                if (frame == 180 && IsBlue(systems, "Flame"))
                {
                    problems.Add("3.0 s: the flame after an orange drift should be orange");
                }
                if (frame == 372 && !IsBlue(systems, "Flame"))
                {
                    problems.Add("6.2 s: the flame after a blue drift should be blue");
                }
                if (!string.IsNullOrEmpty(shots) && stills.ContainsKey(frame))
                {
                    Render(camera, Path.Combine(shots, "kartfx_" + stills[frame] + ".png"));
                }
            }

            if (problems.Count == 0)
            {
                Debug.Log("KartEffects check: PASS (" + systems.Length + " particle systems, smoke, blue and orange sparks, orange and blue boost flames at the right times)");
            }
            else
            {
                Debug.LogError("KartEffects check: FAIL\n- " + string.Join("\n- ", problems));
                EditorApplication.Exit(1);
            }
        }

        // At one frame: are there live particles in the systems with this name, as expected?
        private static void Expect(int frame, int atFrame, string name, bool on, ParticleSystem[] systems, List<string> problems)
        {
            if (frame != atFrame)
            {
                return;
            }
            int count = 0;
            foreach (ParticleSystem system in systems)
            {
                if (system.name == name)
                {
                    count += system.particleCount;
                }
            }
            if ((count > 0) != on)
            {
                problems.Add((frame / 60f).ToString("0.0") + " s: " + name + " has " + count + " particles, expected " + (on ? "some" : "none"));
            }
        }

        // True if the flame's start colour is bluer than it is red.
        private static bool IsBlue(ParticleSystem[] systems, string name)
        {
            foreach (ParticleSystem system in systems)
            {
                if (system.name == name)
                {
                    Color colour = system.main.startColor.colorMin;
                    return colour.b > colour.r;
                }
            }
            return false;
        }

        private static void Render(Camera camera, string file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            RenderTexture target = new RenderTexture(1280, 720, 24) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            Texture2D picture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            picture.Apply();
            File.WriteAllBytes(file, picture.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(picture);
            Object.DestroyImmediate(target);
        }

        // ---------- helpers ----------

        // URP particle material: additive for light (sparks, flames), alpha blended for smoke. intensity > 1 feeds bloom.
        private static Material ParticleMaterial(string name, string texture, bool additive, float intensity)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(TextureFolder + texture + ".png");
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();

            string path = MaterialFolder + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + texture + ".png"));
            material.SetColor("_BaseColor", new Color(intensity, intensity, intensity, 1f));
            material.SetFloat("_Surface", 1f);                       // transparent
            material.SetFloat("_Blend", additive ? 2f : 0f);         // 2 = additive, 0 = alpha
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", additive ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent + (additive ? 10 : 0);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LitMaterial(string name, Color colour, float smoothness)
        {
            string path = MaterialFolder + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static ParticleSystem NewSystem(GameObject parent, string name, Material material, ParticleSystemRenderMode mode)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;                                // KartEffects turns it on and off
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return system;
        }

        private static void SparkSetup(ParticleSystem system, Transform ground, float lifeMin, float lifeMax, int max)
        {
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.06f);   // big enough to read from the chase camera
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.7f;
            main.maxParticles = max;
            ParticleSystem.InheritVelocityModule inherit = system.inheritVelocity;
            inherit.enabled = true;
            inherit.mode = ParticleSystemInheritVelocityMode.Initial;
            inherit.curve = 0.8f;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(new Color(1f, 0.75f, 0.6f), new[] { 0f, 0.65f, 1f }, new[] { 1f, 0.9f, 0f });
            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.SetPlane(0, ground);
            collision.dampen = 0.35f;
            collision.bounce = 0.4f;
            collision.lifetimeLoss = 0.2f;
            collision.radiusScale = 0.4f;
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.velocityScale = 0.045f;
            renderer.lengthScale = 2.5f;
            renderer.maxParticleSize = 0.2f;
        }

        private static void FlameSetup(ParticleSystem system, float lifeMin, float lifeMax, float speedMin, float speedMax,
                                       float sizeMin, float sizeMax, int max)
        {
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;   // the jet stays on the pipe
            main.maxParticles = max;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 0.02f;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(Color.white, new[] { 0f, 0.1f, 0.7f, 1f }, new[] { 0f, 1f, 0.7f, 0f });
        }

        // A gradient from white to endColour with these alpha keys (times and values in matching order).
        private static Gradient Fade(Color endColour, float[] times, float[] alphas)
        {
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                alphaKeys[i] = new GradientAlphaKey(alphas[i], times[i]);
            }
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(endColour, 1f) }, alphaKeys);
            return gradient;
        }

        private static void Box(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = euler;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
