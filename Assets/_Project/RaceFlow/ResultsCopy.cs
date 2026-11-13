namespace CarcavelosCircuit.RaceFlow
{
    // The words on the results card (task R15). Plain C#, tested in ResultsCopyTests, so the card can never say two
    // things that fight each other (the old card showed "TOP TIER: BEAT YOUR OWN TIME" right above "SLOWER THAN YOUR
    // BEST": it read like a contradiction) and never shows a "0" that looks broken.
    // Arcade rule: celebrate first (medal, time), then details in small words.
    public static class ResultsCopy
    {
        // The words in the small delta chip, after "+0.504 s" / "-0.320 s". Follows the ROUNDED milliseconds on screen,
        // so "+0.000 s" never says "new best".
        public static string DeltaWords(int deltaMilliseconds)
        {
            if (deltaMilliseconds < 0)
            {
                return "NEW BEST!";
            }
            if (deltaMilliseconds == 0)
            {
                return "TIED YOUR BEST";
            }
            return "OFF YOUR BEST";
        }

        // The line under the results: what this lap's medal means and what to aim for next.
        // medal = this lap, bestBefore = your best medal before the lap, bestNow = your best medal including this lap,
        // next = the medal after this lap's one (None at the top), nextTime = its lap time as text ("" if unknown).
        public static string MedalLine(Medal medal, Medal bestBefore, Medal bestNow, Medal next, string nextTime)
        {
            bool isNew = medal != Medal.None && medal > bestBefore;
            if (medal < bestNow)
            {
                return "YOUR BEST MEDAL  " + MedalRules.Name(bestNow); // an older lap was better: no "next" advice
            }
            if (medal == Medal.DeansList)
            {
                // The top: nothing left to unlock, so no advice that could clash with a slower lap.
                return isNew ? "NEW MEDAL!  TOP TIER REACHED" : "HIGHEST MEDAL ON THIS TRACK";
            }
            if (next == Medal.None || string.IsNullOrEmpty(nextTime))
            {
                return ""; // medal times broken (RaceManager logs the error): say nothing rather than something wrong
            }
            string start = isNew ? "NEW MEDAL!  NEXT  " : "NEXT  ";
            return start + MedalRules.Name(next) + "  " + nextTime;
        }

        // A count on the lap report (drifts, boosts, top speed). Nothing measured = "-", never a "0" that looks broken.
        public static string Count(int value)
        {
            return value <= 0 ? "-" : HudFormat.Whole(value);
        }

        // Air time on the lap report: "1.4 s", or "-" when the kart never really left the ground (less than 0.05 s
        // would show as "0.0 s").
        public static string AirTime(float seconds)
        {
            if (float.IsNaN(seconds) || seconds < 0.05f)
            {
                return "-";
            }
            return HudFormat.OneDecimal(seconds) + " s";
        }

        // Should the stat show dimmed (nothing measured)? Same rule as Count and AirTime.
        public static bool IsEmptyCount(int value)
        {
            return value <= 0;
        }

        public static bool IsEmptyAirTime(float seconds)
        {
            return float.IsNaN(seconds) || seconds < 0.05f;
        }
    }
}
