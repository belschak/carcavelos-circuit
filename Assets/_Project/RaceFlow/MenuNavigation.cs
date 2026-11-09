namespace CarcavelosCircuit.RaceFlow
{
    // Moving through menu rows and credit pages with the keys (task P7). Plain C#, unit tested.
    public static class MenuNavigation
    {
        // One step up or down a list that wraps around: down on the last row goes to the first one.
        public static int Wrap(int index, int delta, int count)
        {
            if (count <= 0)
            {
                return 0;
            }
            int next = (index + delta) % count;
            return next < 0 ? next + count : next; // C# % keeps the sign, so -1 % 5 is -1, not 4
        }
    }
}
