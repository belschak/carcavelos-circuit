using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: which medal a lap time earns, and that broken medal times are rejected.
    public class MedalRulesTests
    {
        // Bronze 12 s, Silver 11 s, Gold 10.2 s, Dean's List 9.6 s (the RaceSettings defaults).
        private static float[] Thresholds()
        {
            return new float[] { 12f, 11f, 10.2f, 9.6f };
        }

        [Test]
        public void DefaultsFromTheCode_AreValid()
        {
            Assert.IsTrue(MedalRules.AreValid(Thresholds()));
        }

        [TestCase(13f, Medal.None)]
        [TestCase(12.5f, Medal.None)]
        [TestCase(11.5f, Medal.Bronze)]
        [TestCase(10.9f, Medal.Silver)]
        [TestCase(10f, Medal.Gold)]
        [TestCase(9.1f, Medal.DeansList)]
        public void LapTime_GetsTheRightMedal(float lapSeconds, Medal expected)
        {
            Assert.AreEqual(expected, MedalRules.For(lapSeconds, Thresholds()));
        }

        [Test]
        public void ExactlyOnTheThreshold_EarnsTheMedal()
        {
            Assert.AreEqual(Medal.Bronze, MedalRules.For(12f, Thresholds()));
            Assert.AreEqual(Medal.Gold, MedalRules.For(10.2f, Thresholds()));
            Assert.AreEqual(Medal.DeansList, MedalRules.For(9.6f, Thresholds()));
        }

        [Test]
        public void OneMillisecondSlower_MissesTheMedal()
        {
            Assert.AreEqual(Medal.Silver, MedalRules.For(10.201f, Thresholds()));
            Assert.AreEqual(Medal.None, MedalRules.For(12.001f, Thresholds()));
        }

        [Test]
        public void ATimeThatReadsTheSameOnScreen_EarnsTheMedal()
        {
            // 10.2004 s is shown as 0:10.200, exactly the gold time on the chip.
            Assert.AreEqual(Medal.Gold, MedalRules.For(10.2004f, Thresholds()));
        }

        [Test]
        public void NoLapTime_IsNoMedal()
        {
            Assert.AreEqual(Medal.None, MedalRules.For(0f, Thresholds()));
            Assert.AreEqual(Medal.None, MedalRules.For(-1f, Thresholds()));
        }

        [Test]
        public void ThresholdsOutOfOrder_AreRejected()
        {
            Assert.IsFalse(MedalRules.AreValid(new float[] { 11f, 12f, 10.2f, 9.6f }), "silver slower than bronze");
            Assert.IsFalse(MedalRules.AreValid(new float[] { 12f, 11f, 11f, 9.6f }), "two equal tiers");
            Assert.AreEqual(Medal.None, MedalRules.For(5f, new float[] { 11f, 12f, 10.2f, 9.6f }), "broken times give no medal at all");
        }

        [Test]
        public void WrongCountOrNonPositiveThresholds_AreRejected()
        {
            Assert.IsFalse(MedalRules.AreValid(null));
            Assert.IsFalse(MedalRules.AreValid(new float[] { 12f, 11f, 10f }));
            Assert.IsFalse(MedalRules.AreValid(new float[] { 12f, 11f, 10f, 0f }));
        }

        [Test]
        public void NextTarget_IsOneTierUp_AndNoneAtTheTop()
        {
            Assert.AreEqual(Medal.Bronze, MedalRules.NextTarget(Medal.None));
            Assert.AreEqual(Medal.Gold, MedalRules.NextTarget(Medal.Silver));
            Assert.AreEqual(Medal.DeansList, MedalRules.NextTarget(Medal.Gold));
            Assert.AreEqual(Medal.None, MedalRules.NextTarget(Medal.DeansList));
        }

        [Test]
        public void ThresholdFor_ReturnsTheTierTime()
        {
            Assert.AreEqual(12f, MedalRules.ThresholdFor(Medal.Bronze, Thresholds()));
            Assert.AreEqual(9.6f, MedalRules.ThresholdFor(Medal.DeansList, Thresholds()));
            Assert.AreEqual(-1f, MedalRules.ThresholdFor(Medal.None, Thresholds()));
        }

        [Test]
        public void EveryMedal_HasANameAndAColourClass()
        {
            Assert.AreEqual("DEAN'S LIST", MedalRules.Name(Medal.DeansList));
            Assert.AreEqual("NO MEDAL", MedalRules.Name(Medal.None));
            Assert.AreEqual("medal--gold", MedalRules.ColourClass(Medal.Gold));
            Assert.AreEqual("medal--none", MedalRules.ColourClass(Medal.None));
        }
    }
}
