using CarcavelosCircuit.Shared;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R3: shows the race on screen. Layout = UI/RaceHud.uxml, look = UI/RaceHud.uss (like HTML + CSS).
    // Every frame it reads the RaceManager and the kart. It never changes the race (except the Retry button,
    // which calls the same public Retry() as the Enter key).
    //
    // How the HUD animates (details at the top of RaceHud.uss):
    // - One state class on the root (st-ready, st-countdown, st-racing, st-results) fades whole screens in and out.
    // - Parts that come and go get the class "is-shown" (HudFx.SetShown). Nothing uses display:none, because
    //   UI Toolkit cannot animate from display:none.
    // - A "punch" (HudFx.Punch) jumps into a pop class for two frames, then the USS transition animates back.
    // C# only switches classes and writes texts; the USS decides what that looks like and how long it takes.
    //
    // A label only gets a new text when the number it shows changes (see ShownNumber): building strings every frame
    // makes garbage, and in a Web build the garbage collector cleans up all at once, which shows as a hitch.
    [RequireComponent(typeof(UIDocument))]
    public class RaceHud : MonoBehaviour
    {
        [SerializeField]
        private RaceManager raceManager;

        [SerializeField, Range(0f, 1f), Tooltip("Drift charge where stage 1 starts (tick mark on the drift bar). Placeholder kart: 0.8 s of 1.6 s")]
        private float stage1At = 0.5f;

        [SerializeField, Tooltip("How long the R / Enter key hint stays after GO (it comes back for a moment after a recover)")]
        private float controlsHintSeconds = 8f;

        // Below this panel width (reference pixels) the HUD uses the "hud--narrow" layout. The longest hint chip
        // ("Missed a checkpoint! Press R to go back", about 600 px) needs 1230 px to stay clear of the lap time panel.
        private const float NarrowHudWidth = 1230f;

        // ---- Elements (found by name in RaceHud.uxml) ----
        private VisualElement hudRoot;
        private VisualElement timePanel;
        private Label timerMain;
        private Label timerFraction;
        private Label bestLabel;
        private Label lapCounter;
        private VisualElement cpPanel;
        private VisualElement cpPips;
        private Label cpCount;
        private VisualElement split;
        private VisualElement splitChip;
        private Label splitCp;
        private Label splitWord;
        private Label splitValue;
        private VisualElement hint;
        private VisualElement hintChip;
        private Label hintText;
        private VisualElement wrongWay;
        private VisualElement wrongWayChip;
        private VisualElement stuck;
        private VisualElement controlsHint;
        private VisualElement driftPanel;
        private Label driftLabel;
        private VisualElement driftPip1;
        private VisualElement driftPip2;
        private VisualElement driftBar;
        private VisualElement driftFill;
        private VisualElement driftShine;
        private Label speedLabel;
        private SpeedArc speedArc;
        private VisualElement edgeGlowLeft;
        private VisualElement edgeGlowRight;
        private VisualElement countdown;
        private VisualElement countdownTile;
        private Label countdownNumber;
        private VisualElement[] countdownLamps;
        private VisualElement goFlash;
        private VisualElement startScreen;
        private Label pressAnyKey;
        private Label startBest;
        private VisualElement resultsScreen;
        private VisualElement resultsCard;
        private Label recordStamp;
        private Label finalTime;
        private VisualElement resultsDelta;
        private Label resultsDeltaValue;
        private Label resultsDeltaText;
        private Label resultsBest;
        private Button retryButton;
        private VisualElement medalTarget;     // next-medal chip under the time panel (task P5)
        private Label medalTargetName;
        private Label medalTargetTime;
        private Label hintIcon;
        private VisualElement resultsMedalBadge;
        private Label resultsMedalName;
        private Label resultsMedalNext;
        private VisualElement statDrifts;
        private VisualElement statBoosts;
        private Label statSpeedValue;
        private Label statDriftsValue;
        private Label statBoostsValue;
        private Label statAirValue;
        private VisualElement[] pips = new VisualElement[0];

        // Gradient textures made at runtime (USS has no gradients). Destroyed in OnDisable.
        private Texture2D startGradient;
        private Texture2D resultsGradient;
        private Texture2D glowLeftTexture;
        private Texture2D glowRightTexture;

        // ---- Logic helpers (plain C#, tested in Edit Mode) ----
        private readonly WrongWayDetector wrongWayDetector = new WrongWayDetector();
        private readonly StuckDetector stuckDetector = new StuckDetector();

        // What each label shows right now, so we only rebuild its text when this number changes.
        private readonly ShownNumber shownState = new ShownNumber();        // RaceState as int
        private readonly ShownNumber shownTimer = new ShownNumber();        // lap time in ms (fraction label)
        private readonly ShownNumber shownTimerMain = new ShownNumber();    // lap time in whole seconds (main label)
        private readonly ShownNumber shownBest = new ShownNumber();         // best time in ms, -1 = none
        private readonly ShownNumber shownStartBest = new ShownNumber();    // same, on the start screen
        private readonly ShownNumber shownLoading = new ShownNumber();      // 1 = still loading, 0 = ready
        private readonly ShownNumber shownLaps = new ShownNumber();         // lap count
        private readonly ShownNumber shownCpTotal = new ShownNumber();      // checkpoints on the track
        private readonly ShownNumber shownCpPassed = new ShownNumber();     // checkpoints passed this lap
        private readonly ShownNumber shownPulse = new ShownNumber();        // 0/1 blink phase of the "next" pip
        private readonly ShownNumber shownSpeed = new ShownNumber();        // km/h
        private readonly ShownNumber shownDriftFill = new ShownNumber();    // drift bar in half percent steps
        private readonly ShownNumber shownDriftState = new ShownNumber();   // drift stage, +10 while boosting
        private readonly ShownNumber shownFinal = new ShownNumber();        // results time in ms (count-up)
        private readonly ShownNumber shownTarget = new ShownNumber();       // next medal (Medal as int), -1 = no chip
        private readonly ShownNumber shownHintKind = new ShownNumber();     // HintKind as int

        private float resultsStartTime;
        private bool tickerDone;
        private float controlsVisibleUntil;
        private float smoothedKmh;
        private float nextShineTime;
        private int countdownGeneration; // makes late scheduled GO-exit calls harmless after a quick retry
        private bool hintWasShown;
        private int lastStage;      // drift stage last frame, to spot a stage-up
        private bool lastBoosting;  // boost last frame, to spot the boost start
        private int targetMilliseconds = -1; // lap time the next medal needs, -1 = none
        private bool targetMissed;  // the lap is already slower than the target medal

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            FindElements(root);
            AddDrawnShapes(root);
            MakeTextures();

            // Decorations never catch clicks. Only the Retry button does, and only while the results show.
            HudFx.IgnoreClicksDeep(root);
            retryButton.focusable = false; // after a mouse click it must not keep focus, or Space/Enter would press it again

            // Narrow windows (4:3, 16:10): the HUD scales by height, so the panel gets less width than the 1280 reference.
            // Then the centred hint chip would cover the lap time and the key bar the drift panel: "hud--narrow" moves them.
            hudRoot.RegisterCallback<GeometryChangedEvent>(OnHudResized);

            // The labels are new objects after a re-enable, so every cached number must be written again.
            ForgetShownNumbers();
            pips = new VisualElement[0];

            retryButton.clicked += OnRetryClicked;
            raceManager.CountdownStep += OnCountdownStep;
            raceManager.CheckpointPassed += OnCheckpointPassed;
            raceManager.LapFinished += OnLapFinished;
            raceManager.Restarted += OnRestarted;
            raceManager.Recovered += OnRecovered;
        }

        private void OnDisable()
        {
            hudRoot.UnregisterCallback<GeometryChangedEvent>(OnHudResized);
            retryButton.clicked -= OnRetryClicked;
            raceManager.CountdownStep -= OnCountdownStep;
            raceManager.CheckpointPassed -= OnCheckpointPassed;
            raceManager.LapFinished -= OnLapFinished;
            raceManager.Restarted -= OnRestarted;
            raceManager.Recovered -= OnRecovered;
            HudTextures.Destroy(startGradient);
            HudTextures.Destroy(resultsGradient);
            HudTextures.Destroy(glowLeftTexture);
            HudTextures.Destroy(glowRightTexture);
        }

        private void OnHudResized(GeometryChangedEvent evt)
        {
            hudRoot.EnableInClassList("hud--narrow", evt.newRect.width < NarrowHudWidth);
        }

        private void FindElements(VisualElement root)
        {
            hudRoot = root.Q<VisualElement>("hud-root");
            timePanel = root.Q<VisualElement>("time-panel");
            timerMain = root.Q<Label>("timer-main");
            timerFraction = root.Q<Label>("timer-fraction");
            bestLabel = root.Q<Label>("best");
            lapCounter = root.Q<Label>("lap-counter");
            cpPanel = root.Q<VisualElement>("cp-panel");
            cpPips = root.Q<VisualElement>("cp-pips");
            cpCount = root.Q<Label>("cp-count");
            split = root.Q<VisualElement>("split");
            splitChip = root.Q<VisualElement>("split-chip");
            splitCp = root.Q<Label>("split-cp");
            splitWord = root.Q<Label>("split-word");
            splitValue = root.Q<Label>("split-value");
            hint = root.Q<VisualElement>("hint");
            hintChip = hint.Q<VisualElement>(className: "callout-chip");
            hintText = root.Q<Label>("hint-text");
            wrongWay = root.Q<VisualElement>("wrong-way");
            wrongWayChip = root.Q<VisualElement>("wrong-way-chip");
            stuck = root.Q<VisualElement>("stuck");
            controlsHint = root.Q<VisualElement>("controls-hint");
            driftPanel = root.Q<VisualElement>("drift-panel");
            driftLabel = root.Q<Label>("drift-label");
            driftPip1 = root.Q<VisualElement>("drift-pip-1");
            driftPip2 = root.Q<VisualElement>("drift-pip-2");
            driftBar = root.Q<VisualElement>("drift-bar");
            driftFill = root.Q<VisualElement>("drift-fill");
            driftShine = root.Q<VisualElement>("drift-shine");
            speedLabel = root.Q<Label>("speed");
            edgeGlowLeft = root.Q<VisualElement>("edge-glow-left");
            edgeGlowRight = root.Q<VisualElement>("edge-glow-right");
            countdown = root.Q<VisualElement>("countdown");
            countdownTile = root.Q<VisualElement>("countdown-tile");
            countdownNumber = root.Q<Label>("countdown-number");
            countdownLamps = root.Q<VisualElement>("countdown-lamps").Query<VisualElement>(className: "cd-lamp").ToList().ToArray();
            goFlash = root.Q<VisualElement>("go-flash");
            startScreen = root.Q<VisualElement>("start-screen");
            pressAnyKey = root.Q<Label>("press-any-key");
            startBest = root.Q<Label>("start-best");
            resultsScreen = root.Q<VisualElement>("results-screen");
            resultsCard = root.Q<VisualElement>("results-card");
            recordStamp = root.Q<Label>("record");
            finalTime = root.Q<Label>("final-time");
            resultsDelta = root.Q<VisualElement>("res-delta");
            resultsDeltaValue = root.Q<Label>("res-delta-value");
            resultsDeltaText = root.Q<Label>("res-delta-text");
            resultsBest = root.Q<Label>("res-best");
            retryButton = root.Q<Button>("retry-button");
            medalTarget = root.Q<VisualElement>("medal-target");
            medalTargetName = root.Q<Label>("medal-target-name");
            medalTargetTime = root.Q<Label>("medal-target-time");
            hintIcon = root.Q<Label>("hint-icon");
            resultsMedalBadge = root.Q<VisualElement>("res-medal-badge");
            resultsMedalName = root.Q<Label>("res-medal-name");
            resultsMedalNext = root.Q<Label>("res-medal-next");
            statDrifts = root.Q<VisualElement>("stat-drifts");
            statBoosts = root.Q<VisualElement>("stat-boosts");
            statSpeedValue = root.Q<Label>("stat-speed-value");
            statDriftsValue = root.Q<Label>("stat-drifts-value");
            statBoostsValue = root.Q<Label>("stat-boosts-value");
            statAirValue = root.Q<Label>("stat-air-value");

            driftFill.style.scale = new Scale(new Vector3(0f, 1f, 1f));
            root.Q<VisualElement>("drift-tick").style.left = Length.Percent(stage1At * 100f);
        }

        // Shapes drawn in code (UI/SpeedArc.cs, CalcadaWave.cs, AzulejoPattern.cs) go into the empty "...-host" elements.
        private void AddDrawnShapes(VisualElement root)
        {
            speedArc = new SpeedArc();
            root.Q<VisualElement>("speed-arc-host").Add(speedArc);
            root.Q<VisualElement>("start-wave-host").Add(new CalcadaWave());
            root.Q<VisualElement>("results-wave-host").Add(new CalcadaWave());
            root.Q<VisualElement>("start-pattern-host").Add(new AzulejoPattern());
            root.Q<VisualElement>("results-pattern-host").Add(new AzulejoPattern());
        }

        private void MakeTextures()
        {
            // Start screen: deep Atlantic at the top, sea blue, a thin sunset line, dark at the bottom.
            // The middle stays fairly see-through so the 3D scene shows behind the title.
            startGradient = HudTextures.VerticalGradient(
                HudColors.WithAlpha(HudColors.BlueDeep, 0.94f),
                HudColors.WithAlpha(HudColors.BlueDeep, 0.80f),
                HudColors.WithAlpha(HudColors.Blue, 0.55f),
                HudColors.WithAlpha(HudColors.Orange, 0.42f),
                HudColors.WithAlpha(HudColors.Black, 0.92f));
            Child(startScreen, "start-bg").style.backgroundImage = new StyleBackground(startGradient);

            resultsGradient = HudTextures.VerticalGradient(
                HudColors.WithAlpha(HudColors.BlueDeep, 0.84f),
                HudColors.WithAlpha(HudColors.Black, 0.92f));
            Child(resultsScreen, "results-bg").style.backgroundImage = new StyleBackground(resultsGradient);

            glowLeftTexture = HudTextures.EdgeGlow(HudColors.Orange, 0.55f, true);
            glowRightTexture = HudTextures.EdgeGlow(HudColors.Orange, 0.55f, false);
            edgeGlowLeft.style.backgroundImage = new StyleBackground(glowLeftTexture);
            edgeGlowRight.style.backgroundImage = new StyleBackground(glowRightTexture);
        }

        private static VisualElement Child(VisualElement parent, string name)
        {
            return parent.Q<VisualElement>(name);
        }

        private void ForgetShownNumbers()
        {
            shownState.Forget();
            shownTimer.Forget();
            shownTimerMain.Forget();
            shownBest.Forget();
            shownStartBest.Forget();
            shownLoading.Forget();
            shownLaps.Forget();
            shownCpTotal.Forget();
            shownCpPassed.Forget();
            shownPulse.Forget();
            shownSpeed.Forget();
            shownDriftFill.Forget();
            shownDriftState.Forget();
            shownFinal.Forget();
            shownTarget.Forget();
            shownHintKind.Forget();
        }

        private void OnRetryClicked()
        {
            raceManager.Retry();
        }

        // ================= Events from the RaceManager =================

        // 3, 2, 1 = number steps, 0 = GO. Each step "punches" the diamond tile and lights one more lamp.
        private void OnCountdownStep(int number)
        {
            countdownGeneration++;
            countdown.RemoveFromClassList("cd--exit");
            HudFx.SetShown(countdown, true);
            countdown.EnableInClassList("cd--go", number == 0);
            countdownNumber.text = number == 0 ? "GO!" : CountdownDigit(number);
            int from = raceManager.CountdownFrom;
            for (int i = 0; i < countdownLamps.Length; i++)
            {
                countdownLamps[i].EnableInClassList("cd-lamp--lit", number > 0 && i < from - number + 1);
            }
            HudFx.Punch(countdownTile, number == 0 ? "cd-tile--pop-go" : "cd-tile--pop");

            if (number == 0)
            {
                HudFx.Punch(goFlash, "go-flash--pop"); // one soft flash per GO (photosensitivity-safe)
                HudFx.Punch(timePanel, "panel--pop");
                controlsVisibleUntil = Time.time + controlsHintSeconds;
                wrongWayDetector.Reset();
                stuckDetector.Reset();

                // GO flies up and fades after about half a second. The generation check ignores these calls
                // if a retry started a new countdown in the meantime.
                int generation = countdownGeneration;
                countdown.schedule.Execute(() =>
                {
                    if (generation == countdownGeneration) countdown.AddToClassList("cd--exit");
                }).ExecuteLater(550);
                countdown.schedule.Execute(() =>
                {
                    if (generation == countdownGeneration) HudFx.SetShown(countdown, false);
                }).ExecuteLater(820);
            }
        }

        private static string CountdownDigit(int number)
        {
            switch (number)
            {
                case 1: return "1";
                case 2: return "2";
                case 3: return "3";
                default: return number.ToString();
            }
        }

        // A checkpoint counted: slide in the split chip (or punch it if it is still showing).
        private void OnCheckpointPassed(int index)
        {
            int toPass = CheckpointProgress.CheckpointsToPass(raceManager.CheckpointCount);
            if (raceManager.HasSplitDelta)
            {
                int deltaMilliseconds = RaceTimer.ToMilliseconds(raceManager.LastSplitDelta);
                bool ahead = deltaMilliseconds < 0; // from the rounded value, so "+0.000" is never "ahead"
                splitCp.text = "CP " + index;
                splitWord.text = ahead ? "AHEAD" : "BEHIND";
                splitValue.text = HudFormat.Delta(deltaMilliseconds, false);
                split.EnableInClassList("split--behind", !ahead);
                split.EnableInClassList("split--big", deltaMilliseconds <= -500); // a big gain gets a sand frame
                split.RemoveFromClassList("split--neutral");
            }
            else
            {
                // No best time yet (first run): still confirm the checkpoint.
                splitCp.text = "CHECKPOINT";
                splitWord.text = "CLEAR";
                splitValue.text = index + "/" + toPass;
                split.RemoveFromClassList("split--behind");
                split.RemoveFromClassList("split--big");
                split.AddToClassList("split--neutral");
            }

            if (split.ClassListContains("is-shown"))
            {
                HudFx.Punch(splitChip, "split-chip--pop");
            }
            else
            {
                HudFx.SetShown(split, true);
            }
        }

        // The results screen changes only once per lap, so it is filled when the lap ends, not every frame.
        private void OnLapFinished(float lapSeconds, bool isRecord)
        {
            float previousBest = raceManager.PreviousBestSeconds;
            resultsCard.EnableInClassList("card--record", isRecord);
            recordStamp.EnableInClassList("record--on", isRecord);
            resultsDelta.RemoveFromClassList("res-delta--ahead");
            resultsDelta.RemoveFromClassList("res-delta--first");

            if (previousBest > 0f)
            {
                int deltaMilliseconds = RaceTimer.ToMilliseconds(lapSeconds) - RaceTimer.ToMilliseconds(previousBest);
                resultsDeltaValue.style.display = DisplayStyle.Flex;
                resultsDeltaValue.text = HudFormat.Delta(deltaMilliseconds, true);
                // The words follow the rounded number on screen, so "+0.000 s" never says "faster".
                if (deltaMilliseconds < 0)
                {
                    resultsDeltaText.text = "FASTER THAN YOUR BEST";
                }
                else if (deltaMilliseconds == 0)
                {
                    resultsDeltaText.text = "SAME AS YOUR BEST";
                }
                else
                {
                    resultsDeltaText.text = "SLOWER THAN YOUR BEST";
                }
                resultsDelta.EnableInClassList("res-delta--ahead", deltaMilliseconds < 0);
                resultsBest.text = (isRecord ? "PREVIOUS BEST  " : "YOUR BEST  ") + HudFormat.Time(RaceTimer.ToMilliseconds(previousBest));
            }
            else
            {
                resultsDeltaValue.style.display = DisplayStyle.None;
                resultsDeltaText.text = "FIRST LAP: YOUR TIME TO BEAT";
                resultsDelta.AddToClassList("res-delta--first");
                resultsBest.text = "";
            }
            finalTime.text = HudFormat.Time(0);
            shownFinal.Forget();
            ShowMedal(raceManager.LastMedal, raceManager.PreviousBestMedal);
            ShowLapReport(raceManager.Stats);
        }

        // The medal stamp (USS animates it in) and the line under it: what to aim for next.
        private void ShowMedal(Medal medal, Medal bestBefore)
        {
            SetMedalClass(resultsMedalBadge, medal);
            resultsMedalName.text = MedalRules.Name(medal);
            bool isNew = medal != Medal.None && medal > bestBefore;
            resultsMedalNext.EnableInClassList("res-medal-next--new", isNew);

            Medal best = raceManager.BestMedal; // already includes this lap
            Medal next = MedalRules.NextTarget(medal);
            float nextSeconds = raceManager.MedalSeconds(next);
            if (medal < best)
            {
                resultsMedalNext.text = "YOUR BEST MEDAL  " + MedalRules.Name(best);
            }
            else if (medal == Medal.DeansList)
            {
                resultsMedalNext.text = isNew ? "NEW MEDAL!  TOP TIER REACHED" : "TOP TIER: BEAT YOUR OWN TIME";
            }
            else if (nextSeconds > 0f)
            {
                string start = isNew ? "NEW MEDAL!  NEXT  " : "NEXT  ";
                resultsMedalNext.text = start + MedalRules.Name(next) + "  " + HudFormat.Time(RaceTimer.ToMilliseconds(nextSeconds));
            }
            else
            {
                resultsMedalNext.text = ""; // medal times broken (RaceManager logs the error)
            }
        }

        // Lap report: four numbers from RunStats. A zero drift or boost count is dimmed (the coach explains how).
        private void ShowLapReport(RunStats stats)
        {
            statSpeedValue.text = HudFormat.Whole(Mathf.RoundToInt(stats.TopSpeed * 3.6f)); // m/s to km/h
            statDriftsValue.text = HudFormat.Whole(stats.Drifts);
            statBoostsValue.text = HudFormat.Whole(stats.Boosts);
            statAirValue.text = HudFormat.OneDecimal(stats.AirSeconds) + " s";
            statDrifts.EnableInClassList("stat--zero", stats.Drifts == 0);
            statBoosts.EnableInClassList("stat--zero", stats.Boosts == 0);
        }

        // Exactly one medal colour class (medal--bronze ... medal--none) on the element.
        private static void SetMedalClass(VisualElement element, Medal medal)
        {
            for (int i = (int)Medal.None; i <= (int)Medal.DeansList; i++)
            {
                element.EnableInClassList(MedalRules.ColourClass((Medal)i), i == (int)medal);
            }
        }

        private void OnRestarted()
        {
            wrongWayDetector.Reset();
            stuckDetector.Reset();
            HudFx.SetShown(split, false);
        }

        private void OnRecovered()
        {
            wrongWayDetector.Reset();
            stuckDetector.Reset();
            controlsVisibleUntil = Mathf.Max(controlsVisibleUntil, Time.time + 4f);
        }

        // ================= Every frame =================

        private void Update()
        {
            RaceState state = raceManager.State;
            if (shownState.Changed((int)state))
            {
                EnterState(state);
            }

            if (state == RaceState.Ready)
            {
                UpdateStartScreen(); // hidden in every other state, so no need to update it then
            }
            UpdateTimes();
            UpdateMedalTarget(state);
            UpdateCheckpoints(state);
            UpdateSplit(state);
            UpdateCallouts(state);
            UpdateKartInfo(state);
            HudFx.SetShown(controlsHint, state == RaceState.Countdown || (state == RaceState.Racing && Time.time < controlsVisibleUntil));
            if (state == RaceState.Results)
            {
                UpdateResults();
            }
        }

        // Runs once when the race state changes: swaps the root state class and starts screen animations.
        private void EnterState(RaceState state)
        {
            hudRoot.EnableInClassList("st-ready", state == RaceState.Ready);
            hudRoot.EnableInClassList("st-countdown", state == RaceState.Countdown);
            hudRoot.EnableInClassList("st-racing", state == RaceState.Racing);
            hudRoot.EnableInClassList("st-results", state == RaceState.Results);

            // Only the visible Retry button may be clicked.
            HudFx.SetClickable(retryButton, state == RaceState.Results);

            if (state == RaceState.Ready)
            {
                // Title rows slide in: add "intro-in" a few frames after the screen is there (its first style must exist), so the transition can play.
                startScreen.RemoveFromClassList("intro-in");
                startScreen.schedule.Execute(() => startScreen.AddToClassList("intro-in")).ExecuteLater(100);
            }
            else
            {
                pressAnyKey.style.scale = StyleKeyword.Null; // stop the breathing so the punch class can work
                HudFx.Punch(pressAnyKey, "pill--pop");
            }

            if (state == RaceState.Results)
            {
                resultsStartTime = Time.time;
                tickerDone = false;
                resultsScreen.RemoveFromClassList("results--in");
                resultsScreen.schedule.Execute(() => resultsScreen.AddToClassList("results--in")).ExecuteLater(33);
            }
            else
            {
                resultsScreen.RemoveFromClassList("results--in");
                resultsCard.RemoveFromClassList("card--shaking"); // a retry in the middle of the record shake
                resultsCard.RemoveFromClassList("card--shake-l");
                resultsCard.RemoveFromClassList("card--shake-r");
            }

            if (state == RaceState.Ready || state == RaceState.Results)
            {
                countdownGeneration++;
                HudFx.SetShown(countdown, false);
            }
        }

        private void UpdateStartScreen()
        {
            // The pill "breathes" (scale, not opacity: it stays fully readable). Scale is a transform, no layout pass.
            float breathe = 1f + 0.035f * (0.5f + 0.5f * Mathf.Sin(Time.time * 3.2f));
            pressAnyKey.style.scale = new Scale(new Vector3(breathe, breathe, 1f));

            float best = raceManager.BestSeconds;
            if (shownStartBest.Changed(best > 0f ? RaceTimer.ToMilliseconds(best) : -1))
            {
                startBest.text = best > 0f ? "YOUR BEST  " + HudFormat.Time(RaceTimer.ToMilliseconds(best)) : "";
            }

            if (shownLoading.Changed(raceManager.IsSetUp ? 0 : 1))
            {
                pressAnyKey.text = raceManager.IsSetUp ? "CLICK OR PRESS ANY KEY" : "LOADING TRACK...";
                pressAnyKey.EnableInClassList("pill--loading", !raceManager.IsSetUp);
            }
        }

        private void UpdateTimes()
        {
            // Big "1:23" changes once a second, small ".456" every frame. Fixed-width digits (HudFormat) = no wobble.
            int lapMilliseconds = RaceTimer.ToMilliseconds(raceManager.LapSeconds);
            if (shownTimer.Changed(lapMilliseconds))
            {
                timerFraction.text = HudFormat.TimeFraction(lapMilliseconds);
                if (shownTimerMain.Changed(lapMilliseconds / 1000))
                {
                    timerMain.text = HudFormat.TimeMain(lapMilliseconds);
                }
            }

            float best = raceManager.BestSeconds;
            int bestMilliseconds = best > 0f ? RaceTimer.ToMilliseconds(best) : -1;
            if (shownBest.Changed(bestMilliseconds))
            {
                bestLabel.text = best > 0f ? HudFormat.Time(bestMilliseconds) : "--";
                bestLabel.EnableInClassList("best--none", best <= 0f);
            }

            // Lap counter only for races with more than one lap (a time trial is one lap).
            int laps = raceManager.LapCount;
            if (shownLaps.Changed(laps))
            {
                lapCounter.style.display = laps > 1 ? DisplayStyle.Flex : DisplayStyle.None;
                lapCounter.text = "LAP 1/" + laps;
            }
        }

        // Next-medal chip: "GOLD 0:10.200" during countdown and racing. Texts change only when the target changes
        // (after a lap). Once the running lap is slower than the target, the chip dims and gives one small punch.
        private void UpdateMedalTarget(RaceState state)
        {
            Medal target = raceManager.NextMedalTarget;
            bool topTier = target == Medal.None && raceManager.BestMedal == Medal.DeansList;
            float seconds = raceManager.MedalSeconds(target);
            bool hasChip = raceManager.IsSetUp && (topTier || seconds > 0f);
            if (shownTarget.Changed(hasChip ? (int)target : -1))
            {
                SetMedalClass(medalTarget, topTier ? Medal.DeansList : target);
                medalTargetName.text = topTier ? MedalRules.Name(Medal.DeansList) : MedalRules.Name(target);
                targetMilliseconds = seconds > 0f ? RaceTimer.ToMilliseconds(seconds) : -1;
                medalTargetTime.text = topTier ? "EARNED" : HudFormat.Time(targetMilliseconds);
            }
            HudFx.SetShown(medalTarget, hasChip);

            bool missed = state == RaceState.Racing && targetMilliseconds > 0 && !topTier
                && RaceTimer.ToMilliseconds(raceManager.LapSeconds) > targetMilliseconds;
            if (missed != targetMissed)
            {
                targetMissed = missed;
                medalTarget.EnableInClassList("medal-target--missed", missed);
                if (missed)
                {
                    HudFx.Punch(medalTarget, "medal-target--pop");
                }
            }
        }

        // Checkpoint pips: passed = white, next = pulsing outline, later = dim. The last pip is the finish line.
        private void UpdateCheckpoints(RaceState state)
        {
            int total = raceManager.CheckpointCount;
            int toPass = CheckpointProgress.CheckpointsToPass(total);
            if (shownCpTotal.Changed(total))
            {
                BuildPips(toPass);
                shownCpPassed.Forget();
            }
            if (toPass == 0)
            {
                return;
            }

            int passed = state == RaceState.Racing ? raceManager.CheckpointsPassed : 0;
            if (shownCpPassed.Changed(passed))
            {
                cpCount.text = passed + "/" + toPass; // a new string only when a checkpoint counts
                for (int i = 0; i < pips.Length; i++)
                {
                    CheckpointProgress.PipState pipState = CheckpointProgress.StateOf(i, passed);
                    pips[i].EnableInClassList("cp-pip--passed", pipState == CheckpointProgress.PipState.Passed);
                    pips[i].EnableInClassList("cp-pip--next", pipState == CheckpointProgress.PipState.Next);
                    pips[i].RemoveFromClassList("cp-pip--dim");
                }
                if (passed > 0 && passed <= pips.Length)
                {
                    HudFx.Punch(pips[passed - 1], "cp-pip--pop");
                }
            }

            // The "next" pip blinks slowly (opacity transition 700 ms, class toggled every 0.7 s).
            int phase = (int)(Time.time / 0.7f) % 2;
            if (passed < pips.Length && shownPulse.Changed(phase))
            {
                pips[passed].EnableInClassList("cp-pip--dim", phase == 1);
            }
        }

        private void BuildPips(int toPass)
        {
            cpPips.Clear();
            cpPanel.style.display = toPass > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            pips = new VisualElement[toPass + 1]; // one per checkpoint, plus the finish
            for (int i = 0; i < pips.Length; i++)
            {
                VisualElement pip = new VisualElement();
                pip.pickingMode = PickingMode.Ignore;
                pip.AddToClassList("cp-pip");
                pip.EnableInClassList("cp-pip--finish", i == toPass);
                cpPips.Add(pip);
                pips[i] = pip;
            }
        }

        private void UpdateSplit(RaceState state)
        {
            bool show = state == RaceState.Racing && Time.time < raceManager.SplitVisibleUntil;
            if (!show)
            {
                HudFx.SetShown(split, false);
            }
        }

        // Hint (missed checkpoint) > wrong way > stuck: at most one callout at a time, the most important one.
        private void UpdateCallouts(RaceState state)
        {
            bool racing = state == RaceState.Racing;
            bool showHint = racing && Time.time < raceManager.HintVisibleUntil;
            bool isWrongWay = false;
            bool isStuck = false;

            IKartState kart = raceManager.Kart;
            Transform kartTransform = raceManager.KartTransform;
            if (racing && kart != null && kartTransform != null)
            {
                Vector3 nextCheckpoint;
                if (raceManager.TryGetNextCheckpointPosition(out nextCheckpoint))
                {
                    isWrongWay = wrongWayDetector.Update(kartTransform.forward, kart.Speed, nextCheckpoint - kartTransform.position, Time.deltaTime);
                }
                isStuck = stuckDetector.Update(kart.Speed, Time.deltaTime);
            }

            HudFx.SetShown(hint, showHint);
            HudFx.SetShown(wrongWay, isWrongWay && !showHint);
            HudFx.SetShown(stuck, isStuck && !showHint && !isWrongWay);

            if (showHint)
            {
                hintText.text = raceManager.HintText; // the same string object every frame, nothing new is built
                HintKind kind = raceManager.CurrentHintKind;
                bool kindChanged = shownHintKind.Changed((int)kind);
                if (kindChanged)
                {
                    hint.EnableInClassList("hint--tip", kind == HintKind.Tip);
                    hint.EnableInClassList("hint--praise", kind == HintKind.Praise);
                    hintIcon.text = HintIcon(kind);
                }
                if (!hintWasShown || kindChanged)
                {
                    if (kind == HintKind.Warning)
                    {
                        Shake(hintChip);
                    }
                    else
                    {
                        Bounce(hintChip);
                    }
                }
            }
            hintWasShown = showHint;

            // WRONG WAY pulses twice a second (scale transition on the chip).
            if (isWrongWay)
            {
                wrongWayChip.EnableInClassList("wrong-way-chip--pulse", (int)(Time.time * 4f) % 2 == 0);
            }
        }

        private static string HintIcon(HintKind kind)
        {
            switch (kind)
            {
                case HintKind.Tip: return "i";
                case HintKind.Praise: return "+";
                default: return "!";
            }
        }

        // A friendly entry for coach tips and praise: one small scale punch once the chip has slid in.
        private static void Bounce(VisualElement element)
        {
            element.schedule.Execute(() => HudFx.Punch(element, "callout-chip--pop")).ExecuteLater(160);
        }

        // Two quick left-right jolts (60 ms each), then back to rest. Used when a warning hint appears.
        private static void Shake(VisualElement element)
        {
            element.schedule.Execute(() => element.AddToClassList("callout-chip--shake-l")).ExecuteLater(160);
            element.schedule.Execute(() => { element.RemoveFromClassList("callout-chip--shake-l"); element.AddToClassList("callout-chip--shake-r"); }).ExecuteLater(220);
            element.schedule.Execute(() => { element.RemoveFromClassList("callout-chip--shake-r"); element.AddToClassList("callout-chip--shake-l"); }).ExecuteLater(280);
            element.schedule.Execute(() => element.RemoveFromClassList("callout-chip--shake-l")).ExecuteLater(340);
        }

        // Speed gauge, drift meter, boost glow.
        private void UpdateKartInfo(RaceState state)
        {
            IKartState kart = raceManager.Kart;
            if (kart == null)
            {
                return;
            }

            // Smooth the shown number a little so it does not jitter, and write it only when the whole km/h changes.
            float kmh = Mathf.Abs(kart.Speed) * 3.6f; // m/s to km/h
            smoothedKmh = Mathf.Lerp(smoothedKmh, kmh, 1f - Mathf.Exp(-12f * Time.deltaTime));
            int shownKmh = Mathf.RoundToInt(smoothedKmh);
            if (shownSpeed.Changed(shownKmh))
            {
                speedLabel.text = HudFormat.Whole(shownKmh);
            }

            int stage = kart.DriftStage;
            bool boosting = kart.IsBoosting;
            Color arcColor = boosting ? HudColors.Orange : (kart.IsDrifting ? HudColors.DriftStage(stage) : HudColors.White);
            float maxSpeed = kart.MaxSpeed > 0.1f ? kart.MaxSpeed : 20f;
            speedArc.Set(Mathf.Abs(kart.Speed) / maxSpeed, arcColor);

            // Drift bar: scaled from the left in half-percent steps (scale = no layout pass, steps = fewer writes).
            int fillSteps = Mathf.RoundToInt((boosting ? 1f : kart.DriftCharge01) * 200f);
            if (shownDriftFill.Changed(fillSteps))
            {
                driftFill.style.scale = new Scale(new Vector3(fillSteps / 200f, 1f, 1f));
            }

            if (shownDriftState.Changed(boosting ? stage + 10 : stage))
            {
                OnDriftStateChanged(stage, boosting);
            }

            // Boost: a light streak runs through the bar every 0.55 s, and the screen edges glow orange.
            if (boosting && Time.time >= nextShineTime)
            {
                nextShineTime = Time.time + 0.55f;
                driftShine.RemoveFromClassList("drift-shine--run"); // jumps back (the base class has no transition)
                driftShine.schedule.Execute(() => driftShine.AddToClassList("drift-shine--run")).ExecuteLater(33);
            }
            bool glow = boosting && state == RaceState.Racing && Mathf.Abs(kart.Speed) > 3f;
            HudFx.SetShown(edgeGlowLeft, glow);
            HudFx.SetShown(edgeGlowRight, glow);
        }

        private void OnDriftStateChanged(int stage, bool boosting)
        {
            driftPanel.EnableInClassList("drift--s1", stage == 1 && !boosting);
            driftPanel.EnableInClassList("drift--s2", stage == 2 && !boosting);
            driftPanel.EnableInClassList("drift--boost", boosting);

            if (stage > lastStage && !boosting)
            {
                HudFx.Punch(driftBar, "drift-bar--pop");
                HudFx.Punch(stage == 1 ? driftPip1 : driftPip2, "drift-pip--pop");
            }
            if (boosting && !lastBoosting)
            {
                driftLabel.text = "BOOST";
                driftLabel.AddToClassList("drift-label--boost");
                HudFx.Punch(driftLabel, "drift-label--pop");
                HudFx.Punch(speedLabel, "speed--pop");
                nextShineTime = 0f;
            }
            else if (!boosting && lastBoosting)
            {
                driftLabel.text = "DRIFT";
                driftLabel.RemoveFromClassList("drift-label--boost");
            }
            speedLabel.EnableInClassList("speed--boost", boosting);
            lastStage = stage;
            lastBoosting = boosting;
        }

        // Results: the time counts up from 0 (0.25 s wait, 0.6 s, ease-out), then a small punch.
        private void UpdateResults()
        {
            if (tickerDone)
            {
                return; // nothing changes any more, so nothing is written
            }
            float progress = HudEasing.CountUp(Time.time - resultsStartTime, 0.25f, 0.6f);
            int milliseconds = Mathf.RoundToInt(raceManager.LapSeconds * progress * 1000f);
            if (progress >= 1f)
            {
                milliseconds = RaceTimer.ToMilliseconds(raceManager.LapSeconds); // exactly the official time at the end
            }
            if (shownFinal.Changed(milliseconds))
            {
                finalTime.text = HudFormat.Time(milliseconds);
            }
            if (progress >= 1f)
            {
                tickerDone = true;
                HudFx.Punch(finalTime, "final-time--pop");
                if (raceManager.LastLapWasRecord)
                {
                    // The NEW RECORD stamp lands at about 1.25 s: give the card a short shake then.
                    float wait = Mathf.Max(0f, 1.25f - (Time.time - resultsStartTime));
                    long ms = (long)(wait * 1000f);
                    resultsCard.schedule.Execute(() => resultsCard.AddToClassList("card--shaking")).ExecuteLater(ms);
                    resultsCard.schedule.Execute(() => resultsCard.AddToClassList("card--shake-l")).ExecuteLater(ms);
                    resultsCard.schedule.Execute(() => { resultsCard.RemoveFromClassList("card--shake-l"); resultsCard.AddToClassList("card--shake-r"); }).ExecuteLater(ms + 40);
                    resultsCard.schedule.Execute(() => { resultsCard.RemoveFromClassList("card--shake-r"); resultsCard.AddToClassList("card--shake-l"); }).ExecuteLater(ms + 80);
                    resultsCard.schedule.Execute(() => resultsCard.RemoveFromClassList("card--shake-l")).ExecuteLater(ms + 120);
                    resultsCard.schedule.Execute(() => resultsCard.RemoveFromClassList("card--shaking")).ExecuteLater(ms + 200);
                }
            }
        }
    }
}
