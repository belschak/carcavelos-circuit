using System;

namespace CarcavelosCircuit.RaceFlow
{
    // A place that keeps lap times with names (task R10). Implementations: SupabaseLeaderboardService (online),
    // LocalLeaderboardService (this device), FallbackLeaderboardService (online first, this device when offline)
    // and FakeLeaderboardService (tests). The callback may run at once or a few seconds later (web request),
    // always on Unity's main thread, and always exactly once.
    public interface ILeaderboardService
    {
        // Saves one lap time. The caller has already checked name and time (PlayerNameRules, LapTimeRules).
        void SubmitTime(string track, string name, float seconds, Action<LeaderboardReply> done);

        // The best "count" lap times of a track, fastest first.
        void FetchTop(string track, int count, Action<LeaderboardReply> done);
    }
}
