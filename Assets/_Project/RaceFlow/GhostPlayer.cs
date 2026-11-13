using UnityEngine;
using UnityEngine.Rendering;

namespace CarcavelosCircuit.RaceFlow
{
    // Replays a ghost lap as a see-through kart, in step with the race timer: at 12.3 s on the timer the ghost is
    // where the recorded kart was at 12.3 s. Your own best lap if you have one, otherwise the team's Dean's ghost.
    // The ghost is only meshes and a material: no Rigidbody, no collider, no scripts, so it can never touch the
    // kart or a checkpoint. It fades out near your kart and near the camera, so it never blocks the view.
    public class GhostPlayer : MonoBehaviour
    {
        [SerializeField]
        private RaceManager raceManager;

        [SerializeField, Tooltip("URP transparent material (RaceFlow/Materials/Ghost.mat). Referenced here so it is in the Web build")]
        private Material ghostMaterial;

        [SerializeField, Tooltip("The team's best lap, shipped with the game (RaceFlow/Settings/DeansGhost.txt). Used until the player has an own ghost")]
        private TextAsset shippedGhost;

        [SerializeField, Tooltip("Colour of the player's own ghost (alpha is set by the fade)")]
        private Color ownGhostColour = new Color(0.55f, 0.8f, 1f, 1f);

        [SerializeField, Tooltip("Colour of the shipped Dean's ghost, warm gold so it is clearly not yours")]
        private Color deansGhostColour = new Color(1f, 0.8f, 0.4f, 1f);

        [SerializeField, Range(0f, 1f), Tooltip("How visible the ghost is at most (0 = invisible, 1 = solid)")]
        private float maxAlpha = 0.4f;

        [SerializeField, Tooltip("Closer to your kart than this (metres) the ghost is invisible")]
        private float hideDistance = 1.5f;

        [SerializeField, Tooltip("Further from your kart than this (metres) the ghost is fully visible")]
        private float fullDistance = 8f;

        [SerializeField, Tooltip("Closer to the camera than this (metres) the ghost is invisible")]
        private float cameraHideDistance = 2f;

        [SerializeField, Tooltip("Further from the camera than this (metres) the camera fade stops")]
        private float cameraFullDistance = 5f;

        [SerializeField, Tooltip("After its lap the ghost rolls on and fades out over this many seconds")]
        private float endFadeSeconds = 0.8f;

        // Read by RaceHud's ghost chip ("GHOST: DEAN 1:02.4", audit 13).
        public bool HasGhost { get { return ghost != null; } }
        public bool IsShippedGhost { get; private set; }
        public float GhostLapSeconds { get { return ghost == null ? -1f : ghost.LapSeconds; } }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private GhostRecording ghost;
        private GameObject body;          // the see-through kart, built once from the kart's meshes
        private Material materialInstance; // our own copy of the material, so changing its alpha changes nothing else
        private bool needsLoad = true;
        private string loadedOwnText;     // the save text the current ghost came from (no new parsing if unchanged)
        private float playbackTime;       // seconds since GO on the ghost's clock
        private float shownAlpha = -1f;
        private Camera viewCamera;

        private void OnEnable()
        {
            raceManager.Restarted += OnRestarted;
        }

        private void OnDisable()
        {
            raceManager.Restarted -= OnRestarted;
        }

        private void OnDestroy()
        {
            if (body != null)
            {
                Destroy(body);
            }
            if (materialInstance != null)
            {
                Destroy(materialInstance);
            }
        }

        // A retry may come right after a new record: load again, so the new best lap drives along from now on.
        private void OnRestarted()
        {
            needsLoad = true;
            playbackTime = 0f;
        }

        // LateUpdate: after the kart moved this frame, so the fade uses where the kart really is.
        private void LateUpdate()
        {
            if (!raceManager.IsSetUp || raceManager.KartTransform == null)
            {
                return;
            }
            if (body == null && !BuildBody())
            {
                enabled = false; // no material: no ghost at all instead of a pink kart
                return;
            }
            if (needsLoad)
            {
                Load();
                needsLoad = false;
            }
            if (ghost == null)
            {
                ShowAlpha(0f);
                return;
            }

            UpdatePlaybackTime();

            // Before the end: the recorded pose. After the end: keep rolling in the last direction, slowing down
            // to a stop while fading out (so the ghost never freezes on the finish line).
            Vector3 position;
            Quaternion rotation;
            float secondsPastEnd = playbackTime - ghost.Duration;
            ghost.Sample(playbackTime, out position, out rotation);
            if (secondsPastEnd > 0f && endFadeSeconds > 0f)
            {
                float rolled = Mathf.Min(secondsPastEnd, endFadeSeconds);
                float slowDown = 1f - 0.5f * rolled / endFadeSeconds; // speed falls from 100 % to 0 %
                position += ghost.EndVelocity() * rolled * slowDown;
            }
            body.transform.SetPositionAndRotation(position, rotation);

            float toKart = Vector3.Distance(position, raceManager.KartTransform.position);
            float toCamera = DistanceToCamera(position);
            float fade = GhostFade.Combined(toKart, toCamera, secondsPastEnd,
                hideDistance, fullDistance, cameraHideDistance, cameraFullDistance, endFadeSeconds);
            ShowAlpha(maxAlpha * fade);
        }

        // Ready and countdown: the ghost waits on the grid (time 0). Racing: exactly the race timer.
        // Results: the timer has stopped, but the ghost's clock keeps going so a slower ghost can still finish.
        private void UpdatePlaybackTime()
        {
            RaceState state = raceManager.State;
            if (state == RaceState.Racing)
            {
                playbackTime = raceManager.LapSeconds;
            }
            else if (state == RaceState.Results)
            {
                playbackTime += Time.deltaTime;
            }
            else
            {
                playbackTime = 0f;
            }
        }

        private void Load()
        {
            string ownText = PlayerPrefs.GetString(GhostLoader.SaveKey(raceManager.TrackName), "");
            if (loadedOwnText != null && ownText == loadedOwnText)
            {
                return; // same save as last time: keep the ghost we already have
            }
            loadedOwnText = ownText;
            string shippedText = shippedGhost == null ? "" : shippedGhost.text;
            bool isShipped;
            ghost = GhostLoader.Choose(ownText, shippedText, raceManager.TrackName, raceManager.CheckpointCount,
                raceManager.LayoutStamp, out isShipped);
            IsShippedGhost = isShipped;
            shownAlpha = -1f; // the colour may have changed (own blue or Dean's gold): set it again
        }

        private float DistanceToCamera(Vector3 position)
        {
            if (viewCamera == null)
            {
                viewCamera = Camera.main; // the camera tagged MainCamera; may change when Role 1's camera arrives
            }
            if (viewCamera == null)
            {
                return float.MaxValue;
            }
            return Vector3.Distance(position, viewCamera.transform.position);
        }

        // Sets the material's colour only when the alpha really changed, and switches the ghost off when it is
        // invisible (no draw calls for nothing).
        private void ShowAlpha(float alpha)
        {
            if (shownAlpha >= 0f && Mathf.Abs(alpha - shownAlpha) < 0.002f)
            {
                return;
            }
            shownAlpha = alpha;
            bool visible = alpha > 0.003f;
            if (body.activeSelf != visible)
            {
                body.SetActive(visible);
            }
            if (visible)
            {
                Color colour = IsShippedGhost ? deansGhostColour : ownGhostColour;
                colour.a = alpha;
                materialInstance.SetColor(BaseColorId, colour);
            }
        }

        // Copies only the meshes of the kart's "Visual" child (docs/interfaces.md), or of the whole kart if there is
        // none, into a new object. Nothing else is copied: no Rigidbody, no collider, no scripts, no particles.
        // A kart without meshes gets a simple box. False if the ghost material is missing.
        private bool BuildBody()
        {
            if (ghostMaterial == null)
            {
                Debug.LogWarning("GhostPlayer: no ghost material set, the ghost is switched off. Run Carcavelos Circuit > Build Role 4 Scenes.");
                return false;
            }
            materialInstance = new Material(ghostMaterial);

            Transform kart = raceManager.KartTransform;
            Transform source = kart.Find("Visual");
            if (source == null)
            {
                source = kart;
            }

            body = new GameObject("GhostKart");
            body.transform.localScale = kart.lossyScale; // parts are placed in the kart's own space, scale included
            int parts = 0;
            MeshFilter[] filters = source.GetComponentsInChildren<MeshFilter>();
            for (int i = 0; i < filters.Length; i++)
            {
                MeshRenderer meshRenderer = filters[i].GetComponent<MeshRenderer>();
                if (meshRenderer != null && meshRenderer.enabled && filters[i].sharedMesh != null)
                {
                    AddPart(filters[i].sharedMesh, filters[i].transform, kart);
                    parts++;
                }
            }
            SkinnedMeshRenderer[] skinned = source.GetComponentsInChildren<SkinnedMeshRenderer>();
            for (int i = 0; i < skinned.Length; i++)
            {
                if (skinned[i].enabled && skinned[i].sharedMesh != null)
                {
                    AddPart(skinned[i].sharedMesh, skinned[i].transform, kart);
                    parts++;
                }
            }
            if (parts == 0)
            {
                AddBox();
            }
            body.SetActive(false); // invisible until the first fade says otherwise
            return true;
        }

        // One ghost part: same mesh, same place relative to the kart, every sub-mesh with the ghost material.
        private void AddPart(Mesh mesh, Transform part, Transform kart)
        {
            GameObject copy = new GameObject(part.name);
            copy.transform.SetParent(body.transform, false);
            copy.transform.localPosition = kart.InverseTransformPoint(part.position);
            copy.transform.localRotation = Quaternion.Inverse(kart.rotation) * part.rotation;
            copy.transform.localScale = Divide(part.lossyScale, kart.lossyScale);
            copy.AddComponent<MeshFilter>().sharedMesh = mesh;
            SetUpRenderer(copy.AddComponent<MeshRenderer>(), mesh.subMeshCount);
        }

        // Fallback for a kart without meshes: a kart-sized box.
        private void AddBox()
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(box.GetComponent<Collider>()); // right now, before any physics step can see it
            box.name = "GhostBox";
            box.transform.SetParent(body.transform, false);
            box.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            box.transform.localScale = new Vector3(1.1f, 0.5f, 1.7f);
            SetUpRenderer(box.GetComponent<MeshRenderer>(), 1);
        }

        private void SetUpRenderer(MeshRenderer meshRenderer, int subMeshCount)
        {
            Material[] materials = new Material[subMeshCount < 1 ? 1 : subMeshCount];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = materialInstance;
            }
            meshRenderer.sharedMaterials = materials;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off; // a see-through ghost casting a solid shadow looks wrong
            meshRenderer.receiveShadows = false;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private static Vector3 Divide(Vector3 a, Vector3 b)
        {
            return new Vector3(
                b.x == 0f ? 1f : a.x / b.x,
                b.y == 0f ? 1f : a.y / b.y,
                b.z == 0f ? 1f : a.z / b.z);
        }
    }
}
