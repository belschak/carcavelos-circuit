using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Task V10: one bot. It sits on a normal Kart (Role 1's Kart.prefab) and is that kart's "keyboard":
    // it sets itself as IKartDrivable.InputSource, and the kart asks ReadInput() once per frame instead of the keys.
    // So a bot drives with exactly the player's physics and KartTuning; it can be rammed like any kart.
    // It is an IRacer (name tag, minimap dot, positions) and registers itself while enabled.
    // RaceFlow (RaceRoster) puts it on the grid, switches its input on at GO and off at the finish or on pause, and
    // puts it back on Retry. The decisions themselves are plain maths in BotRules / BotStuckCheck / BotTrackMap.
    public class BotDriver : MonoBehaviour, IKartInputSource, IRacer
    {
        private Transform root;
        private IKartState kart;
        private Rigidbody body;
        private BotProfile profile;
        private BotSettings settings;
        private BotTrackMap map;
        private BotStuckCheck stuck;

        private int lineIndex = -1;      // nearest racing line point (BotTrackMap), -1 = find it again
        private float lane;              // metres right (+) / left (-) of the racing line it steers for now
        private float steer;             // smoothed steering, -1 .. +1
        private KartInput current = KartInput.None; // what ReadInput hands the kart this frame
        private bool wasDrifting;        // last frame's kart.IsDrifting, to count drifts

        // Set every frame by BotSquad from RaceFlow's standings: > 0 = this bot is ahead of the player (metres).
        public float MetresAheadOfPlayer { get; set; }
        public BotProfile Profile { get { return profile; } }
        public int PutBackCount { get; private set; } // how often it had to be put back on the line (Play Mode test)
        public int ReverseCount { get; private set; } // how often it backed out of a stuck spot
        public int DriftCount { get; private set; }   // drifts started (Play Mode test log)

        public string DisplayName { get { return profile != null ? profile.displayName : "Bot"; } }
        public Transform Root { get { return root; } }
        public IKartState Kart { get { return kart; } }
        public bool IsPlayer { get { return false; } }
        public Color MapColor { get { return profile != null ? profile.colour : Color.white; } }

        private void Awake()
        {
            root = transform;
            kart = GetComponent<IKartState>();
            body = GetComponent<Rigidbody>();
            IKartDrivable drivable = GetComponent<IKartDrivable>();
            if (drivable != null)
            {
                drivable.InputSource = this; // from now on the kart asks us instead of the keyboard
            }
            else
            {
                Debug.LogWarning("BotDriver: this kart has no IKartDrivable (Role 1's KartController), so the bot cannot drive it.", this);
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

        // Called by BotSquad right after spawning.
        public void SetUp(BotProfile botProfile, BotSettings botSettings, BotTrackMap trackMap)
        {
            profile = botProfile;
            settings = botSettings;
            map = trackMap;
            stuck = new BotStuckCheck(settings.StuckSeconds, 1.5f, 6f, settings.ReverseSeconds, settings.MaxReverseTries);
            lane = profile.lane;
        }

        // IKartInputSource: the kart calls this once per frame while RaceFlow lets it drive.
        public KartInput ReadInput()
        {
            return current;
        }

        // Decide this frame's input. Runs every frame; while the input is off (grid, countdown, pause, finish) the bot
        // only forgets its plan, so it starts fresh after GO or Retry (RaceFlow may have moved it).
        private void Update()
        {
            if (map == null || !map.IsValid || kart == null || settings == null)
            {
                current = KartInput.None;
                return;
            }
            if (!kart.InputEnabled)
            {
                current = KartInput.None;
                lineIndex = -1;
                stuck.Reset();
                return;
            }

            float dt = Time.deltaTime;
            Vector3 position = root.position;
            if (kart.IsDrifting && !wasDrifting)
            {
                DriftCount++;
            }
            wasDrifting = kart.IsDrifting;
            if (lineIndex < 0)
            {
                // Fresh start (GO, Retry, unpause): steer from where the kart stands, then move over to its lane
                // slowly, instead of cutting across the grid towards it.
                lineIndex = map.NearestAnywhere(position);
                lane = map.Lateral(position, lineIndex);
            }
            else
            {
                lineIndex = map.NearestNear(position, lineIndex, 4, 12);
            }
            float speed = kart.Speed;

            // Fell off the world (sea, under the road): no point backing up.
            if (position.y < map.Point(lineIndex).y - settings.FallMetres)
            {
                PutBackOnTheLine("fell off");
                return;
            }
            bool wasReversing = stuck.IsReversing;
            BotRecovery recovery = stuck.Update(speed, dt);
            if (recovery == BotRecovery.PutBack)
            {
                PutBackOnTheLine("was stuck");
                return;
            }
            if (recovery == BotRecovery.Reverse && !wasReversing)
            {
                ReverseCount++;
                Debug.Log("BotDriver: " + DisplayName + " is stuck at " + Metres(lineIndex) + ", backing out (try " + stuck.Tries + ").");
            }

            Vector3 target = AimPoint(speed, dt);
            float wanted = BotRules.SteerTowards(position, Heading(), target, speed,
                settings.TurnDegreesPerSecond, settings.FullSteerSpeed);
            if (recovery == BotRecovery.Reverse)
            {
                // Backing up turns the kart the other way round (like a real car), so steer the opposite way:
                // the nose swings towards the racing line while we back away from the wall.
                steer = -wanted;
                current = new KartInput(-1f, steer, false);
                return;
            }
            steer = Mathf.MoveTowards(steer, wanted, dt / Mathf.Max(0.01f, settings.SteerSeconds));

            float top = kart.MaxSpeed * BotRules.PaceShare(profile.pace,
                BotRules.RubberBand(MetresAheadOfPlayer, settings.RubberBandMetres, settings.RubberBandBoost, settings.RubberBandSlow));
            float allowed = map.AllowedSpeed(lineIndex, top, profile.cornering, settings.TurnDegreesPerSecond,
                settings.MinCornerSpeed, settings.PlanBraking, settings.PlanMetres);
            float throttle = BotRules.Throttle(speed, allowed, settings.BrakeMargin);

            float bend = map.BendAhead(lineIndex, settings.DriftLookMetres);
            float speed01 = kart.MaxSpeed > 0f ? speed / kart.MaxSpeed : 0f;
            bool driftBend = profile.drifts && map.IsDriftBend(lineIndex, settings.DriftLookMetres, settings.DriftMinBendDegrees,
                settings.DriftMaxBendDegrees, settings.DriftMinRadius, settings.DriftMaxClimb);
            bool drift = BotRules.WantsDrift(profile.drifts, driftBend, bend, speed01, kart.IsGrounded, kart.IsDrifting,
                settings.DriftMinBendDegrees, settings.DriftMinSpeed01);
            float steerOut = steer;
            if (drift && !kart.IsDrifting)
            {
                // A drift only starts with clear steering into the bend (Role 1's drift rule).
                steerOut = Mathf.Sign(bend) * Mathf.Max(Mathf.Abs(steer), 0.6f);
            }
            current = new KartInput(throttle, steerOut, drift).Clamped();
        }

        // The point on the racing line the bot aims for: further ahead the faster it goes, moved sideways to its lane.
        private Vector3 AimPoint(float speed, float dt)
        {
            float wantedLane = Mathf.Clamp(profile.lane, -settings.MaxLaneMetres, settings.MaxLaneMetres);
            wantedLane = LaneAroundTraffic(wantedLane);
            lane = Mathf.MoveTowards(lane, wantedLane, settings.LaneChangeSpeed * dt);

            float look = settings.LookAheadMetres + settings.LookAheadSeconds * Mathf.Max(0f, speed);
            int aim = lineIndex + Mathf.Max(1, Mathf.RoundToInt(look / map.Step));
            return map.Point(aim) + map.Right(aim) * lane;
        }

        // Traffic, checked against every racer (the player too): a kart close ahead in my lane = move beside it
        // (BotRules.LaneToPass); a kart right beside me = stay on my side of it (BotRules.LaneBeside).
        private float LaneAroundTraffic(float wantedLane)
        {
            Vector3 forward = map.Forward(lineIndex);
            float myLane = map.Lateral(root.position, lineIndex);
            for (int i = 0; i < RacerRegistry.Count; i++)
            {
                Transform other = RacerRegistry.All[i].Root;
                if (other == null || other == root)
                {
                    continue;
                }
                Vector3 gap = other.position - root.position;
                gap.y = 0f;
                float ahead = Vector3.Dot(gap, forward);
                float otherLane = map.Lateral(other.position, lineIndex);
                if (ahead > 2.5f && ahead < settings.PassLookMetres)
                {
                    wantedLane = BotRules.LaneToPass(wantedLane, otherLane, settings.PassSideMetres, settings.MaxLaneMetres);
                }
                else if (ahead >= -2.5f && ahead <= 2.5f && Mathf.Abs(otherLane - myLane) < 2f * settings.PassSideMetres)
                {
                    wantedLane = BotRules.LaneBeside(wantedLane, myLane, otherLane, settings.PassSideMetres);
                }
            }
            return Mathf.Clamp(wantedLane, -settings.MaxLaneMetres - 1f, settings.MaxLaneMetres + 1f);
        }

        // The direction the kart really MOVES: in a corner it slides a little outwards, and steering by the nose
        // alone would run wide. Slow or backing up: the nose.
        private Vector3 Heading()
        {
            if (body != null)
            {
                Vector3 moving = body.linearVelocity;
                moving.y = 0f;
                if (moving.magnitude > 3f && Vector3.Dot(moving, root.forward) > 0f)
                {
                    return moving.normalized;
                }
            }
            return root.forward;
        }

        // Last resort, like the player's Recover key: back onto the racing line a few metres ahead, facing the
        // driving direction. RaceFlow's own TestBot does the same; RaceFlow still owns the grid, GO and Retry.
        private void PutBackOnTheLine(string why)
        {
            Debug.Log("BotDriver: " + DisplayName + " " + why + " at " + Metres(lineIndex) + ", back on the racing line.");
            int spot = lineIndex + Mathf.RoundToInt(6f / map.Step);
            for (int tries = 0; tries < 3 && IsTaken(map.Point(spot)); tries++)
            {
                spot += Mathf.RoundToInt(4f / map.Step); // someone stands there: a bit further on
            }
            kart.ResetTo(map.Point(spot), Quaternion.LookRotation(map.Forward(spot), Vector3.up));
            PutBackCount++;
            lineIndex = map.Wrap(spot);
            lane = 0f;
            steer = 0f;
            stuck.Reset();
            current = KartInput.None;
        }

        // "412 m" along the lap, for the Console.
        private string Metres(int index)
        {
            return Mathf.RoundToInt(map.Wrap(index) * map.Step) + " m";
        }

        // True if another racer is within 3 m of the point (flat distance).
        private bool IsTaken(Vector3 point)
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
    }
}
