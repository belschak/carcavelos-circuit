using System;
using CarcavelosCircuit.Shared;

namespace CarcavelosCircuit.Online
{
    // The online flags in the page's address (plain C#, tested in OnlineUrlFlagsTests), for the test build and the
    // presentation link: index.html?net=local&room=noon&name=Ana&autopilot=1&autostart=4
    //   net       local = tabs of this browser (BroadcastChannel), photon = the real online build; else the default
    //   room      noon or sunset: join that room without clicking (needs a name too, else the room is only preselected)
    //   name      the player name (checked like a typed name, a bad one opens the name field instead)
    //   autopilot 1 = the kart drives itself (so one person can fill a room with tabs)
    //   autostart N = the host starts 5 s after N players are in the room (2..15)
    //   render    0 = no 3D drawing (cameras off): a scale-test bot on a machine without a GPU still drives and sends
    //             at full speed; the screen shows only the HUD over black (17 Nov 2026, CI runners drew 3 fps)
    // Anything else in the address is ignored; every value is cut to 200 characters before it is looked at.
    public class OnlineUrlFlags
    {
        public const int MaxValueLength = 200;

        public string Net { get; private set; } = "";      // "", "local" or "photon"
        public bool HasRoom { get; private set; }
        public TimeOfDay Room { get; private set; } = TimeOfDay.Noon;
        public string Name { get; private set; } = "";     // as typed in the address, not yet cleaned
        public bool Autopilot { get; private set; }
        public int AutoStart { get; private set; }         // 0 = off
        public bool NoRender { get; private set; }         // render=0: cameras off (scale-test bots)

        // Room and name both given: the lobby joins by itself.
        public bool WantsAutoJoin { get { return HasRoom && Name.Length > 0; } }

        public static OnlineUrlFlags None { get { return new OnlineUrlFlags(); } }

        public static OnlineUrlFlags Parse(string url)
        {
            OnlineUrlFlags flags = new OnlineUrlFlags();
            if (string.IsNullOrEmpty(url))
            {
                return flags;
            }
            int question = url.IndexOf('?');
            if (question < 0)
            {
                return flags;
            }
            string query = url.Substring(question + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0)
            {
                query = query.Substring(0, hash); // "#..." is the page's own anchor, never a flag
            }
            string[] pairs = query.Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                string key;
                string value;
                SplitPair(pairs[i], out key, out value);
                flags.Apply(key, value);
            }
            return flags;
        }

        // "name=Ana%20B" -> key "name", value "Ana B". Keys are compared in lower case.
        private static void SplitPair(string pair, out string key, out string value)
        {
            int equals = pair.IndexOf('=');
            string rawKey = equals < 0 ? pair : pair.Substring(0, equals);
            string rawValue = equals < 0 ? "" : pair.Substring(equals + 1);
            key = Decode(rawKey).Trim().ToLowerInvariant();
            value = Decode(rawValue);
        }

        // Percent escapes and "+" for space, cut to MaxValueLength. A broken escape ("%zz") is kept as it is.
        public static string Decode(string raw)
        {
            if (raw == null)
            {
                return "";
            }
            if (raw.Length > MaxValueLength)
            {
                raw = raw.Substring(0, MaxValueLength);
            }
            string text = raw.Replace('+', ' ');
            try
            {
                return Uri.UnescapeDataString(text);
            }
            catch (Exception)
            {
                return text;
            }
        }

        private void Apply(string key, string value)
        {
            switch (key)
            {
                case "net":
                    string net = value.Trim().ToLowerInvariant();
                    Net = net == OnlineSessions.Local || net == OnlineSessions.Photon ? net : "";
                    break;
                case "room":
                    TimeOfDay room;
                    HasRoom = OnlineRooms.TryParseRoom(value, out room);
                    Room = HasRoom ? room : TimeOfDay.Noon;
                    break;
                case "name":
                    Name = value.Trim();
                    break;
                case "autopilot":
                    string on = value.Trim().ToLowerInvariant();
                    Autopilot = on == "1" || on == "true" || on == "yes";
                    break;
                case "render":
                    string draw = value.Trim().ToLowerInvariant();
                    NoRender = draw == "0" || draw == "false" || draw == "no" || draw == "off";
                    break;
                case "autostart":
                    int wanted;
                    if (int.TryParse(value.Trim(), out wanted) && wanted >= 2)
                    {
                        AutoStart = wanted > OnlineRooms.MaxPlayers ? OnlineRooms.MaxPlayers : wanted;
                    }
                    break;
            }
        }

        // Which transport the lobby uses, or null when none fits (then the Online card says it needs the online build).
        // ?net=local or ?net=photon ask for that one by name; otherwise the default ("photon" when registered),
        // and in the Editor "local" (Loopback), so the lobby can be tried in Play Mode.
        public static string PickTransport(string net, bool hasPhoton, bool hasLocal, bool isEditor)
        {
            if (net == OnlineSessions.Local)
            {
                return hasLocal ? OnlineSessions.Local : null;
            }
            if (net == OnlineSessions.Photon)
            {
                return hasPhoton ? OnlineSessions.Photon : null;
            }
            if (hasPhoton)
            {
                return OnlineSessions.Photon;
            }
            return isEditor && hasLocal ? OnlineSessions.Local : null;
        }
    }
}
