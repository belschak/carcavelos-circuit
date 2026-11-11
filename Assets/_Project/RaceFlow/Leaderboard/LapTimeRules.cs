namespace CarcavelosCircuit.RaceFlow
{
    // Which lap times may go on the leaderboard (task R10). Plain C#, unit tested (LapTimeRulesTests).
    // A real lap cannot be faster than a big share of the Gold medal time (the medal times are stretched to the
    // track's length, so this works on every track), never under 5 s and never over 30 minutes. Anything outside
    // is a bug, a teleport or a cheat, and is not saved. The same window is checked again on rows that come back
    // from the internet, and the database has its own CHECK (5 s to 30 min).
    public static class LapTimeRules
    {
        public const float AbsoluteMinSeconds = 5f;    // same as the SQL CHECK
        public const float MaxSeconds = 1800f;         // 30 minutes, same as the SQL CHECK
        public const float MinShareOfGold = 0.4f;      // 40 % of the Gold time: far below any real lap

        // The fastest time we believe on this track. goldSeconds <= 0 or NaN (no medal times): only the 5 s floor.
        public static float MinSecondsFor(float goldSeconds)
        {
            if (float.IsNaN(goldSeconds) || float.IsInfinity(goldSeconds) || goldSeconds <= 0f)
            {
                return AbsoluteMinSeconds;
            }
            float fromGold = goldSeconds * MinShareOfGold;
            return fromGold > AbsoluteMinSeconds ? fromGold : AbsoluteMinSeconds;
        }

        // True if this lap time may be saved and shown.
        public static bool IsAcceptable(float seconds, float goldSeconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                return false;
            }
            return seconds >= MinSecondsFor(goldSeconds) && seconds <= MaxSeconds;
        }

        public const int MaxTrackLength = 40;

        // The track a time belongs to = its scene name (RaceManager.TrackName, e.g. "Track_CampusWorld").
        // 1 to 40 of A-Z, a-z, 0-9, _ and -: it goes into a web address, so nothing else is allowed.
        public static bool IsValidTrack(string track)
        {
            if (string.IsNullOrEmpty(track) || track.Length > MaxTrackLength)
            {
                return false;
            }
            for (int i = 0; i < track.Length; i++)
            {
                char c = track[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!ok)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
