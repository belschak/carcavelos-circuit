namespace CarcavelosCircuit.Driving
{
    // The drift charge rules (task D3) as plain C#: no GameObjects, no Unity calls, so they can be unit tested.
    // KartController tells it when a drift starts, how long it has lasted, and when the player lets go.
    // Charged long enough = a boost when released; released too early = nothing.
    public class DriftCharge
    {
        private readonly float stage1Seconds;      // charge needed for blue sparks
        private readonly float stage2Seconds;      // charge needed for orange sparks
        private readonly float stage1BoostSeconds; // boost length after a stage 1 release
        private readonly float stage2BoostSeconds; // boost length after a stage 2 release
        private float chargedSeconds;

        public DriftCharge(float stage1Seconds, float stage2Seconds, float stage1BoostSeconds, float stage2BoostSeconds)
        {
            this.stage1Seconds = stage1Seconds;
            this.stage2Seconds = stage2Seconds;
            this.stage1BoostSeconds = stage1BoostSeconds;
            this.stage2BoostSeconds = stage2BoostSeconds;
        }

        public bool IsActive { get; private set; }

        // +1 = drifting to the right, -1 = to the left. Fixed when the drift starts.
        public int Direction { get; private set; }

        // 0 = no sparks yet, 1 = blue, 2 = orange. Always 0 when no drift is running.
        public int Stage
        {
            get
            {
                if (!IsActive) return 0;
                if (chargedSeconds >= stage2Seconds) return 2;
                if (chargedSeconds >= stage1Seconds) return 1;
                return 0;
            }
        }

        // 0..1 for the HUD: 1 means fully charged (stage 2).
        public float Charge01
        {
            get
            {
                if (!IsActive || stage2Seconds <= 0f) return 0f;
                float charge = chargedSeconds / stage2Seconds;
                return charge > 1f ? 1f : charge;
            }
        }

        // A drift starts: charge back to zero. Positive direction = right, anything else = left.
        public void Begin(int direction)
        {
            IsActive = true;
            Direction = direction > 0 ? 1 : -1;
            chargedSeconds = 0f;
        }

        // Called every physics step while the drift key is held. chargeRate 1 = normal speed,
        // above 1 = charges faster (steering into the turn), below 1 = slower.
        public void Tick(float deltaSeconds, float chargeRate)
        {
            if (!IsActive) return;
            chargedSeconds += deltaSeconds * chargeRate;
        }

        // The player let go. Returns how many seconds of boost they earned (0 if released too early).
        public float Release()
        {
            int stage = Stage;
            Cancel();
            if (stage == 2) return stage2BoostSeconds;
            if (stage == 1) return stage1BoostSeconds;
            return 0f;
        }

        // The drift ends without a boost: too slow, in the air, hit a wall, input switched off, reset.
        public void Cancel()
        {
            IsActive = false;
            chargedSeconds = 0f;
        }
    }
}
