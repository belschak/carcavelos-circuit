namespace CarcavelosCircuit.Online
{
    // The phase of the room's race, set by the master (RaceControl.Phase). The numbers travel over the network,
    // so never reorder them.
    public enum OnlinePhase
    {
        Lobby = 0,      // waiting for the host to press START RACE
        Countdown = 1,  // StartAt is set, everyone in the roster counts down to the same GO
        Racing = 2,     // reserved for transports that want to mark it; clients work it out from StartAt
        Results = 3     // reserved, like Racing
    }
}
