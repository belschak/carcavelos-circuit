using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Finish cinematic (help for Role 4, Luc 16 Nov 2026): flag, camera swing, headline and results card timing.
    public class FinishCinematicTests
    {
        [Test]
        public void Order_FlagThenSwingThenHeadlineThenCard()
        {
            Assert.IsTrue(FinishCinematic.FlagShown(0f), "the flag is there at the line");
            Assert.Less(FinishCinematic.SwingStartSeconds, FinishCinematic.HeadlineInSeconds);
            Assert.Less(FinishCinematic.SwingStartSeconds + FinishCinematic.SwingSeconds, FinishCinematic.ResultsCardSeconds,
                "the swing ends before the card covers the road");
            Assert.IsFalse(FinishCinematic.HeadlineShown(1f, false));
            Assert.IsTrue(FinishCinematic.HeadlineShown(FinishCinematic.HeadlineInSeconds, false));
            Assert.IsFalse(FinishCinematic.FlagShown(FinishCinematic.FlagOutSeconds));
            Assert.IsFalse(FinishCinematic.FlagShown(-0.1f), "not before the line");
        }

        [Test]
        public void ReducedMotion_HeadlineAtOnce_NoSwing_NoWave_CardSooner()
        {
            Assert.IsTrue(FinishCinematic.HeadlineShown(0f, true));
            Assert.AreEqual(0f, FinishCinematic.SwingProgress(3f, true));
            Assert.AreEqual(0f, FinishCinematic.FlagWave(0.7f, 5, true));
            Assert.Less(FinishCinematic.ResultsCardDelay(true), FinishCinematic.ResultsCardDelay(false));
        }

        [Test]
        public void Swing_StartsBehind_EndsInFront_StaysInRange()
        {
            Assert.AreEqual(0f, FinishCinematic.SwingProgress(0f, false));
            Assert.AreEqual(0f, FinishCinematic.SwingProgress(FinishCinematic.SwingStartSeconds, false));
            Assert.AreEqual(1f, FinishCinematic.SwingProgress(100f, false));
            Assert.AreEqual(0f, FinishCinematic.SwingProgress(float.NaN, false), "a broken time never moves the camera");
            float previous = 0f;
            for (float t = 0f; t < 4f; t += 0.05f)
            {
                float p = FinishCinematic.SwingProgress(t, false);
                Assert.That(p, Is.InRange(0f, 1f));
                Assert.GreaterOrEqual(p, previous, "the swing never goes back");
                previous = p;
            }

            Vector3 behind = FinishCinematic.CameraOffset(0f);
            Vector3 front = FinishCinematic.CameraOffset(1f);
            Assert.Less(behind.z, -5f, "starts behind the kart, like the chase camera");
            Assert.Greater(front.z, 4f, "ends in front of the kart");
            Assert.Greater(front.x, 0f, "a little to the right, so the steering wheel does not hide the driver");
            Assert.Less(front.y, behind.y, "lower in front: eye level with the driver");
            Assert.Greater(FinishCinematic.CameraOffset(0.5f).x, 4f, "goes round the side, never through the kart");
            Assert.AreEqual(behind, FinishCinematic.CameraOffset(float.NaN));
            Assert.AreEqual(front, FinishCinematic.CameraOffset(7f), "clamped");
        }

        [Test]
        public void Skip_OnlyAfterTheFirstMoment()
        {
            Assert.IsFalse(FinishCinematic.MaySkip(0.2f), "the finger may still be on the gas at the line");
            Assert.IsTrue(FinishCinematic.MaySkip(FinishCinematic.SkipAllowedSeconds));
            Assert.IsFalse(FinishCinematic.MaySkip(-1f), "not finished");
        }
    }
}
