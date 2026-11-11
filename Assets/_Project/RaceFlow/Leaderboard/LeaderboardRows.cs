using System;

namespace CarcavelosCircuit.RaceFlow
{
    // A list of leaderboard lines wrapped in an object, because JsonUtility cannot read or write a bare JSON array.
    // LeaderboardJson wraps the server's "[...]" as {"rows":[...]}; the device save is stored in this shape directly.
    [Serializable]
    public class LeaderboardRows
    {
        public LeaderboardEntry[] rows;
    }
}
