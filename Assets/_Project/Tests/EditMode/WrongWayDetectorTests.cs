using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: "WRONG WAY" after about 1 s of driving away from the next checkpoint, gone after about 0.4 s
    // of driving towards it again (two thresholds, so it does not flicker in a corner).
    public class WrongWayDetectorTests
    {
        private static readonly Vector3 Forward = new Vector3(0f, 0f, 1f);
        private static readonly Vector3 Ahead = new Vector3(0f, 0f, 50f);   // next checkpoint in front of the kart
        private static readonly Vector3 Behind = new Vector3(0f, 0f, -50f); // next checkpoint behind the kart
        private static readonly Vector3 Side = new Vector3(50f, 0f, 0f);    // next checkpoint at a right angle

        private static WrongWayDetector NewDetector()
        {
            return new WrongWayDetector(1f, 0.4f, 2f);
        }

        [Test]
        public void DrivingTowardsTheCheckpoint_IsNotWrongWay()
        {
            WrongWayDetector detector = NewDetector();
            for (int i = 0; i < 10; i++)
            {
                Assert.IsFalse(detector.Update(Forward, 10f, Ahead, 0.5f));
            }
        }

        [Test]
        public void DrivingAway_ForOneSecond_IsWrongWay()
        {
            WrongWayDetector detector = NewDetector();
            Assert.IsFalse(detector.Update(Forward, 10f, Behind, 0.5f));
            Assert.IsTrue(detector.Update(Forward, 10f, Behind, 0.5f));
        }

        [Test]
        public void TurningAround_ClearsTheWarning_AfterTheExitTime()
        {
            WrongWayDetector detector = NewDetector();
            detector.Update(Forward, 10f, Behind, 1f);
            Assert.IsTrue(detector.IsWrongWay);
            Assert.IsTrue(detector.Update(Forward, 10f, Ahead, 0.25f));  // not yet: 0.25 s < 0.4 s
            Assert.IsFalse(detector.Update(Forward, 10f, Ahead, 0.25f)); // 0.5 s of right driving
        }

        [Test]
        public void DrivingSideways_NeverTriggers()
        {
            // A corner: the checkpoint is at a right angle for a while. That is inside the hysteresis band.
            WrongWayDetector detector = NewDetector();
            for (int i = 0; i < 10; i++)
            {
                Assert.IsFalse(detector.Update(Forward, 10f, Side, 0.5f));
            }
        }

        [Test]
        public void StandingStill_KeepsTheCurrentAnswer()
        {
            WrongWayDetector detector = NewDetector();
            Assert.IsFalse(detector.Update(Forward, 1f, Behind, 5f)); // too slow to start the warning
            detector.Update(Forward, 10f, Behind, 1f);
            Assert.IsTrue(detector.Update(Forward, 0f, Ahead, 5f));    // stopped: the warning stays
        }

        [Test]
        public void ReversingTowardsTheCheckpoint_IsNotWrongWay()
        {
            // The kart faces away from the checkpoint but rolls backwards towards it.
            WrongWayDetector detector = NewDetector();
            Assert.IsFalse(detector.Update(Forward, -5f, Behind, 2f));
        }

        [Test]
        public void HeightDifference_IsIgnored()
        {
            // A checkpoint high up a ramp behind the kart is still behind it.
            WrongWayDetector detector = NewDetector();
            Assert.IsTrue(detector.Update(Forward, 10f, new Vector3(0f, 80f, -50f), 1f));
        }

        [Test]
        public void Reset_ClearsTheWarning()
        {
            WrongWayDetector detector = NewDetector();
            detector.Update(Forward, 10f, Behind, 1f);
            detector.Reset();
            Assert.IsFalse(detector.IsWrongWay);
            Assert.IsFalse(detector.Update(Forward, 10f, Behind, 0.5f));
        }
    }
}
