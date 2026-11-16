using CarcavelosCircuit.Shared;

namespace CarcavelosCircuit.Online
{
    // The two rooms of the online race (team decision, WhatsApp 16.11): NOON ROOM and SUNSET ROOM, 15 players each,
    // first come first served. The room also sets the light: everyone in the sunset room races at sunset.
    // Plain C#, tested (OnlineRoomsTests).
    public static class OnlineRooms
    {
        public const int MaxPlayers = 15;
        public const string NoonSession = "carcavelos-noon";
        public const string SunsetSession = "carcavelos-sunset";

        // The session name the transport uses for a room.
        public static string SessionName(TimeOfDay room)
        {
            return room == TimeOfDay.Sunset ? SunsetSession : NoonSession;
        }

        // The big title on the room card.
        public static string Label(TimeOfDay room)
        {
            return room == TimeOfDay.Sunset ? "SUNSET ROOM" : "NOON ROOM";
        }

        // Session name back to the room (= the light). False for any other name.
        public static bool TryRoomOfSession(string sessionName, out TimeOfDay room)
        {
            room = TimeOfDay.Noon;
            if (sessionName == NoonSession)
            {
                return true;
            }
            if (sessionName == SunsetSession)
            {
                room = TimeOfDay.Sunset;
                return true;
            }
            return false;
        }

        // "noon" or "sunset" (any case) from the URL flag ?room=. False for anything else.
        public static bool TryParseRoom(string text, out TimeOfDay room)
        {
            room = TimeOfDay.Noon;
            if (text == null)
            {
                return false;
            }
            string word = text.Trim().ToLowerInvariant();
            if (word == "noon")
            {
                return true;
            }
            if (word == "sunset")
            {
                room = TimeOfDay.Sunset;
                return true;
            }
            return false;
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

        // The message when the room is full.
        public static string FullMessage(TimeOfDay room)
        {
            return "THIS ROOM IS FULL (" + MaxPlayers + "/" + MaxPlayers + "), TRY THE " + (room == TimeOfDay.Sunset ? "NOON" : "SUNSET") + " ROOM";
        }
    }
}
