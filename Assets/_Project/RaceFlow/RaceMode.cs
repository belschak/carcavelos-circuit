namespace CarcavelosCircuit.RaceFlow
{
    // The three modes on the start screen (Luc, 16 Nov 2026). The numbers are saved in PlayerPrefs, so never reorder them.
    public enum RaceMode
    {
        RaceVsBots = 0, // the five bots race with you, the finish shows your place
        TimeAttack = 1, // alone against the clock (and the ghost), the finish shows your lap time
        OnlineRace = 2  // people race live in an online room (M2); never saved as the last mode (RaceModeRules.IsRemembered)
    }
}
