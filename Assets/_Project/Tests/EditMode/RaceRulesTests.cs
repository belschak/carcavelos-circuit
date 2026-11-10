using CarcavelosCircuit.Track;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 2 tests for RaceRules (task T5): order, wrong way, duplicate finish, missed checkpoint.
    public class RaceRulesTests
    {
        // 4 checkpoints: 0 = start/finish line, 1..3 on the lap
        private static RaceRules NewLap()
        {
            RaceRules rules = new RaceRules(4);
            rules.StartLap();
            return rules;
        }

        [Test]
        public void InOrder_ThenFinish_Works()
        {
            RaceRules rules = NewLap();
            Assert.IsTrue(rules.PassCheckpoint(1));
            Assert.IsTrue(rules.PassCheckpoint(2));
            Assert.IsTrue(rules.PassCheckpoint(3));
            Assert.IsTrue(rules.TryFinish());
        }

        [Test]
        public void FinishRightAfterStart_IsRejected()
        {
            RaceRules rules = NewLap();
            Assert.IsFalse(rules.TryFinish(), "crossing the line without a lap does not count");
        }

        [Test]
        public void MissedCheckpoint_IsRejectedAndLapCannotFinish()
        {
            RaceRules rules = NewLap();
            Assert.IsTrue(rules.PassCheckpoint(1));
            Assert.IsFalse(rules.PassCheckpoint(3), "3 before 2 does not count");
            Assert.IsFalse(rules.TryFinish());
            Assert.AreEqual(1, rules.LastValidCheckpoint);
        }

        [Test]
        public void MissedCheckpoint_CanStillBeFixedByGoingBack()
        {
            RaceRules rules = NewLap();
            rules.PassCheckpoint(1);
            rules.PassCheckpoint(3); // ignored
            Assert.IsTrue(rules.PassCheckpoint(2));
            Assert.IsTrue(rules.PassCheckpoint(3));
            Assert.IsTrue(rules.TryFinish());
        }

        [Test]
        public void WrongDirection_BackwardsCheckpointsAreRejected()
        {
            RaceRules rules = NewLap();
            Assert.IsFalse(rules.PassCheckpoint(3), "the last checkpoint first = driving backwards");
            Assert.IsFalse(rules.PassCheckpoint(2));
            Assert.IsFalse(rules.TryFinish());
            Assert.AreEqual(0, rules.LastValidCheckpoint);
        }

        [Test]
        public void WrongDirection_GoingBackAfterProgress_DoesNotChangeProgress()
        {
            RaceRules rules = NewLap();
            rules.PassCheckpoint(1);
            rules.PassCheckpoint(2);
            Assert.IsFalse(rules.PassCheckpoint(1), "an already passed checkpoint does not count again");
            Assert.AreEqual(2, rules.LastValidCheckpoint);
            Assert.IsTrue(rules.PassCheckpoint(3));
        }

        [Test]
        public void SameCheckpointTwice_CountsOnce()
        {
            RaceRules rules = NewLap();
            Assert.IsTrue(rules.PassCheckpoint(1));
            Assert.IsFalse(rules.PassCheckpoint(1));
        }

        [Test]
        public void DuplicateFinish_CountsOnce()
        {
            RaceRules rules = new RaceRules(2);
            rules.StartLap();
            rules.PassCheckpoint(1);
            Assert.IsTrue(rules.TryFinish());
            Assert.IsFalse(rules.TryFinish(), "a second crossing of the line is ignored");
        }

        [Test]
        public void AfterFinish_NothingMoreCounts()
        {
            RaceRules rules = NewLap();
            rules.PassCheckpoint(1);
            rules.PassCheckpoint(2);
            rules.PassCheckpoint(3);
            rules.TryFinish();
            Assert.IsFalse(rules.PassCheckpoint(1));
            Assert.AreEqual(3, rules.LastValidCheckpoint);
        }

        [Test]
        public void InvalidIndexes_AreRejected()
        {
            RaceRules rules = NewLap();
            Assert.IsFalse(rules.PassCheckpoint(0), "0 is the finish line, use TryFinish");
            Assert.IsFalse(rules.PassCheckpoint(-1));
            Assert.IsFalse(rules.PassCheckpoint(4), "there is no checkpoint 4");
            Assert.AreEqual(0, rules.LastValidCheckpoint);
        }

        [Test]
        public void LastValidCheckpoint_ForRespawn()
        {
            RaceRules rules = NewLap();
            Assert.AreEqual(0, rules.LastValidCheckpoint, "before any checkpoint we go back to the start");
            rules.PassCheckpoint(1);
            Assert.AreEqual(1, rules.LastValidCheckpoint);
            rules.PassCheckpoint(2);
            Assert.AreEqual(2, rules.LastValidCheckpoint);
        }

        [Test]
        public void Reset_StartsOver_AlsoAfterFinish()
        {
            RaceRules rules = new RaceRules(2);
            rules.StartLap();
            rules.PassCheckpoint(1);
            rules.TryFinish();
            rules.Reset();
            Assert.AreEqual(0, rules.LastValidCheckpoint);
            Assert.IsTrue(rules.PassCheckpoint(1));
            Assert.IsTrue(rules.TryFinish());
        }

        [Test]
        public void TrackWithOnlyTheLine_FinishesImmediately()
        {
            RaceRules rules = new RaceRules(1);
            rules.StartLap();
            Assert.IsTrue(rules.TryFinish(), "no checkpoints to pass: the line alone ends the lap");
        }
    }
}
