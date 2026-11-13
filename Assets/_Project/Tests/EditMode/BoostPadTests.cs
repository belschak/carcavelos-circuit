using System.Collections.Generic;
using CarcavelosCircuit.Visuals;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 3 tests (task V11): where the boost pads go and when a pad fires.
    public class BoostPadTests
    {
        [Test]
        public void JumpPads_OneBeforeTheJump_OneRightBeforeTheEdge()
        {
            List<float> pads = BoostPadPlan.JumpPads(162f);
            CollectionAssert.AreEqual(new[] { 137f, 156f }, pads);
        }

        [Test]
        public void TunnelPads_84MetreTunnel_GetsThreePads()
        {
            // The campus world's tunnel: s = 818 .. 902.
            List<float> pads = BoostPadPlan.TunnelPads(818f, 902f);
            CollectionAssert.AreEqual(new[] { 832f, 860f, 888f }, pads);
        }

        [Test]
        public void TunnelPads_NoPadInTheLastMetres()
        {
            List<float> pads = BoostPadPlan.TunnelPads(0f, 50f);
            CollectionAssert.AreEqual(new[] { 14f }, pads); // 42 would be only 8 m before the exit
        }

        [Test]
        public void TunnelPads_ShortCoveredBit_GetsNone()
        {
            Assert.AreEqual(0, BoostPadPlan.TunnelPads(100f, 120f).Count);
        }

        [Test]
        public void CoveredRuns_FindsEveryRun_AlsoAtTheEnds()
        {
            bool[] covered = { true, false, false, true, true, false, true };
            List<int[]> runs = BoostPadPlan.CoveredRuns(covered);
            Assert.AreEqual(3, runs.Count);
            CollectionAssert.AreEqual(new[] { 0, 0 }, runs[0]);
            CollectionAssert.AreEqual(new[] { 3, 4 }, runs[1]);
            CollectionAssert.AreEqual(new[] { 6, 6 }, runs[2]);
        }

        [Test]
        public void ClosestPoint_PicksTheNearestDistance_AndWrapsAroundTheLap()
        {
            float[] s = { 0f, 2f, 4f, 6f, 8f };
            Assert.AreEqual(2, BoostPadPlan.ClosestPoint(s, 10f, 4.4f));
            Assert.AreEqual(4, BoostPadPlan.ClosestPoint(s, 10f, -2f));  // 2 m before the start line = s 8
            Assert.AreEqual(1, BoostPadPlan.ClosestPoint(s, 10f, 12f));  // one lap on
        }

        [Test]
        public void Rule_FirstPass_Boosts_BounceRightAfter_DoesNot()
        {
            BoostPadRule rule = new BoostPadRule(0.5f);
            Assert.IsTrue(rule.TryBoost(1, 10f));
            Assert.IsFalse(rule.TryBoost(1, 10.2f));
        }

        [Test]
        public void Rule_AfterTheCooldown_BoostsAgain()
        {
            BoostPadRule rule = new BoostPadRule(0.5f);
            rule.TryBoost(1, 10f);
            Assert.IsTrue(rule.TryBoost(1, 10.6f));
        }

        [Test]
        public void Rule_EveryKartHasItsOwnCooldown()
        {
            BoostPadRule rule = new BoostPadRule(0.5f);
            Assert.IsTrue(rule.TryBoost(1, 10f));
            Assert.IsTrue(rule.TryBoost(2, 10.1f)); // a bot right behind the player still gets its boost
        }

        [Test]
        public void JumpPads_AreStrongerThanTheDriftBoost_TunnelPadsAreNot()
        {
            const float driftBoost = 1.35f; // KartTuning.boostSpeedMultiplier: the faster boost wins, so less adds nothing
            Assert.Greater(BoostPadPlan.JumpSpeedMultiplier, driftBoost);
            Assert.Greater(BoostPadPlan.JumpBoostSeconds, BoostPadPlan.TunnelBoostSeconds);
            Assert.AreEqual(driftBoost, BoostPadPlan.TunnelSpeedMultiplier, 0.0001f);
        }

        [Test]
        public void JumpPads_BoostLastsPastTheEdge()
        {
            // At the boosted top speed the kart covers the 6 m from the second pad to the edge well within its boost.
            float boostedSpeed = 25f * BoostPadPlan.JumpSpeedMultiplier;
            Assert.Less(BoostPadPlan.OnRampMetres / boostedSpeed, BoostPadPlan.JumpBoostSeconds);
        }
    }
}
