using System.Runtime.InteropServices;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The things the game and the Web page around it tell each other (task R10/R11, phone round 17.11). The JavaScript side is
    // RaceFlow/Web/CarcavelosWebPage.jslib (Unity copies it into the Web build) and the page template
    // (Assets/WebGLTemplates/CarcavelosCircuit). In the Editor and in tests every call does nothing.
    // 1. SendRaceState: is a race running (countdown or racing) or paused? The page's touch buttons for driving only show in a running race,
    //    so on a phone they never cover the title, the leaderboard or the name field.
    // 2. Name box: on a phone the name is typed in a real text box of the page laid over the game's name field
    //    (PageNameBox). A phone browser only opens its keyboard for a page text box that a real finger tapped; the
    //    game's own field never gets that tap, and window.prompt (until 23 Nov) did nothing on an Android phone.
    public static class WebPage
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void CarcavelosRaceState(int racing);

        [DllImport("__Internal")]
        private static extern void CarcavelosNameBoxShow(int owner, float left, float top, float width, float height, string current, int maxLength, int reset);

        [DllImport("__Internal")]
        private static extern void CarcavelosNameBoxHide(int owner);

        [DllImport("__Internal")]
        private static extern string CarcavelosNameBoxValue(int owner);

        [DllImport("__Internal")]
        private static extern int CarcavelosNameBoxFocused(int owner);

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

        // True in a Web build on a phone or tablet (or a touch laptop in touch mode): then the name is typed in the
        // page's own text box (PageNameBox). An iPad says it is a Mac, so touch mode counts too.
        public static bool UsesNamePrompt
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return Application.isMobilePlatform || TouchMode;
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

        // Lays the page's name box over a game field. field and screen are in the HUD's own units (worldBound);
        // the page gets the field's place as a share of the screen, so it fits whatever size the canvas has.
        // reset = the box takes "current" (it opens, or the game changed the field). owner: PageNameBox.
        public static void ShowNameBox(int owner, Rect field, Rect screen, string current, int maxLength, bool reset)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false; // keys go to the page's box (the game only hears its own canvas)
            CarcavelosNameBoxShow(owner, (field.x - screen.x) / screen.width, (field.y - screen.y) / screen.height,
                field.width / screen.width, field.height / screen.height, current == null ? "" : current, maxLength, reset ? 1 : 0);
#endif
        }

        public static void HideNameBox(int owner)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CarcavelosNameBoxHide(owner);
            WebGLInput.captureAllKeyboardInput = true; // the box is gone: every key is the game's again
#endif
        }

        // The text in the page's name box ("" in the Editor, in tests, or when the box is closed).
        public static string NameBoxValue(int owner)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string text = CarcavelosNameBoxValue(owner);
            return text == null ? "" : text;
#else
            return "";
#endif
        }

        public static bool NameBoxFocused(int owner)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return CarcavelosNameBoxFocused(owner) != 0;
#else
            return false;
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
