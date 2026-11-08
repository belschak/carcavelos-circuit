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
            timer.Start(0.0);
            timer.Tick(0.5f);
            timer.Tick(0.25f);
            Assert.AreEqual(0.75f, timer.ElapsedSeconds, 0.0001f);
        }

        [Test]
        public void Timer_Freezes_AfterStop()
        {
            RaceTimer timer = new RaceTimer();
            timer.Start(0.0);
            timer.Tick(2f);
            timer.Stop(2.0);
            timer.Tick(5f);
            Assert.AreEqual(2f, timer.ElapsedSeconds, 0.0001f);
            Assert.IsFalse(timer.IsRunning);
        }

        [Test]
        public void Timer_Reset_GoesBackToZero()
        {
            RaceTimer timer = new RaceTimer();
            timer.Start(0.0);
            timer.Tick(3f);
            timer.Reset();
            Assert.AreEqual(0f, timer.ElapsedSeconds);
            Assert.IsFalse(timer.IsRunning);
        }

        [Test]
        public void Timer_IgnoresNegativeFrameTime()
        {
            RaceTimer timer = new RaceTimer();
            timer.Start(0.0);
            timer.Tick(-1f);
            Assert.AreEqual(0f, timer.ElapsedSeconds);
        }

        [Test]
        public void LapTime_ComesFromTimestamps_NotFromAddedFrameTimes()
        {
            // 60 fps for about a minute: the added float frame times drift, the official time must not.
            RaceTimer timer = new RaceTimer();
            timer.Start(100.0);
            for (int i = 0; i < 3600; i++)
            {
                timer.Tick(1f / 60f);
            }
            timer.Stop(160.1234);
            Assert.AreEqual(60.1234f, timer.ElapsedSeconds, 0.00001f);
        }

        [Test]
        public void LapTime_ExactEvenWithLongFrames()
        {
            // The finish happens inside a physics step between two frames; only the timestamps know that moment.
            RaceTimer timer = new RaceTimer();
            timer.Start(5.0);
            timer.Tick(0.333f);
            timer.Stop(5.25);
            Assert.AreEqual(0.25f, timer.ElapsedSeconds, 0.00001f);
        }

        [Test]
        public void Stop_SecondCallChangesNothing()
        {
            RaceTimer timer = new RaceTimer();
            timer.Start(0.0);
            timer.Stop(30.0);
            timer.Stop(45.0);
            Assert.AreEqual(30f, timer.ElapsedSeconds, 0.00001f);
        }

        [Test]
        public void SecondsSinceStart_UsesTimestamps_AndIsNeverNegative()
        {
            RaceTimer timer = new RaceTimer();
            timer.Start(10.0);
            Assert.AreEqual(2.5f, timer.SecondsSinceStart(12.5), 0.00001f);
            Assert.AreEqual(0f, timer.SecondsSinceStart(9.0));
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
        [TestCase(-0.0004f, "+0.000")] // rounds to zero, so no "-0.000"
        [TestCase(-0.0006f, "-0.001")]
        public void FormatDelta_AlwaysHasASign(float delta, string expected)
        {
            Assert.AreEqual(expected, RaceTimer.FormatDelta(delta));
        }

        [TestCase(83456, "1:23.456")]
        [TestCase(7, "0:00.007")]
        [TestCase(6000000, "100:00.000")]
        [TestCase(-5, "0:00.000")]
        public void FormatMilliseconds_WritesMinutesSecondsMilliseconds(int milliseconds, string expected)
        {
            Assert.AreEqual(expected, RaceTimer.FormatMilliseconds(milliseconds));
        }
    }
}
