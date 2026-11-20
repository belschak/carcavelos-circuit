using System;
using System.Collections.Generic;

namespace CarcavelosCircuit.Online
{
    // IOnlineSession over LoopbackHub: the room lives in this process. Used by the Edit Mode tests (two or more
    // sessions in one room) and as the Editor's fallback for ?net=local, where a browser's BroadcastChannel does not
    // exist. Everything is synchronous: another session's Join, Leave, MasterSetControl, ReportFinish or SendPose
    // calls straight into our Changed or PoseReceived, after the room already shows the new state.
    public class LoopbackSession : IOnlineSession
    {
        private static readonly OnlinePlayer[] NoPlayers = new OnlinePlayer[0];

        private LoopbackRoom room;
        private readonly int id = LoopbackHub.NextId();

        public OnlineStatus Status { get; private set; } = OnlineStatus.Idle;
        public string FailReason { get; private set; } = "";
        public int LocalId { get { return id; } }
        public bool IsMaster { get { return room != null && room.Master == this; } }
        public double Now { get { return LoopbackHub.Now; } }
        public IReadOnlyList<OnlinePlayer> Players { get { return room != null ? room.Players : NoPlayers; } }
        public RaceControl Control { get { return room != null ? room.Control : RaceControl.Lobby; } }

        public event Action<int, KartPose> PoseReceived;
        public event Action Changed;

        // Joins at once: InRoom, or Full when the room already has all its seats taken (OnlineRooms.SeatsOf).
        public void Join(string roomName, string name, int colourIndex, int driverIndex)
        {
            if (Status == OnlineStatus.InRoom || Status == OnlineStatus.Connecting)
            {
                return; // already in a room: Leave first
            }
            if (string.IsNullOrEmpty(roomName))
            {
                Status = OnlineStatus.Failed;
                FailReason = "NO ROOM NAME";
                RaiseChanged();
                return;
            }
            LoopbackRoom wanted = LoopbackHub.RoomOf(roomName);
            OnlinePlayer me = new OnlinePlayer(id, name ?? "", colourIndex, Now, driverIndex);
            if (!wanted.Join(this, me))
            {
                Status = OnlineStatus.Full;
                FailReason = "ROOM FULL";
                RaiseChanged();
                return;
            }
            room = wanted;
            Status = OnlineStatus.InRoom;
            FailReason = "";
            // The room already told everyone (including us) in Join; nothing more to raise.
        }

        public void Leave()
        {
            LoopbackRoom old = room;
            room = null;
            Status = OnlineStatus.Idle;
            FailReason = "";
            if (old != null)
            {
                old.Leave(this); // the others hear about it; we are no longer a member, so we do not
            }
            RaiseChanged();
        }

        public void SendPose(in KartPose pose)
        {
            if (room != null)
            {
                room.SendPose(this, pose);
            }
        }

        public void ReportFinish(int raceId, double lapSeconds)
        {
            if (room != null)
            {
                room.Finish(this, raceId, lapSeconds);
            }
        }

        public void MasterSetControl(RaceControl c)
        {
            if (room != null)
            {
                room.SetControl(this, c);
            }
        }

        public void Dispose()
        {
            Leave();
        }

        // Called by the room (same thread).
        internal void RaiseChanged()
        {
            if (Changed != null)
            {
                Changed();
            }
        }

        internal void ReceivePose(int senderId, KartPose pose)
        {
            if (PoseReceived != null)
            {
                PoseReceived(senderId, pose);
            }
        }
    }
}
