using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarcavelosCircuit.RaceFlow
{
    // A small photo studio for kart models (kart builder, 18 Nov 2026): its own camera films one kart (the builder's
    // turntable) or a row of karts (the line-up on the first screen) into a picture (RenderTexture) that the menus show
    // like any image. Transparent background, no post-processing, so the kart sits cleanly on the glass plates.
    // Where: far below the campus (y = -6000 m), out of reach of every game camera (far plane 2000 m), on its own layer.
    // Light: the scene's sun (so noon and sunset look like the world) plus a warm key light and a cool rim light that
    // only light the studio's layer. Fog is switched off while this camera draws (5 m of sea fog would grey the kart).
    // The karts come from Role 3 through KartModels (RaceFlow cannot see the Visuals assembly). Each kart sits on a
    // small StageKart that tells the model "steer this much, standing still" (IKartState), so the wheel turns, the front
    // tyres steer and the driver leans exactly like in a race.
    public class KartStage : MonoBehaviour
    {
        public const int StageLayer = 30;           // an unused layer: only the studio cameras and lights see it
        private const float FieldOfView = 22f;      // a long lens: less distortion, a "product photo" look
        private const float SpinDegreesPerSecond = 16f;
        private const float PopSeconds = 0.28f;

        // 4x multisampling for clean edges; none in batch mode (the CI test machine draws in software, llvmpipe).
        private static int Smoothing { get { return Application.isBatchMode ? 1 : 4; } }

        private Camera studioCamera;
        private Transform pivot;                    // turns (turntable) and holds the karts
        private readonly List<StageKart> karts = new List<StageKart>();
        private RenderTexture picture;
        private bool lineup;
        private float yaw = -32f;                   // 3/4 front view to start with
        private float popStarted = -10f;
        private bool savedFog;
        private Vector3 lookAt;
        private float distance;

        public RenderTexture Picture { get { return picture; } }
        public bool Spins { get; set; }             // the turntable turns by itself (stops while dragged)
        public bool HasKarts { get { return karts.Count > 0; } }
        public int KartCount { get { return karts.Count; } }

        // Draws every frame while true (the menu that shows the picture is open), else the camera rests. The wish is
        // kept apart from the camera: the picture often comes a frame later (its size is known only after layout).
        private bool wantsRendering;

        public bool Rendering
        {
            get { return wantsRendering; }
            set
            {
                wantsRendering = value;
                ApplyRendering();
            }
        }

        // True while the camera really draws (a wish and a picture to draw into).
        public bool IsDrawing { get { return studioCamera != null && studioCamera.enabled; } }

        private void ApplyRendering()
        {
            if (studioCamera != null)
            {
                studioCamera.enabled = wantsRendering && picture != null;
            }
        }

        // A new studio at origin (each studio needs its own spot, 200 m apart, so they never film each other).
        public static KartStage Create(string name, Vector3 origin)
        {
            GameObject root = new GameObject(name);
            root.transform.position = origin;
            KartStage stage = root.AddComponent<KartStage>();
            stage.Build();
            return stage;
        }

        private void Build()
        {
            pivot = new GameObject("Turntable").transform;
            pivot.SetParent(transform, false);

            GameObject cameraObject = new GameObject("Studio Camera");
            cameraObject.transform.SetParent(transform, false);
            studioCamera = cameraObject.AddComponent<Camera>();
            studioCamera.clearFlags = CameraClearFlags.SolidColor;
            studioCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);   // see-through behind the kart
            studioCamera.cullingMask = 1 << StageLayer;
            studioCamera.fieldOfView = FieldOfView;
            studioCamera.nearClipPlane = 0.1f;
            studioCamera.farClipPlane = 40f;
            studioCamera.allowHDR = false;   // the HDR buffer has no alpha channel: the background would turn black
            studioCamera.allowMSAA = true;
            studioCamera.depth = -10f;
            studioCamera.enabled = false;
            UniversalAdditionalCameraData data = studioCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = true;
            data.requiresColorTexture = false;
            data.requiresDepthTexture = false;

            AddLight("Key Light", new Vector3(-2.2f, 2.4f, 2.6f), new Color(1f, 0.86f, 0.70f), 6f, 9f);
            AddLight("Rim Light", new Vector3(2.4f, 2.0f, -2.8f), new Color(0.62f, 0.78f, 1f), 7f, 9f);
            AddLight("Fill Light", new Vector3(0.5f, 0.4f, 3.4f), new Color(1f, 0.95f, 0.9f), 1.6f, 8f);

            RenderPipelineManager.beginCameraRendering += BeforeDraw;
            RenderPipelineManager.endCameraRendering += AfterDraw;
        }

        private void AddLight(string name, Vector3 position, Color colour, float intensity, float range)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << StageLayer;
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeDraw;
            RenderPipelineManager.endCameraRendering -= AfterDraw;
            ReleasePicture();
        }

        private void BeforeDraw(ScriptableRenderContext context, Camera drawing)
        {
            if (drawing == studioCamera)
            {
                savedFog = RenderSettings.fog;
                RenderSettings.fog = false;
            }
        }

        private void AfterDraw(ScriptableRenderContext context, Camera drawing)
        {
            if (drawing == studioCamera)
            {
                RenderSettings.fog = savedFog;
            }
        }

        // The picture's size in pixels (the menu asks for the size it is shown at x the screen's pixel ratio).
        // A new picture only when the size really changed, so resizing the window does not flicker.
        public RenderTexture EnsurePicture(int width, int height)
        {
            width = Mathf.Clamp(width, 64, 2048);
            height = Mathf.Clamp(height, 64, 2048);
            if (picture != null && Mathf.Abs(picture.width - width) < 8 && Mathf.Abs(picture.height - height) < 8)
            {
                return picture;
            }
            ReleasePicture();
            picture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            picture.name = name + " Picture";
            picture.antiAliasing = Smoothing;
            picture.filterMode = FilterMode.Bilinear;
            picture.Create();
            studioCamera.targetTexture = picture;
            studioCamera.aspect = (float)width / height;
            ApplyRendering();
            Frame();
            return picture;
        }

        private void ReleasePicture()
        {
            if (studioCamera != null)
            {
                studioCamera.targetTexture = null;
            }
            if (picture != null)
            {
                picture.Release();
                Destroy(picture);
                picture = null;
            }
        }

        // One kart on the turntable (the builder). A changed set pops a little, so the eye sees what changed.
        public void ShowSingle(KartLoadout loadout)
        {
            ClearKarts();
            lineup = false;
            StageKart kart = AddKart(loadout, Vector3.zero, 0f);
            if (kart != null)
            {
                popStarted = Time.unscaledTime;
            }
            Frame();
        }

        // The four drivers side by side (the first screen), slightly staggered like on a start grid.
        public void ShowLineup(KartLoadout[] loadouts)
        {
            ClearKarts();
            lineup = true;
            yaw = 0f;
            pivot.localRotation = Quaternion.identity;
            float spacing = 1.12f;
            for (int i = 0; i < loadouts.Length; i++)
            {
                float x = -(i - (loadouts.Length - 1) * 0.5f) * spacing;      // the camera looks along -Z: first on the left
                float z = -Mathf.Abs(i - (loadouts.Length - 1) * 0.5f) * 0.55f;   // a shallow V, the middle two in front
                AddKart(loadouts[i], new Vector3(x, 0f, z), -24f + i * 16f);     // each turned a bit towards the centre
            }
            Frame();
        }

        // Turn the turntable by hand (mouse or finger drag, degrees).
        public void Drag(float degrees)
        {
            yaw += degrees;
        }

        private StageKart AddKart(KartLoadout loadout, Vector3 place, float turn)
        {
            GameObject holder = new GameObject("Stage Kart " + loadout.ModelName);
            holder.transform.SetParent(pivot, false);
            holder.transform.localPosition = place;
            holder.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
            StageKart kart = holder.AddComponent<StageKart>();
            kart.Phase = karts.Count * 1.7f;
            GameObject model = KartModels.Make(loadout, holder.transform);
            if (model == null)
            {
                Destroy(holder);
                return null;
            }
            SetLayer(holder.transform, StageLayer);
            kart.Model = model.transform;
            karts.Add(kart);
            return kart;
        }

        private void ClearKarts()
        {
            for (int i = 0; i < karts.Count; i++)
            {
                if (karts[i] != null)
                {
                    karts[i].gameObject.SetActive(false);
                    Destroy(karts[i].gameObject);
                }
            }
            karts.Clear();
        }

        // Where the camera stands: a fixed, flattering 3/4 view from a little above, far enough for the whole set.
        private void Frame()
        {
            if (lineup)
            {
                lookAt = new Vector3(0f, 0.5f, -0.25f);
                float aspect = picture != null ? (float)picture.width / picture.height : 2.2f;
                // the row is about 4.8 m wide: fit it into the picture's width
                float halfWidth = 2.45f;
                float tanHalf = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * aspect;
                distance = Mathf.Max(halfWidth / tanHalf, 5f);
            }
            else
            {
                lookAt = new Vector3(0f, 0.43f, 0f);
                float aspect = picture != null ? (float)picture.width / picture.height : 1.2f;
                float fit = Mathf.Max(1.3f / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad),
                                      1.45f / (Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * aspect));
                distance = fit;
            }
            PlaceCamera();
        }

        private void PlaceCamera()
        {
            if (studioCamera == null)
            {
                return;
            }
            float pitch = lineup ? 9f : 14f;
            Vector3 from = Quaternion.Euler(-pitch, 0f, 0f) * new Vector3(0f, 0f, distance); // in front, a little above
            studioCamera.transform.localPosition = lookAt + from;
            studioCamera.transform.LookAt(transform.TransformPoint(lookAt));
        }

        private void Update()
        {
            if (!lineup)
            {
                if (Spins)
                {
                    yaw += SpinDegreesPerSecond * Time.unscaledDeltaTime;
                }
                pivot.localRotation = Quaternion.Euler(0f, yaw, 0f); // yaw 0: the kart faces the camera (it looks along +Z)
                float t = (Time.unscaledTime - popStarted) / PopSeconds;
                float scale = t >= 1f ? 1f : HudEasing.OutBack(Mathf.Clamp01(t)) * 0.12f + 0.88f;
                pivot.localScale = new Vector3(scale, scale, scale);
            }
            else
            {
                pivot.localRotation = Quaternion.identity;
                pivot.localScale = Vector3.one;
            }
        }

        // Every part of the kart goes on the studio layer (the scene sun lights all layers, the studio lights only it).
        private static void SetLayer(Transform part, int layer)
        {
            part.gameObject.layer = layer;
            for (int i = 0; i < part.childCount; i++)
            {
                SetLayer(part.GetChild(i), layer);
            }
        }

        // Takes one still picture (the builder's kart and tyre tiles) of what is on the turntable now, with only the
        // parts whose names pass the filter visible. Returns a new RenderTexture the caller keeps (and releases).
        public RenderTexture Snapshot(int width, int height, string onlyUnder, bool hideDriver, float snapshotYaw, float margin)
        {
            if (karts.Count == 0 || karts[0] == null || karts[0].Model == null)
            {
                return null;
            }
            Transform model = karts[0].Model;
            Transform focus = model;
            if (!string.IsNullOrEmpty(onlyUnder))
            {
                focus = FindPart(model, onlyUnder);
                if (focus == null)
                {
                    return null;
                }
            }
            var hidden = new List<Renderer>();
            Bounds bounds = new Bounds(focus.position, Vector3.zero);
            bool first = true;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                bool far = IsFarPart(renderer.transform, model);
                bool driver = IsDriverPart(renderer.transform, model);
                bool keep = renderer.transform.IsChildOf(focus) && !far && !(hideDriver && driver);
                if (!keep)
                {
                    if (renderer.enabled)
                    {
                        renderer.enabled = false;
                        hidden.Add(renderer);
                    }
                    continue;
                }
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            LODGroup group = model.GetComponent<LODGroup>();
            if (group != null)
            {
                group.ForceLOD(0);
            }

            var shot = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = Smoothing, name = "Tile " + onlyUnder };
            shot.Create();
            RenderTexture before = studioCamera.targetTexture;
            float aspectBefore = studioCamera.aspect;
            Vector3 positionBefore = studioCamera.transform.position;
            Quaternion rotationBefore = studioCamera.transform.rotation;
            Quaternion pivotBefore = pivot.localRotation;
            Vector3 scaleBefore = pivot.localScale;

            pivot.localScale = Vector3.one;
            pivot.localRotation = Quaternion.Euler(0f, snapshotYaw, 0f);
            // the bounds were measured before the turn: measure again
            bounds = MeasureVisible(model, focus);
            // far enough that the part fits the picture both ways (its depth added, so the near side fits too)
            float aspect = (float)width / height;
            float tanV = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = Mathf.Sqrt(bounds.extents.x * bounds.extents.x + bounds.extents.z * bounds.extents.z);
            float back = Mathf.Max(bounds.extents.y / tanV, halfWidth / (tanV * aspect)) * margin + halfWidth;
            Vector3 direction = Quaternion.Euler(-12f, 0f, 0f) * Vector3.forward; // in front, a little above
            studioCamera.transform.position = bounds.center + transform.rotation * direction * back;
            studioCamera.transform.LookAt(bounds.center);
            studioCamera.targetTexture = shot;
            studioCamera.aspect = aspect;
            studioCamera.Render();

            studioCamera.targetTexture = before;
            studioCamera.aspect = aspectBefore;
            studioCamera.transform.SetPositionAndRotation(positionBefore, rotationBefore);
            pivot.localRotation = pivotBefore;
            pivot.localScale = scaleBefore;
            if (group != null)
            {
                group.ForceLOD(-1);
            }
            for (int i = 0; i < hidden.Count; i++)
            {
                hidden[i].enabled = true;
            }
            return shot;
        }

        private static Bounds MeasureVisible(Transform model, Transform focus)
        {
            Bounds bounds = new Bounds(focus.position, Vector3.zero);
            bool first = true;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled)
                {
                    continue;
                }
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }

        // Role 3's naming (Visuals/Kart): "_Far" = the light copy for distant karts, "Driver_" = the driver.
        private static bool IsFarPart(Transform part, Transform stopAt)
        {
            for (Transform t = part; t != null && t != stopAt; t = t.parent)
            {
                if (t.name.EndsWith("_Far", System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsDriverPart(Transform part, Transform stopAt)
        {
            for (Transform t = part; t != null && t != stopAt; t = t.parent)
            {
                if (t.name.StartsWith("Driver_", System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static Transform FindPart(Transform root, string partName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == partName)
                {
                    return t;
                }
            }
            return null;
        }
    }
}
