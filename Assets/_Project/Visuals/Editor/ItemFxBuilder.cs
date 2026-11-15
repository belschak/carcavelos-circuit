using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarcavelosCircuit.Visuals.Editor
{
    // Builds everything the item effects and the new item boxes draw with (task V34), then the KartEffects prefab
    // (which carries ItemFx). Menu: Carcavelos Circuit > Visuals > Build Item FX. Batch:
    //   Unity -batchmode -projectPath . -executeMethod CarcavelosCircuit.Visuals.Editor.ItemFxBuilder.Build -quit
    // Makes (all under Assets/_Project/Visuals/):
    //   ItemFx/Textures/   FX_Water, FX_FoamBand, FX_Foam, FX_Ring, FX_Streak, FX_Confetti (drawn in code, own work)
    //   ItemFx/Materials/  water, foam and the particle materials
    //   ItemFx/ItemFxLook.asset
    //   Items/Textures/    ItemBox_Azulejo, ItemBox_AzulejoWave (AzulejoTile, 256 px)
    //   Items/Meshes/      ItemBoxRounded.asset (RoundedCube)
    //   Items/Materials/   the box glazes and Luc's six item models' materials
    //   Items/Prefabs/     Item_<Model>, ItemBoxLook, ItemBoxLook_Wave, ItemBoxGround, SeagullRingLook
    // Role 4's ItemSettings points at the four look prefabs (help PR); until then the game keeps the old look.
    public static class ItemFxBuilder
    {
        private const string Root = "Assets/_Project/Visuals/";
        private const string FxTextures = Root + "ItemFx/Textures/";
        private const string FxMaterials = Root + "ItemFx/Materials/";
        private const string LookPath = Root + "ItemFx/ItemFxLook.asset";
        private const string ItemTextures = Root + "Items/Textures/";
        private const string ItemMaterials = Root + "Items/Materials/";
        private const string ItemMeshes = Root + "Items/Meshes/";
        private const string ItemPrefabs = Root + "Items/Prefabs/";
        private const string ItemModels = Root + "Items/Models/";
        private const string KartFxTextures = Root + "KartEffects/Textures/";

        public const string BoxLookPath = ItemPrefabs + "ItemBoxLook.prefab";
        public const string WaveBoxLookPath = ItemPrefabs + "ItemBoxLook_Wave.prefab";
        public const string BoxGroundPath = ItemPrefabs + "ItemBoxGround.prefab";
        public const string RingLookPath = ItemPrefabs + "SeagullRingLook.prefab";

        private static readonly string[] Models = { "Seagull", "SeagullWing", "Lifebuoy", "Laurel", "Mortarboard", "Tassel" };

        [MenuItem("Carcavelos Circuit/Visuals/Build Item FX")]
        public static void Build()
        {
            foreach (string folder in new[] { FxTextures, FxMaterials, ItemTextures, ItemMaterials, ItemMeshes, ItemPrefabs })
            {
                Directory.CreateDirectory(folder);
            }
            AssetDatabase.Refresh();

            WriteTexture(FxTextures + "FX_Water.png", 256, WaterPixel, true, 256, false);
            WriteTexture(FxTextures + "FX_FoamBand.png", 256, FoamBandPixel, true, 256, false);
            WriteTexture(FxTextures + "FX_Foam.png", 256, FoamPixel, true, 256);
            WriteTexture(FxTextures + "FX_Ring.png", 256, RingPixel, false, 256);
            WriteTexture(FxTextures + "FX_Streak.png", 128, StreakPixel, false, 128);
            WriteTexture(FxTextures + "FX_Confetti.png", 32, ConfettiPixel, false, 32);
            WriteTile(ItemTextures + "ItemBox_Azulejo.png", AzulejoTile.Variant.Classic);
            WriteTile(ItemTextures + "ItemBox_AzulejoWave.png", AzulejoTile.Variant.Wave);
            AssetDatabase.Refresh();

            ItemFxLook look = AssetDatabase.LoadAssetAtPath<ItemFxLook>(LookPath);
            if (look == null)
            {
                look = ScriptableObject.CreateInstance<ItemFxLook>();
                AssetDatabase.CreateAsset(look, LookPath);
            }
            look.Water = WaterMaterial(look.WaveTurquoise);
            look.Foam = FoamMaterial();
            look.Droplet = ParticleMaterial("ItemFX_Droplet", KartFxTextures + "FX_Glow.png", false, 1f);
            look.FoamPatch = ParticleMaterial("ItemFX_FoamPatch", FxTextures + "FX_Foam.png", false, 1f);
            look.Mist = ParticleMaterial("ItemFX_Mist", KartFxTextures + "FX_Smoke_4x4.png", false, 1f);
            look.Ring = ParticleMaterial("ItemFX_Ring", FxTextures + "FX_Ring.png", false, 1f); // alpha: reads on bright ground
            look.Trail = ParticleMaterial("ItemFX_Trail", FxTextures + "FX_Streak.png", false, 1f);
            look.Spark = ParticleMaterial("ItemFX_Spark", KartFxTextures + "FX_Spark.png", true, 3f);
            look.Confetti = ParticleMaterial("ItemFX_Confetti", FxTextures + "FX_Confetti.png", false, 1f);
            look.Seagull = ModelPrefab("Seagull", 0f);
            look.SeagullWing = ModelPrefab("SeagullWing", 0f);
            look.Lifebuoy = ModelPrefab("Lifebuoy", 0.12f);
            look.Laurel = ModelPrefab("Laurel", 0.35f);
            look.Mortarboard = ModelPrefab("Mortarboard", 0f);
            look.Tassel = ModelPrefab("Tassel", 0.25f);
            EditorUtility.SetDirty(look);

            Mesh rounded = RoundedMesh();
            Material glaze = GlazeMaterial("ItemBox_Azulejo", ItemTextures + "ItemBox_Azulejo.png");
            Material waveGlaze = GlazeMaterial("ItemBox_AzulejoWave", ItemTextures + "ItemBox_AzulejoWave.png");
            BuildBoxLook(BoxLookPath, rounded, glaze, look, look.AtlanticBlue);
            BuildBoxLook(WaveBoxLookPath, rounded, waveGlaze, look, look.WaveTurquoise);
            BuildGround(look);
            BuildRingLook(look);
            AssetDatabase.SaveAssets();

            KartEffectsBuilder.Build(); // the KartEffects prefab picks up ItemFx + this look
            Debug.Log("ItemFxBuilder: item effects, box looks and ring look built (" + LookPath + ").");
        }

        // ---------- textures (own work, drawn in code) ----------

        private delegate Color Pixel(float u, float v);

        // Water across the wave (v: 0 = inner edge by the kart, 1 = outer edge on the road), streaks along u.
        // Shallow and light by the kart, bright at the crest (v ~0.5), deep teal and more opaque on the outer face,
        // with bright lines running round the wave (never down it: lines down the face read as drips).
        // The colour multiplies the material's turquoise; the alpha multiplies its alpha. u tiles, v does not.
        private static Color WaterPixel(float u, float v)
        {
            float a = u * Mathf.PI * 2f;
            // Bright lines that run round the wave (along u), wobbling a little: light caught on moving water.
            float lines = Mathf.Pow(Mathf.Abs(Mathf.Sin(v * 26f + Mathf.Sin(a * 5f) * 0.9f + Mathf.Sin(a * 11f) * 0.3f)), 14f);
            float sparkle = Mathf.Pow(Mathf.Abs(Mathf.Sin(a * 17f + v * 6f) * Mathf.Sin(a * 7f - v * 11f)), 10f);
            float crest = Mathf.Exp(-Mathf.Pow((v - 0.5f) / 0.14f, 2f));
            float deep = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((v - 0.58f) / 0.35f));
            float value = Mathf.Lerp(1f, 0.6f, deep) + 0.12f * crest + 0.16f * lines * (1f - deep * 0.5f) + 0.2f * sparkle;
            float alpha = Mathf.Lerp(0.7f, 0.95f, Mathf.Max(crest, deep));
            return new Color(Mathf.Clamp01(value * 0.92f), Mathf.Clamp01(value), Mathf.Clamp01(value), alpha);
        }

        // The foam layer on the wave (same mesh, drawn on top): white lace on the crest and the curling lip
        // (v ~0.45-0.63), a thin frothy swash line where the water meets the road (v > 0.93) and a rim by the kart.
        private static Color FoamBandPixel(float u, float v)
        {
            float a = u * Mathf.PI * 2f;
            float n = 0.5f + 0.3f * Mathf.Sin(a * 6f + Mathf.Sin(v * 21f) * 2f) + 0.2f * Mathf.Sin(a * 13f - v * 17f);
            float lace = Mathf.Abs(Mathf.Sin(a * 11f + v * 30f + n * 5f));
            float bubbles = Mathf.Clamp01((n * 0.9f + lace * 0.5f - 0.45f) * 2.4f);
            float crest = Mathf.Exp(-Mathf.Pow((v - 0.54f) / 0.09f, 2f));
            float swash = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((v - 0.93f) / 0.04f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((v - 0.985f) / 0.015f)));
            float rim = Mathf.Exp(-Mathf.Pow(v / 0.06f, 2f)) * 0.6f;
            float mask = Mathf.Clamp01(crest * 1.25f + swash + rim);
            float alpha = Mathf.Clamp01(mask * (0.45f + 0.75f * bubbles));
            return new Color(1f, 1f, 1f, alpha);
        }

        // Foam: white bubbles and lace with blue-grey gaps. Alpha = the foam itself, faded out to a round patch
        // (the road patches use the alpha; the foam band on the wave only uses the colour, which tiles).
        private static Color FoamPixel(float u, float v)
        {
            float a = u * Mathf.PI * 2f;
            float b = v * Mathf.PI * 2f;
            float n = 0.5f + 0.25f * Mathf.Sin(a * 3f + Mathf.Sin(b * 2f) * 2f) + 0.25f * Mathf.Sin(b * 4f + Mathf.Cos(a * 5f) * 1.5f);
            float lace = Mathf.Abs(Mathf.Sin(a * 7f + b * 3f + n * 4f));
            float foam = Mathf.Clamp01((n * 0.8f + lace * 0.45f - 0.35f) * 2.2f);
            Color colour = Color.Lerp(new Color(0.78f, 0.9f, 0.95f), Color.white, foam);
            float x = u - 0.5f;
            float y = v - 0.5f;
            float round = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) * 2f);
            colour.a = foam * Mathf.SmoothStep(0f, 1f, round * 1.6f);
            return colour;
        }

        // A soft ring of light (additive): bright band near the edge, a faint glow inside.
        private static Color RingPixel(float u, float v)
        {
            float x = u - 0.5f;
            float y = v - 0.5f;
            float r = Mathf.Sqrt(x * x + y * y) * 2f;
            float band = Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.11f, 2f));
            float glow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.35f) / 0.45f)) * 0.3f;
            float alpha = Mathf.Clamp01(band + glow) * (r < 1f ? 1f : 0f);
            return new Color(1f, 1f, 1f, alpha);
        }

        // A speed line: bright core across the middle, fading to the sides and towards both ends.
        private static Color StreakPixel(float u, float v)
        {
            float across = Mathf.Exp(-Mathf.Pow((v - 0.5f) / 0.16f, 2f));
            float along = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u * 4f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - u) * 1.5f));
            return new Color(1f, 1f, 1f, across * along);
        }

        // Confetti and leaves: a small rounded rectangle.
        private static Color ConfettiPixel(float u, float v)
        {
            float x = Mathf.Abs(u - 0.5f) * 2f;
            float y = Mathf.Abs(v - 0.5f) * 2f;
            float d = Mathf.Max(x * 0.9f, y * 1.25f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 6f));
        }

        private static void WriteTexture(string path, int size, Pixel pixel, bool repeat, int maxSize, bool repeatV = true)
        {
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
                }
            }
            Save(path, size, pixels);
            Import(path, repeat, maxSize, true);
            if (repeat && !repeatV)
            {
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp; // v is a gradient across the wave, not a pattern
                importer.SaveAndReimport();
            }
        }

        private static void WriteTile(string path, AzulejoTile.Variant variant)
        {
            Save(path, 256, AzulejoTile.Paint(256, variant));
            Import(path, false, 256, false);
        }

        private static void Save(string path, int size, Color[] pixels)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static void Import(string path, bool repeat, int maxSize, bool alpha)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = alpha;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = maxSize;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        // ---------- materials ----------

        private static Material WaterMaterial(Color turquoise)
        {
            Material m = LitMaterial(FxMaterials + "ItemFX_Water.mat");
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FxTextures + "FX_Water.png"));
            m.SetColor("_BaseColor", new Color(turquoise.r, turquoise.g, turquoise.b, 0.92f));
            m.SetFloat("_Smoothness", 0.93f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            Glow(m, turquoise * 0.18f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // The foam layer: transparent, white, a little glossy, drawn just after the water.
        private static Material FoamMaterial()
        {
            Material m = LitMaterial(FxMaterials + "ItemFX_Foam.mat");
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FxTextures + "FX_FoamBand.png"));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.45f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent + 1;
            Glow(m, new Color(0.35f, 0.4f, 0.42f));
            EditorUtility.SetDirty(m);
            return m;
        }

        // The item box glaze: shiny like a glazed tile, the picture glows a little so it reads in the shade.
        private static Material GlazeMaterial(string name, string texturePath)
        {
            Material m = LitMaterial(ItemMaterials + name + ".mat");
            Texture2D tile = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            m.SetTexture("_BaseMap", tile);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.86f);
            m.SetFloat("_Metallic", 0f);
            m.SetTexture("_EmissionMap", tile);
            Glow(m, new Color(0.32f, 0.32f, 0.32f));
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void Glow(Material m, Color emission)
        {
            m.SetColor("_EmissionColor", emission);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        private static Material LitMaterial(string path)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        // URP particle material: additive (light) or alpha blended. intensity > 1 feeds the race camera's bloom.
        private static Material ParticleMaterial(string name, string texturePath, bool additive, float intensity)
        {
            string path = FxMaterials + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            m.SetColor("_BaseColor", new Color(intensity, intensity, intensity, 1f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", additive ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent + (additive ? 10 : 0);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------- Luc's item models (Items v01, Meshy, paid plan) ----------

        // FBX + base colour + "ms" map (R = metallic, A = smoothness) -> a prefab with one URP Lit material.
        private static GameObject ModelPrefab(string name, float glow)
        {
            string folder = ItemModels + name + "/";
            string fbx = folder + "Item_" + name + ".fbx";
            ModelImporter model = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (model == null)
            {
                Debug.LogWarning("ItemFxBuilder: " + fbx + " is missing, that part stays off.");
                return null;
            }
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.importAnimation = false;
            model.importCameras = false;
            model.importLights = false;
            model.isReadable = false;
            model.meshCompression = ModelImporterMeshCompression.Medium;
            model.SaveAndReimport();
            TextureSize(folder + "Item_" + name + "_base.png", 512, true);
            TextureSize(folder + "Item_" + name + "_ms.png", 256, false);

            Material m = LitMaterial(ItemMaterials + "Item_" + name + ".mat");
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "Item_" + name + "_base.png"));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "Item_" + name + "_ms.png"));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Smoothness", 1f);
            if (glow > 0f)
            {
                m.SetTexture("_EmissionMap", m.GetTexture("_BaseMap"));
                Glow(m, new Color(glow, glow, glow));
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            GameObject copy = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            copy.name = "Item_" + name;
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return SavePrefab(copy, ItemPrefabs + "Item_" + name + ".prefab");
        }

        private static void TextureSize(string path, int max, bool srgb)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }
            importer.maxTextureSize = max;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        // ---------- item box looks ----------

        private static Mesh RoundedMesh()
        {
            string path = ItemMeshes + "ItemBoxRounded.asset";
            Mesh built = RoundedCube.ToMesh(RoundedCube.Build(1f, 0.16f, 8), "ItemBoxRounded");
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            EditorUtility.CopySerialized(built, mesh);
            Object.DestroyImmediate(built);
            return mesh;
        }

        // The box body (unit size: ItemBox scales its cube by the box size), a soft glow behind it and a few
        // sparkles drifting round it. Hangs under ItemBox's spinning, bobbing cube.
        private static void BuildBoxLook(string path, Mesh mesh, Material glaze, ItemFxLook look, Color tint)
        {
            GameObject root = new GameObject(Path.GetFileNameWithoutExtension(path));
            GameObject tile = new GameObject("Tile");
            tile.transform.SetParent(root.transform, false);
            tile.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = tile.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = glaze;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            ParticleSystem glow = Looping(root, "Glow", look.Droplet, ParticleSystemRenderMode.Billboard, 3);
            Main(glow, 1.4f, 1.4f, 0f, 0f, 2.3f, 2.3f, new Color(tint.r, tint.g, tint.b, 0.32f), 1.6f);
            Pulse(glow, 0.85f, 1.1f);

            ParticleSystem sparkles = Looping(root, "Sparkles", look.Spark, ParticleSystemRenderMode.Billboard, 12);
            Main(sparkles, 0.9f, 1.4f, 0.05f, 0.25f, 0.05f, 0.1f, Color.Lerp(tint, Color.white, 0.6f), 6f);
            ParticleSystem.ShapeModule shape = sparkles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.75f;
            shape.radiusThickness = 0.1f;
            Twinkle(sparkles);
            SavePrefab(root, path);
        }

        // The light on the road: a pulsing ring while the box is there, a burst when it breaks (ItemBoxLookFx).
        private static void BuildGround(ItemFxLook look)
        {
            GameObject root = new GameObject("ItemBoxGround");
            ParticleSystem ring = Looping(root, "GroundRing", look.Ring, ParticleSystemRenderMode.HorizontalBillboard, 3);
            ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            Main(ring, 1.6f, 1.6f, 0f, 0f, 2.6f, 2.6f, new Color(0.55f, 0.8f, 1f, 0.45f), 0.9f);
            Pulse(ring, 0.45f, 1.15f);

            ParticleSystem sparkles = Burst(root, "BreakSparkles", look.Spark, ParticleSystemRenderMode.Stretch, 60);
            Main(sparkles, 0.35f, 0.7f, 4f, 9f, 0.1f, 0.2f, new Color(0.85f, 0.93f, 1f, 1f), 0f);
            ParticleSystem.MainModule sm = sparkles.main;
            sm.gravityModifier = 1.1f;
            sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.93f, 1f, 1f), new Color(1f, 0.82f, 0.4f, 1f));
            ParticleSystem.ShapeModule shape = sparkles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;
            Twinkle(sparkles);

            ParticleSystem burstRing = Burst(root, "BreakRing", look.Ring, ParticleSystemRenderMode.HorizontalBillboard, 4);
            Main(burstRing, 0.45f, 0.45f, 0f, 0f, 7f, 7f, new Color(0.25f, 0.55f, 0.95f, 1f), 0f); // Atlantic blue: reads on pale stone
            WaveRide.RingGrowth(burstRing);

            ItemBoxLookFx fx = root.AddComponent<ItemBoxLookFx>();
            SerializedObject so = new SerializedObject(fx);
            so.FindProperty("groundRing").objectReferenceValue = ring;
            so.FindProperty("breakSparkles").objectReferenceValue = sparkles;
            so.FindProperty("breakRing").objectReferenceValue = burstRing;
            so.ApplyModifiedPropertiesWithoutUndo();
            SavePrefab(root, BoxGroundPath);
        }

        // Luc's lifebuoy for the Seagull rings, sized for radius 1 standing in the XY plane (SeagullRing scales it),
        // with a soft glow so it reads from far away.
        private static void BuildRingLook(ItemFxLook look)
        {
            GameObject root = new GameObject("SeagullRingLook");
            if (look.Lifebuoy != null)
            {
                GameObject buoy = (GameObject)PrefabUtility.InstantiatePrefab(look.Lifebuoy);
                buoy.transform.SetParent(root.transform, false);
                Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
                bool first = true;
                foreach (Renderer r in buoy.GetComponentsInChildren<Renderer>())
                {
                    if (first) { bounds = r.bounds; first = false; }
                    else bounds.Encapsulate(r.bounds);
                }
                Vector3 size = bounds.size;
                // The thinnest side is the tube's thickness: turn it to face Z (the ring stands across the road).
                Quaternion turn = Quaternion.identity;
                if (size.y <= size.x && size.y <= size.z) turn = Quaternion.Euler(90f, 0f, 0f);
                else if (size.x <= size.y && size.x <= size.z) turn = Quaternion.Euler(0f, 90f, 0f);
                float across = Mathf.Max(size.x, size.y, size.z);
                float scale = across > 0.001f ? 2.3f / across : 1f; // outer diameter 2.3 x radius: the hole fits the trigger
                buoy.transform.localRotation = turn;
                buoy.transform.localScale = Vector3.one * scale;
                buoy.transform.localPosition = -(turn * (bounds.center - buoy.transform.position)) * scale;
            }
            ParticleSystem glow = Looping(root, "Glow", look.Droplet, ParticleSystemRenderMode.Billboard, 3);
            Main(glow, 1.2f, 1.2f, 0f, 0f, 3.4f, 3.4f, new Color(1f, 0.85f, 0.8f, 0.22f), 1.8f);
            Pulse(glow, 0.9f, 1.1f);
            SavePrefab(root, RingLookPath);
        }

        // ---------- particle helpers (built into prefabs) ----------

        private static ParticleSystem Looping(GameObject parent, string name, Material material, ParticleSystemRenderMode mode, int max)
        {
            ParticleSystem system = NewSystem(parent, name, material, mode, max);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.prewarm = true;
            return system;
        }

        private static ParticleSystem Burst(GameObject parent, string name, Material material, ParticleSystemRenderMode mode, int max)
        {
            ParticleSystem system = NewSystem(parent, name, material, mode, max);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f; // ItemBoxLookFx emits bursts
            return system;
        }

        private static ParticleSystem NewSystem(GameObject parent, string name, Material material, ParticleSystemRenderMode mode, int max)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.maxParticles = max;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (mode == ParticleSystemRenderMode.HorizontalBillboard)
            {
                renderer.alignment = ParticleSystemRenderSpace.World;
            }
            if (mode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = 0.05f;
                renderer.lengthScale = 1.5f;
            }
            return system;
        }

        private static void Main(ParticleSystem system, float lifeMin, float lifeMax, float speedMin, float speedMax,
            float sizeMin, float sizeMax, Color colour, float rate)
        {
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = colour;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;
        }

        // Grows from 'from' to 'to' times its size and fades in and out: a slow breathing pulse.
        private static void Pulse(ParticleSystem system, float from, float to)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.35f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(g);
        }

        // Fade in fast, out slowly, and shrink: a twinkle.
        private static void Twinkle(ParticleSystem system)
        {
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(g);
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
