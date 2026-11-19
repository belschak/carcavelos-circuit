using CarcavelosCircuit.RaceFlow;

namespace CarcavelosCircuit.Online
{
    // One player in the online room, as the session knows them (IOnlineSession.Players). The session owns and
    // updates these objects; everyone else only reads them. FinishSeconds is NaN until the player reports a finish
    // for the current race (RaceControl.RaceId); a new race sets it back to NaN.
    // DriverIndex = the player's kart set as one number (RaceFlow's KartLoadout.Code, DriverChoice.Loadout at join):
    // 0-3 = that driver in their own kart (what builds before the kart builder sent), 100-163 = any driver + kart + tyres.
    // Everyone builds that player's kart from it. Any other number (an old or broken client) is the Rookie's own set.
    public class OnlinePlayer
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public int ColourIndex { get; private set; }
        public int DriverIndex { get; private set; }
        public double JoinedAt { get; private set; }    // shared clock when the player joined (join order = roster order)
        public KartPose LatestPose { get; set; }
        public bool HasPose { get; set; }
        public int FinishRaceId { get; set; }           // the race the finish below belongs to, 0 = none
        public double FinishSeconds { get; set; } = double.NaN;

        public OnlinePlayer(int id, string name, int colourIndex, double joinedAt, int driverIndex = DriverRoster.Rookie)
        {
            Id = id;
            Name = name ?? "";
            ColourIndex = colourIndex;
            JoinedAt = joinedAt;
            DriverIndex = SafeDriver(driverIndex);
        }

        // A kart set number KartLoadout can read; anything else = the Rookie's own set.
        public static int SafeDriver(int driverIndex)
        {
            return KartLoadout.IsValidCode(driverIndex) ? driverIndex : DriverRoster.Rookie;
        }

        // The driver, kart and tyres this player races with.
        public KartLoadout Loadout { get { return KartLoadout.FromCode(DriverIndex); } }

        // Has this player a finish time for the given race?
        public bool HasFinished(int raceId)
        {
            return raceId != 0 && FinishRaceId == raceId && !double.IsNaN(FinishSeconds);
        }

        // Records a finish. Only the first report per race counts (a doubled packet changes nothing).
        // A time outside the leaderboard's window (under 5 s or over 30 min, LapTimeRules) is refused: no real lap is that
        // fast, so a changed game that reports 0.01 s cannot win the race on everyone's screen (G-audit, 19 Nov).
        public bool NoteFinish(int raceId, double seconds)
        {
            if (raceId == 0 || !IsBelievableFinish(seconds) || HasFinished(raceId))
            {
                return false;
            }
            FinishRaceId = raceId;
            FinishSeconds = seconds;
            return true;
        }

        public static bool IsBelievableFinish(double seconds)
        {
            return !double.IsNaN(seconds) && !double.IsInfinity(seconds)
                && seconds >= LapTimeRules.AbsoluteMinSeconds && seconds <= LapTimeRules.MaxSeconds;
        }

        // Joined earlier wins; the same moment (two Loopback sessions in one tick): the lower id first.
        public static int CompareJoinOrder(OnlinePlayer a, OnlinePlayer b)
        {
            if (a.JoinedAt != b.JoinedAt)
            {
                return a.JoinedAt < b.JoinedAt ? -1 : 1;
            }
            return a.Id.CompareTo(b.Id);
        }
    }
}
