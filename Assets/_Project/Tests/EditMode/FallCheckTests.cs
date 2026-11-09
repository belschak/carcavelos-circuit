using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: automatic recover after a fall, with a short pause after every recover.
    public class FallCheckTests
    {
        [Test]
        public void BelowTheFallHeight_HasFallen()
        {
            FallCheck check = new FallCheck(-20f, 0.5f);
            Assert.IsTrue(check.HasFallen(-25f, 3f));
            Assert.IsFalse(check.HasFallen(0.5f, 3f));
        }

        [Test]
        public void RightAfterARecover_TheCheckWaits()
        {
            // The kart may still be below the track for a few frames until the physics step moves it.
            FallCheck check = new FallCheck(-20f, 0.5f);
            check.NoteRecovered(10f);
            Assert.IsFalse(check.HasFallen(-25f, 10.02f));
            Assert.IsFalse(check.HasFallen(-25f, 10.49f));
        }

        [Test]
        public void AfterThePause_ARealSecondFallCounts()
        {
            FallCheck check = new FallCheck(-20f, 0.5f);
            check.NoteRecovered(10f);
            Assert.IsTrue(check.HasFallen(-25f, 10.5f));
        }

        [Test]
        public void AfterThePause_AKartOnTheTrackHasNotFallen()
        {
            FallCheck check = new FallCheck(-20f, 0.5f);
            check.NoteRecovered(10f);
            Assert.IsFalse(check.HasFallen(0.5f, 11f));
        }
    }
}
