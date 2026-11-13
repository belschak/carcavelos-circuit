using CarcavelosCircuit.Audio;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (P4): the pretend gearbox of the engine sound.
    public class VirtualGearboxTests
    {
        [Test]
        public void StartsInFirstGear()
        {
            Assert.AreEqual(1, new VirtualGearbox(4).Gear);
        }

        [Test]
        public void ShiftsUpJustBeforeTheGearRunsOutOfRevs()
        {
            VirtualGearbox gearbox = new VirtualGearbox(4);
            float top = EnginePitch.GearTop(1, 4);
            Assert.IsFalse(gearbox.Update(top * 0.9f)); // still room in first gear
            Assert.AreEqual(1, gearbox.Gear);
            Assert.IsTrue(gearbox.Update(top));          // upshift: true only on this frame
            Assert.AreEqual(2, gearbox.Gear);
            Assert.IsFalse(gearbox.Update(top));         // no second pop at the same speed
        }

        [Test]
        public void ClimbsThroughAllGearsAndNeverPastTheLast()
        {
            VirtualGearbox gearbox = new VirtualGearbox(4);
            for (int frame = 0; frame < 10; frame++)
            {
                gearbox.Update(1f);
            }
            Assert.AreEqual(4, gearbox.Gear);
        }

        [Test]
        public void DoesNotFlutterAroundAChangePoint()
        {
            // Speed wobbling just around the 1-2 change point must not shift down again (hysteresis).
            VirtualGearbox gearbox = new VirtualGearbox(4);
            float change = EnginePitch.GearTop(1, 4);
            gearbox.Update(change);
            gearbox.Update(change * 0.97f);
            gearbox.Update(change * 1.01f);
            gearbox.Update(change * 0.95f);
            Assert.AreEqual(2, gearbox.Gear);
        }

        [Test]
        public void ShiftsDownWhenClearlySlower()
        {
            VirtualGearbox gearbox = new VirtualGearbox(4);
            gearbox.Update(EnginePitch.GearTop(1, 4));
            Assert.AreEqual(2, gearbox.Gear);
            Assert.IsFalse(gearbox.Update(0.1f)); // downshift is not an upshift (no pop)
            Assert.AreEqual(1, gearbox.Gear);
        }

        [Test]
        public void ResetGoesBackToFirstGear()
        {
            VirtualGearbox gearbox = new VirtualGearbox(4);
            gearbox.Update(1f);
            gearbox.Update(1f);
            gearbox.Reset();
            Assert.AreEqual(1, gearbox.Gear);
        }
    }
}
