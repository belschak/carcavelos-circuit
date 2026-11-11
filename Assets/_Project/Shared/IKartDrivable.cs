namespace CarcavelosCircuit.Shared
{
    // Round 2 contract (optional second interface on the kart, like IKartBoost): lets a bot drive the same kart
    // the player drives. Role 1's KartController implements it (task D9).
    // InputSource = null (the default): the kart reads the keyboard and the phone buttons as today.
    // InputSource set: the kart reads InputSource.ReadInput() instead, clamped with KartInput.Clamped().
    // InputEnabled (IKartState) still wins: when RaceFlow switches input off, a bot cannot drive either.
    public interface IKartDrivable
    {
        IKartInputSource InputSource { get; set; }
    }
}
