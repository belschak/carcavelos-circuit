using CarcavelosCircuit.Audio;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (audit 11): one take-off whoosh per real jump.
    public class TakeOffDetectorTests
    {
        private const float Frame = 0.05f;

        private static TakeOffDetector Grounded()
        {
            TakeOffDetector takeOff = new TakeOffDetector(0.15f);
            takeOff.Update(true, true, Frame);
            return takeOff;
        }

        [Test]
        public void FiresOnce_AfterEnoughAirTime()
        {
            TakeOffDetector takeOff = Grounded();
            Assert.IsFalse(takeOff.Update(false, true, Frame)); // 0.05 s
            Assert.IsFalse(takeOff.Update(false, true, Frame)); // 0.10 s
            Assert.IsTrue(takeOff.Update(false, true, 0.06f));  // 0.16 s
            Assert.IsFalse(takeOff.Update(false, true, Frame), "one whoosh per jump");
        }

        [Test]
        public void ShortHop_IsSilent()
        {
            TakeOffDetector takeOff = Grounded();
            takeOff.Update(false, true, Frame);
            takeOff.Update(false, true, Frame);
            Assert.IsFalse(takeOff.Update(true, true, Frame));
            Assert.IsFalse(takeOff.Update(false, true, Frame), "the air time starts again from 0");
        }

        [Test]
        public void NextJump_FiresAgain()
        {
            TakeOffDetector takeOff = Grounded();
            takeOff.Update(false, true, 0.2f);
            takeOff.Update(true, true, Frame);
            Assert.IsTrue(takeOff.Update(false, true, 0.2f));
        }

        [Test]
        public void WithoutInput_IsSilent()
        {
            TakeOffDetector takeOff = Grounded();
            Assert.IsFalse(takeOff.Update(false, false, 0.5f), "countdown or results");
        }

        [Test]
        public void AfterResetOrAtStart_WaitsForTheGround()
        {
            TakeOffDetector takeOff = new TakeOffDetector(0.15f);
            Assert.IsFalse(takeOff.Update(false, true, 0.5f), "spawned in the air: not a jump");
            takeOff.Update(true, true, Frame);
            takeOff.Reset(); // teleport onto a respawn point above the road
            Assert.IsFalse(takeOff.Update(false, true, 0.5f));
            takeOff.Update(true, true, Frame);
            Assert.IsTrue(takeOff.Update(false, true, 0.5f), "armed again after touching the ground");
        }
    }
}
