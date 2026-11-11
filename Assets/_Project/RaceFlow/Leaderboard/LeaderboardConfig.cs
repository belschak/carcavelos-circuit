using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Where the online leaderboard lives (task R10). The asset in the repo (RaceFlow/Settings/LeaderboardConfig.asset)
    // stays EMPTY on purpose: the web address and the public key are put in by the CI Web build from the GitHub
    // secrets LEADERBOARD_URL and LEADERBOARD_ANON_KEY just before it builds, and taken out again right after
    // (Role4WebBuild). Empty = the game keeps a leaderboard on this device only, without any error.
    // Setup steps for the database: backend/supabase/README.md.
    [CreateAssetMenu(menuName = "Carcavelos Circuit/Leaderboard Config", fileName = "LeaderboardConfig")]
    public class LeaderboardConfig : ScriptableObject
    {
        [SerializeField, Tooltip("Supabase project address, e.g. https://abcd1234.supabase.co. Leave EMPTY in the repo (filled by the CI build)")]
        private string projectUrl = "";

        [SerializeField, Tooltip("The project's public anon / publishable key (never the secret or service_role key). Leave EMPTY in the repo (filled by the CI build)")]
        private string anonKey = "";

        [SerializeField, Range(2, 20), Tooltip("Seconds to wait for the server before the game says 'offline'")]
        private int timeoutSeconds = 6;

        [SerializeField, Range(3, 10), Tooltip("How many lines the leaderboard shows (start screen and results card)")]
        private int topCount = 10;

        public string ProjectUrl { get { return projectUrl; } }
        public string AnonKey { get { return anonKey; } }
        public int TimeoutSeconds { get { return timeoutSeconds; } }
        public int TopCount { get { return topCount; } }

        // Only the Web build script calls this (Editor), with the values from the CI environment.
        public void SetConnection(string url, string key)
        {
            projectUrl = url == null ? "" : url.Trim();
            anonKey = key == null ? "" : key.Trim();
        }
    }
}
