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
            Assert.IsTrue(OnlineStartRules.ShouldBeginCountdown(103.0, 104.0, 0, 0f), "bad settings never block the start");
            Assert.IsFalse(OnlineStartRules.ShouldBeginCountdown(103.9, 104.0, 0, 0f));
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
