using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CarcavelosCircuit.Playtest
{
    // LOCAL PLAYTEST start menu: 1 = free roam, 2 = bot race, F10 / Cmd+Q / the button = quit (PlaytestTools).
    public class Launcher : MonoBehaviour
    {
        [SerializeField] private string freeRoamScene = "FreeRoam";
        [SerializeField] private string botRaceScene = "BotRace";

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) SceneManager.LoadScene(freeRoamScene);
            if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) SceneManager.LoadScene(botRaceScene);
        }

        private void OnGUI()
        {
            var title = new GUIStyle(GUI.skin.label) { fontSize = 44, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            title.normal.textColor = Color.white;
            var button = new GUIStyle(GUI.skin.button) { fontSize = 28 };
            float w = 520, h = 74, x = (Screen.width - w) / 2f, y = Screen.height / 2f - 140f;
            GUI.Label(new Rect(0, y - 110, Screen.width, 80), "NOVA KART – Testversion", title);
            if (GUI.Button(new Rect(x, y, w, h), "1  Freies Fahren", button)) SceneManager.LoadScene(freeRoamScene);
            if (GUI.Button(new Rect(x, y + h + 16, w, h), "2  Bot-Rennen", button)) SceneManager.LoadScene(botRaceScene);
            if (GUI.Button(new Rect(x, y + 2 * (h + 16), w, h), "Beenden", button))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
    }
}
