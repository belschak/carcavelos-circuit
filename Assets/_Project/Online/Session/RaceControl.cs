namespace CarcavelosCircuit.Online
{
    // What the master tells the room about the race (IOnlineSession.Control): which race (RaceId counts up per
    // start), the phase, the shared moment of GO (StartAt on the shared clock) and who is in it (Roster = player
    // ids in join order; the index is the grid slot). Never changed in place: the master makes a new one and calls
    // MasterSetControl, every client gets the whole thing (state sync, no ordering problems).
    public class RaceControl
    {
        public static readonly RaceControl Lobby = new RaceControl(0, OnlinePhase.Lobby, 0.0, new int[0]);

        public int RaceId { get; private set; }
        public OnlinePhase Phase { get; private set; }
        public double StartAt { get; private set; }
        public int[] Roster { get; private set; }

        public RaceControl(int raceId, OnlinePhase phase, double startAt, int[] roster)
        {
            RaceId = raceId < 0 ? 0 : raceId;
            Phase = phase;
            StartAt = double.IsNaN(startAt) || double.IsInfinity(startAt) ? 0.0 : startAt;
            Roster = roster == null ? new int[0] : (int[])roster.Clone();
        }

        public int RosterCount { get { return Roster.Length; } }

        // The grid slot of a player (its index in the roster), -1 if the player is not in this race.
        public int IndexOf(int playerId)
        {
            for (int i = 0; i < Roster.Length; i++)
            {
                if (Roster[i] == playerId)
                {
                    return i;
                }
            }
            return -1;
        }

        public bool Contains(int playerId)
        {
            return IndexOf(playerId) >= 0;
        }

        // Is a race going on (countdown, racing or its results) that a newcomer is not part of?
        public bool IsRaceUnderway
        {
            get { return RaceId > 0 && Phase != OnlinePhase.Lobby; }
        }

        // The same control (every field equal)? The director uses this to see if anything changed.
        public bool SameAs(RaceControl other)
        {
            if (other == null || RaceId != other.RaceId || Phase != other.Phase || StartAt != other.StartAt || Roster.Length != other.Roster.Length)
            {
                return false;
            }
            for (int i = 0; i < Roster.Length; i++)
            {
                if (Roster[i] != other.Roster[i])
                {
                    return false;
                }
            }
            return true;
        }

        // A copy with another phase (the master moves Countdown -> Racing -> Results).
        public RaceControl WithPhase(OnlinePhase phase)
        {
            return new RaceControl(RaceId, phase, StartAt, Roster);
        }
    }
}
