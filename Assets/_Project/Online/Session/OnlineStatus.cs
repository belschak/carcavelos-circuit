namespace CarcavelosCircuit.Online
{
    // Where the connection to an online room stands (IOnlineSession.Status).
    public enum OnlineStatus
    {
        Idle,        // not connected, nothing asked yet (or after Leave)
        Connecting,  // Join was called, waiting for the room
        InRoom,      // in the room: players, race control and poses flow
        Failed,      // could not connect or the connection broke (FailReason says why)
        Full         // the room already has OnlineRooms.MaxPlayers players
    }
}
