using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarcavelosCircuit.Ambience.Editor
{
    // Makes the beach ambience assets from code (task R12) and puts the "Ambience" object into the open scene.
    // The Role 4 scene builder calls AddToScene() while Main.unity is open, so nobody edits a scene or asset by hand.
    // Safe to run again: the settings asset keeps its values, the two materials are set up again.
    public static class AmbienceBuilder
    {
        private const string Folder = "Assets/_Project/Ambience";
        public const string SettingsPath = Folder + "/Settings/AmbienceSettings.asset";
        private const string WaveMaterialPath = Folder + "/Materials/AmbienceWaves.mat";
        private const string FoamMaterialPath = Folder + "/Materials/AmbienceFoam.mat";
        private const string SurferMaterialPath = Folder + "/Materials/AmbienceSurfers.mat";
        public const string ObjectName = "Ambience";

        // Adds the Ambience root object (SeasideAmbience with its settings and materials) to the active scene.
        public static GameObject AddToScene()
        {
            AmbienceSettings settings = CreateSettings();
            Material waves = CreateWaveMaterial();
            Material foam = CreateFoamMaterial();
            Material surfers = CreateSurferMaterial();
            var go = new GameObject(ObjectName);
            SeasideAmbience ambience = go.AddComponent<SeasideAmbience>();
            var so = new SerializedObject(ambience);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("waveMaterial").objectReferenceValue = waves;
            so.FindProperty("foamMaterial").objectReferenceValue = foam;
            so.FindProperty("surferMaterial").objectReferenceValue = surfers;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (settings == null || waves == null || foam == null || surfers == null)
            {
                throw new System.Exception("AmbienceBuilder: the settings asset or a material could not be made.");
            }
            return go;
        }

        public static AmbienceSettings CreateSettings()
        {
            EnsureFolder(Folder + "/Settings");
            AmbienceSettings settings = AssetDatabase.LoadAssetAtPath<AmbienceSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<AmbienceSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // URP's Particles/Unlit shader reads the mesh's vertex colours (our wave colours and their transparency),
        // costs almost nothing and runs on every Web GPU. See-through, both sides drawn (the curling lip shows its
        // underside), no depth writing so the waves blend into the sea.
        public static Material CreateWaveMaterial()
        {
            return CreateSeeThroughMaterial(WaveMaterialPath, (int)RenderQueue.Transparent);
        }

        // The foam lace layer: the same kind of material, drawn right after the water (queue + 1). Its picture is
        // made in code when the game starts (FoamPattern), so there is no texture file to import.
        public static Material CreateFoamMaterial()
        {
            return CreateSeeThroughMaterial(FoamMaterialPath, (int)RenderQueue.Transparent + 1);
        }

        private static Material CreateSeeThroughMaterial(string path, int queue)
        {
            Material material = LoadOrCreate(path, "Universal Render Pipeline/Particles/Unlit");
            if (material == null)
            {
                return null;
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = queue;
            EditorUtility.SetDirty(material);
            return material;
        }

        // URP's Particles/Simple Lit: lit by the sun like the rest of the world, colours from the vertices, so all
        // surfers and boards share this one material.
        public static Material CreateSurferMaterial()
        {
            Material material = LoadOrCreate(SurferMaterialPath, "Universal Render Pipeline/Particles/Simple Lit");
            if (material == null)
            {
                return null;
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_Smoothness", 0.35f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)RenderQueue.Geometry;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreate(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("AmbienceBuilder: shader " + shaderName + " not found (URP missing?).");
                return null;
            }
            EnsureFolder(Folder + "/Materials");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
