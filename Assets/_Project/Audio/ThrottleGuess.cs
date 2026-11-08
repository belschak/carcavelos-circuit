namespace CarcavelosCircuit.Audio
{
    // IKartState has no throttle value, so the engine sound guesses it from the change in speed (plain C#, tested).
    // Speeding up = full throttle (load 1), slowing down hard = throttle closed (load 0), cruising = in between.
    // The engine sounds a bit lower and quieter when the load is low, like a real kart when you lift off.
    public class ThrottleGuess
    {
        private const float AccelerationForFullLoad = 0.4f;  // m/s per second
        private const float DecelerationForNoLoad = -0.8f;   // m/s per second
        private const float CruiseLoad = 0.55f;
        private const float LoadChangePerSecond = 5f;        // how fast the load may change (0..1 per second)
        private const float Smoothing = 8f;                  // higher = reacts faster to the measured acceleration

        private float lastSpeed;
        private float acceleration;
        private bool hasLastSpeed;

        public float Load { get; private set; } = CruiseLoad;

        // Call once per frame with the kart's speed (m/s, reverse counts as positive). Returns the load 0..1.
        public float Tick(float speed, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return Load;
            }
            if (!hasLastSpeed)
            {
                lastSpeed = speed;
                hasLastSpeed = true;
            }

            // Smoothed acceleration, so one bumpy physics frame does not flip the sound.
            float measured = (speed - lastSpeed) / deltaTime;
            lastSpeed = speed;
            float blend = 1f - (float)System.Math.Exp(-deltaTime * Smoothing);
            acceleration = acceleration + (measured - acceleration) * blend;

            float target = CruiseLoad;
            if (acceleration > AccelerationForFullLoad)
            {
                target = 1f;
            }
            else if (acceleration < DecelerationForNoLoad)
            {
                target = 0f;
            }
            Load = EnginePitch.MoveLimited(Load, target, LoadChangePerSecond, LoadChangePerSecond, deltaTime);
            return Load;
        }

        // Forget the last speed: after a teleport (retry, R) the jump in speed is not real acceleration.
        public void Reset()
        {
            hasLastSpeed = false;
            acceleration = 0f;
            Load = CruiseLoad;
        }
    }
}
