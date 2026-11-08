namespace CarcavelosCircuit.Audio
{
    // The maths behind the engine sound, as plain C# so it can be unit tested.
    // The engine is two looping recordings (Luc's kart_engine_low and kart_engine_high).
    // A recording sounds faster when it plays at a higher pitch (like a tape played faster),
    // and between low and high revs the low loop fades out while the high loop fades in.
    public static class EnginePitch
    {
        // Speed as a number from 0 (standing) to 1 (top speed). Reverse counts as positive, boosting caps at 1.
        public static float Speed01(float speed, float maxSpeed)
        {
            if (maxSpeed <= 0f)
            {
                return 0f;
            }
            float value = System.Math.Abs(speed) / maxSpeed;
            if (value > 1f) value = 1f;
            return value;
        }

        // Moves "current" towards "target", but never faster than the given rates (units per second).
        // The engine revs up a bit slower (rise) than it drops (fall), so it never jumps.
        public static float MoveLimited(float current, float target, float risePerSecond, float fallPerSecond, float deltaTime)
        {
            if (target > current)
            {
                float up = current + risePerSecond * deltaTime;
                return up > target ? target : up;
            }
            float down = current - fallPerSecond * deltaTime;
            return down < target ? target : down;
        }

        // How much of the HIGH loop we hear: 0 below r = 0.2, 1 above r = 0.7,
        // and a smooth S-curve ("smoothstep") in between. The low loop gets the rest (1 - this).
        public static float HighLoopMix(float r)
        {
            float t = (r - 0.2f) / 0.5f;
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return t * t * (3f - 2f * t);
        }

        // Pitch of each loop by revs r (0..1). Numbers from Luc's audio handoff (section 6).
        public static float LowLoopPitch(float r)
        {
            return 0.75f + 0.85f * r;
        }

        public static float HighLoopPitch(float r)
        {
            return 0.8f + 0.55f * r;
        }

        // Equal-power crossfade: the two recordings are different sounds, so a straight 50/50 split sounds
        // about 3 dB quieter in the middle. Cosine and sine keep low^2 + high^2 = 1, so the loudness stays the same.
        public static float EqualPowerLow(float highMix)
        {
            return (float)System.Math.Cos(Clamp01(highMix) * System.Math.PI * 0.5);
        }

        public static float EqualPowerHigh(float highMix)
        {
            return (float)System.Math.Sin(Clamp01(highMix) * System.Math.PI * 0.5);
        }

        // ---- Virtual gearbox (the kart has no real gears, the sound pretends it has) ----

        // Speed share (0..1) at which gear "gear" (1-based) runs out of revs. Lower gears are shorter,
        // like a real gearbox: with 4 gears the tops are about 0.31, 0.55, 0.78 and 1.
        public static float GearTop(int gear, int gears)
        {
            if (gear <= 0 || gears <= 0)
            {
                return 0f;
            }
            if (gear >= gears)
            {
                return 1f;
            }
            return (float)System.Math.Pow(gear / (double)gears, 0.85);
        }

        // Revs (0..1) inside the current gear: gear 1 starts at 0, higher gears start at revsAfterShift
        // (the revs drop after an upshift), and every gear reaches 0.97 at its top.
        public static float GearRevs(float speed01, int gear, int gears, float revsAfterShift)
        {
            float low = GearTop(gear - 1, gears);
            float high = GearTop(gear, gears);
            float fraction = Clamp01((speed01 - low) / System.Math.Max(0.001f, high - low));
            float start = gear <= 1 ? 0f : revsAfterShift;
            return start + (0.97f - start) * fraction;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
