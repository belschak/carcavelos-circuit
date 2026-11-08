namespace CarcavelosCircuit.RaceFlow
{
    // The four phases of one race attempt, in the order they happen.
    public enum RaceState
    {
        Ready,      // "Press any key to start" screen, kart cannot drive
        Countdown,  // 3, 2, 1, GO: kart still locked
        Racing,     // timer runs, kart can drive
        Results     // lap finished: final time on screen, kart locked again
    }
}
