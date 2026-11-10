namespace CarcavelosCircuit.RaceFlow
{
    // Which item a box gives (task R7). Plain C#, Edit Mode tested.
    // Catch-up rule: the further behind your best lap you are (the split at the last checkpoint), the better the
    // odds for the big items. A slow lap can still be saved, a lap that is already fast mostly gets small help.
    // Without a best lap yet (first lap, or before the first checkpoint) the "level" odds are used.
    public static class ItemOdds
    {
        // Chances in percent per situation: Bica, Triple Bica, Onda (each row adds up to 100).
        private static readonly int[] Ahead = { 60, 30, 10 };
        private static readonly int[] Level = { 45, 35, 20 };
        private static readonly int[] Behind = { 30, 40, 30 };
        private static readonly int[] FarBehind = { 15, 40, 45 };

        // behindSeconds = last split minus the best lap's split (positive = slower than the best).
        // levelSeconds: within +- this the lap counts as level. farBehindSeconds: from here the best odds.
        public static int[] WeightsFor(bool knowsGap, float behindSeconds, float levelSeconds, float farBehindSeconds)
        {
            if (!knowsGap)
            {
                return Level;
            }
            if (behindSeconds < -levelSeconds)
            {
                return Ahead;
            }
            if (behindSeconds <= levelSeconds)
            {
                return Level;
            }
            return behindSeconds < farBehindSeconds ? Behind : FarBehind;
        }

        // roll01 = a random number from 0 (inclusive) to 1 (exclusive), e.g. UnityEngine.Random.value.
        // Passed in from outside so a test can pick the exact number.
        public static ItemKind Pick(int[] weights, float roll01)
        {
            int total = weights[0] + weights[1] + weights[2];
            float target = roll01 * total;
            if (target < weights[0])
            {
                return ItemKind.Bica;
            }
            if (target < weights[0] + weights[1])
            {
                return ItemKind.TripleBica;
            }
            return ItemKind.Onda;
        }
    }
}
