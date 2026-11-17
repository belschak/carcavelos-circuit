using System.Collections.Generic;

namespace CarcavelosCircuit.Online
{
    // Is my connection backed up? (plain C#, tested in ConnectionHealthRulesTests). Scale test, 17 Nov 2026: a client
    // on a very bad line (300 ms +- 80 ms jitter AND 1 % loss) got everything later and later: the other karts' newest
    // poses were 2 s old after the first race and 20 s old ten minutes later, and every GO came later (up to 35 s).
    // The others' results stayed right, but that player raced alone, far behind. A fresh connection starts with an
    // empty queue, so the lobby leaves and joins again (OnlineJoinRetry) when this rule says so.
    // Backed up = at least MinOthers other karts have sent poses and at least StaleShare (3 in 4) of them are older
    // than StaleSeconds, for SustainSeconds in a row. One or a few karts going quiet (hidden tabs, a crash) never
    // count: most others are still fresh. Not "all of them": in the second scale run one odd kart stayed "fresh" on
    // the backed-up client and kept it from rejoining while the rest were 16 s late. With only one other player we
    // cannot tell whose line is bad, so we never decide.
    public static class ConnectionHealthRules
    {
        public const int MinOthers = 2;
        public const double StaleSeconds = 5.0;
        public const double SustainSeconds = 3.0;
        public const double StaleShare = 0.75;

        // newestAges = for every other player in the room who has sent poses: now - their newest pose time.
        public static bool LooksBackedUp(IReadOnlyList<double> newestAges)
        {
            if (newestAges == null || newestAges.Count < MinOthers)
            {
                return false;
            }
            int stale = 0;
            for (int i = 0; i < newestAges.Count; i++)
            {
                if (newestAges[i] > StaleSeconds)
                {
                    stale++;
                }
            }
            return stale >= StaleShare * newestAges.Count;
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
