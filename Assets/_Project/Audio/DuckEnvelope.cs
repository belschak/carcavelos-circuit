namespace CarcavelosCircuit.Audio
{
    // "Ducking": the music gets quieter for a moment while an important sound plays (beep, chime, jingle), then
    // comes back. Fast down (attack), slow back up (release): the slow return makes it sound like the "pumping"
    // of a radio mix instead of a volume jump. Plain C#, unit tested.
    // Unity's Web build has no Duck Volume mixer effect, so this does it in script.
    public class DuckEnvelope
    {
        private readonly float attackDbPerSecond;
        private readonly float releaseDbPerSecond;
        private float depthDb;          // how deep the current duck goes (negative dB, 0 = no duck)
        private float holdSecondsLeft;  // how long the music stays down before it comes back

        public float CurrentDb { get; private set; }

        public DuckEnvelope(float attackDbPerSecond, float releaseDbPerSecond)
        {
            this.attackDbPerSecond = attackDbPerSecond;
            this.releaseDbPerSecond = releaseDbPerSecond;
        }

        // Duck by "depth" dB (e.g. -4) and stay down for holdSeconds. If two ducks overlap, the deeper one and the
        // longer hold win, so a small duck can never cut a deep one short.
        public void Duck(float depth, float holdSeconds)
        {
            if (depth < depthDb)
            {
                depthDb = depth;
            }
            if (holdSeconds > holdSecondsLeft)
            {
                holdSecondsLeft = holdSeconds;
            }
        }

        // Call once per frame. Returns the duck in dB (0 = music at its normal level).
        public float Tick(float deltaTime)
        {
            if (holdSecondsLeft > 0f)
            {
                holdSecondsLeft -= deltaTime;
            }
            else
            {
                depthDb = 0f; // hold is over: let the music come back
            }
            // Going down (more negative) is the attack, coming back up is the release.
            CurrentDb = EnginePitch.MoveLimited(CurrentDb, depthDb, releaseDbPerSecond, attackDbPerSecond, deltaTime);
            return CurrentDb;
        }

        // No duck at all, right away (retry).
        public void Clear()
        {
            depthDb = 0f;
            holdSecondsLeft = 0f;
            CurrentDb = 0f;
        }
    }
}
