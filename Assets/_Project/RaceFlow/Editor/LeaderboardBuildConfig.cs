using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow.Editor
{
    // Puts the online leaderboard's address and public key into the Web build (task R10), and takes them out again.
    // The values come from the GitHub secrets LEADERBOARD_URL and LEADERBOARD_ANON_KEY. The CI workflow
    // (.github/workflows/web-build.yml) hands them to Unity as the command-line arguments
    //   -leaderboardUrl <url> -leaderboardKey <key>
    // because GameCI runs Unity in a Docker container that only receives its own list of environment variables.
    // On a laptop the two environment variables LEADERBOARD_URL / LEADERBOARD_ANON_KEY work too.
    // The values are written into RaceFlow/Settings/LeaderboardConfig.asset only for the length of the build
    // (Role4WebBuild clears them in its "finally"), so the file in the repo always stays empty.
    // - No variables (every laptop, a CI run without the secrets): the game keeps its leaderboard on the device.
    // - Variables that fail LeaderboardConfigCheck (http, wrong domain, a SECRET key): the build stops with a clear
    //   error, because a broken or dangerous key must never ship quietly.
    // The key itself is never written to the log.
    public static class LeaderboardBuildConfig
    {
        public const string UrlVariable = "LEADERBOARD_URL";
        public const string KeyVariable = "LEADERBOARD_ANON_KEY";
        public const string UrlArgument = "-leaderboardUrl";
        public const string KeyArgument = "-leaderboardKey";

        public static void FillFromEnvironment()
        {
            string url = ArgumentOrVariable(UrlArgument, UrlVariable);
            string key = ArgumentOrVariable(KeyArgument, KeyVariable);
            if (LeaderboardConfigCheck.IsEmpty(url, key))
            {
                Debug.Log("LeaderboardBuildConfig: no " + UrlVariable + " / " + KeyVariable + ", so this build keeps lap times on the player's device only.");
                return;
            }
            string problem = LeaderboardConfigCheck.Problem(url, key);
            if (problem != "")
            {
                throw new BuildFailedException("LeaderboardBuildConfig: " + problem + ". Fix the GitHub secrets (backend/supabase/README.md), or delete both to build without the online leaderboard.");
            }
            LeaderboardConfig config = Load();
            config.SetConnection(url, key);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            string kind = LeaderboardConfigCheck.IsJwt(key) ? "anon JWT" : "publishable key";
            Debug.Log("LeaderboardBuildConfig: online leaderboard is ON for this build (" + kind + ", " + key.Trim().Length + " characters).");
        }

        // Back to empty. Safe to call when nothing was filled in.
        public static void Clear()
        {
            LeaderboardConfig config = AssetDatabase.LoadAssetAtPath<LeaderboardConfig>(Role4SceneBuilder.LeaderboardConfigPath);
            if (config == null || LeaderboardConfigCheck.IsEmpty(config.ProjectUrl, config.AnonKey))
            {
                return;
            }
            config.SetConnection("", "");
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        // The value after "-leaderboardUrl" on Unity's command line, else the environment variable, else "".
        // A value that starts with "-" is the next argument (the secret was empty), so it counts as "".
        private static string ArgumentOrVariable(string argument, string variable)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == argument)
                {
                    return args[i + 1].StartsWith("-") ? "" : args[i + 1];
                }
            }
            string value = Environment.GetEnvironmentVariable(variable);
            return value == null ? "" : value;
        }

        private static LeaderboardConfig Load()
        {
            LeaderboardConfig config = AssetDatabase.LoadAssetAtPath<LeaderboardConfig>(Role4SceneBuilder.LeaderboardConfigPath);
            if (config == null)
            {
                throw new BuildFailedException("LeaderboardBuildConfig: " + Role4SceneBuilder.LeaderboardConfigPath + " is missing (run Role4SceneBuilder.BuildAll).");
            }
            return config;
        }
    }
}
