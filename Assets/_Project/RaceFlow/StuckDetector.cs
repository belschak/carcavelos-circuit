namespace CarcavelosCircuit.RaceFlow
{
    // Decides when the HUD shows "Stuck? Press R": the kart has been (almost) standing still for a few seconds
    // while racing, for example pushed into a wall. Plain C#, so Edit Mode tests can check it.
    // It only "arms" after the kart has really driven once (since GO or the last recover), so a player who just
    // waits at the start line does not get told to press R.
    public class StuckDetector
    {
        private readonly float stuckAfterSeconds;
        private readonly float stillSpeed;
        private readonly float armSpeed;

        private bool armed;
        private float stillTime;

        public bool IsStuck { get { return armed && stillTime >= stuckAfterSeconds; } }

        // stuckAfterSeconds: how long the kart must stand still. stillSpeed (m/s): slower than this counts as still
        // (1.5 m/s = 5.4 km/h, a kart grinding along a wall). armSpeed (m/s): the kart must have been this fast once.
        public StuckDetector(float stuckAfterSeconds = 3f, float stillSpeed = 1.5f, float armSpeed = 3f)
        {
            this.stuckAfterSeconds = stuckAfterSeconds;
            this.stillSpeed = stillSpeed;
            this.armSpeed = armSpeed;
        }

        // Call once per frame while racing with the kart's speed (sign does not matter). Returns IsStuck.
        public bool Update(float speed, float deltaTime)
        {
            float absSpeed = speed < 0f ? -speed : speed;
            if (absSpeed >= armSpeed)
            {
                armed = true;
            }
            if (absSpeed < stillSpeed)
            {
                stillTime += deltaTime;
            }
            else
            {
                stillTime = 0f;
            }
            return IsStuck;
        }

        // After GO, Retry or Recover: wait for the kart to drive again.
        public void Reset()
        {
            armed = false;
            stillTime = 0f;
        }
    }
}
