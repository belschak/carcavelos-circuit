namespace CarcavelosCircuit.Audio
{
    // One volume level in dB that glides to a new target over a given time (plain C#, unit tested).
    // This replaces mixer snapshots: Unity's Web build only supports volumes, so we fade them in script.
    public class LevelFade
    {
        private float target;
        private float ratePerSecond; // dB per second for the current fade

        public float CurrentDb { get; private set; }

        public LevelFade(float startDb)
        {
            CurrentDb = startDb;
            target = startDb;
        }

        // Start a fade to targetDb that takes "seconds" (0 = jump right away).
        public void FadeTo(float targetDb, float seconds)
        {
            target = targetDb;
            float distance = System.Math.Abs(targetDb - CurrentDb);
            if (seconds <= 0f || distance <= 0f)
            {
                CurrentDb = targetDb;
                ratePerSecond = 0f;
                return;
            }
            ratePerSecond = distance / seconds;
        }

        // Call once per frame. Returns the level in dB.
        public float Tick(float deltaTime)
        {
            CurrentDb = EnginePitch.MoveLimited(CurrentDb, target, ratePerSecond, ratePerSecond, deltaTime);
            return CurrentDb;
        }
    }
}
