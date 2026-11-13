namespace CarcavelosCircuit.RaceFlow
{
    // The rules of the camera look (help for Role 4, task "AAA camera look"), plain C# so Edit Mode tests can check
    // them without a scene. RaceCameraLook (MonoBehaviour) only reads the kart and the quality level and calls these.
    public static class CameraLookRules
    {
        // Share of top speed where the speed effects start (below it the picture stays clean).
        public const float SpeedFeelFrom = 0.6f;

        // How strong the speed effects are (0..1): nothing below 60 % of top speed, then a smooth rise to 1 at top
        // speed; a boost (drift boost, pad, item) always gives the full rush. Invalid numbers (no top speed yet,
        // NaN, negative speed while reversing) give 0, so the picture never flickers.
        public static float SpeedWeight(float speed, float maxSpeed, bool boosting)
        {
            if (boosting)
            {
                return 1f;
            }
            if (!(maxSpeed > 0f) || !(speed > 0f))
            {
                return 0f; // also catches NaN: every comparison with NaN is false
            }
            float t = (speed / maxSpeed - SpeedFeelFrom) / (1f - SpeedFeelFrom);
            if (t <= 0f)
            {
                return 0f;
            }
            if (t >= 1f)
            {
                return 1f;
            }
            return t * t * (3f - 2f * t); // smoothstep: starts and ends softly
        }

        // Expensive effects (motion blur, lens flare, film grain, depth of field, SMAA) only where the GPU has room:
        // not on phones and not in the "Web" or "Mobile" quality levels (the Web build also runs on phones).
        public static bool IsHighEnd(string qualityLevelName, bool isMobileDevice)
        {
            if (isMobileDevice)
            {
                return false;
            }
            return qualityLevelName != "Web" && qualityLevelName != "Mobile";
        }

        // Moves current towards target, the same share per second at any frame rate (no jumps when a frame is slow).
        // rate = how fast, per second; deltaTime <= 0 (paused) keeps the value.
        public static float Approach(float current, float target, float rate, float deltaTime)
        {
            if (!(deltaTime > 0f) || !(rate > 0f))
            {
                return current;
            }
            float share = 1f - (float)System.Math.Exp(-rate * deltaTime);
            return current + (target - current) * share;
        }
    }
}
