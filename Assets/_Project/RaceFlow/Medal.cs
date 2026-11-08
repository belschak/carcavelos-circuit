namespace CarcavelosCircuit.RaceFlow
{
    // The medal a lap earns, from worst to best. The numbers matter: a bigger number is a better medal,
    // and the number is what PersonalBest saves.
    public enum Medal
    {
        None = 0,       // slower than the bronze time (or no lap yet)
        Bronze = 1,
        Silver = 2,
        Gold = 3,
        DeansList = 4   // top tier, campus flavour: "made the Dean's List"
    }
}
