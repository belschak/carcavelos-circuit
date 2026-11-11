namespace CarcavelosCircuit.Shared
{
    // Round 2 contract: something that drives a kart instead of the keyboard, e.g. a bot (Role 3).
    // The kart asks it once per frame while InputEnabled is true.
    public interface IKartInputSource
    {
        KartInput ReadInput();
    }
}
