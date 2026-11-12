using System.Collections.Generic;

namespace CarcavelosCircuit.Bots
{
    // LOCAL PROTOTYPE (bots). A short list of things that happened in the race (respawns, karts leaving the ground
    // after a bump). Only the automatic test reads it, to print it in its report.
    public static class BotRaceLog
    {
        public static readonly List<string> Events = new List<string>();

        public static void Add(string text)
        {
            if (Events.Count < 5000) // never grow without end in a long play session
            {
                Events.Add(text);
            }
        }
    }
}
