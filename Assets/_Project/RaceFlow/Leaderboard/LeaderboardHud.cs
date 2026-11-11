using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // The leaderboard on screen (task R10). On the HUD object next to RaceHud, using the same UIDocument: the cards
    // go into the empty "start-board-host" (start screen) and "results-board-host" (beside the results card) of
    // RaceHud.uxml, so the HUD's menus and fades cover them like everything else.
    // - Start screen: the top 10 of the track, fetched once when the race is set up.
    // - After a lap: the results card offers "SAVE YOUR TIME" (if LapTimeRules accepts the time) and shows the top 10.
    //   SAVE (or Enter while typing) checks the name (PlayerNameRules), remembers it (SettingsStore), sends it, shows
    //   the place ("YOU ARE #4") and fetches the list again with the player's rows highlighted.
    // - While the player types, RaceKeys.TextEntryActive switches the game keys off (M, O, Enter = retry ...).
    // Which service: an online one only when LeaderboardConfig passes LeaderboardConfigCheck, always wrapped in
    // FallbackLeaderboardService (times are kept on this device too). Tests swap in a FakeLeaderboardService.
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(-60)] // before RaceHud (-50): TextEntryActive is set before the menus read the keys this frame
    public class LeaderboardHud : MonoBehaviour
    {
        [SerializeField, Tooltip("The race brain: state, lap time, track name and the Gold medal time")]
        private RaceManager raceManager;

        [SerializeField, Tooltip("Where the online leaderboard is (RaceFlow/Settings/LeaderboardConfig.asset, EMPTY in the repo: then this device only)")]
        private LeaderboardConfig config;

        private const int DefaultTopCount = 10;

        private ILeaderboardService service;
        private VisualElement hudRoot;
        private LeaderboardPanel startBoard;
        private LeaderboardPanel resultsBoard;
        private SaveTimeForm form;
        private string playerName = "";
        private float lapSeconds;
        private int lapNumber;          // +1 per finished lap: answers for an older lap are ignored
        private bool startBoardLoaded;
        private int typingFrame = -10;  // last frame the name field had the cursor (or Enter saved)
        private RaceState lastState = RaceState.Ready;
        private bool sentRacing;        // what the Web page was last told (its driving buttons show only in a race)

        // The service in use (made on first use). Public so a Play Mode test can read it.
        public ILeaderboardService Service
        {
            get
            {
                if (service == null)
                {
                    service = MakeService();
                }
                return service;
            }
        }

        // The fastest lap time we still believe on this track (LapTimeRules); the Play Mode test waits for it.
        public float MinLapSeconds { get { return LapTimeRules.MinSecondsFor(raceManager.MedalSeconds(Medal.Gold)); } }

        public SaveTimeForm Form { get { return form; } }

        private int TopCount { get { return config != null ? config.TopCount : DefaultTopCount; } }

        // Tests: use this service instead (e.g. a FakeLeaderboardService), and load the start board again.
        public void UseService(ILeaderboardService replacement)
        {
            service = replacement;
            startBoardLoaded = false;
        }

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            hudRoot = root.Q<VisualElement>("hud-root");
            VisualElement startHost = root.Q<VisualElement>("start-board-host");
            VisualElement resultsHost = root.Q<VisualElement>("results-board-host");
            startHost.Clear();
            resultsHost.Clear();
            startBoard = new LeaderboardPanel(startHost, TopCount, false);
            resultsBoard = new LeaderboardPanel(resultsHost, TopCount, true);
            form = new SaveTimeForm(resultsBoard.FormSlot);
            form.SaveRequested += SaveTypedName;
            if (WebPage.UsesNamePrompt)
            {
                form.UseNamePrompt(); // phone browser: the name is typed in the browser's own text box
            }
            sentRacing = false;
            WebPage.SendRaceState(false);
            playerName = SettingsStore.LoadPlayerName();
            startBoardLoaded = false;
            raceManager.LapFinished += OnLapFinished;
            raceManager.Restarted += OnRestarted;
        }

        private void OnDisable()
        {
            raceManager.LapFinished -= OnLapFinished;
            raceManager.Restarted -= OnRestarted;
            form.SaveRequested -= SaveTypedName;
            RaceKeys.TextEntryActive = false; // never leave the game keys switched off
            lapNumber++; // answers that arrive after this are for a board that is gone
        }

        private void Update()
        {
            if (!raceManager.IsSetUp)
            {
                return; // the track name (and so the board) is only known after RaceBootstrap set the race up
            }
            if (!startBoardLoaded)
            {
                startBoardLoaded = true;
                startBoard.SetTrack(raceManager.TrackName, TopCount);
                resultsBoard.SetTrack(raceManager.TrackName, TopCount);
                Fetch(startBoard, lapNumber, 0);
            }

            RaceState state = raceManager.State;
            if (state != lastState && lastState == RaceState.Results)
            {
                form.Hide(); // left the results (retry): close the form
            }
            lastState = state;

            // The page's touch buttons for driving only during a race, so on a phone they never cover the boards.
            bool racing = state == RaceState.Countdown || state == RaceState.Racing;
            if (racing != sentRacing)
            {
                sentRacing = racing;
                WebPage.SendRaceState(racing);
            }

            // The form takes clicks only while the results card is really there (RaceHud adds st-results then).
            bool cardIn = state == RaceState.Results && hudRoot.ClassListContains("st-results");
            form.SetClickable(cardIn);

            UpdateTyping(cardIn);
        }

        // Typing: the game keys are off while the cursor is in the field and for one frame after, so the Enter that
        // saves is not also read as "retry" later in the same frame (RaceManager runs after this script).
        private void UpdateTyping(bool cardIn)
        {
            bool focused = cardIn && form.HasFocus();
            if (focused)
            {
                typingFrame = Time.frameCount;
            }
            bool typing = Time.frameCount - typingFrame <= 1;
            RaceKeys.TextEntryActive = typing;
            form.SetTyping(focused);

            if (focused && RaceKeys.NameSubmitPressed())
            {
                typingFrame = Time.frameCount; // keep the keys off for the next frame too
                SaveTypedName();
            }
            else if (!typing && cardIn && form.State == SaveFormState.Editing && RaceKeys.NameFieldKeyPressed())
            {
                form.Focus();
                typingFrame = Time.frameCount;
            }
        }

        // A lap ended: open the form (if this time may be saved) and load the list beside the card.
        private void OnLapFinished(float seconds, bool isRecord)
        {
            lapNumber++;
            lapSeconds = seconds;
            if (LapTimeRules.IsAcceptable(seconds, raceManager.MedalSeconds(Medal.Gold)))
            {
                form.Open(playerName, RaceTimer.ToMilliseconds(seconds));
            }
            else
            {
                form.Hide();
            }
            resultsBoard.ShowLoading();
            Fetch(resultsBoard, lapNumber, 0);
        }

        private void OnRestarted()
        {
            lapNumber++; // a retry: answers for the old lap no longer matter
            form.Hide();
        }

        // SAVE clicked, or Enter while typing. Public so the Play Mode test can press it.
        public void SaveTypedName()
        {
            if (form.State != SaveFormState.Editing || raceManager.State != RaceState.Results)
            {
                return;
            }
            string cleaned;
            NameProblem problem = PlayerNameRules.Check(form.TypedName, out cleaned);
            if (problem != NameProblem.None)
            {
                form.ShowProblem(PlayerNameRules.Message(problem));
                return; // nothing is sent
            }
            playerName = cleaned;
            SettingsStore.SavePlayerName(cleaned);
            form.ShowSaving(cleaned);

            int lap = lapNumber;
            string track = raceManager.TrackName;
            Service.SubmitTime(track, cleaned, lapSeconds, reply =>
            {
                if (this == null || lap != lapNumber)
                {
                    return; // the scene closed or a new lap started while we waited
                }
                form.ShowSaved(reply);
                Fetch(resultsBoard, lap, reply.Success ? reply.Rank : 0);
                Fetch(startBoard, lap, 0); // the start screen shows the new list after a page reload too
            });
        }

        // Loads the top list into a board. newRank = the row to flash (just saved), 0 = none.
        private void Fetch(LeaderboardPanel board, int lap, int newRank)
        {
            float minSeconds = MinLapSeconds;
            Service.FetchTop(raceManager.TrackName, TopCount, reply =>
            {
                if (this == null || lap != lapNumber)
                {
                    return;
                }
                if (!reply.Success)
                {
                    // Only possible when even this device could not answer (a broken track name): show an empty list.
                    board.Show(new List<LeaderboardEntry>(), LeaderboardSource.OfflineFallback, null, 0);
                    return;
                }
                List<LeaderboardEntry> mine = LocalLeaderboardService.SavedHere(raceManager.TrackName); // saved from this browser
                board.Show(LeaderboardRanking.WithoutTooFast(reply.Entries, minSeconds), reply.Source, mine, newRank);
            });
        }

        // Online only when the config is filled in and safe; otherwise this device. Never an error for an empty config.
        private ILeaderboardService MakeService()
        {
            string url = config != null ? config.ProjectUrl : "";
            string key = config != null ? config.AnonKey : "";
            if (LeaderboardConfigCheck.IsEmpty(url, key))
            {
                return new FallbackLeaderboardService(null);
            }
            string problem = LeaderboardConfigCheck.Problem(url, key);
            if (problem != "")
            {
                Debug.LogWarning("Leaderboard: online leaderboard switched off, " + problem + ". Times stay on this device.");
                return new FallbackLeaderboardService(null);
            }
            return new FallbackLeaderboardService(new SupabaseLeaderboardService(url, key, config.TimeoutSeconds));
        }
    }
}
