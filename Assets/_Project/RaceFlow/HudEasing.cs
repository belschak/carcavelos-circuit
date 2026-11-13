namespace CarcavelosCircuit.RaceFlow
{
    // Small maths for HUD animations that C# drives itself (the results time counting up, the breathing pill).
    // Everything else is animated by USS transitions. Plain C#, so Edit Mode tests can check it.
    public static class HudEasing
    {
        // Fast start, soft landing: 1 - (1 - t)^3. t is clamped to 0..1.
        public static float EaseOutCubic(float t)
        {
            t = Clamp01(t);
            float rest = 1f - t;
            return 1f - rest * rest * rest;
        }

        // Progress 0..1 of a count-up that waits `delay` seconds and then runs for `duration` seconds, eased.
        // elapsed = seconds since the results screen opened.
        public static float CountUp(float elapsed, float delay, float duration)
        {
            if (duration <= 0f)
            {
                return elapsed >= delay ? 1f : 0f;
            }
            return EaseOutCubic((elapsed - delay) / duration);
        }

        // "Overshoot and settle" (the same curve as USS ease-out-back): starts fast, goes about 10 % past the end,
        // comes back to exactly 1. Used for punches drawn from C# (the position number). t is clamped to 0..1.
        public static float OutBack(float t)
        {
            t = Clamp01(t);
            const float back = 1.70158f;   // the usual overshoot amount for this curve
            float u = t - 1f;
            return 1f + (back + 1f) * u * u * u + back * u * u;
        }

        private static float Clamp01(float t)
        {
            if (t < 0f) return 0f;
            return t > 1f ? 1f : t;
        }
    }
}
