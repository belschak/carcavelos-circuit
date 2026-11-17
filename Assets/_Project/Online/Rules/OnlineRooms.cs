using CarcavelosCircuit.Shared;

namespace CarcavelosCircuit.Online
{
    // The one online room of the class (team feedback round, 17 Nov 2026): everyone joins the same room "carcavelos",
    // 15 seats, first come first served. Two rooms (noon and sunset, 16 Nov) split a class of 15 into two half-empty
    // races, so there is one room now and the light (noon or sunset) is each player's own look again (T on the start
    // screen or in the lobby); it never travels over the network. Photon Cloud hosts the room, so one room or two
    // costs the same (the free plan counts players, 100 at once, not rooms).
    // The old links ?room=noon and ?room=sunset still work: both open the one room, and they pick that light for
    // this visit (not saved), so the test tools and the sunset video links keep working.
    // Plain C#, tested (OnlineRoomsTests).
    public static class OnlineRooms
    {
        public const int MaxPlayers = 15;
        // The session name every transport uses. New name on purpose: a page still on the old two-room build
        // ("carcavelos-noon" / "carcavelos-sunset") never lands in a room with this build's player data.
        public const string SessionName = "carcavelos";
        public const string Label = "CARCAVELOS ROOM";

        // ?room=... from the address. True for every word that means "the online room": carcavelos, noon, sunset,
        // room, class, 1 (any case). setsLight = the word also names a light (noon / sunset), given in light.
        public static bool TryParseRoom(string text, out bool setsLight, out TimeOfDay light)
        {
            setsLight = false;
            light = TimeOfDay.Noon;
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
                    return false;
            }
        }

        // May one more player come in? (players = how many are in the room now, including nobody new yet)
        public static bool HasSpace(int players)
        {
            return players < MaxPlayers;
        }

        // "7/15" for the lobby.
        public static string CountLine(int players)
        {
            int shown = players < 0 ? 0 : players;
            return shown + "/" + MaxPlayers;
        }

        // The message when the room is full. A race takes a few minutes and players leave between races, so the
        // honest advice is to try again once the current race is over.
        public static string FullMessage()
        {
            return "THE ROOM IS FULL (" + MaxPlayers + "/" + MaxPlayers + "), TRY AGAIN AFTER THIS RACE";
        }
    }
}
