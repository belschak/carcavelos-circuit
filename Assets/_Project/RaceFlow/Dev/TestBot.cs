using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // DEV ONLY (task R8): the simplest possible bot, so the race flow, positions and minimap can be tested and QA'd
    // before Role 3's real bots (task V10) exist. NOT a game feature: RaceSettings > Dev Test Bots = 0 in the game.
    // It drives a kart through IKartDrivable (today only RaceFlow's PlaceholderKart; Role 1's KartController after D8)
    // by aiming at a point a few metres ahead on the racing line: ITrackProgress.PointAt(distance + look-ahead).
    // It is an IRacer like any bot, so RaceRoster puts it on the grid, switches its input and resets it on Retry.
    public class TestBot : MonoBehaviour, IKartInputSource, IRacer
    {
        [SerializeField, Tooltip("Metres ahead on the racing line the bot aims at. More = smoother but cuts corners")]
        private float lookAheadMetres = 9f;

        [SerializeField, Tooltip("Angle to the aim point at which the bot steers fully")]
        private float fullSteerDegrees = 30f;

        private ITrackProgress track;
        private Transform root;
        private IKartState kart;
        private string displayName = "Test Bot";
        private Color mapColour = HudColors.Blue;

        public string DisplayName { get { return displayName; } }
        public Transform Root { get { return root; } }
        public IKartState Kart { get { return kart; } }
        public bool IsPlayer { get { return false; } }
        public Color MapColor { get { return mapColour; } }

        private void Awake()
        {
            root = transform;
            kart = GetComponent<IKartState>();
            IKartDrivable drivable = GetComponent<IKartDrivable>();
            if (drivable != null)
            {
                drivable.InputSource = this; // from now on the kart asks us instead of the keyboard
            }
            else
            {
                Debug.LogWarning("TestBot: this kart has no IKartDrivable, so the test bot cannot drive it.", this);
            }
        }

        private void OnEnable()
        {
            RacerRegistry.Register(this);
        }

        private void OnDisable()
        {
            RacerRegistry.Unregister(this);
        }

        // Called right after spawning (RaceRoster).
        public void SetUp(ITrackProgress trackProgress, string botName, Color colour)
        {
            track = trackProgress;
            displayName = botName;
            mapColour = colour;
        }

        // IKartInputSource: the kart calls this once per frame while its input is on.
        public KartInput ReadInput()
        {
            if (track == null || root == null)
            {
                return KartInput.None;
            }
            float distance = track.DistanceAlongLap(root.position);
            Vector3 target = track.PointAt(distance + lookAheadMetres);
            return Steering(root.position, root.forward, target, fullSteerDegrees);
        }

        // Plain maths (Edit Mode tested): steer towards the target, full steer at fullSteerDegrees or more,
        // and slow down for sharp turns so the bot does not fly off in every corner.
        public static KartInput Steering(Vector3 position, Vector3 forward, Vector3 target, float fullSteerDegrees)
        {
            Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
            Vector3 toTarget = new Vector3(target.x - position.x, 0f, target.z - position.z);
            if (flatForward.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f || float.IsNaN(toTarget.x) || float.IsNaN(toTarget.z))
            {
                return KartInput.None;
            }
            float angle = Vector3.SignedAngle(flatForward, toTarget, Vector3.up); // + = target is to the right
            float steer = Mathf.Clamp(angle / Mathf.Max(1f, fullSteerDegrees), -1f, 1f);
            float sharpness = Mathf.Abs(angle);
            float throttle = 1f;
            if (sharpness > 90f)
            {
                throttle = 0.25f; // behind us: turn round slowly
            }
            else if (sharpness > 45f)
            {
                throttle = 0.6f;
            }
            return new KartInput(throttle, steer, false).Clamped();
        }
    }
}
