using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace CarcavelosCircuit.Bots.Editor
{
    // LOCAL PROTOTYPE (bots). Starts the automatic bot race from the command line (no -quit: BotRaceTest quits Unity
    // itself when the race is over):
    //   Unity -batchmode -nographics -projectPath . -executeMethod CarcavelosCircuit.Bots.Editor.BotRaceBatch.Test -botTest -botOut <folder> [-botNoRubber]
    // For pictures leave out -nographics and add -botShots 2 and/or -botGif 20-28.
    public static class BotRaceBatch
    {
        public static void Test()
        {
            if (!File.Exists(BotRaceSceneBuilder.BotScene))
            {
                BotRaceSceneBuilder.Build();
            }
            EditorSceneManager.OpenScene(BotRaceSceneBuilder.BotScene, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
    }
}
