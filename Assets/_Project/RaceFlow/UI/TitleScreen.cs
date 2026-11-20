using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // The title opener on the start screen (task R11, wordmark since the realistic menu redesign). On the HUD object, next
    // to RaceHud. RaceHud still runs the start screen (state class st-ready, "intro-in" a moment after it shows, the
    // breathing prompt, the best time). This only adds the title show, styled in UI/FrontEnd.uss:
    // 1. Logo reveal: the five logo pictures (sun, wave, badge, word, ribbon) pop in one after another. Pure USS
    //    (transition delays on ".intro-in .lg-..."), nothing to do here.
    // 2. Light sweep: every few seconds a band of light runs over the word. Trick: a narrow box with overflow: hidden
    //    moves to the right while a gold-tinted copy of the word picture inside it moves left by the same amount, so the
    //    copy stays exactly on top of the real word and only shows inside the band. Two such bands share one centre
    //    (a wide soft one and a narrow bright core), so the sheen has soft edges. Four translates per frame, only while
    //    the sweep runs.
    // With reduced motion (the "reduce-motion" class HudMenus puts on hud-root) everything is simply there:
    // no pop, no sweep (the USS sets the durations to 0, this script skips the sweep).
    [RequireComponent(typeof(UIDocument))]
    public class TitleScreen : MonoBehaviour
    {
        [SerializeField, Tooltip("The race brain: the title only animates on the start screen (state Ready)")]
        private RaceManager raceManager;

        [SerializeField, Tooltip("Seconds after the start screen appears before the first light sweep")]
        private float firstSweepSeconds = 1.6f;

        [SerializeField, Tooltip("Seconds from one light sweep to the next")]
        private float sweepEverySeconds = 4.5f;

        [SerializeField, Tooltip("How long one light sweep takes to cross the title, in seconds")]
        private float sweepSeconds = 0.9f;

        private VisualElement hudRoot;
        private VisualElement startScreen;
        private Label bestLabel;              // "YOUR BEST 1:02.345" (RaceHud writes it, "" without a best time)
        private VisualElement logo;
        private VisualElement glint;          // the narrow bright core
        private VisualElement glintLetters;
        private VisualElement softGlint;      // the wide soft band around it
        private VisualElement softGlintLetters;
        private float introStart = -1f; // real time when "intro-in" appeared, -1 = not on the start screen
        private bool sweepWasRunning;

        // "On the track" postcard (bottom right of the start screen): one of the map's sights at a time, so a new
        // player sees what makes this track special while the camera flies over it.
        private static readonly string[,] Sights =
        {
            { "SURF BREAK", "Surfers ride the Atlantic swell right beside the track" },
            { "BEACH TUNNEL", "84 m under the Avenida Marginal, with painted walls" },
            { "ATRIUM PIANO", "The route drives straight through the Hovione atrium" },
            { "BEACH VOLLEY", "Courts on the sand of Carcavelos beach" },
            { "NOVA KART WALL", "Between the plaza and the home straight" },
            { "DRIFT BOOST", "Hold SPACE in a corner, let go for a boost" },
            { "AIR TRICKS", "Press SHIFT in the air for a trick boost" },
            { "ONLINE RACE", "One room for the whole class, up to 15 racers" },
            { "NOON OR SUNSET", "Press T to switch the light over the beach" },
        };

        [SerializeField, Tooltip("Seconds each sight stays on the start screen's postcard")]
        private float sightSeconds = 4f;

        private VisualElement postcard;
        private Label sightTitle;
        private Label sightText;
        private int shownSight = -1;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            hudRoot = root.Q<VisualElement>("hud-root");
            startScreen = root.Q<VisualElement>("start-screen");
            bestLabel = root.Q<Label>("start-best");
            logo = root.Q<VisualElement>("title-logo");
            glint = root.Q<VisualElement>("title-glint");
            glintLetters = root.Q<VisualElement>("title-glint-letters");
            softGlint = root.Q<VisualElement>("title-glint-soft");
            softGlintLetters = root.Q<VisualElement>("title-glint-soft-letters");
            AddWordCopy(glintLetters);
            AddWordCopy(softGlintLetters);
            AddPostcard();
            introStart = -1f;
            // Photo mode (?photo=1 in the web page address): no menus and no HUD, only the game world, so we can
            // take clean screenshots of our own map for the key art of the web page and the menus.
            photoMode = IsPhotoMode(Application.absoluteURL);
        }

        // True only for photo=1 as its own query pair (?photo=1 or &photo=1). Before (G-audit, 19 Nov) a plain
        // "contains" also matched ?photo=10, ?nophoto=1 or #photo=1.
        public static bool IsPhotoMode(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }
            int hash = url.IndexOf('#');
            string beforeHash = hash >= 0 ? url.Substring(0, hash) : url;
            int query = beforeHash.IndexOf('?');
            if (query < 0)
            {
                return false;
            }
            string[] pairs = beforeHash.Substring(query + 1).Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                if (pairs[i] == "photo=1")
                {
                    return true;
                }
            }
            return false;
        }

        private bool photoMode;
        private float nextPhotoHide;

        // Photo mode hides every UI document (start screen, race HUD, minimap, item slot ...), again every second,
        // because some HUD parts are made later in the race.
        private void HideAllUiForPhotos()
        {
            if (Time.unscaledTime < nextPhotoHide)
            {
                return;
            }
            nextPhotoHide = Time.unscaledTime + 1f;
            UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            for (int i = 0; i < documents.Length; i++)
            {
                if (documents[i].rootVisualElement != null)
                {
                    documents[i].rootVisualElement.style.display = DisplayStyle.None;
                }
            }
        }

        // The "on the track" lower third: an orange tab, "ON THE TRACK" and the sight (USS: .pc in UI/FrontEnd.uss).
        private void AddPostcard()
        {
            if (postcard != null)
            {
                postcard.RemoveFromHierarchy();
            }
            postcard = new VisualElement { name = "start-postcard" };
            postcard.pickingMode = PickingMode.Ignore;
            postcard.AddToClassList("pc");
            VisualElement stamp = new VisualElement();
            stamp.pickingMode = PickingMode.Ignore;
            stamp.AddToClassList("pc-stamp");
            postcard.Add(stamp);
            VisualElement words = new VisualElement();
            words.pickingMode = PickingMode.Ignore;
            words.AddToClassList("pc-words");
            Label kicker = new Label("ON THE TRACK");
            kicker.AddToClassList("pc-kicker");
            kicker.AddToClassList("f-ui");
            sightTitle = new Label("");
            sightTitle.AddToClassList("pc-title");
            sightTitle.AddToClassList("f-display");
            sightText = new Label("");
            sightText.AddToClassList("pc-text");
            sightText.AddToClassList("f-ui");
            words.Add(kicker);
            words.Add(sightTitle);
            words.Add(sightText);
            postcard.Add(words);
            startScreen.Add(postcard);
            shownSight = -1;
        }

        // Phone round 17.11: on a phone the tips name the touch buttons (DRIFT, TRICK), not the keys.
        public static string SightText(int index, bool touch)
        {
            if (touch && Sights[index, 0] == "DRIFT BOOST")
            {
                return "Hold DRIFT in a corner, let go for a boost";
            }
            if (touch && Sights[index, 0] == "AIR TRICKS")
            {
                return "Tap TRICK in the air for a trick boost";
            }
            if (touch && Sights[index, 0] == "NOON OR SUNSET")
            {
                return "Tap the light button to switch noon and sunset";
            }
            return Sights[index, 1];
        }

        public static int SightCount
        {
            get { return Sights.GetLength(0); }
        }

        // Which sight shows: changes every sightSeconds; between two sights the card flips out and back in.
        private void UpdatePostcard()
        {
            float onScreen = Time.unscaledTime - introStart;
            int count = Sights.GetLength(0);
            int index = Mathf.FloorToInt(onScreen / sightSeconds) % count;
            float inSight = onScreen % sightSeconds;
            postcard.EnableInClassList("pc--out", inSight > sightSeconds - 0.25f);
            postcard.EnableInClassList("pc--live", onScreen >= sightSeconds); // after the first sight: no intro delay
            if (index != shownSight)
            {
                shownSight = index;
                sightTitle.text = Sights[index, 0];
                sightText.text = SightText(index, WebPage.TouchMode);
                postcard.EnableInClassList("pc--alt", index % 2 == 1); // the stamp changes colour too
            }
        }

        // The copy of the word for a sweep band: a box as big as the logo with the word picture where the real one is
        // (USS: .lg-glint-copy and its .lg-word child, tinted gold).
        private static void AddWordCopy(VisualElement host)
        {
            host.Clear();
            VisualElement word = new VisualElement();
            word.pickingMode = PickingMode.Ignore;
            word.AddToClassList("lg-layer");
            word.AddToClassList("lg-word");
            host.Add(word);
        }

        private void Update()
        {
            if (photoMode)
            {
                HideAllUiForPhotos();
            }
            bool onStart = raceManager.State == RaceState.Ready && startScreen.ClassListContains("intro-in");
            if (!onStart)
            {
                introStart = -1f;
                postcard.RemoveFromClassList("pc--live"); // the next visit starts with the intro delay again
                StopSweep();
                return;
            }
            if (introStart < 0f)
            {
                introStart = Time.unscaledTime;
            }
            bestLabel.EnableInClassList("ts-best--empty", bestLabel.text.Length == 0); // no empty chip without a best time
            UpdatePostcard();
            if (hudRoot.ClassListContains("reduce-motion"))
            {
                StopSweep();
                return;
            }

            // Where in the sweep cycle are we? 0..1 while a sweep runs, otherwise not sweeping.
            float sinceFirst = Time.unscaledTime - introStart - firstSweepSeconds;
            if (sinceFirst < 0f)
            {
                return;
            }
            float inCycle = sinceFirst % sweepEverySeconds;
            if (inCycle > sweepSeconds)
            {
                StopSweep();
                return;
            }
            float progress = Mathf.SmoothStep(0f, 1f, inCycle / sweepSeconds); // slow in, fast over the middle, slow out
            // The centre of the sheen runs from just left of the title to just right of it.
            float width = logo.resolvedStyle.width;
            float soft = softGlint.resolvedStyle.width;
            float centre = Mathf.Lerp(-soft * 0.5f, width + soft * 0.5f, progress);
            MoveBand(glint, glintLetters, centre);
            MoveBand(softGlint, softGlintLetters, centre);
            if (!sweepWasRunning)
            {
                glint.AddToClassList("lg-glint--on");
                softGlint.AddToClassList("lg-glint--on");
                sweepWasRunning = true;
            }
        }

        // The band goes to "centre", its gold letters the same distance back, so they stay on the white ones.
        private static void MoveBand(VisualElement band, VisualElement letters, float centre)
        {
            float x = centre - band.resolvedStyle.width * 0.5f;
            band.style.translate = new Translate(x, 0f);
            letters.style.translate = new Translate(-x, 0f);
        }

        private void StopSweep()
        {
            if (sweepWasRunning)
            {
                glint.RemoveFromClassList("lg-glint--on");
                softGlint.RemoveFromClassList("lg-glint--on");
                sweepWasRunning = false;
            }
        }
    }
}
