using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow.Editor
{
    // Turns the online race (M2, Photon Fusion 2) on for one Web build, and off again afterwards.
    // The Fusion SDK may not be published in our public repo (Photon licence), so it is NOT in Git:
    // CI copies it into Assets/Photon/ from a private repo (.github/workflows/web-build.yml), and a teammate who wants
    // to test online in the editor imports it himself (docs/online.md).
    // - SDK folder present: the scripting define CARCAVELOS_FUSION is added for this build only, so the assembly
    //   CarcavelosCircuit.Online.Fusion (defineConstraints) is compiled into the player.
    // - App ID: from the GitHub secret PHOTON_APP_ID, handed to Unity as "-photonAppId <id>" (GameCI's Docker container
    //   only receives its own environment variables), or the environment variable PHOTON_APP_ID on a laptop.
    //   It is written to Online/Resources/CarcavelosPhotonAppId.txt (ignored by Git) only for the build.
    // No SDK or no App ID = a normal single-player build; the Online card then says it needs the online build.
    public static class OnlineBuildConfig
    {
        public const string Define = "CARCAVELOS_FUSION";
        public const string SdkFolder = "Assets/Photon/Fusion";
        public const string AppIdArgument = "-photonAppId";
        public const string AppIdVariable = "PHOTON_APP_ID";
        public const string AppIdFile = "Assets/_Project/Online/Resources/CarcavelosPhotonAppId.txt";

        private static string definesBefore;

        // Returns true when this build carries the online race.
        public static bool Apply()
        {
            Clear();
            if (!Directory.Exists(SdkFolder))
            {
                Debug.Log("OnlineBuildConfig: no Fusion SDK in " + SdkFolder + ", so this build has no online race.");
                return false;
            }
            string appId = ArgumentOrVariable(AppIdArgument, AppIdVariable).Trim();
            if (OnlineAppIdRules.IsEmpty(appId))
            {
                Debug.Log("OnlineBuildConfig: Fusion SDK found but no " + AppIdVariable + ", so this build has no online race.");
                return false;
            }
            if (!OnlineAppIdRules.IsValid(appId))
            {
                throw new BuildFailedException("OnlineBuildConfig: " + AppIdVariable + " is not a Photon App ID (expected a GUID like 1a2b3c4d-....). Fix the GitHub secret.");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(AppIdFile));
            File.WriteAllText(AppIdFile, appId);
            AssetDatabase.ImportAsset(AppIdFile);

            NamedBuildTarget web = NamedBuildTarget.WebGL;
            definesBefore = PlayerSettings.GetScriptingDefineSymbols(web);
            if (!(";" + definesBefore + ";").Contains(";" + Define + ";"))
            {
                PlayerSettings.SetScriptingDefineSymbols(web, string.IsNullOrEmpty(definesBefore) ? Define : definesBefore + ";" + Define);
            }
            Debug.Log("OnlineBuildConfig: online race is ON for this build (App ID " + appId.Length + " characters).");
            return true;
        }

        // Back to a normal project. Safe to call when nothing was applied.
        public static void Clear()
        {
            if (definesBefore != null)
            {
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, definesBefore);
                definesBefore = null;
            }
            if (File.Exists(AppIdFile))
            {
                AssetDatabase.DeleteAsset(AppIdFile);
            }
        }

        // The value after "-photonAppId" on Unity's command line, else the environment variable, else "".
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
    }
}
