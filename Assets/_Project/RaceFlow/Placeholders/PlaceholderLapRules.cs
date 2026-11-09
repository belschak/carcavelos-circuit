namespace CarcavelosCircuit.RaceFlow
{
    // STAND-IN for Noah's RaceRules (Role 2, task T5) until it is merged.
    // It has exactly the same public methods as the RaceRules contract in docs/interfaces.md,
    // so the swap later is: replace the type name "PlaceholderLapRules" with "RaceRules" in RaceManager.
    // Checkpoint 0 is the start/finish line; the others must be passed in order 1, 2, 3, ...
    public class PlaceholderLapRules
    {
        private readonly int checkpointCount;
        private int nextExpected;

        // Index of the last checkpoint passed correctly (0 = the start line). Used for recovery.
        public int LastValidCheckpoint { get; private set; }

        public PlaceholderLapRules(int checkpointCount)
        {
            this.checkpointCount = checkpointCount;
            Reset();
        }

        // Called when the countdown ends.
        public void StartLap()
        {
            Reset();
        }

        // True only if this is the next checkpoint we expect. Wrong order or a repeat = false.
        public bool PassCheckpoint(int index)
        {
            if (index != nextExpected || index <= 0 || index >= checkpointCount)
            {
                return false;
            }
            LastValidCheckpoint = index;
            nextExpected = nextExpected + 1;
            return true;
        }

        // True only if every checkpoint was passed in order. A second call is false (no double finish).
        public bool TryFinish()
        {
            if (nextExpected != checkpointCount)
            {
                return false;
            }
            nextExpected = checkpointCount + 1; // the lap is over, nothing more counts
            return true;
        }

        public void Reset()
        {
            nextExpected = 1;
            LastValidCheckpoint = 0;
        }
    }
}
