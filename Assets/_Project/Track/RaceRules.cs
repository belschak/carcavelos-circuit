namespace CarcavelosCircuit.Track
{
    // The lap rules as plain C# (no MonoBehaviour, no scene objects) so they can be unit tested.
    // Checkpoint 0 is the start/finish line. Checkpoints 1..N-1 must be passed in this order,
    // then crossing the line again (TryFinish) ends the lap.
    public class RaceRules
    {
        private readonly int checkpointCount;

        // The next checkpoint index the kart has to pass. After the last one it is checkpointCount.
        private int nextExpected;

        // True once the lap is finished, so a second crossing of the line does not count again.
        private bool finished;

        // Index of the last checkpoint passed correctly (0 = the start line). Used for the respawn.
        public int LastValidCheckpoint { get; private set; }

        public RaceRules(int checkpointCount)
        {
            this.checkpointCount = checkpointCount;
            Reset();
        }

        // Called when the countdown ends: a fresh lap begins.
        public void StartLap()
        {
            Reset();
        }

        // True only if this is exactly the next expected checkpoint.
        // Wrong order, wrong direction, a repeat, the line itself (0) or an unknown index = false.
        public bool PassCheckpoint(int index)
        {
            if (finished)
            {
                return false;
            }
            if (index != nextExpected || index <= 0 || index >= checkpointCount)
            {
                return false;
            }
            LastValidCheckpoint = index;
            nextExpected = nextExpected + 1;
            return true;
        }

        // True only if every checkpoint 1..N-1 was passed in order. A second call is false.
        public bool TryFinish()
        {
            if (finished || nextExpected != checkpointCount)
            {
                return false;
            }
            finished = true;
            return true;
        }

        // Back to the start: used by Retry and StartLap.
        public void Reset()
        {
            nextExpected = 1;
            LastValidCheckpoint = 0;
            finished = false;
        }
    }
}
