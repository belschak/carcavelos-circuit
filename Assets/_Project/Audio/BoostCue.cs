namespace CarcavelosCircuit.Audio
{
    // When should the boost whoosh play (audit 11)? Plain C#, unit tested.
    // 1. The boost starts (IsBoosting goes from false to true): the normal case.
    // 2. A chained boost: the kart is STILL boosting when the next charged drift ends. Role 1's kart then makes
    //    the running boost longer, so IsBoosting stays true and rule 1 never fires. We spot it from the drift
    //    instead: a drift that had sparks (stage > 0) ended while boosting, on the ground, while driving.
    //    Not after a wall bump: a wall cancels the drift without a boost (KartController), so that is no new boost.
    public class BoostCue
    {
        private bool wasBoosting;
        private bool wasDrifting;
        private int lastStage;

        // Call once per frame with the kart's values. Returns true on the frame the whoosh should play.
        public bool Update(bool boosting, bool drifting, int driftStage, bool grounded, bool canDrive, bool bumpedThisFrame)
        {
            bool started = boosting && !wasBoosting;
            bool chargedDriftEnded = wasDrifting && !drifting && lastStage > 0;
            bool chained = boosting && wasBoosting && chargedDriftEnded && grounded && canDrive && !bumpedThisFrame;

            wasBoosting = boosting;
            wasDrifting = drifting;
            lastStage = drifting ? driftStage : 0;
            return started || chained;
        }

        // After a teleport (ResetTo clears drift and boost): start fresh.
        public void Reset()
        {
            wasBoosting = false;
            wasDrifting = false;
            lastStage = 0;
        }
    }
}
