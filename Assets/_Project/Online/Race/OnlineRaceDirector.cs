using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Shared;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.Online
{
    // Runs the online race on this screen (M2). Made by OnlineRaceHooks.OpenLobby when the player picks ONLINE RACE
    // (or by the page address, see OnlineBootstrap), gone again when the player leaves the room.
    // What it does, every frame:
    // - drives the lobby pages (OnlineLobbyView) from the session's status, players and control,
    // - starts the local countdown so GO lands on the room's shared StartAt (OnlineStartRules), with the grid slot
    //   from the roster (RaceRoster.PlayerSlotOverride),
    // - sends my kart's pose 15 times a second and moves the other players' ProxyKarts,
    // - reports my finish, builds the room's results (FinishOrderRules) and shows my place through RaceManager,
    // - as the master (host): starts the race, moves the phase on, starts again (rematch), ?autostart=N.
    // RaceManager stays the race brain; it only gets ExternalStartOnly (no key starts, no Enter-retry, no Q, no P)
    // and the external calls A1 built (BeginRaceExternal, ResetToReadyExternal, SetExternalPlace, LeaveOnlineMode).
    // Order -55: after LeaderboardHud (-60) wrote RaceKeys.TextEntryActive for this frame and before RaceHud (-50)
    // reads the menu keys, so while the lobby is open the HUD's O/C/M/T/1/2/3 keys stay quiet.
    [DefaultExecutionOrder(-55)]
    public class OnlineRaceDirector : MonoBehaviour
    {
        public const string ObjectName = "OnlineRace";
        public const float ConnectTimeoutSeconds = 15f;
        public const float PoseSendsPerSecond = 15f;

        private static OnlineRaceDirector current;

        private RaceManager raceManager;
        private RaceRoster roster;
        private OnlineUrlFlags flags = OnlineUrlFlags.None;
        private string transportName;
        private IOnlineSession session;
        private IPumpedSession pumped;
        private OnlineLobbyView view;
        private readonly Dictionary<int, ProxyKart> proxies = new Dictionary<int, ProxyKart>();
        private readonly Dictionary<int, OnlinePlayer> knownPlayers = new Dictionary<int, OnlinePlayer>(); // also the ones who left
        private readonly List<FinishEntry> entries = new List<FinishEntry>();
        private readonly OnlineAutopilot autopilot = new OnlineAutopilot();
        private List<FinishRow> rows = new List<FinishRow>();
        private TimeOfDay room = TimeOfDay.Noon;
        private bool open;
        private bool autoJoinPending;
        private bool sessionDirty;
        private float connectStartedAt;
        private float nextPoseAt;
        private int appliedRaceId;       // the race this screen is set up for (0 = lobby)
        private bool inRace;             // I am in that race's roster
        private bool countdownStarted;   // BeginRaceExternal was called for that race
        private bool raceTimeUp;         // that race ran out of time (OnlineRaceLimitRules): still racing = DNF
        private double raceEndSeconds = OnlineRaceLimitRules.NoFinisherLimitSeconds; // lap seconds after GO it closes
        private bool timedOut;           // my own lap was cut off by the time limit: I watch the results from the grid
        private double autoStartReachedAt = -1.0;
        private bool runInBackgroundBefore;
        private TimeOfDay lightBefore = TimeOfDay.Noon; // the player's own light, back on leave (the room only borrows it)

        public static OnlineRaceDirector Current { get { return current; } }
        public IOnlineSession Session { get { return session; } }
        public bool IsOpen { get { return open; } }
        public int ProxyCount { get { return proxies.Count; } }

        // ---- Entry points ----

        // The player picked ONLINE RACE on the start screen (OnlineRaceHooks.OpenLobby).
        public static void Open(RaceManager raceManager, OnlineUrlFlags urlFlags)
        {
            Ensure().Begin(raceManager, urlFlags);
        }

        // The page address says ?room=...&name=...: wait for the race to be set up, then join without a click.
        public static void OpenForAutoJoin(OnlineUrlFlags urlFlags)
        {
            OnlineRaceDirector director = Ensure();
            director.flags = urlFlags ?? OnlineUrlFlags.None;
            director.autoJoinPending = true;
        }

        private static OnlineRaceDirector Ensure()
        {
            if (current == null)
            {
                GameObject holder = new GameObject(ObjectName);
                current = holder.AddComponent<OnlineRaceDirector>();
            }
            return current;
        }

        private void Begin(RaceManager manager, OnlineUrlFlags urlFlags)
        {
            if (open || manager == null)
            {
                return;
            }
            open = true;
            raceManager = manager;
            flags = urlFlags ?? OnlineUrlFlags.None;
            transportName = OnlineBootstrap.TransportName(flags);
            roster = FindAnyObjectByType<RaceRoster>();

            raceManager.ExternalStartOnly = true;    // the room starts the race, not a key
            raceManager.PauseOnFocusLoss = false;    // the others keep driving on the shared clock
            runInBackgroundBefore = Application.runInBackground;
            lightBefore = TimeOfDayChoice.Current;
            Application.runInBackground = true;      // a tab in the background must keep sending poses
            raceManager.LapFinished += OnLapFinished;

            view = MakeView();
            if (view != null)
            {
                view.JoinRequested += OnJoinRequested;
                view.BackRequested += Close;
                view.StartRequested += OnStartRequested;
            }

            if (transportName == null)
            {
                ShowMessage(OnlineRaceHooks.UnavailableMessage);
                return;
            }
            if (flags.WantsAutoJoin)
            {
                string cleaned;
                NameProblem problem = PlayerNameRules.Check(flags.Name, out cleaned);
                if (problem == NameProblem.None)
                {
                    JoinRoom(flags.Room, cleaned);
                }
                else
                {
                    ShowRooms(flags.Name, PlayerNameRules.Message(problem));
                }
                return;
            }
            if (view != null && flags.HasRoom)
            {
                view.SelectedRoom = flags.Room;
            }
            ShowRooms(SettingsStore.LoadPlayerName(), "");
        }

        // The lobby draws into RaceHud's document (same look, same scaling). Without a HUD there is no lobby.
        private static OnlineLobbyView MakeView()
        {
            RaceHud hud = FindAnyObjectByType<RaceHud>();
            UIDocument document = hud != null ? hud.GetComponent<UIDocument>() : null;
            if (document == null || document.rootVisualElement == null)
            {
                Debug.LogWarning("OnlineRaceDirector: no RaceHud document in the scene, the online lobby cannot be shown.");
                return null;
            }
            return new OnlineLobbyView(document.rootVisualElement);
        }

        // ---- Every frame ----

        private void Update()
        {
            if (autoJoinPending)
            {
                TryAutoJoin();
                return;
            }
            if (!open)
            {
                return;
            }
            if (raceManager == null)
            {
                Close();
                return;
            }
            if (pumped != null)
            {
                pumped.Pump(); // may raise Changed / PoseReceived (handled below through sessionDirty)
            }
            if (raceManager.Mode != RaceMode.OnlineRace)
            {
                Close(); // the player picked another mode on the start screen (keys 1/2) or RaceManager left online
                return;
            }
            if (view != null && (view.BlocksGameKeys || timedOut))
            {
                RaceKeys.TextEntryActive = true; // the lobby owns the keyboard this frame (see the class comment)
            }
            ReadKeys();
            if (session == null)
            {
                return;
            }
            switch (session.Status)
            {
                case OnlineStatus.Connecting:
                    if (Time.unscaledTime - connectStartedAt > ConnectTimeoutSeconds)
                    {
                        DropSession();
                        ShowMessage("COULD NOT CONNECT, CHECK THE WI-FI AND TRY AGAIN");
                    }
                    break;
                case OnlineStatus.Full:
                    DropSession();
                    ShowMessage(OnlineRooms.FullMessage(room));
                    break;
                case OnlineStatus.Failed:
                    string why = session.FailReason ?? "";
                    DropSession();
                    ShowMessage(why.Length > 0 ? "CONNECTION FAILED: " + why : "CONNECTION FAILED");
                    break;
                case OnlineStatus.InRoom:
                    UpdateInRoom();
                    break;
            }
        }

        // ?room=&name=: the race must be set up and on the start screen before the lobby can take over.
        private void TryAutoJoin()
        {
            RaceManager manager = raceManager != null ? raceManager : FindAnyObjectByType<RaceManager>();
            if (manager == null || !manager.IsSetUp || manager.State != RaceState.Ready)
            {
                return;
            }
            autoJoinPending = false;
            string refused;
            if (!manager.TrySelectMode(RaceMode.OnlineRace, out refused))
            {
                Debug.Log("OnlineRaceDirector: the address asked for an online room, but online is not available: " + refused);
                return;
            }
            Begin(manager, flags);
        }

        // Enter = join (rooms page) or START (the host); Q = leave. Read straight from the keyboard: RaceKeys is
        // switched off while the lobby owns it. While the cursor is in the name field only Enter means something.
        private void ReadKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || view == null)
            {
                return;
            }
            bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            bool leave = keyboard.qKey.wasPressedThisFrame;
            if (view.NameFieldHasFocus)
            {
                if (enter)
                {
                    OnJoinRequested(view.SelectedRoom, view.TypedName);
                }
                return;
            }
            if (enter)
            {
                if (view.Current == OnlineLobbyView.Page.Rooms)
                {
                    OnJoinRequested(view.SelectedRoom, view.TypedName);
                }
                else if (session != null && session.Status == OnlineStatus.InRoom)
                {
                    OnStartRequested();
                }
            }
            if (leave && (view.IsOpen || raceManager.State == RaceState.Results))
            {
                Close();
            }
        }

        private void UpdateInRoom()
        {
            if (sessionDirty)
            {
                sessionDirty = false;
                RememberPlayers();
                SyncProxies();
                rows = BuildRows(session.Control);
            }
            RaceControl control = session.Control;
            ApplyControl(control);
            CheckTimeLimit(control);
            SendPoseIfDue();
            double now = session.Now;
            foreach (KeyValuePair<int, ProxyKart> pair in proxies)
            {
                if (pair.Value != null)
                {
                    pair.Value.Show(now);
                    pair.Value.SetVisible(OnlineProxyRules.Shows(pair.Value.HasPose, control, pair.Key));
                }
            }
            UpdateAutopilot();
            UpdatePlace(control);
            UpdateMaster(control);
            UpdateView(control);
        }

        // A new race id = everyone back on the grid in Ready, my slot from the roster; then the countdown at the
        // shared moment. A race I am not in (joined late) is watched from the lobby.
        private void ApplyControl(RaceControl control)
        {
            if (control.RaceId != appliedRaceId)
            {
                appliedRaceId = control.RaceId;
                countdownStarted = false;
                timedOut = false;
                inRace = control.Contains(session.LocalId);
                autopilot.Stop();
                if (raceManager.State != RaceState.Ready)
                {
                    raceManager.ResetToReadyExternal();
                }
                raceManager.ClearExternalPlace();
                if (roster != null)
                {
                    roster.PlayerSlotOverride = inRace ? control.IndexOf(session.LocalId) : RacerGrid.NoOverride;
                }
                rows = BuildRows(control);
            }
            bool due = control.RaceId > 0 && inRace && !countdownStarted && !raceTimeUp && raceManager.State == RaceState.Ready
                && OnlineStartRules.ShouldBeginCountdown(session.Now, control.StartAt, raceManager.CountdownFrom, OnlineStartRules.CountdownStepSeconds);
            if (due)
            {
                // A late control message gets shorter steps, so GO still lands on StartAt (review F2).
                float step = OnlineStartRules.CountdownStepFor(session.Now, control.StartAt, raceManager.CountdownFrom);
                countdownStarted = raceManager.BeginRaceExternal(step);
                if (countdownStarted && view != null)
                {
                    view.Hide();
                }
            }
        }

        // The race time limit (OnlineRaceLimitRules): the moment it runs out, everyone still on the road is DNF on
        // every screen. If that is me, my lap ends: back on the grid, the room's results beside the start screen.
        private void CheckTimeLimit(RaceControl control)
        {
            if (control.RaceId == 0 || control.RaceId != appliedRaceId)
            {
                return;
            }
            if (!raceTimeUp && OnlineRaceLimitRules.IsTimeUp(session.Now, control.StartAt, raceEndSeconds))
            {
                rows = BuildRows(control); // sets raceTimeUp
            }
            RaceState state = raceManager.State;
            if (raceTimeUp && inRace && (state == RaceState.Countdown || state == RaceState.Racing))
            {
                autopilot.Stop();
                raceManager.ResetToReadyExternal();
                timedOut = true;
            }
        }

        private void SendPoseIfDue()
        {
            if (Time.unscaledTime < nextPoseAt || raceManager.KartTransform == null || raceManager.Kart == null)
            {
                return;
            }
            nextPoseAt = Time.unscaledTime + 1f / PoseSendsPerSecond;
            IKartState kart = raceManager.Kart;
            Transform kartTransform = raceManager.KartTransform;
            KartPose pose = new KartPose();
            pose.Time = session.Now;
            pose.Position = kartTransform.position;
            pose.Rotation = kartTransform.rotation;
            pose.Speed = kart.Speed;
            pose.Steer = kart.Steer;
            pose.DriftStage = kart.DriftStage;
            pose.DriftCharge01 = kart.DriftCharge01;
            pose.Flags = KartPose.MakeFlags(kart.IsDrifting, kart.IsBoosting, kart.IsGrounded);
            session.SendPose(pose);
        }

        private void UpdateAutopilot()
        {
            if (!flags.Autopilot)
            {
                return;
            }
            bool racing = raceManager.State == RaceState.Racing && inRace;
            if (racing && !autopilot.IsDriving)
            {
                autopilot.Start(raceManager, roster);
            }
            else if (!racing && autopilot.IsDriving)
            {
                autopilot.Stop();
            }
            if (racing)
            {
                autopilot.Tick(Time.deltaTime);
            }
        }

        // After my finish the HUD shows the room's place, the same number everyone else sees for me.
        private void UpdatePlace(RaceControl control)
        {
            if (raceManager.State != RaceState.Results || !inRace || control.RaceId == 0)
            {
                return;
            }
            int place = FinishOrderRules.PlaceOf(rows, session.LocalId);
            if (place > 0)
            {
                raceManager.SetExternalPlace(place, control.RosterCount);
            }
        }

        // The host's duties: move the phase on, start when asked (button, Enter) or by ?autostart=N.
        private void UpdateMaster(RaceControl control)
        {
            if (!session.IsMaster)
            {
                autoStartReachedAt = -1.0;
                return;
            }
            if (control.RaceId > 0)
            {
                OnlinePhase wanted = OnlineStartRules.PhaseFor(control, session.Now, FinishOrderRules.AllDone(rows));
                if (wanted != control.Phase)
                {
                    session.MasterSetControl(control.WithPhase(wanted));
                    return; // Changed fired: next frame works with the new control
                }
            }
            if (flags.AutoStart <= 0 || !CanStartNow(control))
            {
                autoStartReachedAt = -1.0;
                return;
            }
            bool enough = OnlineStartRules.AutoStartHasEnough(flags.AutoStart, session.Players.Count);
            bool restart = control.RaceId > 0;
            if (!enough || (restart && !FinishOrderRules.AllDone(rows)))
            {
                autoStartReachedAt = -1.0;
                return;
            }
            if (autoStartReachedAt < 0.0)
            {
                autoStartReachedAt = session.Now;
            }
            double wait = restart ? OnlineStartRules.AutoRematchSeconds : OnlineStartRules.AutoStartSeconds;
            if (session.Now - autoStartReachedAt >= wait)
            {
                autoStartReachedAt = -1.0;
                StartRace();
            }
        }

        // May the host start (again) right now? In the lobby from the start screen; after a race only once it is
        // over for everyone (all finished or left, or the time limit ran out), never while someone still drives:
        // the host's own finish is not enough, Enter on the results must not cut the others' lap (review F1).
        private bool CanStartNow(RaceControl control)
        {
            if (session == null || !session.IsMaster)
            {
                return false;
            }
            return OnlineStartRules.HostMayStart(control.RaceId, raceManager.State == RaceState.Ready, FinishOrderRules.AllDone(rows));
        }

        // A new race for everyone in the room right now: ids in join order = grid slots, GO in 4 s.
        private void StartRace()
        {
            IReadOnlyList<OnlinePlayer> players = session.Players;
            int[] ids = new int[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                ids[i] = players[i].Id;
            }
            RaceControl next = new RaceControl(session.Control.RaceId + 1, OnlinePhase.Countdown, OnlineStartRules.StartAtFor(session.Now), ids);
            session.MasterSetControl(next);
        }

        private void UpdateView(RaceControl control)
        {
            if (view == null)
            {
                return;
            }
            RaceState state = raceManager.State;
            if (state == RaceState.Countdown || state == RaceState.Racing)
            {
                view.Hide();
                return;
            }
            bool canStart = CanStartNow(control);
            if (state == RaceState.Results || timedOut)
            {
                view.ShowResults(rows, session.LocalId, canStart, ResultsWaitLine(control, canStart), timedOut ? "TIME UP: ROOM RESULTS" : "ROOM RESULTS");
                return;
            }
            IReadOnlyList<OnlinePlayer> players = session.Players;
            int masterId = players.Count > 0 ? players[0].Id : 0; // Players[0] is the master (IOnlineSession rules)
            string status = "";
            if (control.RaceId > 0 && !inRace && !FinishOrderRules.AllDone(rows))
            {
                status = "RACE IN PROGRESS, NEXT ROUND SOON";
            }
            else if (control.RaceId > 0 && inRace && !countdownStarted)
            {
                status = "GET READY...";
            }
            else if (!canStart)
            {
                status = "WAITING FOR THE HOST";
            }
            view.ShowRoom(room, players, session.LocalId, masterId, status, canStart);
        }

        // Under the room results: nothing for the host's START AGAIN, the time left while others still drive,
        // else the wait for the host.
        private string ResultsWaitLine(RaceControl control, bool canStart)
        {
            if (canStart)
            {
                return "";
            }
            if (control.RaceId > 0 && !FinishOrderRules.AllDone(rows))
            {
                return OnlineRaceLimitRules.ClosesLine(OnlineRaceLimitRules.SecondsLeft(session.Now, control.StartAt, raceEndSeconds));
            }
            return "WAITING FOR THE HOST";
        }

        // ---- Session events (main thread, see IOnlineSession) ----

        private void OnSessionChanged()
        {
            sessionDirty = true;
        }

        private void OnPoseReceived(int senderId, KartPose pose)
        {
            ProxyKart proxy;
            if (proxies.TryGetValue(senderId, out proxy) && proxy != null)
            {
                proxy.AddPose(pose);
            }
        }

        private void OnLapFinished(float lapSeconds, bool newRecord)
        {
            if (session != null && session.Status == OnlineStatus.InRoom && inRace && appliedRaceId > 0)
            {
                session.ReportFinish(appliedRaceId, lapSeconds);
            }
        }

        // Keeps every player object we ever saw: a player who left still has a name and a finish for the results.
        private void RememberPlayers()
        {
            IReadOnlyList<OnlinePlayer> players = session.Players;
            for (int i = 0; i < players.Count; i++)
            {
                knownPlayers[players[i].Id] = players[i];
            }
        }

        // One ProxyKart per other player in the room, none for players who left.
        private void SyncProxies()
        {
            IReadOnlyList<OnlinePlayer> players = session.Players;
            Transform visualSource = raceManager.KartTransform != null ? raceManager.KartTransform.Find(ProxyKart.VisualName) : null;
            float maxSpeed = raceManager.Kart != null ? raceManager.Kart.MaxSpeed : 30f;
            for (int i = 0; i < players.Count; i++)
            {
                OnlinePlayer player = players[i];
                if (player.Id != session.LocalId && !proxies.ContainsKey(player.Id))
                {
                    proxies[player.Id] = ProxyKart.Spawn(player, visualSource, maxSpeed);
                }
            }
            List<int> gone = new List<int>();
            foreach (KeyValuePair<int, ProxyKart> pair in proxies)
            {
                bool present = false;
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].Id == pair.Key)
                    {
                        present = true;
                        break;
                    }
                }
                if (!present)
                {
                    gone.Add(pair.Key);
                }
            }
            for (int i = 0; i < gone.Count; i++)
            {
                ProxyKart proxy = proxies[gone[i]];
                proxies.Remove(gone[i]);
                if (proxy != null)
                {
                    Destroy(proxy.gameObject);
                }
            }
        }

        private void DestroyProxies()
        {
            foreach (KeyValuePair<int, ProxyKart> pair in proxies)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }
            proxies.Clear();
        }

        // The results of the current race: everyone in the roster, present or not, finished or not, with the time
        // limit applied (a lap after it is no finish, still racing after it = DNF). Also sets raceTimeUp/raceEndSeconds.
        private List<FinishRow> BuildRows(RaceControl control)
        {
            entries.Clear();
            raceTimeUp = false;
            raceEndSeconds = OnlineRaceLimitRules.NoFinisherLimitSeconds;
            if (control.RaceId == 0)
            {
                return FinishOrderRules.Order(entries);
            }
            IReadOnlyList<OnlinePlayer> players = session.Players;
            for (int r = 0; r < control.Roster.Length; r++)
            {
                int id = control.Roster[r];
                OnlinePlayer present = null;
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].Id == id)
                    {
                        present = players[i];
                        break;
                    }
                }
                OnlinePlayer known = present;
                if (known == null)
                {
                    knownPlayers.TryGetValue(id, out known);
                }
                string name = known != null ? known.Name : "PLAYER";
                bool finished = known != null && known.HasFinished(control.RaceId);
                double seconds = finished ? known.FinishSeconds : double.NaN;
                entries.Add(new FinishEntry(id, name, present != null, finished, seconds));
            }
            List<FinishEntry> limited = OnlineRaceLimitRules.Apply(entries, session.Now, control.StartAt, out raceTimeUp, out raceEndSeconds);
            return FinishOrderRules.Order(limited);
        }

        // ---- Lobby actions ----

        private void OnJoinRequested(TimeOfDay wantedRoom, string typedName)
        {
            if (session != null)
            {
                return; // already joining or in a room
            }
            string cleaned;
            NameProblem problem = PlayerNameRules.Check(typedName, out cleaned);
            if (problem != NameProblem.None)
            {
                if (view != null)
                {
                    view.ShowProblem(PlayerNameRules.Message(problem));
                }
                return;
            }
            JoinRoom(wantedRoom, cleaned);
        }

        private void OnStartRequested()
        {
            if (session != null && session.Status == OnlineStatus.InRoom && CanStartNow(session.Control))
            {
                StartRace();
            }
        }

        private void JoinRoom(TimeOfDay wantedRoom, string cleanedName)
        {
            room = wantedRoom;
            TimeOfDayChoice.Set(room, false);     // the room sets the light for everyone in it, never saved (review F11)
            SettingsStore.SavePlayerName(cleanedName);
            SetPlayerName(cleanedName);
            DropSession();
            session = OnlineSessions.Create(transportName);
            if (session == null)
            {
                ShowMessage(OnlineRaceHooks.UnavailableMessage);
                return;
            }
            pumped = session as IPumpedSession;
            session.Changed += OnSessionChanged;
            session.PoseReceived += OnPoseReceived;
            appliedRaceId = 0;
            inRace = false;
            countdownStarted = false;
            timedOut = false;
            rows.Clear();
            knownPlayers.Clear();
            connectStartedAt = Time.unscaledTime;
            nextPoseAt = 0f;
            if (view != null)
            {
                view.ShowConnecting(room);
            }
            session.Join(OnlineRooms.SessionName(room), cleanedName, OnlineColours.RandomIndex(new System.Random()));
            sessionDirty = true;
        }

        // The name tag over my kart shows my room name online ("You" offline).
        private void SetPlayerName(string name)
        {
            if (raceManager == null || raceManager.KartTransform == null)
            {
                return;
            }
            PlayerRacer racer = raceManager.KartTransform.GetComponent<PlayerRacer>();
            if (racer != null)
            {
                racer.SetDisplayName(name);
            }
        }

        private void ShowRooms(string name, string problem)
        {
            if (view == null)
            {
                Debug.LogWarning("OnlineRaceDirector: no lobby to show, leaving online mode.");
                Close();
                return;
            }
            view.ShowRooms(name, problem);
        }

        private void ShowMessage(string text)
        {
            if (view == null)
            {
                Debug.LogWarning("OnlineRaceDirector: " + text);
                Close();
                return;
            }
            view.ShowMessage(text);
        }

        // Out of the room: proxies gone, autopilot off, my grid slot back to normal, the session closed.
        private void DropSession()
        {
            autopilot.Stop();
            DestroyProxies();
            if (roster != null)
            {
                roster.PlayerSlotOverride = RacerGrid.NoOverride;
            }
            if (session != null)
            {
                session.Changed -= OnSessionChanged;
                session.PoseReceived -= OnPoseReceived;
                session.Dispose();
                session = null;
                pumped = null;
            }
            appliedRaceId = 0;
            inRace = false;
            countdownStarted = false;
            timedOut = false;
            raceTimeUp = false;
            autoStartReachedAt = -1.0;
            rows.Clear();
        }

        // Back to the start screen in the last offline mode (Q, BACK, another mode picked, scene unload).
        public void Close()
        {
            if (!open)
            {
                return;
            }
            open = false;
            DropSession();
            if (view != null)
            {
                view.Hide();
                view = null;
            }
            if (raceManager != null)
            {
                raceManager.LapFinished -= OnLapFinished;
                SetPlayerName("You");
                raceManager.LeaveOnlineMode();
            }
            Application.runInBackground = runInBackgroundBefore;
            TimeOfDayChoice.Set(lightBefore);      // the player's own light again
            if (current == this)
            {
                current = null;
            }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            Close();
            if (current == this)
            {
                current = null;
            }
        }
    }
}
