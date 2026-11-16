namespace CarcavelosCircuit.Online
{
    // How a player stands in the online results (FinishOrderRules).
    public enum FinishState
    {
        Finished,  // reported a lap time for this race
        Racing,    // still on the road (shown as "RACING...")
        Dnf        // was in the roster but left the room before finishing
    }
}
