using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // STAND-IN chase camera for Main.unity until Alessandro's Cinemachine camera (Role 1, task D2) exists.
    // Follows behind the kart and widens the field of view at speed so going fast feels fast.
    [RequireComponent(typeof(Camera))]
    public class PlaceholderChaseCamera : MonoBehaviour
    {
        [SerializeField, Tooltip("Metres behind the kart")]
        private float distance = 6f;

        [SerializeField, Tooltip("Metres above the kart")]
        private float height = 2.5f;

        [SerializeField, Tooltip("Higher = camera catches up faster")]
        private float followSharpness = 6f;

        [SerializeField, Tooltip("Field of view standing still and at top speed")]
        private float minFieldOfView = 65f;

        [SerializeField]
        private float maxFieldOfView = 80f;

        private Transform target;
        private IKartState kart;
        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        // RaceBootstrap calls this after it spawned the kart.
        public void SetTarget(Transform kartTransform)
        {
            target = kartTransform;
            kart = kartTransform.GetComponent<IKartState>();
            SnapBehindTarget();
        }

        // Jump straight behind the kart (used at spawn and on retry, so the camera does not swoop across the map).
        public void SnapBehindTarget()
        {
            if (target == null) return;
            transform.position = DesiredPosition();
            transform.LookAt(target.position + Vector3.up);
        }

        // Where the camera rests behind the kart: the end of MapFlyover's glide. False before the kart exists.
        public bool TryGetRestPose(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;
            if (target == null) return false;
            position = DesiredPosition();
            rotation = Quaternion.LookRotation(target.position + Vector3.up - position);
            return true;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Framerate-independent smoothing: the same feel at 30 and at 144 frames per second.
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            Vector3 desired = DesiredPosition();
            if ((desired - transform.position).sqrMagnitude > 400f)
            {
                transform.position = desired; // more than 20 m away = the kart was teleported
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, desired, t);
            }
            transform.LookAt(target.position + Vector3.up);

            if (kart != null && kart.MaxSpeed > 0f)
            {
                float speed01 = Mathf.Clamp01(Mathf.Abs(kart.Speed) / kart.MaxSpeed);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Mathf.Lerp(minFieldOfView, maxFieldOfView, speed01), t);
            }
        }

        private Vector3 DesiredPosition()
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
            return target.position - flatForward * distance + Vector3.up * height;
        }
    }
}
