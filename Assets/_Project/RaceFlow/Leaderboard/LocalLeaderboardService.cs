using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The leaderboard on this device (task R10): used when no online leaderboard is set up, and as the fallback when
    // it is offline. One PlayerPrefs text per track (in the browser: the site's IndexedDB storage, like the best time).
    // Answers at once. The list rules live in LocalLeaderboardList (unit tested); this is only the PlayerPrefs glue.
    public class LocalLeaderboardService : ILeaderboardService
    {
        public const string KeyPrefix = "CarcavelosCircuit.Leaderboard.";

        private readonly LeaderboardSource source;

        // source = what the answers say about themselves: ThisDevice (no online leaderboard) or OfflineFallback.
        public LocalLeaderboardService(LeaderboardSource source)
        {
            this.source = source;
        }

        public static string SaveKey(string track)
        {
            return KeyPrefix + track;
        }

        public void SubmitTime(string track, string name, float seconds, Action<LeaderboardReply> done)
        {
            LeaderboardReply reply = new LeaderboardReply();
            reply.Source = source;
            if (LapTimeRules.IsValidTrack(track))
            {
                LocalLeaderboardList list = Load(track);
                reply.Rank = list.Add(name, seconds);
                PlayerPrefs.SetString(SaveKey(track), list.ToSaveString());
                PlayerPrefs.Save(); // in a Web build this writes to the browser right away
                reply.Entries = list.Top(LocalLeaderboardList.Keep);
                reply.Success = true;
            }
            done(reply);
        }

        public void FetchTop(string track, int count, Action<LeaderboardReply> done)
        {
            LeaderboardReply reply = new LeaderboardReply();
            reply.Source = source;
            if (LapTimeRules.IsValidTrack(track))
            {
                reply.Entries = Load(track).Top(count);
                reply.Success = true;
            }
            done(reply);
        }

        // The times saved from this browser for a track (best 10), to highlight the player's own rows.
        public static List<LeaderboardEntry> SavedHere(string track)
        {
            if (!LapTimeRules.IsValidTrack(track))
            {
                return new List<LeaderboardEntry>();
            }
            return Load(track).Top(LocalLeaderboardList.Keep);
        }

        private static LocalLeaderboardList Load(string track)
        {
            return LocalLeaderboardList.FromSaveString(PlayerPrefs.GetString(SaveKey(track), ""));
        }
    }
}
