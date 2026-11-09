using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow.Editor
{
    // Ships the team's best lap with the game ("Dean's ghost"), so a fresh browser (the examiners!) has a ghost
    // to race on lap one. How: play Main.unity in the Editor, set a personal best, stop Play Mode, then
    // Carcavelos Circuit > Save My Best Ghost As Dean's Ghost. It copies the ghost the game saved in the Editor's
    // PlayerPrefs into RaceFlow/Settings/DeansGhost.txt (the file Main.unity already points to). Commit that file.
    // Re-record after every track change: a ghost from another checkpoint count is ignored by the game.
    public static class DeansGhostExporter
    {
        private const string DeansGhostPath = "Assets/_Project/RaceFlow/Settings/DeansGhost.txt";
        private const string SettingsPath = "Assets/_Project/RaceFlow/Settings/RaceSettings.asset";

        [MenuItem("Carcavelos Circuit/Save My Best Ghost As Dean's Ghost")]
        public static void SaveBestGhost()
        {
            RaceSettings settings = AssetDatabase.LoadAssetAtPath<RaceSettings>(SettingsPath);
            if (settings == null)
            {
                Debug.LogError("DeansGhostExporter: " + SettingsPath + " not found. Run Carcavelos Circuit > Build Role 4 Scenes first.");
                return;
            }

            // The campus track first; the test loop only while the campus track is not raceable yet.
            string[] tracks = { settings.TrackSceneName, settings.FallbackTrackSceneName };
            for (int i = 0; i < tracks.Length; i++)
            {
                string text = PlayerPrefs.GetString(GhostLoader.SaveKey(tracks[i]), "");
                if (text != "")
                {
                    System.IO.File.WriteAllText(DeansGhostPath, text);
                    AssetDatabase.ImportAsset(DeansGhostPath);
                    Debug.Log("DeansGhostExporter: saved the best ghost of " + tracks[i] + " (" + text.Length
                        + " characters) to " + DeansGhostPath + ". Commit this file.");
                    return;
                }
            }
            Debug.LogWarning("DeansGhostExporter: no ghost found in the Editor's PlayerPrefs. Play Main.unity, set a personal best, stop Play Mode and try again.");
        }
    }
}
