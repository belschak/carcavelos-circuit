namespace CarcavelosCircuit.Online
{
    // One roster player as the director feeds it into FinishOrderRules (from the session's Players and the roster).
    public struct FinishEntry
    {
        public int Id;
        public string Name;
        public bool Present;       // still in the room
        public bool HasFinished;   // reported a time for this race
        public double Seconds;

        public FinishEntry(int id, string name, bool present, bool hasFinished, double seconds)
        {
            Id = id;
            Name = name ?? "";
            Present = present;
            HasFinished = hasFinished;
            Seconds = seconds;
        }
    }
}
