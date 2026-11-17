using System.IO;
using CarcavelosCircuit.RaceFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CarcavelosCircuit.Visuals.Editor
{
    // Driver select (Luc, 17 Nov 2026): one portrait per driver for the start screen's character select
    // (RaceFlow/UI/DriverPicker). Photographs each driver in their own kart (Visuals/Kart/Prefabs/KartVisual_*, built
    // by NovaKartVisualBuilder and NovaKartRosterBuilder) from the front left, and saves a PNG with a see-through
    // background to RaceFlow/UI/Resources/DriverPortraits/<driver>.png.
    // See-through without any special shader: every portrait is rendered twice, on black and on white. Where the
    // two pictures differ, the background shows through, and that difference is exactly the missing alpha.
    // Menu Carcavelos Circuit > Build Driver Portraits (Role 3), or in batch mode:
    //   Unity -batchmode -projectPath . -executeMethod CarcavelosCircuit.Visuals.Editor.DriverPortraitBuilder.Build -quit
    // (not with -nographics: the camera has to really render).
    public static class DriverPortraitBuilder
    {
        private const string PrefabFolder = "Assets/_Project/Visuals/Kart/Prefabs";
        public const string OutputFolder = "Assets/_Project/RaceFlow/UI/Resources/DriverPortraits";
        private const int RenderSize = 1024; // rendered big, imported at 256 (smooth edges)
        private const int ImportSize = 256;

        [MenuItem("Carcavelos Circuit/Build Driver Portraits (Role 3)")]
        public static void Build()
        {
            Directory.CreateDirectory(OutputFolder);

            // A scratch scene next to whatever is open; closed again at the end, nothing is saved. In batch mode
            // the open scene is an untitled empty one, which Unity cannot keep beside a new scene: then replace it.
            Scene previous = SceneManager.GetActiveScene();
            bool alone = string.IsNullOrEmpty(previous.path);
            if (!alone && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            Scene studio = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, alone ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(studio);
            AmbientMode oldAmbientMode = RenderSettings.ambientMode;
            Color oldAmbient = RenderSettings.ambientLight;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.70f);

            try
            {
                GameObject keyLight = new GameObject("Key light");
                SceneManager.MoveGameObjectToScene(keyLight, studio);
                Light key = keyLight.AddComponent<Light>();
                key.type = LightType.Directional;
                key.intensity = 1.25f;
                key.color = new Color(1f, 0.96f, 0.9f);
                key.shadows = LightShadows.None;
                keyLight.transform.rotation = Quaternion.Euler(38f, 150f, 0f);

                for (int i = 0; i < DriverRoster.Count; i++)
                {
                    DriverInfo driver = DriverRoster.Get(i);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/" + driver.KartVisual + ".prefab");
                    if (prefab == null)
                    {
                        Debug.LogError("DriverPortraitBuilder: no kart model " + driver.KartVisual + " for " + driver.Name);
                        continue;
                    }
                    GameObject kart = (GameObject)PrefabUtility.InstantiatePrefab(prefab, studio);
                    kart.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    // Only the full model: no LOD switching (its cross-fade dithers the kart see-through) and the light
                    // far copy (V32, parts named *_Far) switched off.
                    PrefabUtility.UnpackPrefabInstance(kart, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    foreach (LODGroup lod in kart.GetComponentsInChildren<LODGroup>(true))
                    {
                        Object.DestroyImmediate(lod); // a disabled group still left the body half see-through
                    }
                    // A property block on every part takes it off the SRP Batcher: in batch mode the batcher drew
                    // parts half see-through and mixed up the drivers' textures (the colour itself is unchanged).
                    MaterialPropertyBlock own = new MaterialPropertyBlock();
                    own.SetFloat("_PortraitShot", 1f);
                    foreach (Renderer part in kart.GetComponentsInChildren<Renderer>(true))
                    {
                        if (part.name.EndsWith("_Far", System.StringComparison.Ordinal))
                        {
                            part.enabled = false;
                        }
                        part.SetPropertyBlock(own);
                    }
                    KartDriverArms arms = kart.GetComponent<KartDriverArms>();
                    if (arms != null)
                    {
                        arms.SolveNow(); // hands on the steering wheel
                    }
                    Bounds bounds = BoundsOf(kart);

                    GameObject cameraObject = new GameObject("Portrait camera");
                    SceneManager.MoveGameObjectToScene(cameraObject, studio);
                    Camera camera = cameraObject.AddComponent<Camera>();
                    camera.scene = studio;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.fieldOfView = 24f;
                    camera.nearClipPlane = 0.05f;
                    camera.farClipPlane = 50f;
                    camera.allowHDR = false;
                    camera.allowMSAA = true;
                    // Front left, a little from above, aimed at the driver's chest: kart and driver both fill the square.
                    Vector3 target = bounds.center + new Vector3(0f, -bounds.extents.y * 0.12f, 0f);
                    Vector3 direction = new Vector3(-0.62f, 0.38f, 1f).normalized;
                    float radius = bounds.extents.magnitude;
                    float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.68f;
                    cameraObject.transform.position = target + direction * distance;
                    cameraObject.transform.LookAt(target);
                    string path = OutputFolder + "/" + driver.PortraitFile + ".png";
                    File.WriteAllBytes(path, RenderCutOut(camera).EncodeToPNG());
                    Object.DestroyImmediate(cameraObject);
                    Object.DestroyImmediate(kart);
                    Debug.Log("DriverPortraitBuilder: " + driver.Name + " -> " + path);
                }
            }
            finally
            {
                RenderSettings.ambientMode = oldAmbientMode;
                RenderSettings.ambientLight = oldAmbient;
                if (!alone)
                {
                    SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(studio, true);
                }
            }

            AssetDatabase.Refresh();
            for (int i = 0; i < DriverRoster.Count; i++)
            {
                SetImport(OutputFolder + "/" + DriverRoster.Get(i).PortraitFile + ".png");
            }
            Debug.Log("DriverPortraitBuilder: " + DriverRoster.Count + " portraits in " + OutputFolder);
        }

        // Renders on black and on white and keeps only the kart: alpha from the difference, colour from the black shot.
        private static Texture2D RenderCutOut(Camera camera)
        {
            Color[] onBlack = RenderOn(camera, Color.black);
            Color[] onWhite = RenderOn(camera, Color.white);
            Color[] result = new Color[onBlack.Length];
            for (int p = 0; p < result.Length; p++)
            {
                float seen = ((onWhite[p].r - onBlack[p].r) + (onWhite[p].g - onBlack[p].g) + (onWhite[p].b - onBlack[p].b)) / 3f;
                float alpha = Mathf.Clamp01(1f - seen);
                result[p] = alpha < 0.004f
                    ? new Color(0f, 0f, 0f, 0f)
                    : new Color(Mathf.Clamp01(onBlack[p].r / alpha), Mathf.Clamp01(onBlack[p].g / alpha), Mathf.Clamp01(onBlack[p].b / alpha), alpha);
            }
            Texture2D texture = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false);
            texture.SetPixels(result);
            texture.Apply();
            return texture;
        }

        private static Color[] RenderOn(Camera camera, Color background)
        {
            RenderTexture target = new RenderTexture(RenderSize, RenderSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.antiAliasing = 1; // URP blits into it; smooth edges come from rendering big and importing at 256
            camera.backgroundColor = background;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D read = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, RenderSize, RenderSize), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Color[] pixels = read.GetPixels();
            Object.DestroyImmediate(read);
            target.Release();
            Object.DestroyImmediate(target);
            return pixels;
        }

        private static Bounds BoundsOf(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
            bool first = true;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled || renderers[i].name.EndsWith("_Far"))
                {
                    continue;
                }
                if (first)
                {
                    bounds = renderers[i].bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }
            return bounds;
        }

        // UI picture: no mip maps, see-through edges kept clean, 256 px (six of them cost about 1 MB in memory).
        private static void SetImport(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = ImportSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
    }
}
