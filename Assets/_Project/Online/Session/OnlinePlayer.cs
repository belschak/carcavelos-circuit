using CarcavelosCircuit.RaceFlow;

namespace CarcavelosCircuit.Online
{
    // One player in the online room, as the session knows them (IOnlineSession.Players). The session owns and
    // updates these objects; everyone else only reads them. FinishSeconds is NaN until the player reports a finish
    // for the current race (RaceControl.RaceId); a new race sets it back to NaN.
    // DriverIndex = the driver the player picked (RaceFlow's DriverRoster, DriverChoice at join): everyone builds that
    // player's kart with this driver's own kart model. A number outside the roster (an old or broken client) is the Rookie.
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

        // A driver number that is really in the roster; anything else = the Rookie.
        public static int SafeDriver(int driverIndex)
        {
            return DriverRoster.IsValid(driverIndex) ? driverIndex : DriverRoster.Rookie;
        }

        // Has this player a finish time for the given race?
        public bool HasFinished(int raceId)
        {
            return raceId != 0 && FinishRaceId == raceId && !double.IsNaN(FinishSeconds);
        }

        // Records a finish. Only the first report per race counts (a doubled packet changes nothing).
        public bool NoteFinish(int raceId, double seconds)
        {
            if (raceId == 0 || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.0 || HasFinished(raceId))
            {
                return false;
            }
            FinishRaceId = raceId;
            FinishSeconds = seconds;
            return true;
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
