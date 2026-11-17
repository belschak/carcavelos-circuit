using System.Globalization;

namespace CarcavelosCircuit.Online
{
    // RaceControl as one short text line for transports that carry strings (BroadcastChannelSession):
    //   "raceId;phase;startAt;id,id,id"   e.g. "3;1;1234.5;17,42,8"
    // Decoding refuses anything that is not exactly this shape (every number checked), so a foreign or broken
    // message can never put the room into a nonsense state. Plain C#, tested (RaceControlCodecTests).
    public static class RaceControlCodec
    {
        public const int MaxRoster = 64;          // more than any room can hold (OnlineRooms.MaxPlayers)
        public const int MaxTextLength = 600;     // "raceId;phase;startAt;" + 64 ids of 10 digits

        public static string Encode(RaceControl control)
        {
            if (control == null)
            {
                control = RaceControl.Lobby;
            }
            string ids = "";
            for (int i = 0; i < control.Roster.Length; i++)
            {
                ids += (i == 0 ? "" : ",") + control.Roster[i].ToString(CultureInfo.InvariantCulture);
            }
            return control.RaceId.ToString(CultureInfo.InvariantCulture) + ";" + ((int)control.Phase).ToString(CultureInfo.InvariantCulture)
                + ";" + control.StartAt.ToString("R", CultureInfo.InvariantCulture) + ";" + ids;
        }

        public static bool TryDecode(string text, out RaceControl control)
        {
            control = RaceControl.Lobby;
            if (text == null || text.Length > MaxTextLength)
            {
                return false;
            }
            string[] parts = text.Split(';');
            if (parts.Length != 4)
            {
                return false;
            }
            int raceId;
            int phase;
            double startAt;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out raceId) || raceId < 0)
            {
                return false;
            }
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out phase) || phase < 0 || phase > (int)OnlinePhase.Results)
            {
                return false;
            }
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out startAt) || double.IsNaN(startAt) || double.IsInfinity(startAt))
            {
                return false;
            }
            int[] roster;
            if (parts[3].Length == 0)
            {
                roster = new int[0];
            }
            else
            {
                string[] ids = parts[3].Split(',');
                if (ids.Length > MaxRoster)
                {
                    return false;
                }
                roster = new int[ids.Length];
                for (int i = 0; i < ids.Length; i++)
                {
                    if (!int.TryParse(ids[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out roster[i]))
                    {
                        return false;
                    }
                }
            }
            control = new RaceControl(raceId, (OnlinePhase)phase, startAt, roster);
            return true;
        }
    }
}
