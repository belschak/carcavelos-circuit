using Fusion;

namespace CarcavelosCircuit.Online.Fusion
{
    // The room's race control (RaceControl) as Fusion carries it: one object per room, spawned by the first master
    // client with the "master client object" flag, so state authority moves to the next master when the current one
    // leaves (Fusion does that by itself). Only the master writes; everyone reads it in FusionSession.Tick and turns
    // it back into a RaceControl when something changed.
    public class FusionRaceState : NetworkBehaviour
    {
        [Networked] public int RaceId { get; set; }
        [Networked] public int Phase { get; set; }
        [Networked] public double StartAt { get; set; }
        [Networked] public int RosterCount { get; set; }
        [Networked, Capacity(OnlineRooms.MaxPlayers)] public NetworkArray<int> Roster => default;

        public override void Spawned()
        {
            FusionSession session = FusionSession.Of(Runner);
            if (session != null)
            {
                session.OnRaceSpawned(this);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            FusionSession session = FusionSession.Of(runner);
            if (session != null)
            {
                session.OnRaceDespawned(this);
            }
        }

        // Master only. A roster longer than the room is cut (never happens, the room has 15 slots).
        public void Write(RaceControl control)
        {
            int count = control.RosterCount < Roster.Length ? control.RosterCount : Roster.Length;
            for (int i = 0; i < count; i++)
            {
                Roster.Set(i, control.Roster[i]);
            }
            RosterCount = count;
            RaceId = control.RaceId;
            Phase = (int)control.Phase;
            StartAt = control.StartAt;
        }

        // Is the networked state the same as this control? (No allocation: checked every frame.)
        public bool SameAs(RaceControl control)
        {
            int count = SafeCount();
            if (control == null || RaceId != control.RaceId || Phase != (int)control.Phase || StartAt != control.StartAt || count != control.RosterCount)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                if (Roster.Get(i) != control.Roster[i])
                {
                    return false;
                }
            }
            return true;
        }

        // The networked state as a RaceControl (a new object; RaceControl is never changed in place).
        public RaceControl Read()
        {
            int count = SafeCount();
            int[] roster = new int[count];
            for (int i = 0; i < count; i++)
            {
                roster[i] = Roster.Get(i);
            }
            return new RaceControl(RaceId, PhaseOf(Phase), StartAt, roster);
        }

        private int SafeCount()
        {
            int count = RosterCount;
            return count < 0 ? 0 : (count > Roster.Length ? Roster.Length : count);
        }

        // A number from the network back to a phase; anything unknown is treated as the lobby.
        private static OnlinePhase PhaseOf(int value)
        {
            return value >= (int)OnlinePhase.Lobby && value <= (int)OnlinePhase.Results ? (OnlinePhase)value : OnlinePhase.Lobby;
        }
    }
}
