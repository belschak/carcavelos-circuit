using CarcavelosCircuit.Shared;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R3: shows the race on screen. Layout = UI/RaceHud.uxml, look = UI/RaceHud.uss (like HTML + CSS).
    // Every frame it reads the RaceManager and the kart. It never changes the race.
    // A label only gets a new text when the number it shows changes (see ShownNumber): building strings every frame
    // makes garbage, and in a Web build the garbage collector cleans up all at once, which shows as a hitch.
    [RequireComponent(typeof(UIDocument))]
    public class RaceHud : MonoBehaviour
    {
        [SerializeField]
        private RaceManager raceManager;

        [SerializeField, Tooltip("How long GO! stays on screen after the countdown")]
        private float goVisibleSeconds = 0.8f;

        private Label timerLabel;
        private Label bestLabel;
        private Label splitLabel;
        private Label hintLabel;
        private Label countdownLabel;
        private Label speedLabel;
        private Label boostLabel;
        private VisualElement boostFill;
        private VisualElement startScreen;
        private Label startBestLabel;
        private Label pressAnyKeyLabel;
        private VisualElement resultsScreen;
        private Label recordLabel;
        private Label finalTimeLabel;
        private Label finalDeltaLabel;
        private Button retryButton;
        private float goVisibleUntil;

        // What each label shows right now, so we only rebuild its text when this number changes.
        private readonly ShownNumber shownTimer = new ShownNumber();        // lap time in ms
        private readonly ShownNumber shownBest = new ShownNumber();         // best time in ms, -1 = none
        private readonly ShownNumber shownStartBest = new ShownNumber();    // same, on the start screen
        private readonly ShownNumber shownLoading = new ShownNumber();      // 1 = still loading, 0 = ready
        private readonly ShownNumber shownCountdown = new ShownNumber();    // 3, 2, 1, or -1 for GO!
        private readonly ShownNumber shownSplit = new ShownNumber();        // split delta in ms
        private readonly ShownNumber shownSpeed = new ShownNumber();        // km/h
        private readonly ShownNumber shownBoostWidth = new ShownNumber();   // boost bar in half percent steps
        private readonly ShownNumber shownBoostState = new ShownNumber();   // drift stage, +10 while boosting

        private void OnEnable()
        {
            // Find every element by the name it has in RaceHud.uxml.
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            timerLabel = root.Q<Label>("timer");
            bestLabel = root.Q<Label>("best");
            splitLabel = root.Q<Label>("split");
            hintLabel = root.Q<Label>("hint");
            countdownLabel = root.Q<Label>("countdown");
            speedLabel = root.Q<Label>("speed");
            boostLabel = root.Q<Label>("boost-label");
            boostFill = root.Q<VisualElement>("boost-fill");
            startScreen = root.Q<VisualElement>("start-screen");
            startBestLabel = root.Q<Label>("start-best");
            pressAnyKeyLabel = root.Q<Label>("press-any-key");
            resultsScreen = root.Q<VisualElement>("results-screen");
            recordLabel = root.Q<Label>("record");
            finalTimeLabel = root.Q<Label>("final-time");
            finalDeltaLabel = root.Q<Label>("final-delta");
            retryButton = root.Q<Button>("retry-button");

            // The labels are new objects after a re-enable, so every cached number must be written again.
            ForgetShownNumbers();

            // Not focusable: after a mouse click the button must not keep focus, or Space/Enter while driving would press it again.
            retryButton.focusable = false;
            retryButton.clicked += OnRetryClicked;
            raceManager.CountdownStep += OnCountdownStep;
            raceManager.LapFinished += OnLapFinished;
        }

        private void OnDisable()
        {
            retryButton.clicked -= OnRetryClicked;
            raceManager.CountdownStep -= OnCountdownStep;
            raceManager.LapFinished -= OnLapFinished;
        }

        private void ForgetShownNumbers()
        {
            shownTimer.Forget();
            shownBest.Forget();
            shownStartBest.Forget();
            shownLoading.Forget();
            shownCountdown.Forget();
            shownSplit.Forget();
            shownSpeed.Forget();
            shownBoostWidth.Forget();
            shownBoostState.Forget();
        }

        private void OnRetryClicked()
        {
            raceManager.Retry();
        }

        private void OnCountdownStep(int number)
        {
            if (number == 0)
            {
                goVisibleUntil = Time.time + goVisibleSeconds;
            }
        }

        // The results screen changes only once per lap, so it is filled when the lap ends, not every frame.
        private void OnLapFinished(float lapSeconds, bool isRecord)
        {
            finalTimeLabel.text = RaceTimer.Format(lapSeconds);
            SetVisible(recordLabel, isRecord);
            float previousBest = raceManager.PreviousBestSeconds;
            if (previousBest > 0f)
            {
                finalDeltaLabel.text = RaceTimer.FormatDelta(lapSeconds - previousBest) + " to your previous best";
            }
            else
            {
                finalDeltaLabel.text = "First lap on this track";
            }
        }

        private void Update()
        {
            RaceState state = raceManager.State;

            SetVisible(startScreen, state == RaceState.Ready);
            SetVisible(resultsScreen, state == RaceState.Results);

            if (state == RaceState.Ready)
            {
                UpdateStartScreen(); // hidden in every other state, so no need to update it then
            }
            UpdateCountdown(state);
            UpdateTimes();
            UpdateSplitAndHint(state);
            UpdateKartInfo();
        }

        private void UpdateStartScreen()
        {
            // Gentle blinking so the player notices "Press any key" (Time.time keeps going even before the race).
            pressAnyKeyLabel.style.opacity = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 2.5f));

            float best = raceManager.BestSeconds;
            if (shownStartBest.Changed(best > 0f ? RaceTimer.ToMilliseconds(best) : -1))
            {
                startBestLabel.text = best > 0f ? "Your best: " + RaceTimer.Format(best) : "";
            }

            if (shownLoading.Changed(raceManager.IsSetUp ? 0 : 1))
            {
                pressAnyKeyLabel.text = raceManager.IsSetUp ? "Press any key to start" : "Loading track...";
            }
        }

        private void UpdateCountdown(RaceState state)
        {
            bool showNumber = state == RaceState.Countdown && raceManager.CountdownNumber > 0;
            bool showGo = state == RaceState.Racing && Time.time < goVisibleUntil;
            SetVisible(countdownLabel, showNumber || showGo);
            int shown = showGo ? -1 : raceManager.CountdownNumber;
            if (shownCountdown.Changed(shown))
            {
                countdownLabel.text = showGo ? "GO!" : shown.ToString();
                countdownLabel.EnableInClassList("countdown-go", showGo);
            }
        }

        private void UpdateTimes()
        {
            int lapMilliseconds = RaceTimer.ToMilliseconds(raceManager.LapSeconds);
            if (shownTimer.Changed(lapMilliseconds))
            {
                timerLabel.text = RaceTimer.FormatMilliseconds(lapMilliseconds);
            }

            float best = raceManager.BestSeconds;
            int bestMilliseconds = best > 0f ? RaceTimer.ToMilliseconds(best) : -1;
            if (shownBest.Changed(bestMilliseconds))
            {
                bestLabel.text = best > 0f ? RaceTimer.FormatMilliseconds(bestMilliseconds) : "--:--.---";
            }
        }

        private void UpdateSplitAndHint(RaceState state)
        {
            bool showSplit = state == RaceState.Racing && raceManager.HasSplitDelta && Time.time < raceManager.SplitVisibleUntil;
            SetVisible(splitLabel, showSplit);
            if (showSplit)
            {
                int deltaMilliseconds = RaceTimer.ToMilliseconds(raceManager.LastSplitDelta);
                if (shownSplit.Changed(deltaMilliseconds))
                {
                    splitLabel.text = RaceTimer.FormatDeltaMilliseconds(deltaMilliseconds);
                    // Colour from the rounded value too, so "+0.000" is never shown in the "ahead" colour.
                    splitLabel.EnableInClassList("split-ahead", deltaMilliseconds < 0);
                    splitLabel.EnableInClassList("split-behind", deltaMilliseconds >= 0);
                }
            }

            bool showHint = state == RaceState.Racing && Time.time < raceManager.HintVisibleUntil;
            SetVisible(hintLabel, showHint);
            if (showHint)
            {
                hintLabel.text = raceManager.HintText; // the same string object every frame, nothing new is built
            }
        }

        // Speed and the drift boost cue: the bar fills while drifting, blue at stage 1, orange at stage 2.
        private void UpdateKartInfo()
        {
            IKartState kart = raceManager.Kart;
            if (kart == null)
            {
                return;
            }
            int kmh = Mathf.RoundToInt(Mathf.Abs(kart.Speed) * 3.6f); // m/s to km/h
            if (shownSpeed.Changed(kmh))
            {
                speedLabel.text = kmh.ToString();
            }

            // Half-percent steps: finer than the eye can see, but the width is not rewritten every frame.
            int widthSteps = Mathf.RoundToInt(kart.DriftCharge01 * 200f);
            if (shownBoostWidth.Changed(widthSteps))
            {
                boostFill.style.width = Length.Percent(widthSteps * 0.5f);
            }

            int stage = kart.DriftStage;
            if (shownBoostState.Changed(kart.IsBoosting ? stage + 10 : stage))
            {
                boostFill.EnableInClassList("boost-stage-1", stage == 1);
                boostFill.EnableInClassList("boost-stage-2", stage == 2);
                boostLabel.EnableInClassList("boost-active", kart.IsBoosting);
                if (kart.IsBoosting)
                {
                    boostLabel.text = "BOOST!";
                }
                else if (stage == 2)
                {
                    boostLabel.text = "DRIFT  MAX";
                }
                else if (stage == 1)
                {
                    boostLabel.text = "DRIFT  1";
                }
                else
                {
                    boostLabel.text = "DRIFT";
                }
            }
        }

        // Only writes the style when it changes: every style write makes UI Toolkit check the layout again.
        private static void SetVisible(VisualElement element, bool visible)
        {
            DisplayStyle wanted = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display != wanted)
            {
                element.style.display = wanted;
            }
        }
    }
}
