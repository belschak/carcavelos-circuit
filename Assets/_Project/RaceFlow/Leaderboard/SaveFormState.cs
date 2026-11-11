namespace CarcavelosCircuit.RaceFlow
{
    // The steps of the "save your time" form on the results card (task R10).
    public enum SaveFormState
    {
        Hidden,   // not on the results screen, or this lap cannot be saved (outside LapTimeRules)
        Editing,  // waiting for a name (and the SAVE click or Enter)
        Saving,   // sent, waiting for the answer
        Saved,    // done: shows the place ("YOU ARE #4")
    }
}
