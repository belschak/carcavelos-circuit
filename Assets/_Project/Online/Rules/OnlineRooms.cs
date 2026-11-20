using CarcavelosCircuit.Shared;

namespace CarcavelosCircuit.Online
{
    // The online room of the class (team feedback round, 17 Nov 2026): everyone joins the same room "carcavelos",
    // 15 seats, first come first served. Two rooms (noon and sunset, 16 Nov) split a class of 15 into two half-empty
    // races, so there is one room now and the light (noon or sunset) is each player's own look again (T on the start
    // screen or in the lobby); it never travels over the network. Photon Cloud hosts the room, so one room or two
    // costs the same (the free plan counts players, 100 at once, not rooms).
    // Overflow (20 Nov 2026, presentation safety): a class bigger than 15 must never see "room full" as a dead end.
    // When room 1 is full the lobby goes on to room 2 ("carcavelos-2"), then 3 and 4 (60 seats, well under the 100
    // CCU of the free plan). Each room races on its own. ?room=2 joins room 2 straight away.
    // Test rooms: ?seats=N (2..15) makes small rooms with their own names ("carcavelos-s3", "carcavelos-s3-2") so a
    // test can fill and overflow rooms with a few tabs and never touches the real room of the class.
    // The old links ?room=noon and ?room=sunset still work: both open room 1, and they pick that light for
    // this visit (not saved), so the test tools and the sunset video links keep working.
    // Plain C#, tested (OnlineRoomsTests).
    public static class OnlineRooms
    {
        public const int MaxPlayers = 15;
        public const int MinSeats = 2;
        public const int MaxRooms = 4;
        // The session name of room 1 with 15 seats. New name on purpose: a page still on the old two-room build
        // ("carcavelos-noon" / "carcavelos-sunset") never lands in a room with this build's player data.
        public const string SessionName = "carcavelos";
        public const string Label = "CARCAVELOS ROOM";

        // ?room=... from the address. True for every word that means "the online room": carcavelos, noon, sunset,
        // room, class, 1 (any case). setsLight = the word also names a light (noon / sunset), given in light.
        public static bool TryParseRoom(string text, out bool setsLight, out TimeOfDay light)
        {
            int number;
            return TryParseRoom(text, out setsLight, out light, out number);
        }

        // The same, plus the room number: 1 for every word above, 2..MaxRooms for "2".."4".
        public static bool TryParseRoom(string text, out bool setsLight, out TimeOfDay light, out int number)
        {
            setsLight = false;
            light = TimeOfDay.Noon;
            number = 1;
            if (text == null)
            {
                return false;
            }
            string word = text.Trim().ToLowerInvariant();
            switch (word)
            {
                case "noon":
                    setsLight = true;
                    return true;
                case "sunset":
                    setsLight = true;
                    light = TimeOfDay.Sunset;
                    return true;
                case "carcavelos":
                case "room":
                case "class":
                case "1":
                    return true;
                default:
                    int parsed;
                    if (int.TryParse(word, out parsed) && parsed >= 2 && parsed <= MaxRooms)
                    {
                        number = parsed;
                        return true;
                    }
                    return false;
            }
        }

        // Seats from ?seats=: MinSeats..MaxPlayers, anything else = 0 (the flag is off, the normal 15).
        public static int ParseSeats(string text)
        {
            int seats;
            if (text == null || !int.TryParse(text.Trim(), out seats))
            {
                return 0;
            }
            return seats >= MinSeats && seats <= MaxPlayers ? seats : 0;
        }

        // The session name of room `number` (1..MaxRooms) with `seats` seats (0 or MaxPlayers = the real rooms).
        public static string SessionNameFor(int number, int seats)
        {
            int n = number < 1 ? 1 : (number > MaxRooms ? MaxRooms : number);
            string baseName = IsTestSeats(seats) ? SessionName + "-s" + seats : SessionName;
            return n == 1 ? baseName : baseName + "-" + n;
        }

        // How many seats a session name has: the test rooms say it in their name, every other room has 15.
        public static int SeatsOf(string sessionName)
        {
            string prefix = SessionName + "-s";
            if (sessionName == null || !sessionName.StartsWith(prefix))
            {
                return MaxPlayers;
            }
            string rest = sessionName.Substring(prefix.Length);
            int dash = rest.IndexOf('-');
            if (dash >= 0)
            {
                rest = rest.Substring(0, dash);
            }
            int seats = ParseSeats(rest);
            return seats > 0 ? seats : MaxPlayers;
        }

        // The room after a full one, or 0 when that was the last room.
        public static int NextRoom(int number)
        {
            return number >= 1 && number < MaxRooms ? number + 1 : 0;
        }

        // "CARCAVELOS ROOM" for room 1, "CARCAVELOS ROOM 2" for the overflow rooms.
        public static string LabelFor(int number)
        {
            return number <= 1 ? Label : Label + " " + number;
        }

        // May one more player come in? (players = how many are in the room now, including nobody new yet)
        public static bool HasSpace(int players)
        {
            return HasSpace(players, MaxPlayers);
        }

        public static bool HasSpace(int players, int seats)
        {
            return players < seats;
        }

        // "7/15" for the lobby.
        public static string CountLine(int players)
        {
            return CountLine(players, MaxPlayers);
        }

        public static string CountLine(int players, int seats)
        {
            int shown = players < 0 ? 0 : players;
            return shown + "/" + seats;
        }

        // The line while the lobby goes on from a full room to the next one.
        public static string OverflowLine(int fullRoom, int nextRoom)
        {
            return "ROOM " + fullRoom + " IS FULL, JOINING ROOM " + nextRoom + "...";
        }

        // The message when the room is full. A race takes a few minutes and players leave between races, so the
        // honest advice is to try again once the current race is over.
        public static string FullMessage()
        {
            return "THE ROOM IS FULL (" + MaxPlayers + "/" + MaxPlayers + "), TRY AGAIN AFTER THIS RACE";
        }

        // Every room (1..MaxRooms) was full.
        public static string AllFullMessage()
        {
            return "ALL " + MaxRooms + " ROOMS ARE FULL, TRY AGAIN AFTER THIS RACE";
        }

        private static bool IsTestSeats(int seats)
        {
            return seats >= MinSeats && seats < MaxPlayers;
        }
    }
}
