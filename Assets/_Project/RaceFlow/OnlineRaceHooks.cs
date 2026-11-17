using System;

namespace CarcavelosCircuit.RaceFlow
{
    // The one door between the race and the online race (M2). RaceFlow must not reference the Online assembly
    // (Online references RaceFlow), so the Online assembly plugs itself in here at start-up and RaceFlow only calls back.
    // Nothing set here = no online build: the Online card says so and the game stays single player.
    public static class OnlineRaceHooks
    {
        // Set by the Online assembly. ModePicker calls it when the player picks "ONLINE RACE" on the start screen:
        // the online lobby opens instead of the local countdown.
        public static Action<RaceManager> OpenLobby;

        public static bool Available => OpenLobby != null;

        public const string UnavailableMessage = "ONLINE RACE NEEDS THE ONLINE BUILD";
    }
}
