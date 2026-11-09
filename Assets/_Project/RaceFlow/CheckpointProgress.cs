namespace CarcavelosCircuit.RaceFlow
{
    // The checkpoint pips on the HUD ("CP 2/3" and one diamond per checkpoint). Plain C#, so Edit Mode tests can check it.
    // Checkpoint 0 is the start/finish line, so a track with checkpointCount = 4 has 3 checkpoints to pass (1, 2, 3)
    // and then the finish. The HUD draws one pip per checkpoint to pass plus one finish pip: checkpointCount pips.
    public static class CheckpointProgress
    {
        public enum PipState
        {
            Passed,   // already driven through (white)
            Next,     // the one to drive through now (pulsing outline)
            Ahead     // later (dim)
        }

        // How many checkpoints the player must pass before the finish (the start/finish line does not count).
        public static int CheckpointsToPass(int checkpointCount)
        {
            return checkpointCount > 1 ? checkpointCount - 1 : 0;
        }

        // pip 0 .. CheckpointsToPass-1 = checkpoints 1, 2, 3 ...; the last pip (index CheckpointsToPass) = the finish.
        // passed = how many checkpoints counted so far this lap (RaceManager.CheckpointsPassed).
        public static PipState StateOf(int pip, int passed)
        {
            if (pip < passed)
            {
                return PipState.Passed;
            }
            return pip == passed ? PipState.Next : PipState.Ahead;
        }
    }
}
