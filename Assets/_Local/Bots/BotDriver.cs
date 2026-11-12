using CarcavelosCircuit.Driving;
using CarcavelosCircuit.Track;
using UnityEngine;

namespace CarcavelosCircuit.Bots
{
    // LOCAL PROTOTYPE (bots). Drives one kart like a player would: it only presses gas/brake, steer and drift
    // (KartController.SetInput), so a bot has exactly the player's physics. No machine learning, four simple rules:
    // 1. STEER (pure pursuit): aim at a point on the racing line a bit ahead; the faster the kart, the further ahead.
    // 2. SPEED: look at the corners ahead; each corner has a safe speed; brake early enough to reach it.
    // 3. FAIR RACING: a kart ahead in my cone -> change lane to the free side, or slow down if there is no room.
    //    A kart next to me -> stay on my side. The aim point is always on the road, never another kart.
    // 4. RECOVERY: stuck, wrong way or off the road for 2 s -> back to the respawn point of the last checkpoint.
    public class BotDriver : MonoBehaviour
    {
        [Header("Steering")]
        [Tooltip("Aim this many metres ahead when standing still")]
        public float lookAheadBase = 3.5f;
        [Tooltip("Extra metres of look-ahead per m/s of speed")]
        public float lookAheadPerSpeed = 0.27f;
        [Tooltip("Seconds from straight to full steering lock (smooth hands)")]
        public float steerSeconds = 0.12f;

        [Header("Speed")]
        [Tooltip("Braking the bot plans with, in m/s per second (the kart can do more, so there is a reserve)")]
        public float planBraking = 14f;
        [Tooltip("How far ahead corners are checked, in metres")]
        public float planDistance = 70f;
        [Tooltip("Upper limit for a bot's corner speed factor, whatever its skill, difficulty and form add up to")]
        public float maxCornering = 0.97f;
        [Tooltip("Never plan a corner slower than this (m/s)")]
        public float minCornerSpeed = 7f;
        [Tooltip("Brake (instead of only lifting off the gas) when this much too fast, in m/s")]
        public float brakeMargin = 2f;

        [Header("Lanes and other karts")]
        [Tooltip("Metres kept free between the kart's centre and the road edge")]
        public float edgeMargin = 1.6f;
        [Tooltip("Sideways speed of a lane change, in m/s")]
        public float laneChangeSpeed = 2.5f;
        [Tooltip("Sideways distance wanted between two kart centres, in metres")]
        public float clearance = 1.6f;
        [Tooltip("Half opening angle of the 'kart ahead' cone, in degrees")]
        public float coneDegrees = 30f;
        [Tooltip("Karts this close in front/behind count as 'next to me', in metres")]
        public float alongsideMetres = 2.5f;

        [Tooltip("Seconds the bot looks ahead when it checks that a lane stays free")]
        public float predictSeconds = 0.9f;
        [Tooltip("A started pass keeps its side at least this long, in seconds")]
        public float minCommitSeconds = 0.7f;
        [Tooltip("No new pass when a slow corner or bump is closer than this, in metres (the kart ahead only brakes first there)")]
        public float noPassBeforeCornerMetres = 50f;
        [Tooltip("A kart ahead that drives slower than this (m/s) is treated as an obstacle: the bot may always go round it")]
        public float obstacleSpeed = 5f;
        [Tooltip("Slowest speed (m/s) behind a standing kart: the bot creeps instead of stopping, so it can steer round")]
        public float creepSpeed = 2.5f;
        [Tooltip("A started pass is given up when the other kart is this far ahead again and faster, in metres")]
        public float giveUpGapMetres = 10f;
        [Tooltip("... or after this many seconds without getting past")]
        public float giveUpSeconds = 8f;
        [Tooltip("Speed (m/s) at which the bot goes past such an obstacle when it is close to its lane")]
        public float obstaclePassSpeed = 12f;
        [Tooltip("Slipstream: extra cruise speed while close behind a kart in the same lane (0.03 = +3 %, never above the shared top speed)")]
        public float slipstreamBonus = 0.03f;
        [Tooltip("Slipstream works up to this many seconds of driving behind the other kart")]
        public float slipstreamSeconds = 0.7f;
        [Tooltip("Seconds in the slipstream until it gives its full bonus (it builds up, like a real tow)")]
        public float towChargeSeconds = 1.2f;
        [Tooltip("Slingshot: seconds the slipstream bonus takes to fade after pulling out of the tow")]
        public float slingshotSeconds = 1.5f;
        [Tooltip("Tow: while waiting for a pass on a straight, follow this close (seconds of driving) ...")]
        public float towFollowSeconds = 0.25f;
        [Tooltip("... plus this many metres")]
        public float towBuffer = 1.5f;
        [Tooltip("A pass is only started if it can be finished (other kart fully behind) within this many seconds")]
        public float maxPassSeconds = 5f;
        [Tooltip("Slingshot: pulling out of a built-up tow to pass gives this much extra top speed (0.08 = +8 %) ...")]
        public float slingshotBoost = 0.08f;
        [Tooltip("... for at most this many seconds (ends when the pass is done or given up)")]
        public float slingshotPassSeconds = 5f;
        [Tooltip("Share of a full tow (0..1) needed before pulling out gives the slingshot")]
        public float slingshotMinCharge = 0.5f;
        [Tooltip("A kart that is passing me and up to this many metres behind: I do not move over to its side")]
        public float beingPassedMetres = 12f;
        [Tooltip("Length of a kart in metres")]
        public float kartLength = 2f;
        [Tooltip("Extra metres the passed kart must be behind before the bot moves back")]
        public float mergeBehindMetres = 2f;
        [Tooltip("Following distance: this many seconds of driving ...")]
        public float followSeconds = 0.5f;
        [Tooltip("... plus this many metres")]
        public float followBuffer = 2.5f;

        [Header("Mistakes")]
        [Tooltip("Share of the mistakes that are big ones (about a second off the gas)")]
        public float bigMistakeShare = 0.2f;
        [Tooltip("Seconds after a mistake before the next one is possible")]
        public float mistakeCooldownSeconds = 15f;
        [Tooltip("No mistakes in the first seconds of the race (the start is crowded)")]
        public float startWithoutMistakesSeconds = 15f;

        [Header("Recovery")]
        [Tooltip("Seconds stuck, wrong way or off the road before the bot is put back")]
        public float recoverSeconds = 2f;
        [Tooltip("Slower than this (m/s) counts as stuck")]
        public float stuckSpeed = 1.5f;
        [Tooltip("Metres beyond the road edge that count as off the road")]
        public float offRoadMetres = 2.5f;

        public BotProfile Profile { get { return profile; } }

        // Test switch (-botBlind): bots ignore each other and all drive the middle lane, to provoke many bumps.
        public static bool Blind { get; set; }
        public static float BlindLane { get; set; }     // the lane (m) blind bots drive in

        // Test switch (-botDrift): every bot uses the drift rule, whatever its profile says.
        public static bool AllDrift { get; set; }

        // Test switch (-botNoSlipstream): no slipstream bonus for anyone (to compare the field with and without it).
        public static bool NoSlipstream { get; set; }

        private KartController kart;
        private Rigidbody body;
        private RaceKart racer;
        private BotRace race;
        private BotRoute route;
        private KartTuning tuning;
        private TrackInfo trackInfo;
        private BotProfile profile;

        // Across the road. ALL tactics (passes, free-lane checks, karts beside me) measure in metres from the
        // CENTRE LINE, the same as RaceKart.Lateral. Only free driving is relative to the racing line, because the
        // racing line moves across the road in corners and a free bot should follow it.
        private float offsetLane;      // steering: metres from the racing line at the aim point, moves at laneChangeSpeed
        private float ownOffset;       // free driving: favourite lane + wander + mistake, metres from the racing line
        private float trafficLane = float.NaN; // traffic: metres from the centre line it must hold (NaN = free driving)
        private float followSpeed;     // speed limit because of a kart ahead, decided with the tactics (very big = none)
        private float stepFollowSpeed = float.MaxValue; // the same limit, checked every physics step (safety)
        private float freeSpeed;       // the speed the bot would drive without traffic (track, corners, skill, bonuses)
        private float decisionTimer;
        private float steer;
        private RaceKart passing;      // the kart this bot is committed to pass (null = not passing)
        private bool passOnRight;
        private bool inSlipstream;     // close behind a kart in my lane on a straight (decided with the tactics)
        private float towCharge;       // 0..1: how much slipstream the bot has built up; fades after pulling out (slingshot)
        private RaceKart towTarget;    // the kart the bot closes up behind because it wants to pass it (null = none)
        private float slingshotLeft;   // seconds of slingshot boost left (0 = none)
        private bool slowingForCorner; // true while a corner or bump close ahead will limit the speed
        private float passSeconds;     // how long the bot has been committed
        private float driftSign;       // +1 / -1 while the bot holds the drift key, 0 = not drifting
        private float stuckTime;
        private float wrongWayTime;
        private float offRoadTime;
        private float noiseSeed;
        private System.Random dice;        // this bot's own random numbers (same seed = same race again)
        private bool cornerWasAhead;
        private float mistakeCooldown;     // seconds until the next mistake is possible
        private int lastMistakeLap = -99;
        private float wideMetres;          // mistake "run wide": extra metres towards the outside of this bend (signed)
        private float liftSeconds;         // mistake "lift": seconds off the gas left

        public void SetUp(BotRace botRace, BotRoute raceRoute, KartTuning kartTuning, TrackInfo track, BotProfile botProfile)
        {
            race = botRace;
            route = raceRoute;
            tuning = kartTuning;
            trackInfo = track;
            profile = botProfile;
            kart = GetComponent<KartController>();
            body = GetComponent<Rigidbody>();
            racer = GetComponent<RaceKart>();
            kart.ExternalInput = true; // from now on the keyboard does not drive this kart
            dice = new System.Random(race.Seed * 7919 + racer.Number * 104729); // far-apart seeds, so the bots do not roll alike
            noiseSeed = (float)dice.NextDouble() * 100f;
            ResetForStart();
        }

        public void ResetForStart()
        {
            float lineHere = racer != null && route != null ? route.LineOffset(racer.RouteIndex) : 0f;
            offsetLane = racer != null ? racer.Lateral - lineHere : 0f;
            ownOffset = offsetLane;
            trafficLane = float.NaN;
            followSpeed = float.MaxValue;
            stepFollowSpeed = float.MaxValue;
            towCharge = 0f;
            towTarget = null;
            slingshotLeft = 0f;
            passing = null;
            wideMetres = 0f;
            liftSeconds = 0f;
            mistakeCooldown = startWithoutMistakesSeconds;
            SecondsSinceRecovery = 1000f;
            lastMistakeLap = -99;
            decisionTimer = 0.02f * (racer != null ? racer.Number : 0); // staggered: the bots do not all think in the same step
            steer = 0f;
            driftSign = 0f;
            stuckTime = 0f;
            wrongWayTime = 0f;
            offRoadTime = 0f;
        }

        // Called every physics step by BotRace. rubber = rubber-band correction of the cruise speed (0 = none, 0.03 = +3 %).
        public void Tick(float dt, float rubber)
        {
            if (!kart.InputEnabled)
            {
                kart.SetInput(0f, 0f, false); // countdown or results: hands off
                return;
            }
            if (NeedsRecovery(dt))
            {
                TryRespawn();
                return;
            }

            float speed = Mathf.Max(0f, kart.Speed);
            int index = racer.RouteIndex;
            float halfRoad = HalfRoad(index);

            // Think only every 'reaction' seconds; in between the bot keeps its plan.
            passSeconds += dt;
            SecondsSinceRecovery += dt;
            if (!Probing)
            {
                RollMistake(dt, index);
            }
            decisionTimer -= dt;
            if (decisionTimer <= 0f)
            {
                // Tactics (lanes, passes) are decided every profile.reaction seconds (0.10 s aggressive .. 0.28 s
                // careful), so a careful bot really decides later. Safety does not wait for this: see below.
                decisionTimer = Mathf.Clamp(profile.reaction, 0.05f, 0.3f);
                if (Probing)
                {
                    followSpeed = float.MaxValue;
                    passing = null;
                    trafficLane = float.NaN;
                }
                else
                {
                    Decide(speed, halfRoad);
                }
            }
            // Safety, every physics step for every profile: keep my side of a kart next to me, and never run into
            // the back of a kart in my path.
            if (!Probing && !Blind)
            {
                StayOnMySideOfKartsNextToMe();
                stepFollowSpeed = FollowLimitInMyPath(speed);
            }
            else
            {
                stepFollowSpeed = float.MaxValue;
            }

            // Slipstream builds up over towChargeSeconds behind a kart and fades over slingshotSeconds after leaving it,
            // so a bot that pulls out of the tow keeps its extra speed for a moment: that is what carries it past.
            float towChange = inSlipstream && !NoSlipstream ? dt / towChargeSeconds : -dt / slingshotSeconds;
            towCharge = Mathf.Clamp01(towCharge + towChange);
            if (slingshotLeft > 0f && slingshotLeft <= dt && passing != null)
            {
                BotRaceLog.Add("SLING end: " + racer.DisplayName + " on " + passing.DisplayName + " gap " + GapTo(passing).ToString("0.0")
                    + " my speed " + kart.Speed.ToString("0.0") + " its " + passing.Kart.Speed.ToString("0.0") + " free " + freeSpeed.ToString("0.0")
                    + " follow " + followSpeed.ToString("0.0") + "/" + stepFollowSpeed.ToString("0.0") + " top " + (tuning.MaxSpeed * kart.ExternalTopSpeedFactor).ToString("0.0")
                    + " lat " + racer.Lateral.ToString("0.00") + " its lat " + passing.Lateral.ToString("0.00") + " lane " + trafficLane.ToString("0.00")
                    + " offset " + offsetLane.ToString("0.00") + " target " + TargetLateral.ToString("0.00"));
            }
            slingshotLeft = Mathf.Max(0f, slingshotLeft - dt);

            // Move the steering lane towards the wanted one at laneChangeSpeed. In traffic the wanted lane is a
            // centre-line position, turned into an offset from the racing line at the aim point.
            float aimPos = AimPos(speed, index);
            float lineAtAim = route.LineOffset(aimPos);
            float targetOffset = float.IsNaN(trafficLane) ? ownOffset : Mathf.Clamp(trafficLane, -halfRoad, halfRoad) - lineAtAim;
            offsetLane = Mathf.MoveTowards(offsetLane, targetOffset, laneChangeSpeed * dt);

            float wantedSteer = SteerToLine(speed, aimPos);
            steer = Mathf.MoveTowards(steer, wantedSteer, dt / steerSeconds);

            float allowed = AllowedSpeed(index, rubber);
            float throttle = 0f; // a little too fast: lift off the gas and roll
            if (speed < allowed)
            {
                throttle = 1f;
            }
            else if (speed > allowed + brakeMargin)
            {
                throttle = -1f;
            }

            if (passing != null && passSeconds > 1f)
            {
                PassLaneErrorSum += Mathf.Abs(racer.Lateral - (trafficLane));
                PassLaneSamples++;
                if (Mathf.Abs(route.LineOffset(index)) > 0.5f)
                {
                    PassLaneSamplesOffsetLine++;
                }
            }
            bool drift = WantsDrift(speed, index, wantedSteer);
            if (liftSeconds > 0f)
            {
                liftSeconds -= dt;
                throttle = Mathf.Min(throttle, 0f); // mistake: a short lift off the gas (braking for safety still works)
            }
            Throttle = throttle;
            float steerOut = steer;
            if (drift && !kart.IsDrifting)
            {
                steerOut = driftSign * Mathf.Max(Mathf.Abs(steer), 0.6f); // a drift only starts with clear steering into the corner
            }
            kart.SetInput(throttle, steerOut, drift);
        }

        // Rule 1, pure pursuit. The arc from the kart to the aim point has curvature 2 x sideways / distance^2.
        // A kart turns by (steer x turn rate), and driving an arc needs a turn rate of speed x curvature.
        // Where on the route the bot aims (route position, in route points): further ahead the faster it goes.
        private float AimPos(float speed, int index)
        {
            float look = lookAheadBase + lookAheadPerSpeed * speed;
            return route.RoutePos(transform.position, index) + look / route.Spacing;
        }

        // Where the bot wants to be across the road at its current position, in metres from the centre line.
        // Other bots read this (TargetLateral) to see where a kart is heading, not only where it is now.
        private float WantedLateral()
        {
            return float.IsNaN(trafficLane) ? route.LineOffset(racer.RouteIndex) + ownOffset : trafficLane;
        }

        public float TargetLateral { get { return racer != null ? WantedLateral() : 0f; } }
        public RaceKart PassingTarget { get { return passing; } }   // the kart this bot is passing right now (null = none)
        public bool PassesOnRight { get { return passOnRight; } }

        private float SteerToLine(float speed, float aimPos)
        {
            int aimIndex = route.Wrap(Mathf.RoundToInt(aimPos));
            float halfThere = HalfRoad(aimIndex);
            // Aim = centre line + racing line at the aim point + this bot's offset from it, kept on the road.
            float across = Mathf.Clamp(route.LineOffset(aimPos) + offsetLane, -halfThere, halfThere);
            if (Probing)
            {
                across = ProbeMetres(aimIndex);
            }
            Vector3 aim = route.PointAt(aimPos) + route.Right(aimIndex) * across;

            // Measure from the direction the kart is really MOVING, not where its nose points: in a corner the kart
            // slides a little outwards, and steering by the nose alone would run wide.
            Vector3 moving = body.linearVelocity;
            moving.y = 0f;
            Vector3 forward = moving.magnitude > 3f && Vector3.Dot(moving, transform.forward) > 0f ? moving.normalized : transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            Vector3 toAim = aim - transform.position;
            toAim.y = 0f;
            float sideways = Vector3.Dot(toAim, right);
            if (Vector3.Dot(toAim, forward) < 0f)
            {
                return sideways >= 0f ? 1f : -1f; // the aim point is behind the kart: turn round as fast as possible
            }
            float curvature = 2f * sideways / Mathf.Max(toAim.sqrMagnitude, 0.01f);
            float turnRate = tuning.TurnDegreesPerSecond * Mathf.Deg2Rad;
            return Mathf.Clamp(curvature * Mathf.Max(speed, tuning.FullSteerSpeed) / turnRate, -1f, 1f);
        }

        public int PassAttempts { get; private set; }     // passes started
        // Test numbers for the lane fix: how far the kart is from the lane it should hold while passing
        // (sampled after the first second of a pass), and how many of those samples were where the racing line is
        // moved away from the centre (|offset| > 0.5 m), where mixing up the two references showed most.
        public float PassLaneErrorSum { get; private set; }
        public int PassLaneSamples { get; private set; }
        public int PassLaneSamplesOffsetLine { get; private set; }
        public int PassCompletions { get; private set; }  // passes finished with the other kart clearly behind
        public int PassAborts { get; private set; }       // passes given up

        public int MistakeChances { get; private set; }   // corner entries where a mistake was allowed (the dice were rolled)
        public int BigMistakes { get; private set; }      // of those, the big ones (about a second lost)
        public int Mistakes { get; private set; }         // small driving mistakes made (see RollMistake)

        public float SecondsSinceRecovery { get; private set; } // since the bot was last put back on the road

        // Race-day form: before every race each bot rolls how good its day is, separately for straights and corners
        // (for example +1 % top speed, -0.8 % corner speed). So the same bots do not finish in the same order every time.
        public float FormTopSpeed { get; private set; }
        public float FormCornering { get; private set; }

        public void NewRace(float formRange)
        {
            FormTopSpeed = Mathf.Lerp(-formRange, formRange, (float)dice.NextDouble());
            FormCornering = Mathf.Lerp(-formRange, formRange, (float)dice.NextDouble());
            ResetForStart();
        }

        // Edge probe (test only): the bot holds one fixed line across the road for a whole lap and ignores everything
        // else (other karts, mistakes, the slow-down for bumps), so the report shows what the ROAD does to a kart.
        // The value is a share of the road half-width (0.95 = 95 % towards the right edge, negative = left);
        // +9 / -9 mean "half a metre beyond the right / left edge". Not a number = no probe.
        public float ProbeLane { get; set; } = float.NaN;

        private bool Probing { get { return !float.IsNaN(ProbeLane); } }

        private float ProbeMetres(int index)
        {
            float half = route.WidthAt(index) * 0.5f;
            if (Mathf.Abs(ProbeLane) > 1.5f)
            {
                return Mathf.Sign(ProbeLane) * (half + 0.5f);
            }
            return ProbeLane * half;
        }

        public float Throttle { get; private set; } // what the bot pressed last step (the engine sound reads it)

        // Rule 2. A corner with radius R can be driven at (turn rate x R): faster and the kart cannot turn enough.
        // From a corner that is d metres away the bot may still be faster now, as long as braking gets it down in
        // time: v = sqrt(cornerSpeed^2 + 2 x braking x d). The lowest of all these limits is the speed to drive.
        private float AllowedSpeed(int index, float rubber)
        {
            // Top speed as a share of the kart's normal top speed: the bot's skill x the difficulty x the rubber band.
            // The kart gets the same number (KartController.ExternalTopSpeedFactor), so it can really reach it.
            // Never above 1: no bot is faster on a straight than the player's kart can be.
            float baseFactor = Mathf.Clamp(profile.topSpeed * race.PaceFactor * (1f + FormTopSpeed), 0.5f, 1f);
            // Slipstream: close behind another kart in the same lane the air is thinner, so the bot gets a little more.
            // Slingshot: for a few seconds after pulling out of a tow to pass, the bot may go above the shared top speed.
            float sling = slingshotLeft > 0f ? slingshotBoost : 0f;
            float bonus = rubber + (NoSlipstream ? 0f : slipstreamBonus * towCharge) + sling;
            float factor = baseFactor * (1f + bonus);
            if (bonus > 0f)
            {
                // Bonuses never lift a bot above the shared top speed, except the slingshot while it lasts.
                factor = Mathf.Min(factor, Mathf.Max(1f, baseFactor) * (1f + sling));
            }
            if (racer.Finished)
            {
                factor = 0.6f; // race over for this bot: cool-down lap
            }
            kart.ExternalTopSpeedFactor = factor;
            float top = tuning.MaxSpeed * factor;
            float allowed = top;
            float slowestSoon = top; // slowest corner within the next noPassBeforeCornerMetres
            float turnRate = tuning.TurnDegreesPerSecond * Mathf.Deg2Rad;
            // Corner skill x difficulty x race-day form, but never more than maxCornering: beyond that the kart
            // cannot turn tightly enough and runs wide.
            float cornering = Mathf.Min(maxCornering, profile.cornering * race.PaceFactor * (1f + FormCornering));
            int steps = Mathf.CeilToInt(planDistance / route.Spacing);
            for (int k = 0; k <= steps; k++)
            {
                float curve = Mathf.Abs(route.Curvature(index + k));
                float cornerSpeed = Probing ? float.MaxValue : route.BumpSpeed(index + k); // a ramp edge next to a corner: no long flight
                if (curve > 0.001f)
                {
                    cornerSpeed = Mathf.Min(cornerSpeed, turnRate / curve * cornering);
                }
                if (cornerSpeed >= top)
                {
                    continue; // straight and flat: no limit from this point
                }
                cornerSpeed = Mathf.Max(minCornerSpeed, cornerSpeed);
                if (k * route.Spacing < noPassBeforeCornerMetres && cornerSpeed < slowestSoon)
                {
                    slowestSoon = cornerSpeed;
                }
                float distance = k * route.Spacing;
                float limit = Mathf.Sqrt(cornerSpeed * cornerSpeed + 2f * planBraking * distance);
                if (limit < allowed)
                {
                    allowed = limit;
                }
            }
            slowingForCorner = slowestSoon < top - 1f;
            freeSpeed = allowed; // what the road allows this bot right now, before any kart ahead limits it
            return Mathf.Min(allowed, Mathf.Min(followSpeed, stepFollowSpeed));
        }

        // Small human mistakes. The dice are rolled ONCE when a corner comes up (never every step), and only when it
        // is harmless: not at the start, not in traffic, not in a pass, not on ramps, jumps or narrow parts, at most
        // one per lap and one every mistakeCooldownSeconds. Two kinds: run a bit wide for this bend, or a short lift.
        private void RollMistake(float dt, int index)
        {
            mistakeCooldown -= dt;
            bool cornerAhead = Mathf.Abs(route.Curvature(index + 5)) > 1f / 40f;
            bool inCorner = Mathf.Abs(route.Curvature(index)) > 1f / 40f;
            if (!cornerAhead && !inCorner)
            {
                wideMetres = 0f; // the bend is over: back to the normal line
            }
            bool entering = cornerAhead && !cornerWasAhead;
            cornerWasAhead = cornerAhead;
            if (!entering || mistakeCooldown > 0f || racer.Lap == lastMistakeLap || racer.Finished || passing != null
                || route.IsNoPassZone(index) || route.IsNoPassZone(index + 5) || route.WidthAt(index + 5) < 7.5f || KartWithin(6f))
            {
                return;
            }
            MistakeChances++;
            if (dice.NextDouble() >= profile.mistakeChance)
            {
                return;
            }
            Mistakes++;
            lastMistakeLap = racer.Lap;
            mistakeCooldown = mistakeCooldownSeconds;
            if (dice.NextDouble() < bigMistakeShare)
            {
                // A big moment: off the gas for about a second. This one usually costs a place.
                liftSeconds = Mathf.Lerp(0.8f, 1.2f, (float)dice.NextDouble());
                BigMistakes++;
                BotRaceLog.Add("MISTAKE " + racer.DisplayName + " BIG lift for " + liftSeconds.ToString("0.0") + " s at route index " + index);
            }
            else if (dice.NextDouble() < 0.5)
            {
                // Outside of the bend = away from the turn direction (curvature + = right turn, so outside = left = -).
                float outside = route.Curvature(index + 5) > 0f ? -1f : 1f;
                wideMetres = outside * Mathf.Lerp(0.3f, 0.6f, (float)dice.NextDouble());
                BotRaceLog.Add("MISTAKE " + racer.DisplayName + " runs " + Mathf.Abs(wideMetres).ToString("0.0") + " m wide at route index " + index);
            }
            else
            {
                liftSeconds = Mathf.Lerp(0.2f, 0.4f, (float)dice.NextDouble());
                BotRaceLog.Add("MISTAKE " + racer.DisplayName + " lifts for " + liftSeconds.ToString("0.0") + " s at route index " + index);
            }
        }

        private bool KartWithin(float metres)
        {
            RaceKart[] karts = race.Karts;
            for (int i = 0; i < karts.Length; i++)
            {
                if (karts[i] != racer && Mathf.Abs(GapTo(karts[i])) < metres)
                {
                    return true;
                }
            }
            return false;
        }

        // Rule 3. Traffic. The bot is always in one of four states: FREE (own lane), FOLLOW (behind a slower kart,
        // matching its speed), PASS LEFT or PASS RIGHT (committed to one side of one kart).
        // - A pass starts only if the lane next to the other kart is free now AND stays free while the bot moves over
        //   (it looks about a second ahead), and not in a no-pass zone (narrow bend, ramp, landing).
        // - Once started, the bot keeps that side for at least minCommitSeconds, so it never weaves.
        // - It moves back only when the passed kart is fully behind plus one kart length.
        // - A kart next to the bot always means: stay on my side.
        private void Decide(float speed, float halfRoad)
        {
            // Favourite lane plus a slow wander, so no two laps look the same (metres from the racing line).
            float wander = (Mathf.PerlinNoise(Time.time * 0.2f, noiseSeed) * 2f - 1f) * profile.lineNoise;
            ownOffset = profile.lane * halfRoad + wander + wideMetres;
            trafficLane = float.NaN; // free until the traffic rules below say otherwise
            followSpeed = float.MaxValue;
            towTarget = null;

            if (Blind)
            {
                ownOffset = BlindLane;
                return;
            }

            inSlipstream = BehindAKart(speed);
            if (passing != null)
            {
                ContinuePass(speed, halfRoad);
            }
            else
            {
                LookForPass(speed, halfRoad);
            }
            StayOnMySideOfKartsNextToMe();
            SlowDownNearStandingKarts(speed);
        }

        // A standing kart close to my lane (a crashed or parked player): go past at a careful speed.
        // At race speed the kart does not follow its lane exactly enough in a corner to promise it will miss.
        private void SlowDownNearStandingKarts(float speed)
        {
            RaceKart[] karts = race.Karts;
            for (int i = 0; i < karts.Length; i++)
            {
                RaceKart other = karts[i];
                if (other == racer || other.Kart.Speed >= obstacleSpeed)
                {
                    continue;
                }
                float gap = GapTo(other);
                bool closeAhead = gap > -kartLength && gap < 10f + speed;
                if (closeAhead && Mathf.Abs(other.Lateral - WantedLateral()) < clearance + 1f)
                {
                    followSpeed = Mathf.Min(followSpeed, obstaclePassSpeed);
                }
                // Right in my path and inside my braking distance: creep, and forget any pass on another kart,
                // so that the next decision deals with this standing kart first.
                float brakeRoom = speed * speed / (2f * planBraking);
                bool inMyPath = Mathf.Abs(other.Lateral - racer.Lateral) < clearance * 0.85f;
                if (inMyPath && gap > 0f && gap < followBuffer + followSeconds * speed + brakeRoom)
                {
                    followSpeed = Mathf.Min(followSpeed, creepSpeed);
                    if (passing != null && passing != other)
                    {
                        passing = null;
                    }
                }
            }
        }

        // Metres the other kart is ahead of me along the road. The short way round the loop, so a lapped kart
        // just in front counts as "just in front" (race ORDER uses the full progress instead).
        private float GapTo(RaceKart other)
        {
            return Mathf.Repeat(other.Progress - racer.Progress + route.Length * 0.5f, route.Length) - route.Length * 0.5f;
        }

        // True when a kart drives up to slipstreamSeconds ahead of me in my lane (not next to me).
        private bool BehindAKart(float speed)
        {
            RaceKart[] karts = race.Karts;
            for (int i = 0; i < karts.Length; i++)
            {
                RaceKart other = karts[i];
                float gap = GapTo(other);
                if (other != racer && gap > kartLength && gap < kartLength + slipstreamSeconds * speed
                    && Mathf.Abs(other.Lateral - racer.Lateral) < clearance && other.Kart.Speed > obstacleSpeed)
                {
                    return true;
                }
            }
            return false;
        }

        private void LookForPass(float speed, float halfRoad)
        {
            // The nearest slower kart ahead that is in my cone or in the lane I want.
            float coneLength = 6f + 0.7f * speed;
            RaceKart slower = null;
            float slowerGap = float.MaxValue;
            RaceKart[] karts = race.Karts;
            for (int i = 0; i < karts.Length; i++)
            {
                RaceKart other = karts[i];
                float gap = GapTo(other);
                if (other == racer || gap <= alongsideMetres || gap > coneLength || gap >= slowerGap)
                {
                    continue;
                }
                Vector3 toOther = other.transform.position - transform.position;
                toOther.y = 0f;
                bool inCone = Vector3.Angle(transform.forward, toOther) < coneDegrees;
                bool inMyLane = Mathf.Abs(other.Lateral - WantedLateral()) < clearance;
                if ((inCone || inMyLane) && Mathf.Max(0f, other.Kart.Speed) < speed + 1f)
                {
                    slower = other;
                    slowerGap = gap;
                }
            }
            if (slower == null)
            {
                return; // FREE
            }

            float otherSpeed = Mathf.Max(0f, slower.Kart.Speed);
            float closing = speed - otherSpeed;
            // INTENT: would I be faster than the kart ahead if it were not there? That is my free speed (road,
            // corners, skill, bonuses) minus its speed. The closing speed right now is no good for this: once I follow
            // it, I drive exactly its speed and closing drops to zero, so a faster bot would stay behind for ever.
            // Not while I am slowing for a corner myself: the kart ahead only looks slow there because it brakes first.
            // A kart that stands or crawls is an obstacle, not a rival: going round it is always allowed.
            bool obstacle = otherSpeed < obstacleSpeed;
            bool fasterThanIt = freeSpeed - otherSpeed >= profile.passClosingSpeed;
            bool wantsToPass = obstacle
                || (fasterThanIt && !slowingForCorner && !route.IsNoPassZone(racer.RouteIndex));
            if (wantsToPass)
            {
                // EXECUTION is checked with the real positions and speeds (TryStartPass).
                // Prefer the side I am already on; if that lane is not safe, try the other one.
                bool rightFirst = racer.Lateral >= slower.Lateral;
                if (TryStartPass(slower, slowerGap, closing, speed, halfRoad, rightFirst)
                    || TryStartPass(slower, slowerGap, closing, speed, halfRoad, !rightFirst))
                {
                    return;
                }
            }
            // FOLLOW: close behind a kart in my path, do not run into it.
            // The distance kept grows with the speed difference: room to brake down to the other kart's speed.
            // TOW: if I want to pass but cannot yet (lane busy or not enough speed to finish), I close up to the
            // shorter tow distance on the straight, so the slipstream builds up for the next try.
            bool towing = wantsToPass && !obstacle;
            towTarget = towing ? slower : null;
            float keepSeconds = towing ? towFollowSeconds : followSeconds;
            float keepBuffer = towing ? towBuffer : followBuffer;
            bool inMyPath = Mathf.Abs(slower.Lateral - racer.Lateral) < clearance * 0.75f;
            float brakeRoom = closing > 0f ? closing * closing / (2f * planBraking) : 0f;
            if (inMyPath && slowerGap < keepBuffer + keepSeconds * speed + brakeRoom)
            {
                // Behind a standing kart the bot never stops completely: it creeps, so it can steer round it.
                followSpeed = obstacle ? Mathf.Max(otherSpeed, creepSpeed) : otherSpeed;
            }
        }

        // Checks one side of the other kart and commits to it if it is safe. Returns true when the pass started.
        private bool TryStartPass(RaceKart target, float gap, float closing, float speed, float halfRoad, bool onRight)
        {
            float lane = PassLane(target, onRight, halfRoad);
            if (Mathf.Abs(lane) > halfRoad)
            {
                return false; // that lane is off the road
            }
            // Am I across before I reach the other kart? (Careful bots want extra road here.)
            // While moving over the bot may speed up towards its free speed, so the gap can shrink faster than the
            // closing speed alone says: accelerate for tAccel seconds, then keep the higher speed.
            float changeSeconds = Mathf.Abs(lane - racer.Lateral) / laneChangeSpeed;
            float t = changeSeconds + profile.extraClearSeconds;
            float speedUp = Mathf.Max(0f, freeSpeed - speed);
            float accel = Mathf.Max(0.1f, tuning.Acceleration);
            float tAccel = Mathf.Min(t, speedUp / accel);
            float shrink = Mathf.Max(0f, closing) * t + accel * tAccel * (t - 0.5f * tAccel);
            float gapWhenAcross = gap - shrink;
            bool creepingRoundObstacle = target.Kart.Speed < obstacleSpeed && speed <= obstaclePassSpeed;
            if (gapWhenAcross < alongsideMetres && !creepingRoundObstacle)
            {
                return false;
            }
            // Can I finish it? A pass only makes sense if I get fully past (plus the merge room) before the time
            // limit. My speed over the other kart: my free speed (with the tow I have built up), plus the slingshot
            // for its few seconds if I have enough tow. Otherwise I stay in the tow and try again later instead of
            // driving beside it for seconds.
            bool movingTarget = !creepingRoundObstacle && target.Kart.Speed >= obstacleSpeed;
            bool slingshot = movingTarget && !NoSlipstream && towCharge >= slingshotMinCharge;
            if (movingTarget)
            {
                float baseAdvantage = Mathf.Max(freeSpeed, speed) - Mathf.Max(0f, target.Kart.Speed);
                float boost = slingshot ? tuning.MaxSpeed * slingshotBoost : 0f;
                float toGetPast = gap + kartLength + mergeBehindMetres;
                float finishSeconds = PassSeconds(toGetPast, baseAdvantage, boost, accel);
                if (finishSeconds > maxPassSeconds)
                {
                    return false;
                }
            }
            if (!LaneStaysFree(lane, target, speed))
            {
                return false;
            }
            if (movingTarget && SlowerKartInLaneAhead(lane, target, gap + kartLength + mergeBehindMetres + followBuffer + followSeconds * speed))
            {
                return false; // I would only get stuck behind that one, half beside the kart I wanted to pass
            }
            BotRaceLog.Add("PASS start: " + racer.DisplayName + " on " + target.DisplayName + " gap " + gap.ToString("0.0") + " closing " + closing.ToString("0.0")
                + " free " + (freeSpeed - Mathf.Max(0f, target.Kart.Speed)).ToString("0.0") + " at index " + racer.RouteIndex);
            passing = target;
            passOnRight = onRight;
            passSeconds = 0f;
            PassAttempts++;
            trafficLane = lane; // the same centre-line value the checks above used: steering aims exactly there
            slingshotLeft = slingshot ? slingshotPassSeconds : 0f;
            return true;
        }

        // True when another kart (not the one I pass) drives in the pass lane ahead within 'reach' metres and is not
        // clearly faster than the kart I pass: I would have to slow down behind it before I am past.
        private bool SlowerKartInLaneAhead(float lane, RaceKart target, float reach)
        {
            float targetSpeed = Mathf.Max(0f, target.Kart.Speed);
            RaceKart[] karts = race.Karts;
            for (int i = 0; i < karts.Length; i++)
            {
                RaceKart other = karts[i];
                if (other == racer || other == target)
                {
                    continue;
                }
                float gap = GapTo(other);
                if (gap > 0f && gap < reach && Mathf.Abs(other.Lateral - lane) < clearance * 0.75f
                    && Mathf.Max(0f, other.Kart.Speed) < targetSpeed + 1f)
                {
                    return true;
                }
            }
            return false;
        }

        // Seconds to gain 'distance' metres on the other kart: 'boost' m/s extra for the slingshot time (reached
        // after accelerating at 'accel'), then only the base advantage. MaxValue when it never gets there.
        private float PassSeconds(float distance, float baseAdvantage, float boost, float accel)
        {
            float lag = boost > 0f ? boost / accel : 0f;              // time to reach the boosted speed
            float boostTime = boost > 0f ? slingshotPassSeconds : 0f;
            float boostedGain = (baseAdvantage + boost) * boostTime - 0.5f * boost * lag;
            if (boost > 0f && boostedGain >= distance && baseAdvantage + boost > 0.05f)
            {
                return (distance + 0.5f * boost * lag) / (baseAdvantage + boost);
            }
            if (baseAdvantage <= 0.05f)
            {
                return float.MaxValue;
            }
            return boostTime + (distance - Mathf.Max(0f, boostedGain)) / baseAdvantage;
        }

        // Looks predictSeconds ahead in a few steps, along the WHOLE way across: my position moves from where I am
        // now to the pass lane at laneChangeSpeed, and every other kart moves from where it is now to where it is
        // heading (another bot's TargetLateral, so a kart that is changing lanes itself counts too). If at any step
        // a kart would be beside me (front to back) and less than 'clearance' away sideways, the lane is not free.
        // All values are metres from the centre line.
        private bool LaneStaysFree(float lane, RaceKart target, float speed)
        {
            const int steps = 4;
            RaceKart[] karts = race.Karts;
            for (int i = 0; i < karts.Length; i++)
            {
                RaceKart other = karts[i];
                if (other == racer || other == target)
                {
                    continue;
                }
                float gap = GapTo(other);
                float otherSpeed = Mathf.Max(0f, other.Kart.Speed);
                BotDriver otherDriver = race.DriverOf(other);
                float otherHeading = otherDriver != null ? otherDriver.TargetLateral : other.Lateral;
                for (int step = 0; step <= steps; step++)
                {
                    float t = predictSeconds * step / steps;
                    float gapThen = gap + (otherSpeed - speed) * t;
                    if (Mathf.Abs(gapThen) >= alongsideMetres + 1f)
                    {
                        continue; // not beside me at that moment
                    }
                    float mine = Mathf.MoveTowards(racer.Lateral, lane, laneChangeSpeed * t);
                    float its = Mathf.MoveTowards(other.Lateral, otherHeading, laneChangeSpeed * t);
                    if (Mathf.Abs(mine - its) < clearance)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // The lane beside the other kart. Round a standing kart the bot leaves half a metre more, if the road has it.
        private float PassLane(RaceKart target, bool onRight, float halfRoad)
        {
            float beside = clearance;
            if (target.Kart.Speed < obstacleSpeed && Mathf.Abs(target.Lateral + (onRight ? 1f : -1f) * (clearance + 0.5f)) <= halfRoad)
            {
                beside = clearance + 0.5f;
            }
            return onRight ? target.Lateral + beside : target.Lateral - beside;
        }

        private void ContinuePass(float speed, float halfRoad)
        {
            float gap = GapTo(passing);
            bool heldLongEnough = passSeconds >= minCommitSeconds;

            // Done: the other kart is fully behind me plus one kart length. Now the bot may move back.
            if (gap < -(kartLength + mergeBehindMetres) && heldLongEnough)
            {
                PassCompletions++;
                passing = null;
                slingshotLeft = 0f;
                return;
            }
            // Given up: the other kart pulled away again, or it is far off (it respawned, for example).
            // (Being a little slower for a moment is not a reason: it must really have got away.)
            bool pulledAway = gap > giveUpGapMetres && Mathf.Max(0f, passing.Kart.Speed) > speed + 1f;
            if (heldLongEnough && (pulledAway || Mathf.Abs(gap) > 40f || passSeconds > giveUpSeconds))
            {
                PassAborts++;
                BotRaceLog.Add("PASS given up: " + racer.DisplayName + " on " + passing.DisplayName + " gap " + gap.ToString("0.0") + " my speed " + speed.ToString("0.0")
                    + " its speed " + passing.Kart.Speed.ToString("0.0") + " after " + passSeconds.ToString("0.0") + " s at index " + racer.RouteIndex + " follow " + (followSpeed < 1000f));
                passing = null;
                slingshotLeft = 0f;
                return;
            }
            // Keep my side of it. If I am still behind it and not yet clear sideways, do not hit its back.
            trafficLane = Mathf.Clamp(PassLane(passing, passOnRight, halfRoad), -halfRoad, halfRoad);
            bool notClearYet = Mathf.Abs(passing.Lateral - racer.Lateral) < clearance * 0.75f; // same as FollowLimitInMyPath
            float closing = speed - Mathf.Max(0f, passing.Kart.Speed);
            float brakeRoom = closing > 0f ? closing * closing / (2f * planBraking) : 0f;
            if (notClearYet && gap > 0f && gap < followBuffer + followSeconds * speed + brakeRoom)
            {
                float itsSpeed = Mathf.Max(0f, passing.Kart.Speed);
                followSpeed = itsSpeed < obstacleSpeed ? Mathf.Max(itsSpeed, creepSpeed) : itsSpeed;
            }
        }

        // A kart next to me (in front-to-back terms): hold my side of it, never move across.
        // Runs every physics step (safety). Works in centre-line metres; only when a kart beside me really limits
        // where I want to go does the bot switch from free driving to a fixed traffic lane.
        private void StayOnMySideOfKartsNextToMe()
        {
            float wanted = WantedLateral();
            float limited = wanted;
            RaceKart[] karts = race.Karts;
            for (int i = 0; i < karts.Length; i++)
            {
                RaceKart other = karts[i];
                if (other == racer)
                {
                    continue;
                }
                // Fair racing: a kart that is passing me (still behind me, already committed to one side) gets its
                // room. I may move away from it, but not over to its side: there I hold the line I am on now.
                float gap = GapTo(other);
                BotDriver otherDriver = race.DriverOf(other);
                if (otherDriver != null && otherDriver.PassingTarget == racer && gap < -alongsideMetres && gap > -beingPassedMetres)
                {
                    limited = otherDriver.PassesOnRight ? Mathf.Min(limited, racer.Lateral) : Mathf.Max(limited, racer.Lateral);
                    continue;
                }
                if (Mathf.Abs(gap) > alongsideMetres)
                {
                    continue;
                }
                if (other.Lateral > racer.Lateral)
                {
                    limited = Mathf.Min(limited, other.Lateral - clearance);
                }
                else
                {
                    limited = Mathf.Max(limited, other.Lateral + clearance);
                }
            }
            if (limited != wanted)
            {
                trafficLane = limited;
            }
        }

        // Safety, every physics step: the speed limit from a kart right in my path ahead (same rule as FOLLOW in
        // LookForPass, but without waiting for the next decision). Very big = nothing in the way.
        private float FollowLimitInMyPath(float speed)
        {
            float limit = float.MaxValue;
            RaceKart[] karts = race.Karts;
            for (int i = 0; i < karts.Length; i++)
            {
                RaceKart other = karts[i];
                if (other == racer)
                {
                    continue;
                }
                float gap = GapTo(other);
                if (gap <= 0f || Mathf.Abs(other.Lateral - racer.Lateral) >= clearance * 0.75f)
                {
                    continue;
                }
                float otherSpeed = Mathf.Max(0f, other.Kart.Speed);
                float closing = speed - otherSpeed;
                float brakeRoom = closing > 0f ? closing * closing / (2f * planBraking) : 0f;
                bool tow = other == towTarget;
                float keep = (tow ? towBuffer : followBuffer) + (tow ? towFollowSeconds : followSeconds) * speed;
                if (gap < keep + brakeRoom)
                {
                    limit = Mathf.Min(limit, otherSpeed < obstacleSpeed ? Mathf.Max(otherSpeed, creepSpeed) : otherSpeed);
                }
            }
            return limit;
        }

        // Optional (profile.drifts): drift through ONE marked corner (BotRoute.driftFrom .. driftTo) for the boost.
        // Start only when everything is right: at the marker, on the ground, fast enough, pointing along the road,
        // close to the line, nobody near. Hold the drift key like a player, release at the end marker (a charged
        // drift then boosts down the straight that follows).
        // If something goes wrong on the way (running off the road, a kart in the way) the drift is ABORTED:
        // releasing the key would boost, so instead the bot's input is switched off for one physics step.
        // KartController then cancels the drift without a boost (its own rule for "input off").
        private bool WantsDrift(float speed, int index, float wantedSteer)
        {
            bool mayDrift = (profile.drifts || AllDrift) && route.driftTo != route.driftFrom && !racer.IsPlayer;
            int sinceStart = route.IndexDifference(route.driftFrom, index);
            int cornerPoints = route.IndexDifference(route.driftFrom, route.driftTo);
            float sign = route.Curvature(route.driftFrom + cornerPoints / 2) > 0f ? 1f : -1f;

            if (driftSign == 0f)
            {
                bool atMarker = sinceStart >= 0 && sinceStart <= 2;
                bool aligned = Vector3.Dot(transform.forward, route.Forward(index)) > 0.95f
                    && Mathf.Abs(racer.Lateral - route.LineOffset(index) - offsetLane) < 1f;
                bool fastEnough = speed > tuning.MaxSpeed * 0.75f;
                if (mayDrift && atMarker && kart.IsGrounded && aligned && fastEnough && passing == null
                    && followSpeed == float.MaxValue && stepFollowSpeed == float.MaxValue && !KartWithin(8f))
                {
                    driftSign = sign;
                    DriftEntries++;
                }
                return driftSign != 0f;
            }

            // Holding. Reached the end marker: let go (boost if it is charged).
            if (sinceStart >= cornerPoints || sinceStart < 0)
            {
                driftSign = 0f;
                return false;
            }
            // The drift never started (the kart refused it) or was cancelled by a wall: nothing to hold any more.
            if (!kart.IsDrifting && sinceStart > 3)
            {
                driftSign = 0f;
                return false;
            }
            // Envelope broken: abort without a boost.
            float halfRoad = HalfRoad(index);
            bool offLine = Mathf.Abs(racer.Lateral) > halfRoad + 0.6f;
            if (kart.IsDrifting && (offLine || followSpeed < float.MaxValue || stepFollowSpeed < float.MaxValue || !kart.IsGrounded))
            {
                DriftAborts++;
                driftSign = 0f;
                kart.InputEnabled = false; // one step without input = drift cancelled, no boost (BotRace switches it on again)
                BotRaceLog.Add("DRIFT aborted: " + racer.DisplayName + " at route index " + index + (offLine ? " off line" : " traffic or air"));
                return false;
            }
            return true;
        }

        public int DriftEntries { get; private set; }  // drifts started at the marked corner
        public int DriftAborts { get; private set; }   // drifts cancelled without a boost because something went wrong

        // Rule 4. Three timers; any one above the limit asks for a respawn.
        private bool NeedsRecovery(float dt)
        {
            int index = racer.RouteIndex;
            bool stuck = Mathf.Abs(kart.Speed) < stuckSpeed;
            bool wrongWay = Vector3.Dot(transform.forward, route.Forward(index)) < -0.3f;
            float beyondEdge = Mathf.Abs(racer.Lateral) - route.WidthAt(index) * 0.5f;
            float below = route.points[index].y - transform.position.y;
            bool offRoad = beyondEdge > (Probing ? offRoadMetres + 1.5f : offRoadMetres) || below > 6f;

            stuckTime = stuck ? stuckTime + dt : 0f;
            wrongWayTime = wrongWay ? wrongWayTime + dt : 0f;
            offRoadTime = offRoad ? offRoadTime + dt : 0f;
            return stuckTime > recoverSeconds || wrongWayTime > recoverSeconds || offRoadTime > recoverSeconds;
        }

        // Back to the safe pose after the last checkpoint (Role 2's TrackInfo). Waits if another kart stands there.
        private void TryRespawn()
        {
            kart.SetInput(0f, 0f, false);
            Transform spot = trackInfo.GetRespawnPoint(racer.LastCheckpoint);
            RaceKart[] karts = race.Karts;
            for (int i = 0; i < karts.Length; i++)
            {
                if (karts[i] != racer && (karts[i].transform.position - spot.position).sqrMagnitude < 3f * 3f)
                {
                    return; // occupied, try again next step
                }
            }
            racer.NoteRespawn(stuckTime > recoverSeconds);
            BotRaceLog.Add(race.RaceTime.ToString("0.0") + " s  " + racer.DisplayName + " respawn at route index " + racer.RouteIndex
                + (stuckTime > recoverSeconds ? " STUCK" : "") + (wrongWayTime > recoverSeconds ? " WRONG WAY" : "")
                + (offRoadTime > recoverSeconds ? " OFF ROAD" : "") + "  lateral " + racer.Lateral.ToString("0.0")
                + "  height above road " + (transform.position.y - route.points[racer.RouteIndex].y).ToString("0.0"));
            kart.ResetTo(spot.position, spot.rotation);
            racer.SnapToRoute();
            ResetForStart();
            SecondsSinceRecovery = 0f;
        }

        // Half the road width the bot may use at a route point.
        private float HalfRoad(int index)
        {
            return Mathf.Max(0.3f, route.WidthAt(index) * 0.5f - edgeMargin);
        }
    }
}
