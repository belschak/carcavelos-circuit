using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task P6b): the timing of the finish moment (slow motion curve, finish beat, when confetti fires).
    public class FinishMomentTests
    {
        [Test]
        public void TimeScale_IsFullSpeed_AtTheStartAndAfterTheEnd()
        {
            Assert.AreEqual(1f, FinishMoment.TimeScaleAt(0f), 0.0001f);
            Assert.AreEqual(1f, FinishMoment.TimeScaleAt(FinishMoment.SlowMotionSeconds), 0.0001f);
            Assert.AreEqual(1f, FinishMoment.TimeScaleAt(5f), 0.0001f);
        }

        [Test]
        public void TimeScale_IsSlowest_DuringTheHold()
        {
            Assert.AreEqual(FinishMoment.SlowestScale, FinishMoment.TimeScaleAt(0.2f), 0.0001f);
            Assert.AreEqual(FinishMoment.SlowestScale, FinishMoment.TimeScaleAt(0.5f), 0.0001f);
        }

        [Test]
        public void TimeScale_BrokenOrNegativeTime_IsFullSpeed()
        {
            // A mistake must never leave the game slow.
            Assert.AreEqual(1f, FinishMoment.TimeScaleAt(float.NaN), 0.0001f);
            Assert.AreEqual(1f, FinishMoment.TimeScaleAt(-0.5f), 0.0001f);
            Assert.AreEqual(1f, FinishMoment.TimeScaleAt(float.PositiveInfinity), 0.0001f);
        }

        [Test]
        public void TimeScale_StaysBetweenSlowestAndOne_OnAOneMillisecondSweep()
        {
            for (int ms = 0; ms <= 1300; ms++)
            {
                float scale = FinishMoment.TimeScaleAt(ms / 1000f);
                Assert.GreaterOrEqual(scale, FinishMoment.SlowestScale - 0.0001f, "at " + ms + " ms");
                Assert.LessOrEqual(scale, 1f + 0.0001f, "at " + ms + " ms");
            }
        }

        [Test]
        public void TimeScale_GoesOnlyDown_ThenOnlyUp()
        {
            // No wobble: down until the hold ends, then up until the end (1 ms steps).
            float last = 1f;
            for (int ms = 1; ms <= 550; ms++)
            {
                float scale = FinishMoment.TimeScaleAt(ms / 1000f);
                Assert.LessOrEqual(scale, last + 0.0001f, "down at " + ms + " ms");
                last = scale;
            }
            for (int ms = 551; ms <= 1200; ms++)
            {
                float scale = FinishMoment.TimeScaleAt(ms / 1000f);
                Assert.GreaterOrEqual(scale, last - 0.0001f, "up at " + ms + " ms");
                last = scale;
            }
        }

        [Test]
        public void TimeScale_HasNoJump_AtTheJoins()
        {
            // At each join of the curve the value just before and just after is almost the same.
            float[] joins = { FinishMoment.SlowDownSeconds, FinishMoment.HoldUntilSeconds, FinishMoment.SlowMotionSeconds };
            for (int i = 0; i < joins.Length; i++)
            {
                float before = FinishMoment.TimeScaleAt(joins[i] - 0.0005f);
                float after = FinishMoment.TimeScaleAt(joins[i] + 0.0005f);
                Assert.AreEqual(before, after, 0.01f, "join at " + joins[i] + " s");
            }
        }

        [Test]
        public void IsSlowMotionRunning_OnlyInsideTheWindow()
        {
            Assert.IsFalse(FinishMoment.IsSlowMotionRunning(-0.01f));
            Assert.IsTrue(FinishMoment.IsSlowMotionRunning(0f));
            Assert.IsTrue(FinishMoment.IsSlowMotionRunning(1.19f));
            Assert.IsFalse(FinishMoment.IsSlowMotionRunning(FinishMoment.SlowMotionSeconds));
            Assert.IsFalse(FinishMoment.IsSlowMotionRunning(float.NaN));
        }

        [Test]
        public void Beat_IsZero_WithReducedMotion()
        {
            Assert.AreEqual(FinishMoment.BeatSeconds, FinishMoment.BeatFor(false), 0.0001f);
            Assert.AreEqual(0f, FinishMoment.BeatFor(true), 0.0001f);
        }

        [Test]
        public void Beat_EndsBeforeTheSlowMotion()
        {
            // The card comes in while the game speeds up again, never after it is back at full speed.
            Assert.Less(FinishMoment.BeatSeconds, FinishMoment.SlowMotionSeconds);
            Assert.Greater(FinishMoment.BeatSeconds, FinishMoment.HoldUntilSeconds);
        }

        [Test]
        public void Celebrates_NewRecord()
        {
            Assert.IsTrue(FinishMoment.Celebrates(true, Medal.None, Medal.None, false));
            Assert.IsTrue(FinishMoment.Celebrates(true, Medal.Gold, Medal.Gold, false));
        }

        [Test]
        public void Celebrates_NewMedal_WithoutRecord()
        {
            // Can happen after the medal times were re-tuned.
            Assert.IsTrue(FinishMoment.Celebrates(false, Medal.Silver, Medal.Bronze, false));
            Assert.IsTrue(FinishMoment.Celebrates(false, Medal.Bronze, Medal.None, false));
        }

        [Test]
        public void DoesNotCelebrate_AnOrdinaryLap()
        {
            Assert.IsFalse(FinishMoment.Celebrates(false, Medal.Gold, Medal.Gold, false));     // same medal again
            Assert.IsFalse(FinishMoment.Celebrates(false, Medal.Bronze, Medal.Gold, false));   // worse medal
            Assert.IsFalse(FinishMoment.Celebrates(false, Medal.None, Medal.None, false));     // no medal at all
        }

        [Test]
        public void DoesNotCelebrate_WithReducedMotion()
        {
            Assert.IsFalse(FinishMoment.Celebrates(true, Medal.DeansList, Medal.None, true));
            Assert.IsFalse(FinishMoment.Celebrates(false, Medal.Silver, Medal.Bronze, true));
        }

        [Test]
        public void Confetti_FiresWithTheStampThatLandsLast()
        {
            Assert.AreEqual(FinishMoment.RecordStampLandsSeconds, FinishMoment.ConfettiDelay(true), 0.0001f);
            Assert.AreEqual(FinishMoment.MedalStampLandsSeconds, FinishMoment.ConfettiDelay(false), 0.0001f);
            Assert.Greater(FinishMoment.RecordStampLandsSeconds, FinishMoment.MedalStampLandsSeconds);
        }
    }
}
