using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (help by Luc's Claude): when the camera's speed effects show and where the expensive effects run.
    public class CameraLookRulesTests
    {
        [Test]
        public void SpeedWeight_SlowerThanSixtyPercent_IsZero()
        {
            Assert.AreEqual(0f, CameraLookRules.SpeedWeight(10f, 25f, false));  // 40 %
            Assert.AreEqual(0f, CameraLookRules.SpeedWeight(15f, 25f, false));  // exactly 60 %: still clean
        }

        [Test]
        public void SpeedWeight_TopSpeed_IsOne_AndFasterStaysOne()
        {
            Assert.AreEqual(1f, CameraLookRules.SpeedWeight(25f, 25f, false));
            Assert.AreEqual(1f, CameraLookRules.SpeedWeight(40f, 25f, false));  // a boost pad pushes past top speed
        }

        [Test]
        public void SpeedWeight_HalfwayBetween_IsHalf_AndRisesWithSpeed()
        {
            Assert.AreEqual(0.5f, CameraLookRules.SpeedWeight(20f, 25f, false), 0.0001f);  // 80 % = middle of 60..100 %
            Assert.Less(CameraLookRules.SpeedWeight(18f, 25f, false), CameraLookRules.SpeedWeight(22f, 25f, false));
        }

        [Test]
        public void SpeedWeight_Boosting_IsAlwaysOne()
        {
            Assert.AreEqual(1f, CameraLookRules.SpeedWeight(5f, 25f, true));
        }

        [Test]
        public void SpeedWeight_InvalidNumbers_AreZero()
        {
            Assert.AreEqual(0f, CameraLookRules.SpeedWeight(20f, 0f, false));          // no top speed yet
            Assert.AreEqual(0f, CameraLookRules.SpeedWeight(-20f, 25f, false));        // reversing
            Assert.AreEqual(0f, CameraLookRules.SpeedWeight(float.NaN, 25f, false));
            Assert.AreEqual(0f, CameraLookRules.SpeedWeight(20f, float.NaN, false));
        }

        [Test]
        public void IsHighEnd_PcAndMac_Yes_WebMobileAndPhones_No()
        {
            Assert.IsTrue(CameraLookRules.IsHighEnd("PC", false));
            Assert.IsFalse(CameraLookRules.IsHighEnd("Web", false));
            Assert.IsFalse(CameraLookRules.IsHighEnd("Mobile", false));
            Assert.IsFalse(CameraLookRules.IsHighEnd("PC", true));   // a phone is never high end
        }

        [Test]
        public void Approach_MovesPartWay_AndPausedKeepsTheValue()
        {
            float moved = CameraLookRules.Approach(0f, 1f, 3f, 0.1f);
            Assert.Greater(moved, 0f);
            Assert.Less(moved, 1f);
            Assert.AreEqual(0.4f, CameraLookRules.Approach(0.4f, 1f, 3f, 0f));        // paused: dt 0
            Assert.AreEqual(0.4f, CameraLookRules.Approach(0.4f, 1f, 0f, 0.1f));      // no rate
        }

        [Test]
        public void Approach_SameResultAtAnyFrameRate()
        {
            float oneStep = CameraLookRules.Approach(0f, 1f, 3f, 0.2f);
            float twoSteps = CameraLookRules.Approach(CameraLookRules.Approach(0f, 1f, 3f, 0.1f), 1f, 3f, 0.1f);
            Assert.AreEqual(oneStep, twoSteps, 0.0001f);
        }
    }
}
