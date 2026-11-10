using CarcavelosCircuit.Shared;
using Unity.Cinemachine;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Connects Role 1's Cinemachine chase camera (Driving/ChaseCamera.prefab, task D2) to the race (task Q13).
    // Sits on the Main Camera next to the CinemachineBrain. Role 1 owns how the camera follows (offset, damping,
    // framing in the prefab); this script only does the race's part: aim it at the spawned kart, cut instead of
    // swooping when the kart is teleported (retry, recover), widen the view at speed, and step aside while
    // MapFlyover flies the camera over the map. Same calls as the old placeholder camera had, so RaceBootstrap
    // and MapFlyover kept their logic.
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(CinemachineBrain))]
    public class ChaseCameraRig : MonoBehaviour
    {
        [SerializeField, Tooltip("The ChaseCamera prefab instance in Main.unity (Role 1)")]
        private CinemachineCamera chaseCamera;

        [SerializeField, Tooltip("Extra field of view at top speed, in degrees, so going fast feels fast. 0 = off")]
        private float speedFieldOfViewBoost = 12f;

        [SerializeField, Tooltip("Higher = the field of view follows the speed faster")]
        private float fieldOfViewSharpness = 4f;

        private CinemachineBrain brain;
        private Transform target;
        private IKartState kart;
        private float baseFieldOfView;

        private void Awake()
        {
            brain = GetComponent<CinemachineBrain>();
            if (chaseCamera == null)
            {
                Debug.LogError("ChaseCameraRig: no CinemachineCamera set. Rebuild Main with Carcavelos Circuit > Build Role 4 Scenes.");
                return;
            }
            // Cinemachine drives the Main Camera's lens, clip planes included. The campus world has sea, backdrop and
            // fort beyond 1000 m, so the camera's own near/far planes (set by the scene builder) win.
            Camera cam = GetComponent<Camera>();
            chaseCamera.Lens.NearClipPlane = cam.nearClipPlane;
            chaseCamera.Lens.FarClipPlane = cam.farClipPlane;
            baseFieldOfView = chaseCamera.Lens.FieldOfView;
            // A track scene could bring its own Cinemachine camera; ours must be the live one.
            chaseCamera.Priority = 100;
        }

        // MapFlyover switches this component off while it flies: then the brain stops moving the camera too.
        private void OnEnable()
        {
            if (brain != null) brain.enabled = true;
        }

        private void OnDisable()
        {
            if (brain != null) brain.enabled = false;
        }

        // RaceBootstrap calls this after it spawned the kart.
        public void SetTarget(Transform kartTransform)
        {
            target = kartTransform;
            kart = kartTransform.GetComponent<IKartState>();
            if (chaseCamera == null) return;
            chaseCamera.Follow = kartTransform; // LookAt uses the same target (the prefab's Rotation Composer)
            SnapBehindTarget();
        }

        // Retry, recover, end of the fly-in: jump straight behind the kart instead of flying across the track.
        // An invalid previous state makes Cinemachine skip damping for one frame, whatever the teleport distance.
        public void SnapBehindTarget()
        {
            if (chaseCamera == null || target == null) return;
            chaseCamera.PreviousStateIsValid = false;
        }

        // Where the camera rests behind the kart: the end of MapFlyover's glide. False before the kart exists.
        // Mirrors the prefab: CinemachineFollow offset in the kart's yaw frame (binding "lock to target with world
        // up"), the Rotation Composer looking at the kart plus its target offset.
        public bool TryGetRestPose(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;
            if (target == null || chaseCamera == null) return false;

            Vector3 offset = new Vector3(0f, 3f, -6f);
            CinemachineFollow follow = chaseCamera.GetComponent<CinemachineFollow>();
            if (follow != null) offset = follow.FollowOffset;
            Vector3 lookOffset = Vector3.up;
            CinemachineRotationComposer composer = chaseCamera.GetComponent<CinemachineRotationComposer>();
            if (composer != null) lookOffset = composer.TargetOffset;

            Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            Quaternion yaw = flatForward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flatForward.normalized, Vector3.up) : Quaternion.identity;
            position = target.position + yaw * offset;
            rotation = Quaternion.LookRotation(target.position + yaw * lookOffset - position, Vector3.up);
            return true;
        }

        private void LateUpdate()
        {
            if (chaseCamera == null || kart == null || kart.MaxSpeed <= 0f || speedFieldOfViewBoost <= 0f) return;
            // Framerate-independent smoothing: the same feel at 30 and at 144 frames per second.
            float t = 1f - Mathf.Exp(-fieldOfViewSharpness * Time.deltaTime);
            float speed01 = Mathf.Clamp01(Mathf.Abs(kart.Speed) / kart.MaxSpeed);
            float wanted = baseFieldOfView + speedFieldOfViewBoost * speed01;
            chaseCamera.Lens.FieldOfView = Mathf.Lerp(chaseCamera.Lens.FieldOfView, wanted, t);
        }
    }
}
