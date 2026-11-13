namespace CarcavelosCircuit.RaceFlow
{
    // Decides when a kart that fell off the track is put back automatically. Plain C#, so it can be tested.
    // After every recover the check pauses for a moment: a physics kart may only arrive at the respawn point in the
    // next physics step. Without the pause it would still be "below the track" for a few frames and be recovered
    // again and again (several respawn whooshes in a row).
    public class FallCheck
    {
        private readonly float fallHeight;
        private readonly float cooldownSeconds;
        private float pausedUntil = float.MinValue;

        public FallCheck(float fallHeight, float cooldownSeconds)
        {
            this.fallHeight = fallHeight;
            this.cooldownSeconds = cooldownSeconds;
        }

        // True if the kart is below the fall height and we are not in the pause after a recover. now = Time.time.
        public bool HasFallen(float kartHeight, float now)
        {
            if (now < pausedUntil)
            {
                return false;
            }
            return kartHeight < fallHeight;
        }

        // Call after every recover (R or a fall), so the fall check waits cooldownSeconds.
        public void NoteRecovered(float now)
        {
            pausedUntil = now + cooldownSeconds;
        }
    }
}
