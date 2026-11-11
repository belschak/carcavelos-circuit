namespace CarcavelosCircuit.RaceFlow
{
    // The items an item box can give (task R7). Luc's four item designs (Role 3, items_showcase.png, 2026-11-08),
    // ordered from the most common to the rarest:
    // Espresso         = a coffee kick: one short boost.
    // SeagullDelivery  = a courier gull drops lifebuoy rings on the road ahead; every ring you drive through boosts.
    // CarcavelosWave   = you surf a wave: one long, extra-fast boost, best saved for a long straight.
    // DeansList        = top of the class: a golden boost plus a shield that gives your speed back after one bump.
    public enum ItemKind
    {
        None = 0,
        Espresso = 1,
        SeagullDelivery = 2,
        CarcavelosWave = 3,
        DeansList = 4
    }
}
