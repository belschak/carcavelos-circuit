namespace CarcavelosCircuit.Driving
{
    // Task D11: the trick in the air as plain C# (no Unity objects), so it can be unit tested.
    // KartController tells it when the kart takes off, when the trick key is pressed, how much time passed
    // and when the kart lands. One trick per jump. A trick that is finished before landing gives a short boost
    // on landing; a trick that is still running at landing, or no trick at all, gives nothing.
    public class TrickRule
    {
        // A bad tuning value (0 or negative) can never make a trick instant.
        private const float ShortestTrickSeconds = 0.1f;

        private readonly float trickSeconds; // how long one spin takes
        private readonly float boostSeconds; // boost after a clean landing
        private float secondsLeft;
        private bool usedThisJump;
        private bool finished;

        public TrickRule(float trickSeconds, float boostSeconds)
        {
            this.trickSeconds = trickSeconds > ShortestTrickSeconds ? trickSeconds : ShortestTrickSeconds;
            this.boostSeconds = boostSeconds > 0f ? boostSeconds : 0f;
        }

        public bool IsRunning { get; private set; }

        // 0..1 how far the spin is (the model turns 360 degrees over it). 0 when no trick is running.
        public float Progress01
        {
            get { return IsRunning ? 1f - secondsLeft / trickSeconds : 0f; }
        }

        // The kart just left the ground: a new jump, so one new trick is allowed.
        public void TakeOff()
        {
            Reset();
        }

        // The trick key was pressed. Returns true if a trick starts: only in the air and only once per jump.
        public bool TryStart(bool airborne)
        {
            if (!airborne || usedThisJump)
            {
                return false;
            }
            usedThisJump = true;
            IsRunning = true;
            finished = false;
            secondsLeft = trickSeconds;
            return true;
        }

        // Called every physics step while the kart is in the air.
        public void Tick(float deltaSeconds)
        {
            if (!IsRunning || !(deltaSeconds > 0f)) // also skips NaN
            {
                return;
            }
            secondsLeft -= deltaSeconds;
            if (secondsLeft <= 0f)
            {
                secondsLeft = 0f;
                IsRunning = false;
                finished = true;
            }
        }

        // The kart just touched the ground. Returns the boost seconds it earned (0 = none).
        public float Land()
        {
            float earned = finished ? boostSeconds : 0f;
            Reset();
            return earned;
        }

        // Back to "no trick" (landing, ResetTo for retry and recovery).
        public void Reset()
        {
            IsRunning = false;
            finished = false;
            usedThisJump = false;
            secondsLeft = 0f;
        }
    }
}
