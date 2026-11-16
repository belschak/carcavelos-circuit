namespace CarcavelosCircuit.Visuals
{
    // What a stuck bot should do now (BotStuckCheck.Update).
    public enum BotRecovery
    {
        Drive,    // all fine, follow the racing line
        Reverse,  // stuck: back up and steer out
        PutBack   // backing up did not help several times (or the bot fell off): put it back on the racing line
    }

    // Task V10, rule 5: a bot that stands still for stuckSeconds while it may drive (against a wall, in a pile-up)
    // reverses for reverseSeconds and then tries again. After maxTries tries in a row without getting going again
    // it asks to be put back on the racing line (like the player's Recover key). Plain C#, Edit Mode tested.
    public class BotStuckCheck
    {
        private readonly float stuckSeconds;
        private readonly float stillSpeed;
        private readonly float freeSpeed;
        private readonly float reverseSeconds;
        private readonly int maxTries;

        private float stillTime;   // seconds slower than stillSpeed in a row
        private float reverseLeft; // seconds of backing up left (0 = driving forwards)
        private int tries;         // reverses since the bot last drove freely

        public int Tries { get { return tries; } }
        public bool IsReversing { get { return reverseLeft > 0f; } }

        // stillSpeed (m/s): slower counts as standing. freeSpeed (m/s): this fast again = the bot got out, tries reset.
        public BotStuckCheck(float stuckSeconds = 2f, float stillSpeed = 1.5f, float freeSpeed = 6f,
            float reverseSeconds = 1.2f, int maxTries = 3)
        {
            this.stuckSeconds = stuckSeconds;
            this.stillSpeed = stillSpeed;
            this.freeSpeed = freeSpeed;
            this.reverseSeconds = reverseSeconds;
            this.maxTries = maxTries;
        }

        // Call every frame while the bot may drive, with its forward speed (m/s, sign does not matter).
        public BotRecovery Update(float speed, float deltaTime)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed))
            {
                speed = 0f; // no number = we do not know that it moves
            }
            if (float.IsNaN(deltaTime) || deltaTime < 0f)
            {
                deltaTime = 0f;
            }
            float absSpeed = speed < 0f ? -speed : speed;

            if (reverseLeft > 0f)
            {
                reverseLeft -= deltaTime;
                return BotRecovery.Reverse;
            }
            if (absSpeed >= freeSpeed)
            {
                tries = 0; // driving properly again
            }
            stillTime = absSpeed < stillSpeed ? stillTime + deltaTime : 0f;
            if (stillTime < stuckSeconds)
            {
                return BotRecovery.Drive;
            }

            stillTime = 0f;
            tries++;
            if (tries > maxTries)
            {
                tries = 0;
                return BotRecovery.PutBack;
            }
            reverseLeft = reverseSeconds;
            return BotRecovery.Reverse;
        }

        // Countdown, pause, results, Retry, after being put back: start counting from zero.
        public void Reset()
        {
            stillTime = 0f;
            reverseLeft = 0f;
            tries = 0;
        }
    }
}
