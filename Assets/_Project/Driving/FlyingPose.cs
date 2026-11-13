using UnityEngine;

namespace CarcavelosCircuit.Driving
{
    // Task D10: how the kart model leans in the air. Plain C# (only maths, no objects), so it can be unit tested.
    // Looks only: KartController applies it to the Visual child, the physics body never tilts.
    public static class FlyingPose
    {
        // The wobble grows in over this many seconds after take-off, so it never starts with a jerk.
        private const float WobbleFadeInSeconds = 0.25f;

        // Nose angle in degrees, positive = nose up, negative = nose down.
        // 0 on the ground. In the air it follows the vertical speed: rising = nose up, falling = nose down,
        // full angle at fullPitchSpeed (m/s) and never more than maxPitchDegrees.
        public static float PitchDegrees(bool grounded, float verticalSpeed, float maxPitchDegrees, float fullPitchSpeed)
        {
            if (grounded || float.IsNaN(verticalSpeed) || maxPitchDegrees <= 0f || fullPitchSpeed <= 0f)
            {
                return 0f; // on the ground, or a bad number: stay level
            }
            float share = Mathf.Clamp(verticalSpeed / fullPitchSpeed, -1f, 1f);
            return share * maxPitchDegrees;
        }

        // Small side-to-side wobble in degrees (a roll), only in the air, never more than wobbleDegrees.
        public static float RollDegrees(bool grounded, float airSeconds, float wobbleDegrees, float wobbleHertz)
        {
            if (grounded || airSeconds <= 0f || wobbleDegrees <= 0f)
            {
                return 0f;
            }
            float fadeIn = Mathf.Clamp01(airSeconds / WobbleFadeInSeconds);
            return Mathf.Sin(airSeconds * wobbleHertz * 2f * Mathf.PI) * wobbleDegrees * fadeIn;
        }
    }
}
