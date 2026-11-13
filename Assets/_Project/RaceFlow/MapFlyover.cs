using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The camera's big moments (David's must-have): on the start screen it circles high above the whole campus,
    // when the countdown starts it glides down behind the kart, and after the finish it rises back up over the map
    // behind the results card. While driving the chase camera has the camera; this script only switches it off
    // while it flies. Works on any track: everything is measured from the track's checkpoints.
    [RequireComponent(typeof(Camera))]
    public class MapFlyover : MonoBehaviour
    {
        private enum Mode { Orbit, FlyIn, WaitForOutro, Outro, Chase }

        [SerializeField, Tooltip("Reads the race state and the track's checkpoints")]
        private RaceManager raceManager;

        [SerializeField, Tooltip("Gets the camera back after the fly-in (Role 1's Cinemachine camera via ChaseCameraRig)")]
        private ChaseCameraRig chaseCamera;

        [SerializeField, Tooltip("How fast the camera circles over the map, in degrees per second")]
        private float orbitDegreesPerSecond = 6f;

        [SerializeField, Tooltip("Seconds for the glide from the map down behind the kart (a bit shorter than the 3-2-1 countdown)")]
        private float flyInSeconds = 2.7f;

        [SerializeField, Tooltip("Real seconds after the finish before the camera rises (lets the slow motion play first)")]
        private float outroDelaySeconds = 1.6f;

        [SerializeField, Tooltip("Seconds for the rise from behind the kart up over the map")]
        private float outroSeconds = 3f;

        [SerializeField, Tooltip("Lowest height above the track for the overview, in metres (small tracks)")]
        private float minHeight = 35f;

        [SerializeField, Tooltip("Extra height in the middle of a glide, so the camera does not skim through buildings")]
        private float humpMetres = 12f;

        private Mode mode = Mode.Chase;
        private bool hasState;
        private RaceState lastState;
        private Vector3 centre;
        private float radius;
        private float angle = 225f; // start from the south-west, looking across the campus
        private float progress;     // 0..1 of the current glide
        private float waitUntil;
        private Vector3 fromPosition;
        private Quaternion fromRotation;

        // True while the camera floats high over the map (start screen orbit, rise after the finish); RaceCameraLook
        // softens the far background then, like a camera lens. False during the glide down, so it clears in time.
        public bool IsOverview { get { return mode == Mode.Orbit || mode == Mode.Outro; } }

        private void LateUpdate()
        {
            if (raceManager == null || !raceManager.IsSetUp)
            {
                return;
            }
            if (!hasState)
            {
                FlyoverPath.Measure(raceManager.TrackPoints, out centre, out radius);
                hasState = true;
                lastState = raceManager.State;
                OnStateEntered(lastState, lastState);
            }
            else if (raceManager.State != lastState)
            {
                RaceState before = lastState;
                lastState = raceManager.State;
                OnStateEntered(lastState, before);
            }

            if (mode == Mode.Orbit)
            {
                if (!raceManager.ReducedMotion)
                {
                    angle += orbitDegreesPerSecond * Time.unscaledDeltaTime; // real time: also turns while paused
                }
                LookFrom(FlyoverPath.OrbitPosition(centre, radius, angle, minHeight));
            }
            else if (mode == Mode.FlyIn)
            {
                FlyIn();
            }
            else if (mode == Mode.WaitForOutro && Time.unscaledTime >= waitUntil)
            {
                StartGlide(Mode.Outro);
            }
            else if (mode == Mode.Outro)
            {
                Outro();
            }
        }

        // What the camera does when the race enters a new state.
        private void OnStateEntered(RaceState state, RaceState before)
        {
            if (state == RaceState.Ready)
            {
                TakeCamera(Mode.Orbit);
            }
            else if (state == RaceState.Countdown && before == RaceState.Ready && !raceManager.ReducedMotion)
            {
                StartGlide(Mode.FlyIn); // the first start: glide down while the countdown runs
            }
            else if (state == RaceState.Results)
            {
                waitUntil = Time.unscaledTime + (raceManager.ReducedMotion ? 0f : outroDelaySeconds);
                mode = Mode.WaitForOutro; // the chase camera keeps the kart in view until then
            }
            else
            {
                HandBack(); // retry, GO, reduced motion: straight behind the kart
            }
        }

        private void StartGlide(Mode glide)
        {
            TakeCamera(glide);
            fromPosition = transform.position;
            fromRotation = transform.rotation;
            progress = 0f;
        }

        private void FlyIn()
        {
            Vector3 restPosition;
            Quaternion restRotation;
            if (chaseCamera == null || !chaseCamera.TryGetRestPose(out restPosition, out restRotation))
            {
                HandBack();
                return;
            }
            progress += Time.deltaTime / Mathf.Max(0.01f, flyInSeconds); // game time: in step with the countdown
            transform.position = FlyoverPath.Glide(fromPosition, restPosition, progress, humpMetres);
            transform.rotation = Quaternion.Slerp(fromRotation, restRotation, FlyoverPath.Ease(progress));
            if (progress >= 1f)
            {
                HandBack();
            }
        }

        private void Outro()
        {
            if (raceManager.ReducedMotion)
            {
                mode = Mode.Orbit; // a cut instead of a flight
                return;
            }
            progress += Time.unscaledDeltaTime / Mathf.Max(0.01f, outroSeconds); // real time: ignores the slow motion
            angle += orbitDegreesPerSecond * Time.unscaledDeltaTime;
            Vector3 target = FlyoverPath.OrbitPosition(centre, radius, angle, minHeight);
            transform.position = FlyoverPath.Glide(fromPosition, target, progress, 0f);
            Quaternion look = Quaternion.LookRotation(centre - transform.position);
            transform.rotation = Quaternion.Slerp(fromRotation, look, FlyoverPath.Ease(progress));
            if (progress >= 1f)
            {
                mode = Mode.Orbit;
            }
        }

        private void LookFrom(Vector3 position)
        {
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(centre - position);
        }

        // The fly-over moves the camera; the chase camera waits.
        private void TakeCamera(Mode newMode)
        {
            mode = newMode;
            if (chaseCamera != null)
            {
                chaseCamera.enabled = false;
            }
        }

        // The chase camera takes over again, right behind the kart.
        private void HandBack()
        {
            mode = Mode.Chase;
            if (chaseCamera != null && !chaseCamera.enabled)
            {
                chaseCamera.enabled = true;
                chaseCamera.SnapBehindTarget();
            }
        }
    }
}
