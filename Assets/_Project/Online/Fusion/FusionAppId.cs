using CarcavelosCircuit.RaceFlow;
using UnityEngine;

namespace CarcavelosCircuit.Online.Fusion
{
    // Where the Photon App ID of this build comes from (the ID that tells the Photon Cloud which app we are).
    // The Web build on GitHub writes it to Online/Resources/CarcavelosPhotonAppId.txt during the build only
    // (OnlineBuildConfig, from the PHOTON_APP_ID secret; the file is never committed). A developer who imported
    // the SDK into the editor may instead type it into Assets/Photon/Fusion/Resources/PhotonAppSettings.asset.
    // Either way it must look like a Photon App ID (OnlineAppIdRules), else the online race says it has none.
    public static class FusionAppId
    {
        public const string ResourceName = "CarcavelosPhotonAppId";

        // The ID of this build, or "" when there is none. The build's text file wins over the settings asset.
        public static string Find(string fromSettingsAsset)
        {
            string fromBuild = Clean(LoadBuildText());
            if (OnlineAppIdRules.IsValid(fromBuild))
            {
                return fromBuild;
            }
            string fromAsset = Clean(fromSettingsAsset);
            return OnlineAppIdRules.IsValid(fromAsset) ? fromAsset : "";
        }

        private static string LoadBuildText()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourceName);
            return asset == null ? "" : asset.text;
        }

        private static string Clean(string text)
        {
            return text == null ? "" : text.Trim();
        }
    }
}
