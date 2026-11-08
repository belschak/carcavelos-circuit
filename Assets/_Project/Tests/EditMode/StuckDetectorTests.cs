using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: "Stuck? Press R" after a few seconds standing still, but only after the kart really drove.
    public class StuckDetectorTests
    {
        private static StuckDetector NewDetector()
        {
            return new StuckDetector(3f, 1.5f, 3f); // 3 s still, below 1.5 m/s = still, armed from 3 m/s
        }

        [Test]
        public void WaitingAtTheStart_IsNeverStuck()
        {
            StuckDetector detector = NewDetector();
            for (int i = 0; i < 10; i++)
            {
                Assert.IsFalse(detector.Update(0f, 1f));
            }
        }

        [Test]
        public void AfterDriving_ThreeSecondsStill_IsStuck()
        {
            StuckDetector detector = NewDetector();
            detector.Update(8f, 0.1f);
            Assert.IsFalse(detector.Update(0f, 1f));
            Assert.IsFalse(detector.Update(0f, 1f));
            Assert.IsTrue(detector.Update(0f, 1f));
            Assert.IsTrue(detector.IsStuck);
        }

        [Test]
        public void GrindingAlongAWall_SlowerThanStillSpeed_CountsAsStill()
        {
            StuckDetector detector = NewDetector();
            detector.Update(8f, 0.1f);
            detector.Update(1f, 1.5f);
            Assert.IsTrue(detector.Update(-1f, 1.5f)); // creeping backwards also counts as still
        }

        [Test]
        public void DrivingAgain_ClearsTheStillTime()
        {
            StuckDetector detector = NewDetector();
            detector.Update(8f, 0.1f);
            detector.Update(0f, 2f);
            detector.Update(5f, 0.1f); // moving again
            Assert.IsFalse(detector.Update(0f, 2f));
            Assert.IsTrue(detector.Update(0f, 1f));
        }

        [Test]
        public void ReversingFast_AlsoArmsTheDetector()
        {
            StuckDetector detector = NewDetector();
            detector.Update(-5f, 0.1f);
            Assert.IsTrue(detector.Update(0f, 3f));
        }

        [Test]
        public void Reset_DisarmsUntilTheKartDrivesAgain()
        {
            StuckDetector detector = NewDetector();
            detector.Update(8f, 0.1f);
            detector.Update(0f, 3f);
            Assert.IsTrue(detector.IsStuck);
            detector.Reset(); // e.g. after the player pressed R
            Assert.IsFalse(detector.IsStuck);
            Assert.IsFalse(detector.Update(0f, 5f));
        }
    }
}
