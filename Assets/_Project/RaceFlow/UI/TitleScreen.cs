using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // The title opener on the start screen (task R11 "Carcavelos Round 3"). On the HUD object, next to RaceHud.
    // RaceHud still runs the start screen (state class st-ready, "intro-in" a moment after it shows, the breathing
    // pill, the best time). This only adds the title show, all styled in UI/StartScreen.uss:
    // 1. Logo reveal: the letters of CARCAVELOS drop in one after another (one Label per letter, made here, each
    //    with its own transition-delay), the racing stripes slide in, the wave wipes open, CIRCUIT slides in, and
    //    the team's slogan CARCAVELOS ROUND 3 lands like a stamp. The USS animates; C# only sets the delays.
    // 2. Light sweep: every few seconds a band of light runs over the letters. Trick: a narrow box with
    //    overflow: hidden moves to the right while a second, gold copy of the letters inside it moves left by the
    //    same amount, so the copy stays exactly on top of the white letters and only shows inside the band.
    //    Written as two translates per frame (no layout pass), only while the sweep runs.
    // With reduced motion (the "reduce-motion" class HudMenus puts on hud-root) everything is simply there:
    // no drop, no stamp, no sweep (the USS sets the durations to 0, this script skips the sweep).
    [RequireComponent(typeof(UIDocument))]
    public class TitleScreen : MonoBehaviour
    {
        private const string Title = "CARCAVELOS";

        [SerializeField, Tooltip("The race brain: the title only animates on the start screen (state Ready)")]
        private RaceManager raceManager;

        [SerializeField, Tooltip("Milliseconds between two letters dropping in")]
        private int letterStepMs = 45;

        [SerializeField, Tooltip("Seconds after the start screen appears before the first light sweep")]
        private float firstSweepSeconds = 1.6f;

        [SerializeField, Tooltip("Seconds from one light sweep to the next")]
        private float sweepEverySeconds = 4.5f;

        [SerializeField, Tooltip("How long one light sweep takes to cross the title, in seconds")]
        private float sweepSeconds = 0.9f;

        private VisualElement hudRoot;
        private VisualElement startScreen;
        private VisualElement logo;
        private VisualElement glint;
        private VisualElement glintLetters;
        private float introStart = -1f; // real time when "intro-in" appeared, -1 = not on the start screen
        private bool sweepWasRunning;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            hudRoot = root.Q<VisualElement>("hud-root");
            startScreen = root.Q<VisualElement>("start-screen");
            logo = root.Q<VisualElement>("title-logo");
            glint = root.Q<VisualElement>("title-glint");
            glintLetters = root.Q<VisualElement>("title-glint-letters");
            AddLetters(root.Q<VisualElement>("title-letters"), true);
            AddLetters(glintLetters, false);
            introStart = -1f;
        }

        // One Label per letter. The real letters get a growing delay, so they drop in from left to right.
        // The gold copy for the sweep has no delay and no drop (USS: .ts-letters--glint).
        private void AddLetters(VisualElement host, bool staggered)
        {
            host.Clear();
            for (int i = 0; i < Title.Length; i++)
            {
                Label letter = new Label(Title.Substring(i, 1));
                letter.pickingMode = PickingMode.Ignore;
                letter.AddToClassList("ts-letter");
                letter.AddToClassList("f-display");
                if (staggered)
                {
                    int delay = 80 + i * letterStepMs;
                    letter.style.transitionDelay = new StyleList<TimeValue>(new System.Collections.Generic.List<TimeValue> { new TimeValue(delay, TimeUnit.Millisecond) });
                }
                host.Add(letter);
            }
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
            float width = logo.resolvedStyle.width;
            float band = glint.resolvedStyle.width;
            float x = Mathf.Lerp(-band, width, progress);
            glint.style.translate = new Translate(x, 0f);
            glintLetters.style.translate = new Translate(-x, 0f);
            if (!sweepWasRunning)
            {
                glint.AddToClassList("ts-glint--on");
                sweepWasRunning = true;
            }
        }

        private void StopSweep()
        {
            if (sweepWasRunning)
            {
                glint.RemoveFromClassList("ts-glint--on");
                sweepWasRunning = false;
            }
        }
    }
}
