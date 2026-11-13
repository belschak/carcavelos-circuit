using CarcavelosCircuit.Driving;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Tests for the trick rule (D11): accepted (trick in the air, finished before landing = boost),
    // rejected (on the ground, second trick in the same jump, still spinning at landing) and edge cases.
    // Numbers used: a trick takes 0.5 s, a clean landing gives 0.6 s of boost (the tests use their own numbers, not the game defaults).
    public class TrickRuleTests
    {
        private static TrickRule MakeTrick()
        {
            return new TrickRule(0.5f, 0.6f);
        }

        [Test]
        public void NoTrick_LandingGivesNothing()
        {
            TrickRule trick = MakeTrick();
            trick.TakeOff();
            trick.Tick(1f);
            Assert.AreEqual(0f, trick.Land(), 0.0001f);
        }

        [Test]
        public void FinishedTrick_LandingGivesTheBoost()
        {
            TrickRule trick = MakeTrick();
            trick.TakeOff();
            Assert.IsTrue(trick.TryStart(true));
            trick.Tick(0.6f);
            Assert.IsFalse(trick.IsRunning, "0.6 s is longer than the 0.5 s trick");
            Assert.AreEqual(0.6f, trick.Land(), 0.0001f);
        }

        [Test]
        public void TrickFinishingExactlyOnTime_GivesTheBoost()
        {
            TrickRule trick = MakeTrick();
            trick.TakeOff();
            trick.TryStart(true);
            trick.Tick(0.25f);
            trick.Tick(0.25f);
            Assert.AreEqual(0.6f, trick.Land(), 0.0001f);
        }

        [Test]
        public void TrickStillSpinningAtLanding_GivesNothing()
        {
            TrickRule trick = MakeTrick();
            trick.TakeOff();
            trick.TryStart(true);
            trick.Tick(0.3f);
            Assert.IsTrue(trick.IsRunning);
            Assert.AreEqual(0f, trick.Land(), 0.0001f);
            Assert.IsFalse(trick.IsRunning, "landing ends the spin");
        }

        [Test]
        public void OnTheGround_TrickIsRefused()
        {
            TrickRule trick = MakeTrick();
            Assert.IsFalse(trick.TryStart(false));
            Assert.IsFalse(trick.IsRunning);
        }

        [Test]
        public void SecondTrickInTheSameJump_IsRefused()
        {
            TrickRule trick = MakeTrick();
            trick.TakeOff();
            Assert.IsTrue(trick.TryStart(true));
            trick.Tick(0.6f);
            Assert.IsFalse(trick.TryStart(true), "one trick per jump");
            Assert.AreEqual(0.6f, trick.Land(), 0.0001f, "the first trick still counts");
        }

        [Test]
        public void PressingAgainDuringTheSpin_DoesNotRestartIt()
        {
            TrickRule trick = MakeTrick();
            trick.TakeOff();
            trick.TryStart(true);
            trick.Tick(0.4f);
            Assert.IsFalse(trick.TryStart(true));
            trick.Tick(0.2f);
            Assert.AreEqual(0.6f, trick.Land(), 0.0001f, "the original spin finished at 0.5 s");
        }

        [Test]
        public void NextJump_AllowsANewTrick()
        {
            TrickRule trick = MakeTrick();
            trick.TakeOff();
            trick.TryStart(true);
            trick.Tick(0.6f);
            trick.Land();
            trick.TakeOff();
            Assert.IsTrue(trick.TryStart(true));
        }

        [Test]
        public void LandingTwice_PaysOnlyOnce()
        {
            TrickRule trick = MakeTrick();
            trick.TakeOff();
            trick.TryStart(true);
            trick.Tick(0.6f);
            Assert.AreEqual(0.6f, trick.Land(), 0.0001f);
            Assert.AreEqual(0f, trick.Land(), 0.0001f);
        }

        [Test]
        public void Progress_GoesFromZeroToOne_AndIsZeroWithoutATrick()
        {
            TrickRule trick = MakeTrick();
            Assert.AreEqual(0f, trick.Progress01, 0.0001f);
            trick.TakeOff();
            trick.TryStart(true);
            Assert.AreEqual(0f, trick.Progress01, 0.0001f);
            trick.Tick(0.25f);
            Assert.AreEqual(0.5f, trick.Progress01, 0.0001f);
            trick.Tick(0.5f);
            Assert.AreEqual(0f, trick.Progress01, 0.0001f, "finished = no spin any more");
        }

        [Test]
        public void Reset_CancelsARunningTrickWithoutABoost()
        {
            TrickRule trick = MakeTrick();
            trick.TakeOff();
            trick.TryStart(true);
            trick.Tick(0.6f);
            trick.Reset();
            Assert.AreEqual(0f, trick.Land(), 0.0001f);
        }

        [Test]
        public void BadNumbers_AreSafe()
        {
            TrickRule zero = new TrickRule(0f, -1f);
            zero.TakeOff();
            zero.TryStart(true);
            zero.Tick(0.05f);
            Assert.IsTrue(zero.IsRunning, "a 0 s trick still takes the shortest time (0.1 s)");
            zero.Tick(0.1f);
            Assert.AreEqual(0f, zero.Land(), 0.0001f, "a negative boost is treated as no boost");

            TrickRule trick = MakeTrick();
            trick.TakeOff();
            trick.TryStart(true);
            trick.Tick(float.NaN);
            trick.Tick(-1f);
            Assert.AreEqual(0f, trick.Progress01, 0.0001f, "NaN or negative time does not move the spin");
        }
    }
}
