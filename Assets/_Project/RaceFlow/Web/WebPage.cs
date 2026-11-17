using System.Runtime.InteropServices;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The things the game and the Web page around it tell each other (task R10/R11, phone round 17.11). The JavaScript side is
    // RaceFlow/Web/CarcavelosWebPage.jslib (Unity copies it into the Web build) and the page template
    // (Assets/WebGLTemplates/CarcavelosCircuit). In the Editor and in tests every call does nothing.
    // 1. SendRaceState: is a race running (countdown or racing) or paused? The page's touch buttons for driving only show in a running race,
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

        [DllImport("__Internal")]
        private static extern int CarcavelosTouchMode();
#endif

        // 3. TouchMode (phone round 17.11): true while the page shows its touch buttons (phones, tablets, a touch laptop
        //    after a touch). The HUD then moves the drift bar and the speedometer between the thumbs and hides key hints.
        //    It can change during the game (a touch laptop: a key press switches back), so callers ask again now and then.
        public static bool TouchMode
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return CarcavelosTouchMode() != 0;
#else
                return false;
#endif
            }
        }

        // What the page is told (phone round 17.11): menus, a race (countdown or racing) or a paused race. The touch
        // buttons for driving show only in a running race, so they hide under the PAUSED card (no thumb covers it,
        // a tap on the game continues); the page keeps its fullscreen button in the race spot while paused.
        public const int PageMenus = 0;
        public const int PageRacing = 1;
        public const int PagePaused = 2;

        public static int PageRaceState(RaceState state, bool paused)
        {
            if (state != RaceState.Countdown && state != RaceState.Racing)
            {
                return PageMenus;
            }
            return paused ? PagePaused : PageRacing;
        }

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
            SendRaceState(racing ? PageRacing : PageMenus);
        }

        // pageState: PageMenus, PageRacing or PagePaused (PageRaceState).
        public static void SendRaceState(int pageState)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CarcavelosRaceState(pageState);
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
