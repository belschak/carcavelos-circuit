using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The leaderboard kept on this device for one track (task R10): the best 10 lap times with names.
    // Plain C# with a save text in and out, so it is unit tested (LocalLeaderboardListTests) without PlayerPrefs.
    // The save is JSON ({"rows":[...]}). A save that was edited by hand or broken is read row by row like an answer
    // from the internet: bad rows are dropped, a broken text gives an empty list.
    public class LocalLeaderboardList
    {
        public const int Keep = 10;

        private readonly List<LeaderboardEntry> entries = new List<LeaderboardEntry>();

        public int Count { get { return entries.Count; } }

        public static LocalLeaderboardList FromSaveString(string save)
        {
            LocalLeaderboardList list = new LocalLeaderboardList();
            if (string.IsNullOrEmpty(save) || save.Length > LeaderboardJson.MaxAnswerLength)
            {
                return list;
            }
            LeaderboardRows parsed;
            try
            {
                parsed = JsonUtility.FromJson<LeaderboardRows>(save);
            }
            catch (System.ArgumentException)
            {
                return list; // broken save: start fresh rather than crash
            }
            if (parsed == null || parsed.rows == null)
            {
                return list;
            }
            List<LeaderboardEntry> good = new List<LeaderboardEntry>();
            for (int i = 0; i < parsed.rows.Length; i++)
            {
                if (LeaderboardJson.IsGoodRow(parsed.rows[i]))
                {
                    good.Add(parsed.rows[i]);
                }
            }
            list.entries.AddRange(LeaderboardRanking.Sorted(good, Keep));
            return list;
        }

        public string ToSaveString()
        {
            LeaderboardRows rows = new LeaderboardRows();
            rows.rows = entries.ToArray();
            return JsonUtility.ToJson(rows);
        }

        // Adds a lap time. Returns its place on this device (1 = fastest), 0 if it is not in the best 10.
        public int Add(string name, float seconds)
        {
            return LeaderboardRanking.Insert(entries, new LeaderboardEntry(name, seconds), Keep);
        }

        // A copy of the best "count" lines (a copy, so the caller cannot change the saved list by accident).
        public List<LeaderboardEntry> Top(int count)
        {
            List<LeaderboardEntry> top = new List<LeaderboardEntry>();
            for (int i = 0; i < entries.Count && i < count; i++)
            {
                top.Add(new LeaderboardEntry(entries[i].name, entries[i].seconds));
            }
            return top;
        }
    }
}
