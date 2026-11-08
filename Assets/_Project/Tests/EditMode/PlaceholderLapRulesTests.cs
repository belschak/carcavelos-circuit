using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests for the stand-in lap rules. Noah's real RaceRules (T5) gets its own tests in Role 2.
    public class PlaceholderLapRulesTests
    {
        [Test]
        public void InOrder_ThenFinish_Works()
        {
            PlaceholderLapRules rules = new PlaceholderLapRules(4); // 0 = finish, 1-3 on the lap
            rules.StartLap();
            Assert.IsTrue(rules.PassCheckpoint(1));
            Assert.IsTrue(rules.PassCheckpoint(2));
            Assert.IsTrue(rules.PassCheckpoint(3));
            Assert.IsTrue(rules.TryFinish());
        }

        [Test]
        public void SkippedCheckpoint_CannotFinish()
        {
            PlaceholderLapRules rules = new PlaceholderLapRules(4);
            rules.StartLap();
            rules.PassCheckpoint(1);
            Assert.IsFalse(rules.PassCheckpoint(3), "3 before 2 does not count");
            Assert.IsFalse(rules.TryFinish());
        }

        [Test]
        public void SameCheckpointTwice_CountsOnce()
        {
            PlaceholderLapRules rules = new PlaceholderLapRules(4);
            rules.StartLap();
            Assert.IsTrue(rules.PassCheckpoint(1));
            Assert.IsFalse(rules.PassCheckpoint(1));
        }

        [Test]
        public void NoDoubleFinish()
        {
            PlaceholderLapRules rules = new PlaceholderLapRules(2);
            rules.StartLap();
            rules.PassCheckpoint(1);
            Assert.IsTrue(rules.TryFinish());
            Assert.IsFalse(rules.TryFinish());
        }

        [Test]
        public void LastValidCheckpoint_ForRecovery()
        {
            PlaceholderLapRules rules = new PlaceholderLapRules(4);
            rules.StartLap();
            Assert.AreEqual(0, rules.LastValidCheckpoint, "before any checkpoint we go back to the start");
            rules.PassCheckpoint(1);
            rules.PassCheckpoint(3); // wrong, ignored
            Assert.AreEqual(1, rules.LastValidCheckpoint);
        }

        [Test]
        public void Reset_StartsOver()
        {
            PlaceholderLapRules rules = new PlaceholderLapRules(3);
            rules.StartLap();
            rules.PassCheckpoint(1);
            rules.Reset();
            Assert.AreEqual(0, rules.LastValidCheckpoint);
            Assert.IsTrue(rules.PassCheckpoint(1));
        }
    }
}
