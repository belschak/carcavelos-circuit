using System;
using System.Collections.Generic;

namespace CarcavelosCircuit.RaceFlow
{
    // A leaderboard that lives only in memory, for the tests (task R10). Set Fails = true to act like a server that
    // is down. Counts the calls, so a test can check that something was (or was not) sent. Answers at once.
    // It is in the game code (not the test folder) because the Play Mode test hands it to the real HUD.
    public class FakeLeaderboardService : ILeaderboardService
    {
        public bool Fails;
        public int SubmitCalls;
        public int FetchCalls;
        public string LastName = "";

        private readonly Dictionary<string, List<LeaderboardEntry>> tracks = new Dictionary<string, List<LeaderboardEntry>>();

        public void SubmitTime(string track, string name, float seconds, Action<LeaderboardReply> done)
        {
            SubmitCalls++;
            LastName = name;
            if (Fails)
            {
                done(LeaderboardReply.Failed());
                return;
            }
            LeaderboardReply reply = new LeaderboardReply();
            reply.Success = true;
            reply.Rank = LeaderboardRanking.Insert(ListOf(track), new LeaderboardEntry(name, seconds), int.MaxValue);
            done(reply);
        }

        public void FetchTop(string track, int count, Action<LeaderboardReply> done)
        {
            FetchCalls++;
            if (Fails)
            {
                done(LeaderboardReply.Failed());
                return;
            }
            LeaderboardReply reply = new LeaderboardReply();
            reply.Success = true;
            List<LeaderboardEntry> all = ListOf(track);
            for (int i = 0; i < all.Count && i < count; i++)
            {
                reply.Entries.Add(all[i]);
            }
            done(reply);
        }

        private List<LeaderboardEntry> ListOf(string track)
        {
            List<LeaderboardEntry> list;
            if (!tracks.TryGetValue(track, out list))
            {
                list = new List<LeaderboardEntry>();
                tracks[track] = list;
            }
            return list;
        }
    }
}
