using System.Collections.Generic;

namespace CarcavelosCircuit.RaceFlow
{
    // The answer of a leaderboard service (task R10), handed to the callback of SubmitTime or FetchTop.
    public class LeaderboardReply
    {
        // False = the service could not do it (no internet, server error, bad answer). The HUD never shows why
        // in the server's words, only our own "offline" note.
        public bool Success;

        // FetchTop: the lines, fastest first, already checked (LeaderboardJson). Empty list = no times yet.
        public List<LeaderboardEntry> Entries = new List<LeaderboardEntry>();

        // SubmitTime: our place (1 = fastest), 0 if unknown (outside the device's top 10).
        public int Rank;

        public LeaderboardSource Source = LeaderboardSource.Online;

        public static LeaderboardReply Failed()
        {
            LeaderboardReply reply = new LeaderboardReply();
            reply.Success = false;
            return reply;
        }
    }
}
