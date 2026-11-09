using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: the lap report counts each drift and boost once, sums air time, keeps the top speed.
    public class RunStatsTests
    {
        private const float Frame = 0.02f; // 50 fps

        [Test]
        public void ALongDrift_CountsOnce()
        {
            RunStats stats = new RunStats();
            for (int i = 0; i < 150; i++) // 3 s of drifting
            {
                stats.Tick(Frame, 15f, true, false, true);
            }
            stats.Tick(Frame, 15f, false, false, true);
            Assert.AreEqual(1, stats.Drifts);
        }

        [Test]
        public void TwoSeparateDrifts_CountTwice()
        {
            RunStats stats = new RunStats();
            stats.Tick(Frame, 15f, true, false, true);
            stats.Tick(Frame, 15f, false, false, true);
            stats.Tick(Frame, 15f, true, false, true);
            Assert.AreEqual(2, stats.Drifts);
        }

        [Test]
        public void Boosts_CountOnTheRisingEdge()
        {
            RunStats stats = new RunStats();
            stats.Tick(Frame, 15f, false, true, true);
            stats.Tick(Frame, 15f, false, true, true);
            stats.Tick(Frame, 15f, false, false, true);
            stats.Tick(Frame, 15f, false, true, true);
            Assert.AreEqual(2, stats.Boosts);
        }

        [Test]
        public void TopSpeed_KeepsTheMaximum_ReverseCounts()
        {
            RunStats stats = new RunStats();
            stats.Tick(Frame, 10f, false, false, true);
            stats.Tick(Frame, 22f, false, false, true);
            stats.Tick(Frame, 5f, false, false, true);
            Assert.AreEqual(22f, stats.TopSpeed, 0.0001f);
            stats.Tick(Frame, -30f, false, false, true);
            Assert.AreEqual(30f, stats.TopSpeed, 0.0001f);
        }

        [Test]
        public void AirTime_SumsOnlyWhileAirborne_LongestJumpKeepsTheMax()
        {
            RunStats stats = new RunStats();
            stats.Tick(0.5f, 15f, false, false, true);   // on the ground: no air time
            stats.Tick(0.25f, 15f, false, false, false); // jump 1: 0.5 s
            stats.Tick(0.25f, 15f, false, false, false);
            stats.Tick(0.5f, 15f, false, false, true);   // landed
            stats.Tick(0.25f, 15f, false, false, false); // jump 2: 0.25 s
            stats.Tick(0.5f, 15f, false, false, true);
            Assert.AreEqual(0.75f, stats.AirSeconds, 0.0001f);
            Assert.AreEqual(0.5f, stats.LongestJumpSeconds, 0.0001f);
        }

        [Test]
        public void Recoveries_AreCounted_AndEndTheJump()
        {
            RunStats stats = new RunStats();
            stats.Tick(0.5f, 15f, false, false, false);
            stats.NoteRecovery();
            stats.Tick(0.25f, 0f, false, false, false); // falling again after the teleport = a new jump
            Assert.AreEqual(1, stats.Recoveries);
            Assert.AreEqual(0.75f, stats.AirSeconds, 0.0001f);
            Assert.AreEqual(0.5f, stats.LongestJumpSeconds, 0.0001f);
        }

        [Test]
        public void NegativeFrameTime_IsIgnored()
        {
            RunStats stats = new RunStats();
            stats.Tick(-1f, 0f, false, false, false);
            Assert.AreEqual(0f, stats.AirSeconds);
        }

        [Test]
        public void Reset_ClearsEverything_AndADriftStillHeldCountsAgain()
        {
            RunStats stats = new RunStats();
            stats.Tick(0.5f, 20f, true, true, false);
            stats.NoteRecovery();
            stats.Reset();
            Assert.AreEqual(0f, stats.TopSpeed);
            Assert.AreEqual(0, stats.Drifts);
            Assert.AreEqual(0, stats.Boosts);
            Assert.AreEqual(0f, stats.AirSeconds);
            Assert.AreEqual(0f, stats.LongestJumpSeconds);
            Assert.AreEqual(0, stats.Recoveries);
            stats.Tick(Frame, 20f, true, false, true);
            Assert.AreEqual(1, stats.Drifts, "after Reset the first drifting frame is a new drift");
        }
    }
}
