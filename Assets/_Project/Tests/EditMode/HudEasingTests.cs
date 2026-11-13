using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: the easing used for the results time counting up.
    public class HudEasingTests
    {
        [Test]
        public void EaseOutCubic_StartsAtZero_EndsAtOne()
        {
            Assert.AreEqual(0f, HudEasing.EaseOutCubic(0f), 0.0001f);
            Assert.AreEqual(1f, HudEasing.EaseOutCubic(1f), 0.0001f);
        }

        [Test]
        public void EaseOutCubic_IsFastAtTheStart()
        {
            // Halfway through the time, 1 - 0.5^3 = 87.5 % of the way is done.
            Assert.AreEqual(0.875f, HudEasing.EaseOutCubic(0.5f), 0.0001f);
        }

        [Test]
        public void EaseOutCubic_ClampsOutsideZeroToOne()
        {
            Assert.AreEqual(0f, HudEasing.EaseOutCubic(-1f), 0.0001f);
            Assert.AreEqual(1f, HudEasing.EaseOutCubic(2f), 0.0001f);
        }

        [Test]
        public void EaseOutCubic_NeverGoesBackwards()
        {
            float last = 0f;
            for (int i = 1; i <= 20; i++)
            {
                float value = HudEasing.EaseOutCubic(i / 20f);
                Assert.GreaterOrEqual(value, last);
                last = value;
            }
        }

        [Test]
        public void CountUp_WaitsForTheDelay_ThenRunsForTheDuration()
        {
            Assert.AreEqual(0f, HudEasing.CountUp(0.1f, 0.25f, 0.6f), 0.0001f);     // still waiting
            Assert.AreEqual(0.875f, HudEasing.CountUp(0.55f, 0.25f, 0.6f), 0.001f); // halfway
            Assert.AreEqual(1f, HudEasing.CountUp(0.85f, 0.25f, 0.6f), 0.0001f);    // done
            Assert.AreEqual(1f, HudEasing.CountUp(5f, 0.25f, 0.6f), 0.0001f);       // long after
        }

        [Test]
        public void CountUp_WithZeroDuration_JumpsAtTheDelay()
        {
            Assert.AreEqual(0f, HudEasing.CountUp(0.2f, 0.25f, 0f));
            Assert.AreEqual(1f, HudEasing.CountUp(0.25f, 0.25f, 0f));
        }
    }
}
