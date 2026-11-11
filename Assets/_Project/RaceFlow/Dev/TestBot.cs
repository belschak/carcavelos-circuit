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

        [SerializeField, Tooltip("A bend of this many degrees or more within two look-aheads makes the bot brake down to Corner Speed")]
        private float cornerDegrees = 30f;

        [SerializeField, Range(0.2f, 1f), Tooltip("Share of the kart's top speed the bot takes such a bend with")]
        private float cornerSpeed01 = 0.6f;

        [SerializeField, Tooltip("Every this many seconds of racing the bot checks how far it got along the lap")]
        private float stuckSeconds = 2f;

        [SerializeField, Tooltip("Fewer metres along the lap than this in Stuck Seconds = stuck (wall, tree, pile-up, driving in circles): the bot is put back on the racing line")]
        private float stuckMetres = 6f;

        [SerializeField, Tooltip("A stuck or fallen bot goes to the first racing line point at least this far ahead")]
        private float recoverAheadMetres = 8f;

        [SerializeField, Tooltip("This far below the racing line = fallen off the track")]
        private float fallMetres = 15f;

        private ITrackProgress track;
        private Transform root;
        private IKartState kart;
        private string displayName = "Test Bot";
        private Color mapColour = HudColors.Blue;
        private float checkSeconds;   // time since the last "did I get anywhere?" check
        private float checkDistance;  // distance along the lap at that check, -1 = no check running

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
            KartInput input = Steering(root.position, root.forward, target, fullSteerDegrees);

            // Brake before a bend: compare the road direction here with the one two look-aheads further on.
            float bend = Vector3.Angle(track.DirectionAt(distance), track.DirectionAt(distance + lookAheadMetres * 2f));
            float speed01 = kart != null && kart.MaxSpeed > 0f ? kart.Speed / kart.MaxSpeed : 0f;
            input.Throttle = CornerThrottle(input.Throttle, bend, speed01, cornerDegrees, cornerSpeed01);
            return input;
        }

        // Stuck (against a wall, in a tree, in a pile-up, circling) or fallen off: back onto the racing line a little
        // ahead. "Stuck" is measured as progress along the lap, not as speed: a bot that pushes against a wall at
        // 2 m/s or drives in circles is fast but gets nowhere. A real bot would reverse out; for a dev bot a jump is
        // enough, and it keeps QA races and the Play Mode test running.
        // RaceFlow's own dev bot may call ResetTo on its own kart (Role 3's bots never do: docs/interfaces.md).
        private void Update()
        {
            if (track == null || kart == null || root == null || !kart.InputEnabled)
            {
                checkDistance = -1f; // countdown, pause, results: standing still is correct
                return;
            }
            float distance = track.DistanceAlongLap(root.position);
            if (checkDistance < 0f)
            {
                checkDistance = distance;
                checkSeconds = 0f;
            }
            checkSeconds += Time.deltaTime;
            bool fell = root.position.y < track.PointAt(distance).y - fallMetres;
            bool stuck = false;
            if (checkSeconds >= stuckSeconds)
            {
                stuck = ProgressGain(checkDistance, distance, track.LapLength) < stuckMetres;
                checkDistance = distance;
                checkSeconds = 0f;
            }
            if (!fell && !stuck)
            {
                return;
            }
            int ahead = LinePointAhead(track, distance, recoverAheadMetres);
            // Not onto another kart: if someone stands on that point, take the one after it (two tries).
            for (int tries = 0; tries < 2 && ahead >= 0 && IsOccupied(track.RacingLine[ahead]); tries++)
            {
                ahead = LinePointAhead(track, track.DistanceAlongLap(track.RacingLine[ahead]), recoverAheadMetres);
            }
            if (ahead < 0)
            {
                return;
            }
            Vector3 point = track.RacingLine[ahead];
            Vector3 direction = track.DirectionAt(track.DistanceAlongLap(point) + 1f); // the piece that leaves this point
            kart.ResetTo(point, Quaternion.LookRotation(direction, Vector3.up));
            checkDistance = -1f; // start measuring again from the new spot
            Debug.Log("TestBot: " + displayName + (fell ? " fell off" : " was stuck") + ", back on the racing line at point " + ahead + ".");
        }

        // True if another racer is within 3 m of the point (flat distance).
        private bool IsOccupied(Vector3 point)
        {
            for (int i = 0; i < RacerRegistry.Count; i++)
            {
                Transform other = RacerRegistry.All[i].Root;
                if (other == null || other == root)
                {
                    continue;
                }
                Vector3 gap = other.position - point;
                gap.y = 0f;
                if (gap.sqrMagnitude < 9f)
                {
                    return true;
                }
            }
            return false;
        }

        // Plain maths (Edit Mode tested): metres gained along the lap from one distance to a later one. Crossing the
        // start line forwards (1175 -> 5 on a 1180 m lap) is +10, not -1170; backwards it is negative.
        public static float ProgressGain(float fromDistance, float toDistance, float lapLength)
        {
            if (float.IsNaN(fromDistance) || float.IsNaN(toDistance) || float.IsNaN(lapLength) || lapLength <= 0f)
            {
                return 0f;
            }
            float gain = toDistance - fromDistance;
            if (gain < -lapLength * 0.5f)
            {
                gain += lapLength;
            }
            else if (gain > lapLength * 0.5f)
            {
                gain -= lapLength;
            }
            return gain;
        }

        // Plain maths (Edit Mode tested): the racing line point with the smallest gap ahead of `distance` that is at
        // least minAheadMetres (wraps over the start line). Line points are on the road by contract (checkpoints, or
        // Role 2's road points); a spot between two of them might be inside a building. -1 if there is none.
        public static int LinePointAhead(ITrackProgress trackProgress, float distance, float minAheadMetres)
        {
            if (trackProgress == null || trackProgress.RacingLine == null || trackProgress.LapLength <= 0f || float.IsNaN(distance))
            {
                return -1;
            }
            int best = -1;
            float bestGap = float.MaxValue;
            for (int i = 0; i < trackProgress.RacingLine.Count; i++)
            {
                float gap = trackProgress.DistanceAlongLap(trackProgress.RacingLine[i]) - distance;
                if (gap < 0f)
                {
                    gap += trackProgress.LapLength;
                }
                if (gap >= minAheadMetres && gap < bestGap)
                {
                    bestGap = gap;
                    best = i;
                }
            }
            return best;
        }

        // Plain maths (Edit Mode tested): in front of a bend (bendDegrees >= cornerDegrees) brake while faster than
        // cornerSpeed01 of the top speed, otherwise keep the throttle the steering asked for.
        public static float CornerThrottle(float throttle, float bendDegrees, float speed01, float cornerDegrees, float cornerSpeed01)
        {
            if (float.IsNaN(bendDegrees) || float.IsNaN(speed01) || bendDegrees < cornerDegrees)
            {
                return throttle;
            }
            return speed01 > cornerSpeed01 ? -1f : throttle;
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
