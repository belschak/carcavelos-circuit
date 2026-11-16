using CarcavelosCircuit.Online;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // The shared-clock start of an online race (M2): GO lands on StartAt on every screen.
    public class OnlineStartRulesTests
    {
        [Test]
        public void StartAt_IsFourSecondsOut()
        {
            Assert.AreEqual(104.0, OnlineStartRules.StartAtFor(100.0), 0.0);
        }

        [Test]
        public void Countdown_BeginsThreeStepsBeforeGo()
        {
            Assert.IsFalse(OnlineStartRules.ShouldBeginCountdown(100.9, 104.0, 3, 1f), "too early");
            Assert.IsTrue(OnlineStartRules.ShouldBeginCountdown(101.0, 104.0, 3, 1f), "exactly 3 s before GO");
            Assert.IsTrue(OnlineStartRules.ShouldBeginCountdown(103.0, 104.0, 3, 1f), "late is still a start");
            // Bad settings become one 0.05 s step, so the start is never blocked, only as late as possible.
            Assert.IsFalse(OnlineStartRules.ShouldBeginCountdown(103.9, 104.0, 0, 0f), "bad settings: not before the last step");
            Assert.IsTrue(OnlineStartRules.ShouldBeginCountdown(103.95, 104.0, 0, 0f), "bad settings never block the start");
            Assert.IsTrue(OnlineStartRules.ShouldBeginCountdown(105.0, 104.0, 0, 0f), "bad settings, already past GO");
        }

        [Test]
        public void CountdownStep_ShrinksForALateControl_SoGoStillLandsOnStartAt()
        {
            Assert.AreEqual(1f, OnlineStartRules.CountdownStepFor(101.0, 104.0, 3), 0.0001f, "on time: whole seconds");
            Assert.AreEqual(1f, OnlineStartRules.CountdownStepFor(100.0, 104.0, 3), 0.0001f, "early: never slower than 1 s");
            Assert.AreEqual(0.5f, OnlineStartRules.CountdownStepFor(102.5, 104.0, 3), 0.0001f, "1.5 s late: three 0.5 s steps");
            Assert.AreEqual(0.05f, OnlineStartRules.CountdownStepFor(103.99, 104.0, 3), 0.0001f, "at the floor");
            Assert.AreEqual(0.05f, OnlineStartRules.CountdownStepFor(106.0, 104.0, 3), 0.0001f, "past GO: the quickest countdown");
            Assert.AreEqual(0.05f, OnlineStartRules.CountdownStepFor(double.NaN, 104.0, 3), 0.0001f);
            Assert.AreEqual(1f, OnlineStartRules.CountdownStepFor(101.0, 104.0, 0), 0.0001f, "a bad setting counts as one step");
        }

        [Test]
        public void Host_StartsAgainOnlyWhenTheRaceIsOverForEveryone()
        {
            Assert.IsTrue(OnlineStartRules.HostMayStart(0, true, false), "first race from the start screen");
            Assert.IsFalse(OnlineStartRules.HostMayStart(0, false, true), "first race: not while the screen is busy");
            Assert.IsFalse(OnlineStartRules.HostMayStart(3, true, false), "others still racing: Enter never restarts");
            Assert.IsTrue(OnlineStartRules.HostMayStart(3, false, true), "all done or out of time");
        }

        [Test]
        public void Phase_FollowsTheClock_AndTheFinishes()
        {
            RaceControl race = new RaceControl(1, OnlinePhase.Countdown, 104.0, new[] { 1, 2 });
            Assert.AreEqual(OnlinePhase.Countdown, OnlineStartRules.PhaseFor(race, 103.0, false));
            Assert.AreEqual(OnlinePhase.Racing, OnlineStartRules.PhaseFor(race, 104.0, false));
            Assert.AreEqual(OnlinePhase.Results, OnlineStartRules.PhaseFor(race, 200.0, true));
            Assert.AreEqual(OnlinePhase.Lobby, OnlineStartRules.PhaseFor(RaceControl.Lobby, 200.0, true), "no race yet");
            Assert.AreEqual(OnlinePhase.Lobby, OnlineStartRules.PhaseFor(null, 200.0, true));
        }

        [Test]
        public void AutoStart_NeedsTheFlag_AndEnoughPlayers()
        {
            Assert.IsFalse(OnlineStartRules.AutoStartHasEnough(0, 15), "flag off");
            Assert.IsFalse(OnlineStartRules.AutoStartHasEnough(1, 15), "one player is never a race");
            Assert.IsFalse(OnlineStartRules.AutoStartHasEnough(3, 2));
            Assert.IsTrue(OnlineStartRules.AutoStartHasEnough(3, 3));
            Assert.IsTrue(OnlineStartRules.AutoStartHasEnough(3, 7));
        }
    }
}
