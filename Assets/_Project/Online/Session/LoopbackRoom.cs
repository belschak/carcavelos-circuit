using System.Collections.Generic;

namespace CarcavelosCircuit.Online
{
    // One in-process room of LoopbackHub: its members in join order, the master's control, and the fan-out of poses,
    // finishes and changes to every member. Sessions call in, the room calls every member's handlers back (on the
    // same thread, after the room's lists already show the new state).
    public class LoopbackRoom
    {
        private readonly List<LoopbackSession> sessions = new List<LoopbackSession>();
        private readonly List<OnlinePlayer> players = new List<OnlinePlayer>();

        public string Name { get; private set; }
        public RaceControl Control { get; private set; } = RaceControl.Lobby;
        public int Count { get { return sessions.Count; } }
        public IReadOnlyList<OnlinePlayer> Players { get { return players; } }

        public LoopbackRoom(string name)
        {
            Name = name;
        }

        // The earliest member still here is the master; null for an empty room.
        public LoopbackSession Master
        {
            get { return sessions.Count == 0 ? null : sessions[0]; }
        }

        public bool IsFull
        {
            get { return !OnlineRooms.HasSpace(sessions.Count, OnlineRooms.SeatsOf(Name)); }
        }

        // Adds the session (false when the room is full) and tells everyone.
        public bool Join(LoopbackSession session, OnlinePlayer player)
        {
            if (IsFull || sessions.Contains(session))
            {
                return false;
            }
            sessions.Add(session);
            players.Add(player);
            NotifyChanged();
            return true;
        }

        public void Leave(LoopbackSession session)
        {
            int i = sessions.IndexOf(session);
            if (i < 0)
            {
                return;
            }
            sessions.RemoveAt(i);
            players.RemoveAt(i);
            NotifyChanged();
        }

        public OnlinePlayer PlayerOf(LoopbackSession session)
        {
            int i = sessions.IndexOf(session);
            return i < 0 ? null : players[i];
        }

        // Only the master's control counts; everyone else's call is dropped.
        public void SetControl(LoopbackSession from, RaceControl control)
        {
            if (from != Master || control == null)
            {
                return;
            }
            Control = control;
            NotifyChanged();
        }

        public void Finish(LoopbackSession from, int raceId, double seconds)
        {
            OnlinePlayer player = PlayerOf(from);
            if (player != null && player.NoteFinish(raceId, seconds))
            {
                NotifyChanged();
            }
        }

        // The sender keeps its own pose; everyone else gets PoseReceived.
        public void SendPose(LoopbackSession from, KartPose pose)
        {
            OnlinePlayer player = PlayerOf(from);
            if (player == null)
            {
                return;
            }
            player.LatestPose = pose;
            player.HasPose = true;
            LoopbackSession[] receivers = sessions.ToArray(); // a handler may leave the room while we loop
            for (int i = 0; i < receivers.Length; i++)
            {
                if (receivers[i] != from)
                {
                    receivers[i].ReceivePose(player.Id, pose);
                }
            }
        }

        private void NotifyChanged()
        {
            LoopbackSession[] receivers = sessions.ToArray();
            for (int i = 0; i < receivers.Length; i++)
            {
                receivers[i].RaiseChanged();
            }
        }
    }
}
