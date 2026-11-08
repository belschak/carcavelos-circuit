namespace CarcavelosCircuit.RaceFlow
{
    // The "lap report" on the results card: top speed, drifts, boosts, air time, recoveries.
    // RaceManager feeds it the kart's flags (IKartState) once per frame while racing. Plain C#, so Edit Mode tests
    // can drive it with fake frames.
    // Drifts and boosts are counted on the RISING EDGE: the frame a flag turns from false to true. A drift that
    // lasts 3 s is one drift, not 180 frames of drifting.
    public class RunStats
    {
        public float TopSpeed { get; private set; }            // m/s, the fastest moment of the lap (reverse counts too)
        public int Drifts { get; private set; }                // drifts started
        public int Boosts { get; private set; }                // boosts started (small and big)
        public float AirSeconds { get; private set; }          // total time with no wheels on the ground
        public float LongestJumpSeconds { get; private set; }  // longest single time in the air
        public int Recoveries { get; private set; }            // R presses and automatic recovers after a fall

        private bool wasDrifting;
        private bool wasBoosting;
        private float currentJumpSeconds;

        // One frame of racing.
        public void Tick(float deltaTime, float speed, bool drifting, bool boosting, bool grounded)
        {
            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }
            float absSpeed = speed < 0f ? -speed : speed;
            if (absSpeed > TopSpeed)
            {
                TopSpeed = absSpeed;
            }

            if (drifting && !wasDrifting)
            {
                Drifts++;
            }
            if (boosting && !wasBoosting)
            {
                Boosts++;
            }
            wasDrifting = drifting;
            wasBoosting = boosting;

            if (!grounded)
            {
                AirSeconds += deltaTime;
                currentJumpSeconds += deltaTime;
                if (currentJumpSeconds > LongestJumpSeconds)
                {
                    LongestJumpSeconds = currentJumpSeconds;
                }
            }
            else
            {
                currentJumpSeconds = 0f; // landed: the next jump starts from zero
            }
        }

        // The kart was put back on the track (R or a fall).
        public void NoteRecovery()
        {
            Recoveries++;
            currentJumpSeconds = 0f; // the teleport ends the jump; the fall before it still counts as air time
        }

        // A new attempt (GO or Retry): everything back to zero.
        public void Reset()
        {
            TopSpeed = 0f;
            Drifts = 0;
            Boosts = 0;
            AirSeconds = 0f;
            LongestJumpSeconds = 0f;
            Recoveries = 0;
            wasDrifting = false;
            wasBoosting = false;
            currentJumpSeconds = 0f;
        }
    }
}
