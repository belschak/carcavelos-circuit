using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // The title opener on the start screen (task R11, logo since the "Postcard" menu redesign). On the HUD object, next
    // to RaceHud. RaceHud still runs the start screen (state class st-ready, "intro-in" a moment after it shows, the
    // breathing prompt, the best time). This only adds the title show, styled in UI/Postcard.uss:
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
            introStart = -1f;
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
            bool onStart = raceManager.State == RaceState.Ready && startScreen.ClassListContains("intro-in");
            if (!onStart)
            {
                introStart = -1f;
                StopSweep();
                return;
            }
            if (introStart < 0f)
            {
                introStart = Time.unscaledTime;
            }
            bestLabel.EnableInClassList("ts-best--empty", bestLabel.text.Length == 0); // no empty chip without a best time
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
