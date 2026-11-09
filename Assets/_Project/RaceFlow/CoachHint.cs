namespace CarcavelosCircuit.RaceFlow
{
    // The one-time tips OnboardingCoach can give. The numbers are bit flags, so all "already shown" tips
    // fit into one int in PlayerPrefs (1 + 4 = 5 means tips HowToDrift and BigBoost are done).
    public enum CoachHint
    {
        None = 0,
        HowToDrift = 1,   // racing for a while and never drifted
        HoldLonger = 2,   // let go of a drift before the first sparks (no boost)
        BigBoost = 4      // first boost after a stage 2 (orange) drift: praise
    }
}
