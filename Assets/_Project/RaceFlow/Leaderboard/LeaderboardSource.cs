namespace CarcavelosCircuit.RaceFlow
{
    // Where the lines of a leaderboard answer come from (task R10). The HUD shows a different note for each.
    public enum LeaderboardSource
    {
        Online,          // the online leaderboard answered
        ThisDevice,      // no online leaderboard is set up (empty LeaderboardConfig): this device's times
        OfflineFallback, // the online leaderboard did not answer (no internet, server down, timeout): this device's times
    }
}
