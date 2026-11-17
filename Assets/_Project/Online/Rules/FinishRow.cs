namespace CarcavelosCircuit.Online
{
    // One line of the online results, in order (FinishOrderRules.Order).
    public struct FinishRow
    {
        public int Id;
        public string Name;
        public FinishState State;
        public double Seconds;     // only meaningful when Finished
        public int Place;          // 1, 2, 3 ... for finished players, 0 for the others
    }
}
