using System;
using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;

namespace CarcavelosCircuit.Online.Fusion
{
    // IOnlineSession over Photon Fusion 2 in Shared Mode ("photon", registered by FusionBootstrap). One room per
    // session name ("carcavelos-noon" / "carcavelos-sunset") in the EU region of the Photon Cloud.
    // How it works:
    // - Join starts a NetworkRunner. Once it runs, every client spawns one FusionPlayerState for itself (it alone
    //   writes it: name, colour, pose, finish) and the master client spawns one FusionRaceState for the room (the
    //   RaceControl; Fusion hands it to the next master when the master leaves).
    // - Fusion calls back during its own update. The callbacks and Spawned/Despawned only note what happened; the
    //   real work is in Tick, called once per frame by FusionSessionDriver (order -56, just before the director).
    //   So Changed and PoseReceived always fire on the main thread and only after Status/Players/Control already
    //   show the new state (the rules in IOnlineSession).
    // - Player id = Photon's actor number (never reused while the room lives), master = Photon's shared-mode
    //   master client (the earliest joiner still in the room), Now = Fusion's render clock (the same on every
    //   client within about a tick plus half the ping).
    // Everything off the wire is checked like the tab transport does: names by PlayerNameRules, poses by
    // KartPoseCodec, the roster size by FusionRaceState.
    public class FusionSession : IOnlineSession, INetworkRunnerCallbacks
    {
        private const string HostName = "Online (Photon Fusion)";
        private const string RunnerName = "Online (Photon Fusion) runner";
        private const string Region = "eu";
        private const float ConnectTimeoutSeconds = 20f;
        private const float RaceSpawnRetrySeconds = 5f;

        private static readonly OnlinePlayer[] NoPlayers = new OnlinePlayer[0];

        // Which session a runner belongs to (the networked objects only know their runner).
        private static readonly Dictionary<NetworkRunner, FusionSession> sessions = new Dictionary<NetworkRunner, FusionSession>();

        // One networked player state and the OnlinePlayer made from it (null until its first state arrived).
        private class Entry
        {
            public FusionPlayerState State;
            public OnlinePlayer Player;
            public int LastPoseSeq;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<OnlinePlayer> players = new List<OnlinePlayer>();
        private readonly List<FusionRaceState> raceStates = new List<FusionRaceState>();

        private NetworkRunner runner;
        private GameObject host;
        private FusionPlayerState me;
        private OnlinePlayer myPlayer;
        private RaceControl pendingControl;   // set by the master, written as soon as it has authority over the race state
        private string myName = "";
        private int myColour;
        private float connectStartedAt;
        private float lastRaceSpawnAt = -1000f;
        private bool listChanged;

        public OnlineStatus Status { get; private set; } = OnlineStatus.Idle;
        public string FailReason { get; private set; } = "";
        public int LocalId { get; private set; }
        public RaceControl Control { get; private set; } = RaceControl.Lobby;

        public event Action<int, KartPose> PoseReceived;
        public event Action Changed;

        public bool IsMaster
        {
            get { return Status == OnlineStatus.InRoom && runner != null && runner.IsSharedModeMasterClient; }
        }

        // Fusion's render clock: seconds since tick 0 of the room. 0 while no runner runs.
        public double Now
        {
            get { return runner != null && runner.IsRunning ? (double)runner.LocalRenderTime : 0.0; }
        }

        public IReadOnlyList<OnlinePlayer> Players
        {
            get { return Status == OnlineStatus.InRoom ? (IReadOnlyList<OnlinePlayer>)players : NoPlayers; }
        }

        // The session a runner belongs to, or null (a runner from another session or one that already ended).
        internal static FusionSession Of(NetworkRunner r)
        {
            FusionSession session;
            return r != null && sessions.TryGetValue(r, out session) ? session : null;
        }

        // Domain reload can be off in the editor: start every Play Mode with an empty table.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sessions.Clear();
        }

        public void Join(string room, string name, int colourIndex)
        {
            if (Status == OnlineStatus.InRoom || Status == OnlineStatus.Connecting)
            {
                return; // already joining or in a room: Leave first
            }
            if (string.IsNullOrEmpty(room))
            {
                Fail(OnlineStatus.Failed, "NO ROOM NAME");
                return;
            }
            PhotonAppSettings global;
            if (!PhotonAppSettings.TryGetGlobal(out global) || global == null || global.AppSettings == null)
            {
                Fail(OnlineStatus.Failed, "NO PHOTON SETTINGS IN THIS BUILD");
                return;
            }
            string appId = FusionAppId.Find(global.AppSettings.AppIdFusion);
            if (string.IsNullOrEmpty(appId))
            {
                Fail(OnlineStatus.Failed, "NO PHOTON APP ID IN THIS BUILD");
                return;
            }

            Application.runInBackground = true;
            myName = SafeName(name);
            myColour = colourIndex;

            host = new GameObject(HostName);
            FusionSessionDriver driver = host.AddComponent<FusionSessionDriver>();
            driver.Session = this;

            GameObject runnerObject = new GameObject(RunnerName);
            runner = runnerObject.AddComponent<NetworkRunner>();
            NetworkSceneManagerDefault sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();
            FusionObjectProvider provider = runnerObject.AddComponent<FusionObjectProvider>();
            sessions[runner] = this;
            runner.AddCallbacks(this);

            FusionAppSettings settings = global.AppSettings.GetCopy();
            settings.AppIdFusion = appId;
            settings.FixedRegion = Region;

            Status = OnlineStatus.Connecting;
            FailReason = "";
            connectStartedAt = Time.realtimeSinceStartup;
            lastRaceSpawnAt = -1000f;
            RaiseChanged();

            StartAsync(runner, new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = room,
                PlayerCount = OnlineRooms.MaxPlayers,
                CustomPhotonAppSettings = settings,
                SceneManager = sceneManager,
                ObjectProvider = provider
            });
        }

        // Runs on the main thread (Unity's synchronisation context brings the await back there).
        private async void StartAsync(NetworkRunner r, StartGameArgs args)
        {
            StartGameResult result;
            try
            {
                result = await r.StartGame(args);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Photon Fusion: StartGame threw: " + e.Message);
                if (r == runner && Status == OnlineStatus.Connecting)
                {
                    End(OnlineStatus.Failed, "COULD NOT CONNECT (" + e.GetType().Name + ")");
                }
                return;
            }
            if (r != runner || Status != OnlineStatus.Connecting)
            {
                return; // Leave was called meanwhile, or a callback already ended the session
            }
            if (!result.Ok)
            {
                End(FusionFailMessages.StatusOf(result.ShutdownReason), FusionFailMessages.Message(result.ShutdownReason));
                return;
            }
            SpawnMe();
        }

        // Our own player object. Its first state already carries id, join time, name and colour (Init runs before
        // the object goes live), so nobody ever sees a nameless player.
        private void SpawnMe()
        {
            int? actorId = runner.GetPlayerActorId(runner.LocalPlayer); // null when Photon does not know the player (yet)
            int id = actorId.HasValue ? actorId.Value : 0;
            if (id <= 0)
            {
                id = runner.LocalPlayer.PlayerId + 1;
            }
            double joinedAt = Now;
            string name = myName;
            int colour = myColour;
            try
            {
                runner.Spawn(NetworkPrefabId.FromRaw(FusionObjectProvider.PlayerTypeId), Vector3.zero, Quaternion.identity, runner.LocalPlayer,
                    (r, obj) => obj.GetComponent<FusionPlayerState>().Init(id, joinedAt, name, colour),
                    NetworkSpawnFlags.SharedModeStateAuthLocalPlayer);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Photon Fusion: spawning the player failed: " + e.Message);
                End(OnlineStatus.Failed, "COULD NOT CREATE THE PLAYER");
            }
        }

        // --- Called by the networked objects (Spawned / Despawned, main thread, during Fusion's update) ---

        internal void OnPlayerSpawned(FusionPlayerState state)
        {
            if (state == null || FindEntry(state) != null)
            {
                return;
            }
            entries.Add(new Entry { State = state, Player = null, LastPoseSeq = 0 });
            if (state.HasStateAuthority)
            {
                me = state;
            }
        }

        internal void OnPlayerDespawned(FusionPlayerState state)
        {
            Entry entry = FindEntry(state);
            if (entry == null)
            {
                return;
            }
            entries.Remove(entry);
            if (entry.Player != null)
            {
                players.Remove(entry.Player);
                listChanged = true;
            }
            if (state == me)
            {
                me = null;
                myPlayer = null;
            }
        }

        internal void OnRaceSpawned(FusionRaceState state)
        {
            if (state != null && !raceStates.Contains(state))
            {
                raceStates.Add(state);
            }
        }

        internal void OnRaceDespawned(FusionRaceState state)
        {
            raceStates.Remove(state);
        }

        // --- Once per frame (FusionSessionDriver) ---

        public void Tick()
        {
            if (runner == null)
            {
                return;
            }
            bool changed = listChanged;
            listChanged = false;

            if (SettleEntries())
            {
                changed = true;
            }
            if (Status == OnlineStatus.Connecting)
            {
                if (Time.realtimeSinceStartup - connectStartedAt > ConnectTimeoutSeconds)
                {
                    End(OnlineStatus.Failed, "NO ANSWER FROM THE SERVER (TIMEOUT), CHECK THE WI-FI");
                    return;
                }
            }
            else if (Status == OnlineStatus.InRoom)
            {
                EnsureRaceState();
                TryWriteControl();
                if (ReadControl())
                {
                    changed = true;
                }
                if (ReadFinishes())
                {
                    changed = true;
                }
                ReadPoses();
                if (runner == null)
                {
                    return; // a PoseReceived handler left the room (End already raised Changed)
                }
            }
            if (changed)
            {
                RaiseChanged();
            }
        }

        // Entries whose first state arrived become OnlinePlayers (inserted in join order). Our own one also moves
        // us from Connecting to InRoom. True when the player list changed.
        private bool SettleEntries()
        {
            bool changed = false;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.Player != null || entry.State == null || entry.State.OwnerId == 0)
                {
                    continue;
                }
                FusionPlayerState state = entry.State;
                entry.Player = new OnlinePlayer(state.OwnerId, SafeName(state.PlayerName.ToString()), state.ColourIndex, state.JoinedAt);
                InsertInJoinOrder(entry.Player);
                changed = true;
                if (state == me)
                {
                    myPlayer = entry.Player;
                    if (Status == OnlineStatus.Connecting)
                    {
                        LocalId = state.OwnerId;
                        Status = OnlineStatus.InRoom;
                        FailReason = "";
                    }
                }
            }
            return changed;
        }

        private void InsertInJoinOrder(OnlinePlayer player)
        {
            int at = players.Count;
            for (int i = 0; i < players.Count; i++)
            {
                if (OnlinePlayer.CompareJoinOrder(player, players[i]) < 0)
                {
                    at = i;
                    break;
                }
            }
            players.Insert(at, player);
        }

        // The master makes sure the room has exactly one race state: spawns one when there is none (again every few
        // seconds until it shows up) and removes extra ones it owns (two masters for a moment after a migration).
        private void EnsureRaceState()
        {
            if (!runner.IsSharedModeMasterClient)
            {
                return;
            }
            if (raceStates.Count == 0)
            {
                float now = Time.realtimeSinceStartup;
                if (now - lastRaceSpawnAt < RaceSpawnRetrySeconds)
                {
                    return;
                }
                lastRaceSpawnAt = now;
                RaceControl current = pendingControl ?? Control;
                try
                {
                    runner.Spawn(NetworkPrefabId.FromRaw(FusionObjectProvider.RaceTypeId), Vector3.zero, Quaternion.identity, runner.LocalPlayer,
                        (r, obj) => obj.GetComponent<FusionRaceState>().Write(current),
                        NetworkSpawnFlags.SharedModeStateAuthMasterClient);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Photon Fusion: spawning the race state failed: " + e.Message);
                }
                return;
            }
            if (raceStates.Count > 1)
            {
                FusionRaceState keep = PrimaryRaceState();
                for (int i = raceStates.Count - 1; i >= 0; i--)
                {
                    FusionRaceState state = raceStates[i];
                    if (state != keep && state != null && state.HasStateAuthority)
                    {
                        runner.Despawn(state.Object);
                    }
                }
            }
        }

        // The race state everyone reads: the one with the lowest object id (the same choice on every client).
        private FusionRaceState PrimaryRaceState()
        {
            FusionRaceState best = null;
            for (int i = 0; i < raceStates.Count; i++)
            {
                FusionRaceState state = raceStates[i];
                if (state == null || state.Object == null)
                {
                    continue;
                }
                if (best == null || state.Object.Id.Raw < best.Object.Id.Raw)
                {
                    best = state;
                }
            }
            return best;
        }

        private void TryWriteControl()
        {
            if (pendingControl == null)
            {
                return;
            }
            FusionRaceState primary = PrimaryRaceState();
            if (primary != null && primary.HasStateAuthority)
            {
                primary.Write(pendingControl);
                pendingControl = null;
            }
        }

        // True when the room's race control differs from ours (we take the networked one).
        private bool ReadControl()
        {
            if (pendingControl != null)
            {
                return false; // our own newer control is not written yet; do not overwrite it with the old one
            }
            FusionRaceState primary = PrimaryRaceState();
            if (primary == null || primary.SameAs(Control))
            {
                return false;
            }
            Control = primary.Read();
            return true;
        }

        // Finish times of the others (each player writes its own; the first one per race counts).
        private bool ReadFinishes()
        {
            bool changed = false;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.Player == null || entry.State == null || entry.State == me)
                {
                    continue;
                }
                int raceId = entry.State.FinishRaceId;
                if (raceId != 0 && entry.Player.NoteFinish(raceId, entry.State.FinishSeconds))
                {
                    changed = true;
                }
            }
            return changed;
        }

        // New poses of the others (PoseSeq moved on since we last looked).
        private void ReadPoses()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.Player == null || entry.State == null || entry.State == me)
                {
                    continue;
                }
                int seq = entry.State.PoseSeq;
                if (seq == entry.LastPoseSeq)
                {
                    continue;
                }
                entry.LastPoseSeq = seq;
                KartPose pose;
                if (!entry.State.TryReadPose(out pose))
                {
                    continue;
                }
                entry.Player.LatestPose = pose;
                entry.Player.HasPose = true;
                if (PoseReceived != null)
                {
                    PoseReceived(entry.Player.Id, pose);
                }
                if (runner == null)
                {
                    return; // the handler left the room; the lists are gone
                }
            }
        }

        // --- The game's calls ---

        public void SendPose(in KartPose pose)
        {
            if (Status != OnlineStatus.InRoom || me == null || myPlayer == null)
            {
                return;
            }
            myPlayer.LatestPose = pose;
            myPlayer.HasPose = true;
            me.WritePose(pose);
        }

        public void ReportFinish(int raceId, double lapSeconds)
        {
            if (Status != OnlineStatus.InRoom || me == null || myPlayer == null)
            {
                return;
            }
            if (myPlayer.NoteFinish(raceId, lapSeconds))
            {
                me.WriteFinish(raceId, lapSeconds);
                RaiseChanged();
            }
        }

        public void MasterSetControl(RaceControl c)
        {
            if (!IsMaster || c == null)
            {
                return;
            }
            Control = c;
            pendingControl = c;
            TryWriteControl();
            RaiseChanged();
        }

        public void Leave()
        {
            End(OnlineStatus.Idle, "");
        }

        public void Dispose()
        {
            Leave();
        }

        // Stops the runner (unless Fusion is already shutting it down), forgets the room and tells the game.
        private void End(OnlineStatus status, string reason, bool callShutdown = true)
        {
            NetworkRunner r = runner;
            runner = null;
            if (r != null)
            {
                sessions.Remove(r);
                if (callShutdown && !r.IsShutdown)
                {
                    try
                    {
                        r.Shutdown(); // also destroys the runner's GameObject
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("Photon Fusion: Shutdown threw: " + e.Message);
                    }
                }
            }
            if (host != null)
            {
                UnityEngine.Object.Destroy(host);
                host = null;
            }
            entries.Clear();
            players.Clear();
            raceStates.Clear();
            me = null;
            myPlayer = null;
            pendingControl = null;
            listChanged = false;
            Control = RaceControl.Lobby;
            LocalId = 0;
            Fail(status, reason);
        }

        private void Fail(OnlineStatus status, string reason)
        {
            Status = status;
            FailReason = reason ?? "";
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            if (Changed != null)
            {
                Changed();
            }
        }

        private Entry FindEntry(FusionPlayerState state)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].State == state)
                {
                    return entries[i];
                }
            }
            return null;
        }

        private static string SafeName(string name)
        {
            return PlayerNameRules.IsValidStoredName(name) ? name : "PLAYER";
        }

        // --- INetworkRunnerCallbacks (main thread, during Fusion's update) ---

        public void OnShutdown(NetworkRunner r, ShutdownReason reason)
        {
            if (r != runner)
            {
                return;
            }
            string message = Status == OnlineStatus.InRoom ? "CONNECTION LOST (" + reason + ")" : FusionFailMessages.Message(reason);
            End(FusionFailMessages.StatusOf(reason), message, false);
        }

        public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason)
        {
            if (r == runner)
            {
                End(OnlineStatus.Failed, "CONNECTION LOST (" + reason + ")");
            }
        }

        public void OnConnectFailed(NetworkRunner r, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            if (r == runner)
            {
                End(OnlineStatus.Failed, "COULD NOT CONNECT (" + reason + ")");
            }
        }

        public void OnPlayerJoined(NetworkRunner r, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner r, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
        public void OnInput(NetworkRunner r, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner r, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner r) { }
        public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner r, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner r, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner r, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner r) { }
        public void OnSceneLoadStart(NetworkRunner r) { }
    }
}
