using UnityEngine;

namespace CarcavelosCircuit.Driving
{
    // Every driving number in one asset (Driving/Settings/KartTuning.asset), so nobody hard-codes them.
    // Change a value while the game runs in Play Mode to feel the difference (the asset keeps it, so note it down).
    [CreateAssetMenu(fileName = "KartTuning", menuName = "Carcavelos Circuit/Kart Tuning")]
    public class KartTuning : ScriptableObject
    {
        [Header("Speed")]
        [SerializeField, Tooltip("Top speed going forward, in metres per second (25 m/s = 90 km/h)")]
        private float maxSpeed = 25f;

        [SerializeField, Tooltip("Top speed in reverse, in metres per second")]
        private float maxReverseSpeed = 8f;

        [Header("Pedals")]
        [SerializeField, Tooltip("How fast the kart gains speed with the gas pressed, in m/s per second")]
        private float acceleration = 14f;

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
    }
}
