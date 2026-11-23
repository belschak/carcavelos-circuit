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

        [DllImport("__Internal")]
        private static extern string CarcavelosWorldBundleUrl();

        [DllImport("__Internal")]
        private static extern void CarcavelosWorldState(int state, float progress);

        [DllImport("__Internal")]
        private static extern int CarcavelosFirstScreen();

        [DllImport("__Internal")]
        private static extern int CarcavelosFirstScreenFloor();
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

        // Added to a race state when the race is online (phone QA 17.11): an online race cannot be paused or retried
        // by hand (the room runs on), so the page hides its Pause and Retry buttons there instead of two dead buttons.
        public const int PageOnline = 4;

        public static int PageRaceState(RaceState state, bool paused)
        {
            return PageRaceState(state, paused, false);
        }

        public static int PageRaceState(RaceState state, bool paused, bool online)
        {
            if (state != RaceState.Countdown && state != RaceState.Racing)
            {
                // Online results (phone QA 23.11): the ROOM RESULTS card sits top right, where the page puts its
                // fullscreen button on the menus, so the page moves the button while this says "online results".
                return online && state == RaceState.Results ? PageMenus + PageOnline : PageMenus;
            }
            return (paused ? PagePaused : PageRacing) + (online ? PageOnline : 0);
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
        // 4. The campus world as its own download (load time P8, 18 Nov). The page picks the world file that fits the GPU
        //    (DXT or ETC2, like the .data) and gives its full address here; "" = no separate world file (the Editor,
        //    tests, or a build where the world is inside the .data). See RaceFlow/WorldStream.cs.
        public static string WorldBundleUrl
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                string url = CarcavelosWorldBundleUrl();
                return url == null ? "" : url;
#else
                return "";
#endif
            }
        }

        // What the page is told about the world: its loading bar goes on while the world downloads (WorldLoading,
        // progress 0 to 1), "Click to play" waits for WorldReady (the track is set up), WorldFailed shows the
        // page's network card.
        public const int WorldLoading = 0;
        public const int WorldReady = 1;
        public const int WorldFailed = 2;

        public static void SendWorldState(int state, float progress)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CarcavelosWorldState(state, progress);
#endif
        }

        // 5. The page's first screen (kart builder, 18 Nov): FirstScreenGone = the game has the screen (always in the
        //    Editor and in tests), FirstScreenCovers = the page still covers the game (loading), FirstScreenLive =
        //    "Click to play" is up and the right half of the screen shows the live game: the HUD draws the four drivers
        //    there (UI/StartLineup) and keeps its own start screen hidden until the click.
        public const int FirstScreenGone = 0;
        public const int FirstScreenCovers = 1;
        public const int FirstScreenLive = 2;

        // Tests only: pretend the page is in this first-screen state (null = ask the page).
        public static int? FirstScreenOverride;

        public static int FirstScreen
        {
            get
            {
                if (FirstScreenOverride.HasValue)
                {
                    return FirstScreenOverride.Value;
                }
#if UNITY_WEBGL && !UNITY_EDITOR
                return CarcavelosFirstScreen();
#else
                return FirstScreenGone;
#endif
            }
        }

        // Where the page's controls legend starts on the first screen, as a share of the window height from the top
        // (1 = no legend). The line-up's names stay above it (UI/StartLineup).
        public static float FirstScreenFloor
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return CarcavelosFirstScreenFloor() / 1000f;
#else
                return 1f;
#endif
            }
        }
    }
}
