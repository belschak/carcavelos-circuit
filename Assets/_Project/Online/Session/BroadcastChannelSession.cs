using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using CarcavelosCircuit.RaceFlow;

namespace CarcavelosCircuit.Online
{
    // IOnlineSession over the browser's BroadcastChannel (?net=local): every tab of the SAME browser on one machine
    // that opened the same room hears every message. It exists so we can test the whole online flow in 2 to 4 tabs
    // before the Photon build exists. The JavaScript side is Online/Plugins/OnlineBroadcast.jslib. Outside a Web
    // build the jslib does not exist, so OnlineBootstrap uses LoopbackSession instead (IsSupported says which).
    // Protocol, one text line per message, fields split by "|" (names cannot contain "|": PlayerNameRules):
    //   hello|id|joinedAt|name|colour|driver              I joined (everyone answers with "here", the master with "control")
    //   here|id|joinedAt|name|colour|raceId|secs|driver   my record (answer to hello, and every 2 s as a heartbeat)
    //   (driver = DriverRoster index, 17 Nov 2026; a line without it, from an older page, means the Rookie)
    //   control|id|<RaceControlCodec>              the master's race control
    //   pose|id|<KartPoseCodec base64>             my kart, about 15 times a second
    //   finish|id|raceId|seconds                   I crossed the line
    //   leave|id                                   I am gone (also sent by the page when the tab closes)
    // Join order = joinedAt on the shared clock (performance.timeOrigin + performance.now(), the same on one machine).
    // Master = the earliest join still present. A player not heard from for 6 s is dropped.
    // Everything from the channel is checked (numbers, name rules, pose format) before it changes anything.
    public class BroadcastChannelSession : IOnlineSession, IPumpedSession
    {
        public const string ChannelPrefix = "carcavelos-online-";
        public const double ConnectSeconds = 0.8;     // how long we listen for "here" answers before we know the room
        public const double HeartbeatSeconds = 2.0;
        public const double SilenceSeconds = 6.0;     // a player silent this long has gone
        public const int MaxMessageLength = 800;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int OnlineBroadcastOpen(string channel);
        [DllImport("__Internal")] private static extern void OnlineBroadcastClose();
        [DllImport("__Internal")] private static extern void OnlineBroadcastPost(string message);
        [DllImport("__Internal")] private static extern void OnlineBroadcastSetLeaveMessage(string message);
        [DllImport("__Internal")] private static extern string OnlineBroadcastPoll();
        [DllImport("__Internal")] private static extern double OnlineBroadcastNow();
        [DllImport("__Internal")] private static extern int OnlineBroadcastRandomId();
#endif

        private static readonly OnlinePlayer[] NoPlayers = new OnlinePlayer[0];

        private readonly List<OnlinePlayer> players = new List<OnlinePlayer>();   // join order
        private readonly Dictionary<int, double> lastHeard = new Dictionary<int, double>();
        private OnlinePlayer me;
        private double connectUntil;
        private double nextHeartbeat;
        private bool channelOpen;

        public OnlineStatus Status { get; private set; } = OnlineStatus.Idle;
        public string FailReason { get; private set; } = "";
        public int LocalId { get; private set; }
        public RaceControl Control { get; private set; } = RaceControl.Lobby;
        public IReadOnlyList<OnlinePlayer> Players { get { return Status == OnlineStatus.InRoom || Status == OnlineStatus.Connecting ? players : NoPlayers; } }

        public event Action<int, KartPose> PoseReceived;
        public event Action Changed;

        // True only in a Web build: there the browser has BroadcastChannel and the jslib is compiled in.
        public static bool IsSupported
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public bool IsMaster
        {
            get { return Status == OnlineStatus.InRoom && players.Count > 0 && players[0].Id == LocalId; }
        }

        public double Now
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return OnlineBroadcastNow();
#else
                return LoopbackHub.Now;
#endif
            }
        }

        public void Join(string room, string name, int colourIndex, int driverIndex)
        {
            if (Status == OnlineStatus.InRoom || Status == OnlineStatus.Connecting)
            {
                return;
            }
            if (!IsSupported)
            {
                Status = OnlineStatus.Failed;
                FailReason = "NO BROWSER CHANNEL";
                RaiseChanged();
                return;
            }
            if (string.IsNullOrEmpty(room))
            {
                Status = OnlineStatus.Failed;
                FailReason = "NO ROOM NAME";
                RaiseChanged();
                return;
            }
            if (!Open(ChannelPrefix + room))
            {
                Status = OnlineStatus.Failed;
                FailReason = "THIS BROWSER HAS NO BROADCAST CHANNEL";
                RaiseChanged();
                return;
            }
            channelOpen = true;
            LocalId = RandomId();
            me = new OnlinePlayer(LocalId, SafeName(name), colourIndex, Now, driverIndex);
            players.Clear();
            lastHeard.Clear();
            players.Add(me);
            Control = RaceControl.Lobby;
            Status = OnlineStatus.Connecting;
            FailReason = "";
            connectUntil = Now + ConnectSeconds;
            nextHeartbeat = Now + HeartbeatSeconds;
            SetLeaveMessage("leave|" + LocalId);
            Post("hello|" + LocalId + "|" + Number(me.JoinedAt) + "|" + me.Name + "|" + me.ColourIndex + "|" + me.DriverIndex);
            RaiseChanged();
        }

        public void Leave()
        {
            if (channelOpen)
            {
                if (Status == OnlineStatus.InRoom || Status == OnlineStatus.Connecting)
                {
                    Post("leave|" + LocalId);
                }
                Close();
                channelOpen = false;
            }
            players.Clear();
            lastHeard.Clear();
            me = null;
            Control = RaceControl.Lobby;
            Status = OnlineStatus.Idle;
            FailReason = "";
            RaiseChanged();
        }

        public void Dispose()
        {
            Leave();
        }

        public void SendPose(in KartPose pose)
        {
            if (Status != OnlineStatus.InRoom)
            {
                return;
            }
            me.LatestPose = pose;
            me.HasPose = true;
            Post("pose|" + LocalId + "|" + KartPoseCodec.EncodeText(pose));
        }

        public void ReportFinish(int raceId, double lapSeconds)
        {
            if (Status != OnlineStatus.InRoom || !me.NoteFinish(raceId, lapSeconds))
            {
                return;
            }
            Post("finish|" + LocalId + "|" + raceId + "|" + Number(lapSeconds));
            RaiseChanged();
        }

        public void MasterSetControl(RaceControl c)
        {
            if (!IsMaster || c == null)
            {
                return;
            }
            Control = c;
            Post("control|" + LocalId + "|" + RaceControlCodec.Encode(c));
            RaiseChanged();
        }

        // Once per frame: read what the other tabs sent, send the heartbeat, drop silent tabs, finish connecting.
        public void Pump()
        {
            if (Status != OnlineStatus.InRoom && Status != OnlineStatus.Connecting)
            {
                return;
            }
            for (int i = 0; i < 512; i++) // a hard cap per frame, so a flood can never freeze a frame
            {
                string message = Poll();
                if (message == null)
                {
                    break;
                }
                Handle(message);
                if (Status != OnlineStatus.InRoom && Status != OnlineStatus.Connecting)
                {
                    return; // a handler ended the session
                }
            }
            double now = Now;
            if (Status == OnlineStatus.Connecting && now >= connectUntil)
            {
                FinishConnecting();
            }
            if (Status == OnlineStatus.InRoom && now >= nextHeartbeat)
            {
                nextHeartbeat = now + HeartbeatSeconds;
                Post(HereLine());
            }
            DropSilent(now);
        }

        // After listening for a moment we know who is in: full = we never were; else we are in.
        private void FinishConnecting()
        {
            if (players.Count > OnlineRooms.MaxPlayers)
            {
                Post("leave|" + LocalId);
                Close();
                channelOpen = false;
                players.Clear();
                lastHeard.Clear();
                Status = OnlineStatus.Full;
                FailReason = "ROOM FULL";
                RaiseChanged();
                return;
            }
            Status = OnlineStatus.InRoom;
            RaiseChanged();
        }

        // ---- Incoming ----

        private void Handle(string message)
        {
            if (message == null || message.Length == 0 || message.Length > MaxMessageLength)
            {
                return;
            }
            string[] parts = message.Split('|');
            if (parts.Length < 2)
            {
                return;
            }
            int senderId;
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out senderId) || senderId <= 0 || senderId == LocalId)
            {
                return; // not a player id, or our own line bounced back
            }
            switch (parts[0])
            {
                case "hello":
                    if ((parts.Length == 5 || parts.Length == 6) && AddOrUpdate(senderId, parts[2], parts[3], parts[4], parts.Length == 6 ? parts[5] : null))
                    {
                        Post(HereLine());
                        if (IsMaster && Control.RaceId > 0)
                        {
                            Post("control|" + LocalId + "|" + RaceControlCodec.Encode(Control));
                        }
                    }
                    break;
                case "here":
                    if ((parts.Length == 7 || parts.Length == 8) && AddOrUpdate(senderId, parts[2], parts[3], parts[4], parts.Length == 8 ? parts[7] : null))
                    {
                        NoteFinishText(senderId, parts[5], parts[6], false);
                    }
                    break;
                case "control":
                    if (parts.Length == 3)
                    {
                        AcceptControl(senderId, parts[2]);
                    }
                    break;
                case "pose":
                    if (parts.Length == 3)
                    {
                        AcceptPose(senderId, parts[2]);
                    }
                    break;
                case "finish":
                    if (parts.Length == 4)
                    {
                        NoteFinishText(senderId, parts[2], parts[3], true);
                    }
                    break;
                case "leave":
                    Remove(senderId);
                    break;
            }
        }

        // A player record from hello or here. New = added in join order + Changed; known = only "heard now".
        // driverText null or not a number = the Rookie (OnlinePlayer.SafeDriver also catches numbers outside the roster).
        private bool AddOrUpdate(int senderId, string joinedAtText, string name, string colourText, string driverText)
        {
            double joinedAt;
            int colour;
            if (!double.TryParse(joinedAtText, NumberStyles.Float, CultureInfo.InvariantCulture, out joinedAt) || double.IsNaN(joinedAt) || double.IsInfinity(joinedAt))
            {
                return false;
            }
            if (!int.TryParse(colourText, NumberStyles.Integer, CultureInfo.InvariantCulture, out colour))
            {
                return false;
            }
            lastHeard[senderId] = Now;
            if (Find(senderId) != null)
            {
                return true;
            }
            if (players.Count >= OnlineRooms.MaxPlayers + 1)
            {
                return false; // more than a full room plus us: ignore the rest
            }
            int driver;
            if (driverText == null || !int.TryParse(driverText, NumberStyles.Integer, CultureInfo.InvariantCulture, out driver))
            {
                driver = 0;
            }
            OnlinePlayer player = new OnlinePlayer(senderId, SafeName(name), colour, joinedAt, driver);
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
            RaiseChanged();
            return true;
        }

        private void Remove(int senderId)
        {
            OnlinePlayer player = Find(senderId);
            lastHeard.Remove(senderId);
            if (player == null)
            {
                return;
            }
            players.Remove(player);
            RaiseChanged();
        }

        private void AcceptControl(int senderId, string text)
        {
            OnlinePlayer sender = Find(senderId);
            if (sender == null)
            {
                return;
            }
            // Only the master's control counts. While connecting we may not know everyone yet, so then any known
            // player is trusted (the room only ever has one master anyway).
            bool fromMaster = Status == OnlineStatus.Connecting || (players.Count > 0 && players[0].Id == senderId);
            RaceControl control;
            if (!fromMaster || !RaceControlCodec.TryDecode(text, out control))
            {
                return;
            }
            lastHeard[senderId] = Now;
            if (control.SameAs(Control))
            {
                return;
            }
            Control = control;
            RaiseChanged();
        }

        private void AcceptPose(int senderId, string text)
        {
            OnlinePlayer sender = Find(senderId);
            KartPose pose;
            if (sender == null || !KartPoseCodec.TryDecodeText(text, out pose))
            {
                return;
            }
            lastHeard[senderId] = Now;
            sender.LatestPose = pose;
            sender.HasPose = true;
            if (PoseReceived != null)
            {
                PoseReceived(senderId, pose);
            }
        }

        private void NoteFinishText(int senderId, string raceIdText, string secondsText, bool announce)
        {
            OnlinePlayer sender = Find(senderId);
            int raceId;
            double seconds;
            if (sender == null || !int.TryParse(raceIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out raceId)
                || !double.TryParse(secondsText, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
            {
                return;
            }
            lastHeard[senderId] = Now;
            if (sender.NoteFinish(raceId, seconds))
            {
                RaiseChanged();
            }
        }

        private void DropSilent(double now)
        {
            for (int i = players.Count - 1; i >= 0; i--)
            {
                int id = players[i].Id;
                double heard;
                if (id != LocalId && lastHeard.TryGetValue(id, out heard) && now - heard > SilenceSeconds)
                {
                    Remove(id);
                }
            }
        }

        private OnlinePlayer Find(int id)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Id == id)
                {
                    return players[i];
                }
            }
            return null;
        }

        private string HereLine()
        {
            string raceId = me.FinishRaceId.ToString(CultureInfo.InvariantCulture);
            string seconds = double.IsNaN(me.FinishSeconds) ? "0" : Number(me.FinishSeconds);
            return "here|" + LocalId + "|" + Number(me.JoinedAt) + "|" + me.Name + "|" + me.ColourIndex + "|" + raceId + "|" + seconds + "|" + me.DriverIndex;
        }

        // A name from the address bar or from another tab: only a name that passes the player name rules travels
        // (so no "|", no HTML, no rude words); anything else is shown as PLAYER.
        public static string SafeName(string name)
        {
            return PlayerNameRules.IsValidStoredName(name) ? name : "PLAYER";
        }

        private static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private void RaiseChanged()
        {
            if (Changed != null)
            {
                Changed();
            }
        }

        // ---- The browser (jslib) ----

        private static bool Open(string channel)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return OnlineBroadcastOpen(channel) == 1;
#else
            return false;
#endif
        }

        private static void Close()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            OnlineBroadcastClose();
#endif
        }

        private static void Post(string message)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            OnlineBroadcastPost(message);
#endif
        }

        private static void SetLeaveMessage(string message)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            OnlineBroadcastSetLeaveMessage(message);
#endif
        }

        private static string Poll()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return OnlineBroadcastPoll();
#else
            return null;
#endif
        }

        private static int RandomId()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return OnlineBroadcastRandomId();
#else
            return new Random().Next(1, int.MaxValue);
#endif
        }
    }
}
