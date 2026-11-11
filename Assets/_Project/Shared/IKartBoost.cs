namespace CarcavelosCircuit.Shared
{
    // Optional second contract for the kart (item boxes, docs/interfaces.md, decision 2026-11-10).
    // A kart that implements it can be given a boost from outside: RaceFlow's items call it when the player uses
    // a speed item. A kart without it still races, the items then give no speed (RaceFlow logs that once).
    // Separate from IKartState on purpose: adding a member to IKartState would break every kart until all of them
    // implement it, a second interface can be added by each kart when its owner is ready.
    public interface IKartBoost
    {
        // Adds seconds to the running boost (boosts chain). While the boost runs the kart's top speed is
        // MaxSpeed * speedMultiplier, or the kart's own drift boost multiplier if that is higher.
        void AddBoost(float seconds, float speedMultiplier);
    }
}
