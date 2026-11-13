using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Task V10: every bot tuning number in one asset (Visuals/Bots/BotSettings.asset, menu
    // Carcavelos Circuit > Bots > Create Bot Settings). BotSquad reads it; without an asset the defaults below apply.
    [CreateAssetMenu(menuName = "Carcavelos Circuit/Bot Settings", fileName = "BotSettings")]
    public class BotSettings : ScriptableObject
    {
        [Header("The five bot drivers (made-up cartoon characters: 3 locals, the Dean, a professor)")]
        [SerializeField, Tooltip("One bot per entry, in grid order (the first starts in the front row)")]
        private BotProfile[] bots =
        {
            new BotProfile("Gaivota", new Color(0.88f, 0.16f, 0.16f), 0.97f, 0.94f, -0.8f, true),        // seagull: bold, drifts
            new BotProfile("Bica", new Color(0.56f, 0.27f, 0.86f), 0.99f, 0.90f, 0.7f, true),            // espresso: fast on straights
            new BotProfile("Sardinha", new Color(0.16f, 0.66f, 0.36f), 0.95f, 0.96f, 0f, false),         // sardine: steady
            new BotProfile("The Dean", new Color(0.98f, 0.80f, 0.12f), 0.98f, 0.95f, -0.3f, false),      // the Dean: clean lines
            new BotProfile("Prof. Pastel", new Color(0.16f, 0.46f, 0.96f), 0.94f, 0.97f, 1.0f, false)    // professor: careful
        };

        [Header("The kart (copy of Role 1's KartTuning: the bot plans with these, the kart drives with the real ones)")]
        [SerializeField, Tooltip("KartTuning > Turn Degrees Per Second")]
        private float turnDegreesPerSecond = 110f;

        [SerializeField, Tooltip("KartTuning > Full Steer Speed (m/s)")]
        private float fullSteerSpeed = 5f;

        [Header("Following the racing line")]
        [SerializeField, Tooltip("Metres ahead on the racing line the bot aims at, when standing")]
        private float lookAheadMetres = 4f;

        [SerializeField, Tooltip("Extra look-ahead per m/s of speed (seconds): faster = aims further ahead = smoother")]
        private float lookAheadSeconds = 0.3f;

        [SerializeField, Tooltip("Seconds the steering needs from straight to full lock (smooths small wobbles)")]
        private float steerSeconds = 0.1f;

        [SerializeField, Tooltip("How far ahead (m) the bot looks for corners to slow down for")]
        private float planMetres = 40f;

        [SerializeField, Tooltip("Braking the bot plans with (m/s per second; the kart can brake harder, this leaves room)")]
        private float planBraking = 12f;

        [SerializeField, Tooltip("Never plans slower than this in a corner (m/s)")]
        private float minCornerSpeed = 6f;

        [SerializeField, Tooltip("This many m/s too fast before the bot brakes (in between it just rolls)")]
        private float brakeMargin = 1.5f;

        [Header("Light rubber band (keeps the race close)")]
        [SerializeField, Tooltip("At this many metres behind (or ahead of) the player the full correction applies")]
        private float rubberBandMetres = 80f;

        [SerializeField, Range(0f, 0.2f), Tooltip("Far behind the player: pace x (1 + this), but never above the kart's top speed")]
        private float rubberBandBoost = 0.06f;

        [SerializeField, Range(0f, 0.3f), Tooltip("Far ahead of the player: pace x (1 - this)")]
        private float rubberBandSlow = 0.1f;

        [Header("Traffic")]
        [SerializeField, Tooltip("A kart closer than this ahead (m) in my lane makes me move beside it")]
        private float passLookMetres = 9f;

        [SerializeField, Tooltip("Metres beside a kart when passing it")]
        private float passSideMetres = 1.8f;

        [SerializeField, Tooltip("Never further than this from the racing line (m); campus roads are 6 to 8 m wide")]
        private float maxLaneMetres = 2.2f;

        [SerializeField, Tooltip("Metres per second the bot moves sideways to a new lane")]
        private float laneChangeSpeed = 2.5f;

        [Header("Drifting (bots with Drifts on)")]
        [SerializeField, Tooltip("Looks this many metres ahead to judge a bend")]
        private float driftLookMetres = 24f;

        [SerializeField, Tooltip("Starts a drift when the road ahead turns at least this much (degrees)")]
        private float driftMinBendDegrees = 50f;

        [SerializeField, Tooltip("No drift into a hairpin sharper than this (degrees)")]
        private float driftMaxBendDegrees = 130f;

        [SerializeField, Tooltip("No drift if any part of the bend is tighter than this radius (m)")]
        private float driftMinRadius = 14f;

        [SerializeField, Tooltip("No drift where the road climbs or drops more than this (m) along the bend (ramps, stairs)")]
        private float driftMaxClimb = 0.4f;

        [SerializeField, Range(0f, 1f), Tooltip("Only at this share of the top speed or faster")]
        private float driftMinSpeed01 = 0.7f;

        [Header("Stuck and fallen off")]
        [SerializeField, Tooltip("Standing still this long while racing = stuck: back up and steer out")]
        private float stuckSeconds = 2f;

        [SerializeField, Tooltip("Seconds of backing up")]
        private float reverseSeconds = 1.2f;

        [SerializeField, Tooltip("Backed up this often in a row without getting going = put back on the racing line")]
        private int maxReverseTries = 3;

        [SerializeField, Tooltip("This many metres below the racing line = fell off: put back on the line")]
        private float fallMetres = 8f;

        public int BotCount { get { return bots == null ? 0 : bots.Length; } }
        public float TurnDegreesPerSecond { get { return turnDegreesPerSecond; } }
        public float FullSteerSpeed { get { return fullSteerSpeed; } }
        public float LookAheadMetres { get { return lookAheadMetres; } }
        public float LookAheadSeconds { get { return lookAheadSeconds; } }
        public float SteerSeconds { get { return steerSeconds; } }
        public float PlanMetres { get { return planMetres; } }
        public float PlanBraking { get { return planBraking; } }
        public float MinCornerSpeed { get { return minCornerSpeed; } }
        public float BrakeMargin { get { return brakeMargin; } }
        public float RubberBandMetres { get { return rubberBandMetres; } }
        public float RubberBandBoost { get { return rubberBandBoost; } }
        public float RubberBandSlow { get { return rubberBandSlow; } }
        public float PassLookMetres { get { return passLookMetres; } }
        public float PassSideMetres { get { return passSideMetres; } }
        public float MaxLaneMetres { get { return maxLaneMetres; } }
        public float LaneChangeSpeed { get { return laneChangeSpeed; } }
        public float DriftLookMetres { get { return driftLookMetres; } }
        public float DriftMinBendDegrees { get { return driftMinBendDegrees; } }
        public float DriftMaxBendDegrees { get { return driftMaxBendDegrees; } }
        public float DriftMinRadius { get { return driftMinRadius; } }
        public float DriftMaxClimb { get { return driftMaxClimb; } }
        public float DriftMinSpeed01 { get { return driftMinSpeed01; } }
        public float StuckSeconds { get { return stuckSeconds; } }
        public float ReverseSeconds { get { return reverseSeconds; } }
        public int MaxReverseTries { get { return maxReverseTries; } }
        public float FallMetres { get { return fallMetres; } }

        // The profile for grid position `index`, or null if there is none.
        public BotProfile Bot(int index)
        {
            if (bots == null || index < 0 || index >= bots.Length)
            {
                return null;
            }
            return bots[index];
        }
    }
}
