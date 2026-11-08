namespace CarcavelosCircuit.RaceFlow
{
    // Checks the track's checkpoint numbers once when the race is set up, so a wrong track setup shows a clear
    // error instead of a lap that silently can never finish. Plain C#, so it can be tested.
    // The rule from docs/interfaces.md: the indices are exactly 0, 1, ..., N-1, each one once, and
    // index 0 is the start/finish line. The others are numbered 1, 2, 3 ... in driving order.
    public static class CheckpointValidation
    {
        // Returns "" when the indices are fine, otherwise a sentence that explains the first problem found.
        public static string FindProblem(int[] indices)
        {
            if (indices == null || indices.Length == 0)
            {
                return "the track has no checkpoints";
            }

            bool[] seen = new bool[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                int index = indices[i];
                if (index < 0 || index >= indices.Length)
                {
                    return "checkpoint index " + index + " is outside 0.." + (indices.Length - 1)
                        + " (index 0 = start/finish line, then 1, 2, 3 ... in driving order)";
                }
                if (seen[index])
                {
                    return "checkpoint index " + index + " is used twice";
                }
                seen[index] = true;
            }
            // N different numbers, all inside 0..N-1, means every number from 0 to N-1 is there exactly once.
            return "";
        }
    }
}
