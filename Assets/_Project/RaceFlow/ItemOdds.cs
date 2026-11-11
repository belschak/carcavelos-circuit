namespace CarcavelosCircuit.RaceFlow
{
    // Which item a box gives (task R7). Plain C#, Edit Mode tested.
    // Catch-up rule: the further behind your best lap you are (the split at the last checkpoint), the better the
    // odds for the big items. A slow lap can still be saved, a lap that is already fast mostly gets small help.
    // Dean's List never comes when you are ahead: it is the rescue item.
    // Without a best lap yet (first lap, or before the first checkpoint) the "level" odds are used.
    public static class ItemOdds
    {
        // Chances in percent per situation, in ItemKind order: Espresso, Seagull Delivery, Carcavelos Wave,
        // Dean's List (each row adds up to 100).
        private static readonly int[] Ahead = { 60, 30, 10, 0 };
        private static readonly int[] Level = { 40, 30, 22, 8 };
        private static readonly int[] Behind = { 25, 30, 30, 15 };
        private static readonly int[] FarBehind = { 10, 25, 35, 30 };

        // The items in the same order as the weights.
        private static readonly ItemKind[] Order =
            { ItemKind.Espresso, ItemKind.SeagullDelivery, ItemKind.CarcavelosWave, ItemKind.DeansList };

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
        // Walks along the weights: the roll lands in one item's share (a weight of 0 never wins).
        public static ItemKind Pick(int[] weights, float roll01)
        {
            int total = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                total += weights[i];
            }
            float target = roll01 * total;
            int upTo = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                upTo += weights[i];
                if (target < upTo)
                {
                    return Order[i];
                }
            }
            return Order[weights.Length - 1]; // roll01 = 1 exactly (should not happen): the last item
        }
    }
}
