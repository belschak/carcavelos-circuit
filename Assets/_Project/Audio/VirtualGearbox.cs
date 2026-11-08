namespace CarcavelosCircuit.Audio
{
    // Decides which pretend gear the engine sound is in, from the speed alone (plain C#, unit tested).
    // Shifts up just before a gear runs out of revs, and shifts down only clearly below the gear change point,
    // so the sound never flips between two gears when the speed hovers around a change point.
    public class VirtualGearbox
    {
        private readonly int gears;

        public int Gear { get; private set; } = 1;

        public VirtualGearbox(int gears)
        {
            this.gears = gears < 1 ? 1 : gears;
        }

        // Call once per frame. Returns true on the frame of an upshift (then the engine plays its shift pop).
        public bool Update(float speed01)
        {
            if (Gear < gears && speed01 > EnginePitch.GearTop(Gear, gears) * 0.985f)
            {
                Gear = Gear + 1;
                return true;
            }
            if (Gear > 1 && speed01 < EnginePitch.GearTop(Gear - 1, gears) * 0.9f)
            {
                Gear = Gear - 1; // downshifts are quiet, a real driver just lifts off
            }
            return false;
        }

        // Back to first gear (countdown, retry, results).
        public void Reset()
        {
            Gear = 1;
        }
    }
}
