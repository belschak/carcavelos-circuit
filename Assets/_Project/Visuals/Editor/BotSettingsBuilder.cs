using UnityEditor;

namespace CarcavelosCircuit.Visuals.Editor
{
    // Task V10: creates Visuals/Bots/BotSettings.asset with the defaults written in BotSettings.cs (the five bot
    // drivers and their tuning). An existing asset is kept, so tuning done in the Inspector is never overwritten.
    // RaceFlow's scene builder loads it from AssetPath and gives it to BotSquad in Main.unity.
    public static class BotSettingsBuilder
    {
        public const string AssetPath = "Assets/_Project/Visuals/Bots/BotSettings.asset";

        [MenuItem("Carcavelos Circuit/Bots/Create Bot Settings")]
        public static void CreateOrKeep()
        {
            if (AssetDatabase.LoadAssetAtPath<BotSettings>(AssetPath) != null)
            {
                UnityEngine.Debug.Log("BotSettingsBuilder: " + AssetPath + " already exists, kept as it is.");
                return;
            }
            BotSettings settings = UnityEngine.ScriptableObject.CreateInstance<BotSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("BotSettingsBuilder: created " + AssetPath + ".");
        }
    }
}
