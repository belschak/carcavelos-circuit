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
            if (!knowsGap || float.IsNaN(behindSeconds))
            {
                return Level; // no gap, or a broken one (NaN): the plain odds
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

        // What a box gives: a wave box (a WaveBoxSpot on the track, V19) always the Carcavelos Wave, whatever the
        // odds; every other box rolls the odds with Pick.
        public static ItemKind PickForBox(bool waveBox, int[] weights, float roll01)
        {
            return waveBox ? ItemKind.CarcavelosWave : Pick(weights, roll01);
        }

        // roll01 = a random number from 0 (inclusive) to 1 (exclusive), e.g. UnityEngine.Random.value.
        // Passed in from outside so a test can pick the exact number.
        // Walks along the weights: the roll lands in one item's share (a weight of 0 never wins).
        // Rejected input (G-audit, 19 Nov): no weights, too many, negative or all zero weights give the Espresso; a roll
        // outside 0..1 or NaN is clamped. UnityEngine.Random.value CAN return exactly 1, and that used to fall through
        // to the last item even with weight 0 (a Dean's List while ahead): now 1 lands in the last item that can win.
        public static ItemKind Pick(int[] weights, float roll01)
        {
            if (weights == null || weights.Length == 0 || weights.Length > Order.Length)
            {
                return ItemKind.Espresso;
            }
            int total = 0;
            int lastWinner = -1;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0)
                {
                    total += weights[i];
                    lastWinner = i;
                }
            }
            if (total <= 0)
            {
                return ItemKind.Espresso;
            }
            float roll = float.IsNaN(roll01) ? 0f : (roll01 < 0f ? 0f : roll01);
            float target = roll * total;
            int upTo = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0)
                {
                    continue; // a weight of 0 (or less) never wins
                }
                upTo += weights[i];
                if (target < upTo)
                {
                    return Order[i];
                }
            }
            return Order[lastWinner]; // roll01 = 1 (or more): the last item that can win
        }
    }
}
