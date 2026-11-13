using UnityEngine;

namespace CarcavelosCircuit.Driving
{
    // Every driving number in one asset (Driving/Settings/KartTuning.asset), so nobody hard-codes them.
    // Change a value while the game runs in Play Mode to feel the difference (the asset keeps it, so note it down).
    [CreateAssetMenu(fileName = "KartTuning", menuName = "Carcavelos Circuit/Kart Tuning")]
    public class KartTuning : ScriptableObject
    {
        [Header("Speed")]
        [SerializeField, Tooltip("Top speed going forward, in metres per second (18 m/s = 65 km/h; D7 lowered it from 25 so a first lap is controllable)")]
        private float maxSpeed = 18f;

        [SerializeField, Tooltip("Top speed in reverse, in metres per second")]
        private float maxReverseSpeed = 8f;

        [Header("Pedals")]
        [SerializeField, Tooltip("How fast the kart gains speed with the gas pressed, in m/s per second")]
        private float acceleration = 10f;

        [SerializeField, Tooltip("How fast the kart slows down when you press brake while moving forward, in m/s per second")]
        private float brakeDeceleration = 30f;

        [SerializeField, Tooltip("How fast the kart gains speed in reverse, in m/s per second")]
        private float reverseAcceleration = 10f;

        [SerializeField, Tooltip("How fast the kart slows down on its own when no key is pressed, in m/s per second")]
        private float coastDeceleration = 6f;

        [Header("Steering")]
        [SerializeField, Tooltip("Turning speed at full steering, in degrees per second")]
        private float turnDegreesPerSecond = 110f;

        [SerializeField, Tooltip("Below this speed (m/s) steering is weaker, so the kart cannot spin on the spot")]
        private float fullSteerSpeed = 5f;

        [SerializeField, Tooltip("How quickly sideways sliding is removed. Higher = more grip, lower = more slidey")]
        private float sideGrip = 10f;

        [Header("Ground")]
        [SerializeField, Tooltip("How far below the bottom of the ball the ground still counts as 'touching', in metres")]
        private float groundCheckExtra = 0.3f;

        [Header("Drift: start and stop")]
        [SerializeField, Tooltip("Steering needed (0..1) together with the drift key to start a drift")]
        private float driftMinSteer = 0.5f;

        [SerializeField, Tooltip("A drift can only start above this share of the top speed (0.4 = 40 %)")]
        private float driftStartSpeedFraction = 0.4f;

        [SerializeField, Tooltip("A running drift is cancelled (no boost) when the speed falls below this share of the top speed")]
        private float driftMinSpeedFraction = 0.3f;

        [Header("Drift: feel")]
        [SerializeField, Tooltip("Grip while drifting as a share of the normal grip. Lower = longer slide")]
        private float driftGripMultiplier = 0.3f;

        [SerializeField, Tooltip("Turning in a drift as a share of normal turning when you steer against the drift (tight arc needs more)")]
        private float driftMinTurn = 0.4f;

        [SerializeField, Tooltip("Turning in a drift as a share of normal turning when you steer into the drift (wider = faster turn)")]
        private float driftMaxTurn = 1.1f;

        [SerializeField, Tooltip("Speed lost per second while drifting, as a share (0.1 = 10 % per second)")]
        private float driftSpeedLoss = 0.1f;

        [Header("Drift: charge and boost")]
        [SerializeField, Tooltip("Seconds of drifting until blue sparks (stage 1)")]
        private float stage1Seconds = 0.8f;

        [SerializeField, Tooltip("Seconds of drifting until orange sparks (stage 2)")]
        private float stage2Seconds = 1.6f;

        [SerializeField, Tooltip("Charge speed when you steer against the drift (1 = normal)")]
        private float chargeRateAgainstTurn = 0.7f;

        [SerializeField, Tooltip("Charge speed when you steer into the drift (1 = normal)")]
        private float chargeRateIntoTurn = 1.3f;

        [SerializeField, Tooltip("Top speed multiplier while boosting (1.45 = 45 % faster; raised from 1.35 in D7 so a boost still feels strong at the lower top speed)")]
        private float boostSpeedMultiplier = 1.45f;

        [SerializeField, Tooltip("How fast the kart gains speed while boosting, in m/s per second")]
        private float boostAcceleration = 40f;

        [SerializeField, Tooltip("Boost length after releasing a stage 1 drift, in seconds")]
        private float stage1BoostSeconds = 0.4f;

        [SerializeField, Tooltip("Boost length after releasing a stage 2 drift, in seconds")]
        private float stage2BoostSeconds = 1.2f;

        [Header("Air (ramp jump)")]
        [SerializeField, Tooltip("Steering in the air as a share of normal steering (0.25 = a quarter)")]
        private float airSteerFraction = 0.25f;

        [SerializeField, Tooltip("Gravity multiplier while falling (after the top of the jump). Above 1 = lands sooner, feels less floaty")]
        private float fallGravityMultiplier = 1.5f;

        [SerializeField, Tooltip("How fast the kart model tilts to the slope (and back to level in the air). Higher = snappier")]
        private float visualTiltSpeed = 10f;

        [SerializeField, Tooltip("Flying animation: the nose tilts up or down by at most this many degrees (model only)")]
        private float maxAirPitchDegrees = 25f;

        [SerializeField, Tooltip("Flying animation: the vertical speed (m/s) at which the nose reaches the full angle")]
        private float airPitchFullSpeed = 8f;

        [SerializeField, Tooltip("Flying animation: the small side-to-side wobble, in degrees (0 = none)")]
        private float airWobbleDegrees = 3f;

        [SerializeField, Tooltip("Flying animation: how many wobbles per second")]
        private float airWobbleHertz = 1.2f;

        public float MaxSpeed { get { return maxSpeed; } }
        public float MaxReverseSpeed { get { return maxReverseSpeed; } }
        public float Acceleration { get { return acceleration; } }
        public float BrakeDeceleration { get { return brakeDeceleration; } }
        public float ReverseAcceleration { get { return reverseAcceleration; } }
        public float CoastDeceleration { get { return coastDeceleration; } }
        public float TurnDegreesPerSecond { get { return turnDegreesPerSecond; } }
        public float FullSteerSpeed { get { return fullSteerSpeed; } }
        public float SideGrip { get { return sideGrip; } }
        public float GroundCheckExtra { get { return groundCheckExtra; } }

        public float DriftMinSteer { get { return driftMinSteer; } }
        public float DriftStartSpeedFraction { get { return driftStartSpeedFraction; } }
        public float DriftMinSpeedFraction { get { return driftMinSpeedFraction; } }
        public float DriftGripMultiplier { get { return driftGripMultiplier; } }
        public float DriftMinTurn { get { return driftMinTurn; } }
        public float DriftMaxTurn { get { return driftMaxTurn; } }
        public float DriftSpeedLoss { get { return driftSpeedLoss; } }
        public float Stage1Seconds { get { return stage1Seconds; } }
        public float Stage2Seconds { get { return stage2Seconds; } }
        public float ChargeRateAgainstTurn { get { return chargeRateAgainstTurn; } }
        public float ChargeRateIntoTurn { get { return chargeRateIntoTurn; } }
        public float BoostSpeedMultiplier { get { return boostSpeedMultiplier; } }
        public float BoostAcceleration { get { return boostAcceleration; } }
        public float Stage1BoostSeconds { get { return stage1BoostSeconds; } }
        public float Stage2BoostSeconds { get { return stage2BoostSeconds; } }

        public float AirSteerFraction { get { return airSteerFraction; } }
        public float FallGravityMultiplier { get { return fallGravityMultiplier; } }
        public float VisualTiltSpeed { get { return visualTiltSpeed; } }
        public float MaxAirPitchDegrees { get { return maxAirPitchDegrees; } }
        public float AirPitchFullSpeed { get { return airPitchFullSpeed; } }
        public float AirWobbleDegrees { get { return airWobbleDegrees; } }
        public float AirWobbleHertz { get { return airWobbleHertz; } }
    }
}
