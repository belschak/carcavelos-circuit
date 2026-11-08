using CarcavelosCircuit.Shared;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R3: shows the race on screen. Layout = UI/RaceHud.uxml, look = UI/RaceHud.uss (like HTML + CSS).
    // Every frame it reads the RaceManager and the kart and updates the labels. It never changes the race.
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

            // Not focusable: after a mouse click the button must not keep focus, or Space/Enter while driving would press it again.
            retryButton.focusable = false;
            retryButton.clicked += OnRetryClicked;
            raceManager.CountdownStep += OnCountdownStep;
        }

        private void OnDisable()
        {
            retryButton.clicked -= OnRetryClicked;
            raceManager.CountdownStep -= OnCountdownStep;
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

        private void Update()
        {
            RaceState state = raceManager.State;

            SetVisible(startScreen, state == RaceState.Ready);
            SetVisible(resultsScreen, state == RaceState.Results);

            UpdateStartScreen();
            UpdateCountdown(state);
            UpdateTimes();
            UpdateSplitAndHint(state);
            UpdateKartInfo();
            if (state == RaceState.Results)
            {
                UpdateResults();
            }
        }

        private void UpdateStartScreen()
        {
            // Gentle blinking so the player notices "Press any key" (Time.time keeps going even before the race).
            pressAnyKeyLabel.style.opacity = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 2.5f));
            float best = raceManager.BestSeconds;
            startBestLabel.text = best > 0f ? "Your best: " + RaceTimer.Format(best) : "";
            if (!raceManager.IsSetUp)
            {
                pressAnyKeyLabel.text = "Loading track...";
            }
            else
            {
                pressAnyKeyLabel.text = "Press any key to start";
            }
        }

        private void UpdateCountdown(RaceState state)
        {
            bool showNumber = state == RaceState.Countdown && raceManager.CountdownNumber > 0;
            bool showGo = state == RaceState.Racing && Time.time < goVisibleUntil;
            SetVisible(countdownLabel, showNumber || showGo);
            countdownLabel.text = showGo ? "GO!" : raceManager.CountdownNumber.ToString();
            countdownLabel.EnableInClassList("countdown-go", showGo);
        }

        private void UpdateTimes()
        {
            timerLabel.text = RaceTimer.Format(raceManager.LapSeconds);
            float best = raceManager.BestSeconds;
            bestLabel.text = best > 0f ? RaceTimer.Format(best) : "--:--.---";
        }

        private void UpdateSplitAndHint(RaceState state)
        {
            bool showSplit = state == RaceState.Racing && raceManager.HasSplitDelta && Time.time < raceManager.SplitVisibleUntil;
            SetVisible(splitLabel, showSplit);
            if (showSplit)
            {
                float delta = raceManager.LastSplitDelta;
                splitLabel.text = RaceTimer.FormatDelta(delta);
                splitLabel.EnableInClassList("split-ahead", delta < 0f);
                splitLabel.EnableInClassList("split-behind", delta >= 0f);
            }

            bool showHint = state == RaceState.Racing && Time.time < raceManager.HintVisibleUntil;
            SetVisible(hintLabel, showHint);
            hintLabel.text = raceManager.HintText;
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
            speedLabel.text = kmh.ToString();

            boostFill.style.width = Length.Percent(kart.DriftCharge01 * 100f);
            boostFill.EnableInClassList("boost-stage-1", kart.DriftStage == 1);
            boostFill.EnableInClassList("boost-stage-2", kart.DriftStage == 2);

            if (kart.IsBoosting)
            {
                boostLabel.text = "BOOST!";
            }
            else if (kart.DriftStage == 2)
            {
                boostLabel.text = "DRIFT  MAX";
            }
            else if (kart.DriftStage == 1)
            {
                boostLabel.text = "DRIFT  1";
            }
            else
            {
                boostLabel.text = "DRIFT";
            }
            boostLabel.EnableInClassList("boost-active", kart.IsBoosting);
        }

        private void UpdateResults()
        {
            finalTimeLabel.text = RaceTimer.Format(raceManager.LapSeconds);
            SetVisible(recordLabel, raceManager.LastLapWasRecord);
            float previousBest = raceManager.PreviousBestSeconds;
            if (previousBest > 0f)
            {
                finalDeltaLabel.text = RaceTimer.FormatDelta(raceManager.LapSeconds - previousBest) + " to your previous best";
            }
            else
            {
                finalDeltaLabel.text = "First lap on this track";
            }
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
