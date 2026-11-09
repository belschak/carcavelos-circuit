namespace CarcavelosCircuit.RaceFlow
{
    // Which medal a lap time earns. Plain C#, so Edit Mode tests can check every boundary.
    // The thresholds come from RaceSettings as an array of 4 lap times in seconds:
    //   [0] = Bronze, [1] = Silver, [2] = Gold, [3] = Dean's List, each one faster (smaller) than the one before.
    // Times are compared in whole milliseconds, like the HUD shows them, so a lap that reads "0:10.200"
    // always earns a medal whose target reads "0:10.200".
    public static class MedalRules
    {
        public const int TierCount = 4;

        // True if there are exactly 4 positive thresholds and each tier is strictly faster than the one below it.
        public static bool AreValid(float[] thresholds)
        {
            if (thresholds == null || thresholds.Length != TierCount)
            {
                return false;
            }
            for (int i = 0; i < TierCount; i++)
            {
                if (thresholds[i] <= 0f)
                {
                    return false;
                }
                if (i > 0 && RaceTimer.ToMilliseconds(thresholds[i]) >= RaceTimer.ToMilliseconds(thresholds[i - 1]))
                {
                    return false; // out of order or equal: two tiers could never be told apart
                }
            }
            return true;
        }

        // The best medal this lap earns. Exactly on a threshold counts. No lap (0 or less) or broken thresholds = None.
        public static Medal For(float lapSeconds, float[] thresholds)
        {
            if (lapSeconds <= 0f || !AreValid(thresholds))
            {
                return Medal.None;
            }
            int lapMilliseconds = RaceTimer.ToMilliseconds(lapSeconds);
            Medal earned = Medal.None;
            for (int i = 0; i < TierCount; i++)
            {
                if (lapMilliseconds <= RaceTimer.ToMilliseconds(thresholds[i]))
                {
                    earned = (Medal)(i + 1); // thresholds[0] is Bronze = 1
                }
            }
            return earned;
        }

        // The next medal to aim for: one tier above what you have. None when you already have the top tier.
        public static Medal NextTarget(Medal current)
        {
            if (current >= Medal.DeansList)
            {
                return Medal.None;
            }
            return current + 1;
        }

        // The lap time needed for a medal, in seconds. -1 for None or broken thresholds.
        public static float ThresholdFor(Medal medal, float[] thresholds)
        {
            if (medal == Medal.None || !AreValid(thresholds))
            {
                return -1f;
            }
            return thresholds[(int)medal - 1];
        }

        // Upper-case name for the HUD. Constant strings, so the HUD never builds a new one.
        public static string Name(Medal medal)
        {
            switch (medal)
            {
                case Medal.Bronze: return "BRONZE";
                case Medal.Silver: return "SILVER";
                case Medal.Gold: return "GOLD";
                case Medal.DeansList: return "DEAN'S LIST";
                default: return "NO MEDAL";
            }
        }

        // USS class for the medal colour (tokens --medal-* in RaceHud.uss).
        public static string ColourClass(Medal medal)
        {
            switch (medal)
            {
                case Medal.Bronze: return "medal--bronze";
                case Medal.Silver: return "medal--silver";
                case Medal.Gold: return "medal--gold";
                case Medal.DeansList: return "medal--deans";
                default: return "medal--none";
            }
        }
    }
}
