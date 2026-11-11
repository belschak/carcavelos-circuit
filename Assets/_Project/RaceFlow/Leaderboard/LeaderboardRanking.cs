using System.Collections.Generic;

namespace CarcavelosCircuit.RaceFlow
{
    // Sorting and ranking leaderboard lines (task R10). Plain C#, unit tested (LeaderboardRankingTests).
    // Times are compared in whole milliseconds (what the HUD shows), so two laps that both read 1:02.345 are a tie.
    // In a tie the line that was there first stays in front: you have to be faster to pass someone.
    public static class LeaderboardRanking
    {
        // Puts the entry into the sorted list and keeps only the best "keep" lines.
        // Returns its place (1 = fastest), or 0 if it did not make it into the kept lines.
        public static int Insert(List<LeaderboardEntry> sorted, LeaderboardEntry entry, int keep)
        {
            int milliseconds = RaceTimer.ToMilliseconds(entry.seconds);
            int index = sorted.Count;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (RaceTimer.ToMilliseconds(sorted[i].seconds) > milliseconds)
                {
                    index = i; // the first line that is slower: we go in front of it (equal times stay ahead)
                    break;
                }
            }
            sorted.Insert(index, entry);
            Cut(sorted, keep);
            return index < keep ? index + 1 : 0;
        }

        // A sorted copy (fastest first, ties keep their order) with at most "keep" lines.
        // Rows from the server already come sorted; sorting again means a wrong order can never reach the screen.
        public static List<LeaderboardEntry> Sorted(List<LeaderboardEntry> rows, int keep)
        {
            List<LeaderboardEntry> sorted = new List<LeaderboardEntry>();
            for (int i = 0; i < rows.Count; i++)
            {
                Insert(sorted, rows[i], int.MaxValue);
            }
            Cut(sorted, keep);
            return sorted;
        }

        // Drops lines faster than this track allows (LapTimeRules.MinSecondsFor of the track's Gold time).
        // The server only knows the general 5 s floor, so a fake "5.1 s" line on a long track is removed here.
        public static List<LeaderboardEntry> WithoutTooFast(List<LeaderboardEntry> rows, float minSeconds)
        {
            List<LeaderboardEntry> kept = new List<LeaderboardEntry>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].seconds >= minSeconds)
                {
                    kept.Add(rows[i]);
                }
            }
            return kept;
        }

        // The place (1 = first) of the first line with this name and this time, 0 if it is not in the list.
        public static int RankOf(List<LeaderboardEntry> sorted, string name, float seconds)
        {
            int milliseconds = RaceTimer.ToMilliseconds(seconds);
            for (int i = 0; i < sorted.Count; i++)
            {
                if (PlayerNameRules.SameName(sorted[i].name, name) && RaceTimer.ToMilliseconds(sorted[i].seconds) == milliseconds)
                {
                    return i + 1;
                }
            }
            return 0;
        }

        private static void Cut(List<LeaderboardEntry> list, int keep)
        {
            if (keep < 0)
            {
                keep = 0;
            }
            if (list.Count > keep)
            {
                list.RemoveRange(keep, list.Count - keep);
            }
        }
    }
}
