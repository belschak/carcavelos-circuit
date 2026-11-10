using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Stand-in kart for the KartEffects_Demo scene only: it drives in a circle and runs a fixed drift pattern, so the
    // effects can be checked without the real KartController (Role 1). Never used in the race.
    // Every 8 s: a long drift (blue sparks at 0.8 s, orange at 1.6 s, like the KartTuning defaults) and a 1.2 s boost,
    // then a short drift released at blue and a 0.4 s boost.
    public class KartEffectsDemoKart : MonoBehaviour, IKartState
    {
        [SerializeField, Tooltip("Radius of the circle the kart drives (m)")]
        private float radius = 7f;

        [SerializeField, Tooltip("Normal speed (m/s); boosting adds 35 %")]
        private float cruiseSpeed = 12f;

        public float Speed { get; private set; }
        public float MaxSpeed { get { return 25f; } }
        public bool IsDrifting { get; private set; }
        public float DriftCharge01 { get; private set; }
        public int DriftStage { get; private set; }
        public bool IsBoosting { get; private set; }
        public bool IsGrounded { get { return true; } }

        // The demo kart always turns right (more while drifting). Steer is in IKartState since #55; as a plain
        // public property it also compiles before that PR is merged.
        public float Steer { get { return IsDrifting ? 1f : 0.6f; } }

        public bool InputEnabled { get; set; }

        private float clock;

        private void Update()
        {
            Step(Time.deltaTime);
        }

        // One step of the pattern. Public so the builder can step it in Edit Mode for the check renders.
        public void Step(float dt)
        {
            clock = (clock + dt) % 8f;
            bool longDrift = clock < 2.4f;
            bool shortDrift = clock >= 5f && clock < 6f;
            IsDrifting = longDrift || shortDrift;
            float driftTime = longDrift ? clock : clock - 5f;

            DriftStage = 0;
            if (IsDrifting && driftTime >= 1.6f)
            {
                DriftStage = 2;
            }
            else if (IsDrifting && driftTime >= 0.8f)
            {
                DriftStage = 1;
            }
            DriftCharge01 = IsDrifting ? Mathf.Clamp01(driftTime / 1.6f) : 0f;
            IsBoosting = (clock >= 2.4f && clock < 3.6f) || (clock >= 6f && clock < 6.4f);

            // Drive: always turning right, a bit tighter while drifting.
            Speed = IsBoosting ? cruiseSpeed * 1.35f : cruiseSpeed;
            float turnRadius = IsDrifting ? radius * 0.8f : radius;
            transform.Rotate(0f, Speed / turnRadius * dt * Mathf.Rad2Deg, 0f);
            transform.position += transform.forward * Speed * dt;
        }

        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            clock = 0f;
            IsDrifting = false;
            DriftStage = 0;
            DriftCharge01 = 0f;
            IsBoosting = false;
        }
    }
}
