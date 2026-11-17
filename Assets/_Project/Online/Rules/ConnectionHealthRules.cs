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
        public const double MaxStepSeconds = 0.1;   // one frame counts at most this much (a hitch is not a backed-up line)

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
        // A frame counts at most MaxStepSeconds: live check 17 Nov, a laptop froze for ~15 s, the first frame after
        // it saw every kart 15 s old and rejoined at once although fresh poses came in the very next frames. Now
        // SustainSeconds needs 30+ frames in a row of old data, which only a really backed-up line gives.
        public static double Sustained(double before, bool backedUp, double dt)
        {
            double step = dt > 0.0 ? (dt > MaxStepSeconds ? MaxStepSeconds : dt) : 0.0;
            return backedUp ? before + step : 0.0;
        }

        public static bool ShouldReconnect(double sustainedSeconds)
        {
            return sustainedSeconds >= SustainSeconds;
        }
    }
}
