namespace CarcavelosCircuit.Online
{
    // Which other players' karts are drawn on my screen (plain C#, tested in OnlineProxyRulesTests). A shown proxy
    // is also in RacerRegistry, so it counts in the positions readout ("OF 4"), the minimap and the name tags.
    // - No pose yet: hidden, or it would stand at the world origin and count as a racer (review F7).
    // - Between races (lobby, RaceId 0): everyone in the room is shown on the grid.
    // - A race is on: only the players in its roster. A late joiner watches parked on the grid and is not a racer
    //   (review F6); the next race takes them in.
    public static class OnlineProxyRules
    {
        public static bool Shows(bool hasPose, RaceControl control, int playerId)
        {
            if (!hasPose)
            {
                return false;
            }
            if (control == null || control.RaceId == 0)
            {
                return true;
            }
            return control.Contains(playerId);
        }
    }
}
