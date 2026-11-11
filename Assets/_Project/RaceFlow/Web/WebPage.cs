using System.Runtime.InteropServices;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The two things the game tells the Web page around it (task R10/R11). The JavaScript side is
    // RaceFlow/Web/CarcavelosWebPage.jslib (Unity copies it into the Web build) and the page template
    // (Assets/WebGLTemplates/CarcavelosCircuit). In the Editor and in tests every call does nothing.
    // 1. SendRaceState: is a race running (countdown or racing)? The page's touch buttons for driving only show then,
    //    so on a phone they never cover the title, the leaderboard or the name field.
    // 2. PromptName: on a phone the name is asked with the browser's own text box (window.prompt). A phone browser
    //    only opens its keyboard for a text field when a real finger tap focuses it; the game's tap is passed on by
    //    the page a moment later, so a game text field would not always get the keyboard (iPhones never).
    public static class WebPage
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void CarcavelosRaceState(int racing);

        [DllImport("__Internal")]
        private static extern string CarcavelosPromptName(string current);
#endif

        // True in a Web build on a phone or tablet: then the name is typed in the browser's own text box.
        public static bool UsesNamePrompt
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return Application.isMobilePlatform;
#else
                return false;
#endif
            }
        }

        public static void SendRaceState(bool racing)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CarcavelosRaceState(racing ? 1 : 0);
#endif
        }

        // Shows the browser's text box filled with "current". Returns what was typed, or "current" when cancelled.
        // The answer is checked by PlayerNameRules like any typed name.
        public static string PromptName(string current)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string answer = CarcavelosPromptName(current == null ? "" : current);
            return answer == null ? current : answer;
#else
            return current;
#endif
        }
    }
}
