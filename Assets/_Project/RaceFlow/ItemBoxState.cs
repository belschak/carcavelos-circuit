namespace CarcavelosCircuit.RaceFlow
{
    // One item box's life (task R7): there -> hit (breaks, gone) -> grows back after a few seconds.
    // Plain C#, Edit Mode tested. ItemBox (the MonoBehaviour) only shows what this says.
    public class ItemBoxState
    {
        public bool IsAvailable { get; private set; } = true;

        private float secondsUntilBack;

        // The kart drove through. True if the box was there (it breaks), false if it is still growing back.
        public bool TryBreak(float respawnSeconds)
        {
            if (!IsAvailable)
            {
                return false;
            }
            IsAvailable = false;
            secondsUntilBack = respawnSeconds;
            return true;
        }

        // Called every frame. True only on the frame the box comes back (its "pop in" animation starts).
        public bool Tick(float deltaSeconds)
        {
            if (IsAvailable)
            {
                return false;
            }
            secondsUntilBack -= deltaSeconds;
            if (secondsUntilBack > 0f)
            {
                return false;
            }
            IsAvailable = true;
            return true;
        }

        // Retry: every box is there again at once.
        public void Reset()
        {
            IsAvailable = true;
            secondsUntilBack = 0f;
        }
    }
}
