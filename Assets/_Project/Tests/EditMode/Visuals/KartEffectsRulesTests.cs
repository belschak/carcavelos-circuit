using CarcavelosCircuit.Visuals;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Tests for the kart effect rules (V5): when smoke, sparks, bursts and boost flames show, and in which colour.
    // Update(isDrifting, isGrounded, driftStage, isBoosting) is one frame of IKartState values.
    public class KartEffectsRulesTests
    {
        [Test]
        public void DrivingStraight_ShowsNothing()
        {
            KartEffectsRules rules = new KartEffectsRules();
            rules.Update(false, true, 0, false);
            Assert.IsFalse(rules.ShowSmoke);
            Assert.AreEqual(0, rules.SparkStage);
            Assert.IsFalse(rules.ShowFlame);
            Assert.IsFalse(rules.Ignition);
        }

        [Test]
        public void DriftWithoutStage_SmokeButNoSparks()
        {
            KartEffectsRules rules = new KartEffectsRules();
            rules.Update(true, true, 0, false);
            Assert.IsTrue(rules.ShowSmoke);
            Assert.AreEqual(0, rules.SparkStage);
            Assert.IsFalse(rules.StageUp);
        }

        [Test]
        public void EachNewStage_GivesOneBurst()
        {
            KartEffectsRules rules = new KartEffectsRules();
            rules.Update(true, true, 0, false);
            rules.Update(true, true, 1, false);
            Assert.AreEqual(1, rules.SparkStage, "stage 1 = blue sparks");
            Assert.IsTrue(rules.StageUp, "burst when blue starts");
            rules.Update(true, true, 1, false);
            Assert.IsFalse(rules.StageUp, "only one burst per stage");
            rules.Update(true, true, 2, false);
            Assert.AreEqual(2, rules.SparkStage, "stage 2 = orange sparks");
            Assert.IsTrue(rules.StageUp, "burst when orange starts");
        }

        [Test]
        public void InTheAir_NoSmokeNoSparksNoBurst()
        {
            KartEffectsRules rules = new KartEffectsRules();
            rules.Update(true, false, 1, false);
            Assert.IsFalse(rules.ShowSmoke);
            Assert.AreEqual(0, rules.SparkStage);
            Assert.IsFalse(rules.StageUp);
        }

        [Test]
        public void ReleaseAtOrange_IgnitesOnceWithOrangeFlame()
        {
            KartEffectsRules rules = new KartEffectsRules();
            rules.Update(true, true, 2, false);
            rules.Update(false, true, 0, true);           // released: the drift ends, the boost starts
            Assert.IsTrue(rules.ShowFlame);
            Assert.IsTrue(rules.Ignition);
            Assert.AreEqual(2, rules.FlameStage);
            Assert.IsFalse(rules.ShowSmoke);
            rules.Update(false, true, 0, true);
            Assert.IsTrue(rules.ShowFlame, "the flame stays while the boost runs");
            Assert.IsFalse(rules.Ignition, "only one pop per boost");
            rules.Update(false, true, 0, false);
            Assert.IsFalse(rules.ShowFlame);
        }

        [Test]
        public void ReleaseAtBlue_BlueFlame()
        {
            KartEffectsRules rules = new KartEffectsRules();
            rules.Update(true, true, 1, false);
            rules.Update(false, true, 0, true);
            Assert.AreEqual(1, rules.FlameStage);
        }

        [Test]
        public void CancelledDrift_NoFlame()
        {
            KartEffectsRules rules = new KartEffectsRules();
            rules.Update(true, true, 2, false);
            rules.Update(false, true, 0, false);          // hit a wall: the drift ends without a boost
            Assert.IsFalse(rules.ShowFlame);
            Assert.IsFalse(rules.Ignition);
        }

        [Test]
        public void DriftReleasedDuringABoost_PopsAgainWithTheNewColour()
        {
            KartEffectsRules rules = new KartEffectsRules();
            rules.Update(true, true, 2, false);
            rules.Update(false, true, 0, true);           // orange boost
            rules.Update(true, true, 1, true);            // next drift while still boosting
            Assert.IsFalse(rules.Ignition);
            rules.Update(false, true, 0, true);           // released at blue, the boost goes on
            Assert.IsTrue(rules.Ignition);
            Assert.AreEqual(1, rules.FlameStage);
        }

        [Test]
        public void Reset_ForgetsTheLastDrift()
        {
            KartEffectsRules rules = new KartEffectsRules();
            rules.Update(true, true, 2, false);
            rules.Reset();
            Assert.IsFalse(rules.ShowSmoke);
            Assert.AreEqual(0, rules.SparkStage);
            rules.Update(false, true, 0, true);           // a boost right after a reset has no drift colour
            Assert.AreEqual(0, rules.FlameStage);
        }

        [Test]
        public void StageAboveTwo_CountsAsOrange()
        {
            KartEffectsRules rules = new KartEffectsRules();
            rules.Update(true, true, 5, false);
            Assert.AreEqual(2, rules.SparkStage);
        }
    }
}
