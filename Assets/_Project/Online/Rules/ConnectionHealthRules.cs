using System.Collections.Generic;

namespace CarcavelosCircuit.Online
{
    // Is my connection backed up? (plain C#, tested in ConnectionHealthRulesTests). Scale test, 17 Nov 2026: a client
    // on a very bad line (300 ms +- 80 ms jitter AND 1 % loss) got everything later and later: the other karts' newest
    // poses were 2 s old after the first race and 20 s old ten minutes later, and every GO came later (up to 35 s).
    // The others' results stayed right, but that player raced alone, far behind. A fresh connection starts with an
    // empty queue, so the lobby leaves and joins again (OnlineJoinRetry) when this rule says so.
    // Backed up = at least MinOthers other karts have sent poses and even the FRESHEST of them is older than
    // StaleSeconds, for SustainSeconds in a row. One kart going quiet (a hidden tab, a crash) never counts: the
    // others are still fresh. With only one other player we cannot tell whose line is bad, so we never decide.
    public static class ConnectionHealthRules
    {
        public const int MinOthers = 2;
        public const double StaleSeconds = 5.0;
        public const double SustainSeconds = 3.0;

        // newestAges = for every other player in the room who has sent poses: now - their newest pose time.
        public static bool LooksBackedUp(IReadOnlyList<double> newestAges)
        {
            if (newestAges == null || newestAges.Count < MinOthers)
            {
                return false;
            }
            double freshest = double.MaxValue;
            for (int i = 0; i < newestAges.Count; i++)
            {
                if (newestAges[i] < freshest)
                {
                    freshest = newestAges[i];
                }
            }
            return freshest > StaleSeconds;
        }

        // How long it has looked backed up (pass the previous value; resets to 0 the moment it looks fine).
        public static double Sustained(double before, bool backedUp, double dt)
        {
            return backedUp ? before + (dt > 0.0 ? dt : 0.0) : 0.0;
        }

        public static bool ShouldReconnect(double sustainedSeconds)
        {
            return sustainedSeconds >= SustainSeconds;
        }
    }
}
