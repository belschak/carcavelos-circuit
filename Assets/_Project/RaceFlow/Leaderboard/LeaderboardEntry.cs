using System;

namespace CarcavelosCircuit.RaceFlow
{
    // One line of the leaderboard: a name and a lap time (task R10).
    // Public fields with these exact lower-case names because JsonUtility reads and writes them: they match the
    // columns of the database table (backend/supabase/001_lap_times.sql) and the device save.
    [Serializable]
    public class LeaderboardEntry
    {
        public string name;
        public float seconds;

        public LeaderboardEntry()
        {
        }

        public LeaderboardEntry(string name, float seconds)
        {
            this.name = name;
            this.seconds = seconds;
        }
    }
}
