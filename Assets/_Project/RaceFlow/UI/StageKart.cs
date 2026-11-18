using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // One kart in the studio: tells the model (Role 3's KartVisualFromState reads IKartState from its parent) to stand
    // still and steer gently left and right, so the line-up looks alive. Moves nothing else.
    public class StageKart : MonoBehaviour, IKartState
    {
        public Transform Model { get; set; }
        public float Phase { get; set; }
        public float SwayAmount { get; set; } = 0.35f;

        public float Speed { get { return 0f; } }
        public float MaxSpeed { get { return 30f; } }
        public bool IsDrifting { get { return false; } }
        public float DriftCharge01 { get { return 0f; } }
        public int DriftStage { get { return 0; } }
        public bool IsBoosting { get { return false; } }
        public bool IsGrounded { get { return true; } }
        public float Steer { get { return Mathf.Sin(Time.unscaledTime * 0.9f + Phase) * SwayAmount; } }
        public bool InputEnabled { get; set; }

        public void ResetTo(Vector3 position, Quaternion rotation)
        {
        }

        private void Update()
        {
            if (Model != null)
            {
                // a soft idle bob, like an engine ticking over
                float bob = Mathf.Sin(Time.unscaledTime * 7.5f + Phase) * 0.004f;
                Model.localPosition = new Vector3(0f, bob, 0f);
            }
        }
    }
}
