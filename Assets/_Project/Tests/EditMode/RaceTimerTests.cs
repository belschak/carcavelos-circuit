using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: the lap timer and how times are written on screen.
    public class RaceTimerTests
    {
        [Test]
        public void Timer_DoesNotCount_BeforeStart()
        {
            RaceTimer timer = new RaceTimer();
            timer.Tick(1f);
            Assert.AreEqual(0f, timer.ElapsedSeconds);
        }

        [Test]
        public void Timer_AddsFrameTimes_WhileRunning()
        {
            RaceTimer timer = new RaceTimer();
            timer.Start();
            timer.Tick(0.5f);
            timer.Tick(0.25f);
            Assert.AreEqual(0.75f, timer.ElapsedSeconds, 0.0001f);
        }

        [Test]
        public void Timer_Freezes_AfterStop()
        {
            RaceTimer timer = new RaceTimer();
            timer.Start();
            timer.Tick(2f);
            timer.Stop();
            timer.Tick(5f);
            Assert.AreEqual(2f, timer.ElapsedSeconds, 0.0001f);
            Assert.IsFalse(timer.IsRunning);
        }

        [Test]
        public void Timer_Reset_GoesBackToZero()
        {
            RaceTimer timer = new RaceTimer();
            timer.Start();
            timer.Tick(3f);
            timer.Reset();
            Assert.AreEqual(0f, timer.ElapsedSeconds);
            Assert.IsFalse(timer.IsRunning);
        }

        [Test]
        public void Timer_IgnoresNegativeFrameTime()
        {
            RaceTimer timer = new RaceTimer();
            timer.Start();
            timer.Tick(-1f);
            Assert.AreEqual(0f, timer.ElapsedSeconds);
        }

        [TestCase(0f, "0:00.000")]
        [TestCase(5.5f, "0:05.500")]
        [TestCase(83.456f, "1:23.456")]
        [TestCase(59.9996f, "1:00.000")] // rounds up into the next minute, never "0:60.000"
        [TestCase(-3f, "0:00.000")]
        public void Format_WritesMinutesSecondsMilliseconds(float seconds, string expected)
        {
            Assert.AreEqual(expected, RaceTimer.Format(seconds));
        }

        [TestCase(-1.204f, "-1.204")]
        [TestCase(0.5f, "+0.500")]
        [TestCase(0f, "+0.000")]
        [TestCase(12.3456f, "+12.346")]
        public void FormatDelta_AlwaysHasASign(float delta, string expected)
        {
            Assert.AreEqual(expected, RaceTimer.FormatDelta(delta));
        }
    }
}
