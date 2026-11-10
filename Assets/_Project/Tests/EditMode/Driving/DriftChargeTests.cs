using CarcavelosCircuit.Driving;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Tests for the drift charge rules (D3): stages, boost length, early release, cancel.
    // Numbers used: blue at 0.8 s, orange at 1.6 s, boost 0.4 s / 1.2 s (the KartTuning defaults).
    public class DriftChargeTests
    {
        private static DriftCharge MakeDrift()
        {
            return new DriftCharge(0.8f, 1.6f, 0.4f, 1.2f);
        }

        [Test]
        public void NoDriftRunning_StageAndChargeAreZero()
        {
            DriftCharge drift = MakeDrift();
            Assert.IsFalse(drift.IsActive);
            Assert.AreEqual(0, drift.Stage);
            Assert.AreEqual(0f, drift.Charge01, 0.0001f);
        }

        [Test]
        public void StagesAppearAtTheRightTimes()
        {
            DriftCharge drift = MakeDrift();
            drift.Begin(1);
            drift.Tick(0.5f, 1f);
            Assert.AreEqual(0, drift.Stage, "0.5 s: no sparks yet");
            drift.Tick(0.3f, 1f);
            Assert.AreEqual(1, drift.Stage, "0.8 s: blue sparks");
            drift.Tick(0.8f, 1f);
            Assert.AreEqual(2, drift.Stage, "1.6 s: orange sparks");
        }

        [Test]
        public void EarlyRelease_GivesNoBoost()
        {
            DriftCharge drift = MakeDrift();
            drift.Begin(-1);
            drift.Tick(0.5f, 1f);
            Assert.AreEqual(0f, drift.Release(), 0.0001f);
            Assert.IsFalse(drift.IsActive);
        }

        [Test]
        public void Stage1Release_GivesShortBoost()
        {
            DriftCharge drift = MakeDrift();
            drift.Begin(1);
            drift.Tick(1.0f, 1f);
            Assert.AreEqual(0.4f, drift.Release(), 0.0001f);
        }

        [Test]
        public void Stage2Release_GivesLongBoost()
        {
            DriftCharge drift = MakeDrift();
            drift.Begin(1);
            drift.Tick(2.0f, 1f);
            Assert.AreEqual(1.2f, drift.Release(), 0.0001f);
        }

        [Test]
        public void Cancel_GivesNoBoostAndClearsCharge()
        {
            DriftCharge drift = MakeDrift();
            drift.Begin(1);
            drift.Tick(2.0f, 1f);
            drift.Cancel();
            Assert.IsFalse(drift.IsActive);
            Assert.AreEqual(0, drift.Stage);
            Assert.AreEqual(0f, drift.Release(), 0.0001f);
        }

        [Test]
        public void SteeringIntoTheTurn_ChargesFaster()
        {
            DriftCharge normal = MakeDrift();
            normal.Begin(1);
            normal.Tick(0.6f, 1f);
            Assert.AreEqual(0, normal.Stage, "0.6 s at normal rate is not enough");

            DriftCharge fast = MakeDrift();
            fast.Begin(1);
            fast.Tick(0.6f, 1.5f);
            Assert.AreEqual(1, fast.Stage, "0.6 s at 1.5x counts as 0.9 s");
        }

        [Test]
        public void Charge01_GoesFromZeroToOneAndStaysAtOne()
        {
            DriftCharge drift = MakeDrift();
            drift.Begin(1);
            drift.Tick(0.8f, 1f);
            Assert.AreEqual(0.5f, drift.Charge01, 0.0001f);
            drift.Tick(5f, 1f);
            Assert.AreEqual(1f, drift.Charge01, 0.0001f);
        }

        [Test]
        public void Release_WithoutADrift_GivesNothing()
        {
            DriftCharge drift = MakeDrift();
            Assert.AreEqual(0f, drift.Release(), 0.0001f);
        }

        [Test]
        public void Tick_WithoutADrift_IsIgnored()
        {
            DriftCharge drift = MakeDrift();
            drift.Tick(5f, 1f);
            drift.Begin(1);
            Assert.AreEqual(0, drift.Stage, "time before Begin must not count");
        }

        [Test]
        public void Begin_RemembersTheDirectionAndRestartsTheCharge()
        {
            DriftCharge drift = MakeDrift();
            drift.Begin(-1);
            Assert.AreEqual(-1, drift.Direction);
            drift.Tick(2f, 1f);
            drift.Begin(1);
            Assert.AreEqual(1, drift.Direction);
            Assert.AreEqual(0, drift.Stage, "a new drift starts from zero");
        }
    }
}
