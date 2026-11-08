namespace CarcavelosCircuit.Audio
{
    // The maths behind the engine sound, as plain C# so it can be unit tested.
    // One looping engine recording sounds faster when it plays at a higher pitch (like a tape played faster).
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

        // 0 -> minPitch (idle), 1 -> maxPitch (top speed), straight line in between.
        public static float FromSpeed01(float speed01, float minPitch, float maxPitch)
        {
            if (speed01 < 0f) speed01 = 0f;
            if (speed01 > 1f) speed01 = 1f;
            return minPitch + (maxPitch - minPitch) * speed01;
        }

        // Moves the current value towards the target by a share that does not depend on the frame rate.
        // sharpness 6 = after 1/6 s about 63 % of the way there, at 30 or 144 FPS alike.
        public static float Smooth(float current, float target, float sharpness, float deltaTime)
        {
            float share = 1f - (float)System.Math.Exp(-sharpness * deltaTime);
            return current + (target - current) * share;
        }
    }
}
