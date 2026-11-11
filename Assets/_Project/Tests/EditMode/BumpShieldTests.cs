using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R7): the Dean's List shield. A bump = a big, sudden speed drop; braking is not a bump.
    public class BumpShieldTests
    {
        private const float MinDrop = 4f;
        private const float Share = 0.3f;
        private const float Frame = 1f / 60f;

        [TestCase(20f, 5f, true)]    // wall: 20 -> 5 m/s in one frame
        [TestCase(20f, 13.5f, true)] // 6.5 m/s and 32.5 %: just over both limits
        [TestCase(20f, 15f, false)]  // 5 m/s but only 25 %
        [TestCase(10f, 6.5f, false)] // 35 % but only 3.5 m/s
        [TestCase(20f, 19.5f, false)] // hard braking: half a metre per second per frame
        [TestCase(20f, 25f, false)]  // speeding up
        [TestCase(-1f, 0f, false)]   // no frame before (just switched on or teleported)
        public void IsBump(float before, float now, bool expected)
        {
            Assert.AreEqual(expected, BumpShield.IsBump(before, now, MinDrop, Share));
        }

        [Test]
        public void ABump_IsBlockedOnce_ThenTheShieldIsGone()
        {
            BumpShield shield = new BumpShield();
            shield.Activate(8f);
            Assert.AreEqual(BumpShield.Result.Nothing, shield.Tick(Frame, 22f, MinDrop, Share));
            Assert.AreEqual(BumpShield.Result.Blocked, shield.Tick(Frame, 6f, MinDrop, Share));
            Assert.IsFalse(shield.IsActive);
            Assert.AreEqual(BumpShield.Result.Nothing, shield.Tick(Frame, 0f, MinDrop, Share)); // no second block
        }

        [Test]
        public void NoBump_RunsOut_AfterItsTime()
        {
            BumpShield shield = new BumpShield();
            shield.Activate(1f);
            BumpShield.Result last = BumpShield.Result.Nothing;
            for (int i = 0; i < 59; i++)
            {
                last = shield.Tick(Frame, 20f, MinDrop, Share);
                Assert.AreEqual(BumpShield.Result.Nothing, last, "frame " + i);
            }
            last = shield.Tick(Frame * 2f, 20f, MinDrop, Share);
            Assert.AreEqual(BumpShield.Result.Expired, last);
            Assert.IsFalse(shield.IsActive);
        }

        [Test]
        public void FirstFrame_AfterSwitchingOn_IsNeverABump()
        {
            // The shield has no speed to compare with yet, even if the kart is slow right then.
            BumpShield shield = new BumpShield();
            shield.Activate(8f);
            Assert.AreEqual(BumpShield.Result.Nothing, shield.Tick(Frame, 0f, MinDrop, Share));
        }

        [Test]
        public void Teleport_ToSpeedZero_IsNotABump()
        {
            BumpShield shield = new BumpShield();
            shield.Activate(8f);
            shield.Tick(Frame, 25f, MinDrop, Share);
            shield.NoteTeleport(); // recover R: the kart stands still at the last checkpoint
            Assert.AreEqual(BumpShield.Result.Nothing, shield.Tick(Frame, 0f, MinDrop, Share));
            Assert.IsTrue(shield.IsActive);
        }

        [Test]
        public void Clear_SwitchesItOff()
        {
            BumpShield shield = new BumpShield();
            shield.Activate(8f);
            shield.Clear();
            Assert.IsFalse(shield.IsActive);
            Assert.AreEqual(BumpShield.Result.Nothing, shield.Tick(Frame, 0f, MinDrop, Share));
        }
    }
}
