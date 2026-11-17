using System;
using System.Collections.Generic;

namespace CarcavelosCircuit.Online
{
    // The time limit of an online race (plain C#, tested in OnlineRaceLimitRulesTests), so one player who stops
    // driving can never hold up the room. After the first finish the others get GraceAfterFirstSeconds more; if
    // nobody finishes, the race closes NoFinisherLimitSeconds after GO. Then everyone still on the road is DNF, the
    // results are final and the host may start again.
    // Everything is counted in lap seconds from GO (the reported lap times), not in arrival times, so every screen
    // works out the same limit from the same reports, however late a message came in.
    public static class OnlineRaceLimitRules
    {
        public const double GraceAfterFirstSeconds = 60.0;  // the others' time after the winner crossed the line
        public const double NoFinisherLimitSeconds = 180.0; // nobody finished one lap by then: the race is over

        // The fastest reported lap that counts as a first finish (NaN = none). A lap slower than the no-finisher
        // limit came in after the race was already over, so it never opens the grace window.
        public static double FirstFinishSeconds(IReadOnlyList<FinishEntry> entries)
        {
            double best = double.NaN;
            if (entries == null)
            {
                return best;
            }
            for (int i = 0; i < entries.Count; i++)
            {
                FinishEntry entry = entries[i];
                if (!IsValidTime(entry) || entry.Seconds > NoFinisherLimitSeconds)
                {
                    continue;
                }
                if (double.IsNaN(best) || entry.Seconds < best)
                {
                    best = entry.Seconds;
                }
            }
            return best;
        }

        // Lap seconds after GO at which the race closes: first finish + grace, or the no-finisher limit.
        public static double EndSeconds(double firstFinishSeconds)
        {
            if (double.IsNaN(firstFinishSeconds) || double.IsInfinity(firstFinishSeconds) || firstFinishSeconds < 0.0)
            {
                return NoFinisherLimitSeconds;
            }
            return firstFinishSeconds + GraceAfterFirstSeconds;
        }

        // Is the race closed on the shared clock?
        public static bool IsTimeUp(double now, double startAt, double endSeconds)
        {
            return now >= startAt + endSeconds;
        }

        // Whole seconds until the race closes (0 when it is closed), for the "RACE CLOSES IN 42 S" line.
        public static int SecondsLeft(double now, double startAt, double endSeconds)
        {
            double left = startAt + endSeconds - now;
            if (double.IsNaN(left) || left <= 0.0)
            {
                return 0;
            }
            return (int)Math.Ceiling(Math.Min(left, 9999.0));
        }

        public static string ClosesLine(int secondsLeft)
        {
            return "RACE CLOSES IN " + (secondsLeft < 0 ? 0 : secondsLeft) + " S";
        }

        // The entries with the limit applied, ready for FinishOrderRules.Order: a lap slower than the limit is no
        // finish, and once the time is up a player still on the road counts as gone (DNF). timeUp says which.
        public static List<FinishEntry> Apply(IReadOnlyList<FinishEntry> entries, double now, double startAt, out bool timeUp, out double endSeconds)
        {
            endSeconds = EndSeconds(FirstFinishSeconds(entries));
            timeUp = IsTimeUp(now, startAt, endSeconds);
            List<FinishEntry> limited = new List<FinishEntry>(entries == null ? 0 : entries.Count);
            if (entries == null)
            {
                return limited;
            }
            for (int i = 0; i < entries.Count; i++)
            {
                FinishEntry entry = entries[i];
                bool finished = IsValidTime(entry) && entry.Seconds <= endSeconds;
                bool present = entry.Present && !timeUp;
                limited.Add(new FinishEntry(entry.Id, entry.Name, present, finished, finished ? entry.Seconds : double.NaN));
            }
            return limited;
        }

        private static bool IsValidTime(FinishEntry entry)
        {
            return entry.HasFinished && !double.IsNaN(entry.Seconds) && !double.IsInfinity(entry.Seconds) && entry.Seconds >= 0.0;
        }
    }
}
