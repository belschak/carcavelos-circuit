using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // M2 online race: which keys RaceManager still obeys while the online lobby owns the race (ExternalStartOnly).
    public class ExternalStartRulesTests
    {
        [Test]
        public void Keys_StartAnOfflineRace_UnlessAMenuUsesThem()
        {
            Assert.IsTrue(ExternalStartRules.MayStartFromKeys(false, RaceMode.RaceVsBots, false));
            Assert.IsTrue(ExternalStartRules.MayStartFromKeys(false, RaceMode.TimeAttack, false));
            Assert.IsFalse(ExternalStartRules.MayStartFromKeys(false, RaceMode.RaceVsBots, true), "the settings menu has the keys");
        }

        [Test]
        public void Keys_NeverStartWhileTheLobbyOwnsTheRace()
        {
            Assert.IsFalse(ExternalStartRules.MayStartFromKeys(true, RaceMode.RaceVsBots, false));
            Assert.IsFalse(ExternalStartRules.MayStartFromKeys(true, RaceMode.TimeAttack, false));
            Assert.IsFalse(ExternalStartRules.MayStartFromKeys(true, RaceMode.OnlineRace, false));
        }

        [Test]
        public void Keys_NeverStartAnOnlineRace_EvenWhenTheLobbyForgotTheFlag()
        {
            Assert.IsFalse(ExternalStartRules.MayStartFromKeys(false, RaceMode.OnlineRace, false), "the host starts it for everyone");
            Assert.IsFalse(ExternalStartRules.MayStartFromKeys(false, (RaceMode)9, false), "an unknown mode never starts");
        }

        [Test]
        public void RetryLeaveAndPause_OnlyWhileThePlayerOwnsTheRace()
        {
            Assert.IsTrue(ExternalStartRules.MayRetryOrLeave(false));
            Assert.IsFalse(ExternalStartRules.MayRetryOrLeave(true), "Enter and Q belong to the lobby online");
            Assert.IsTrue(ExternalStartRules.MayPauseByHand(false));
            Assert.IsFalse(ExternalStartRules.MayPauseByHand(true), "a pause would make this player's GO late");
        }

        [Test]
        public void TheLobby_StartsOnlyFromTheStartScreen_InAPlayableMode()
        {
            Assert.IsTrue(ExternalStartRules.MayBeginExternal(RaceState.Ready, RaceMode.OnlineRace));
            Assert.IsTrue(ExternalStartRules.MayBeginExternal(RaceState.Ready, RaceMode.TimeAttack), "a local test lobby may start any mode");
            Assert.IsFalse(ExternalStartRules.MayBeginExternal(RaceState.Countdown, RaceMode.OnlineRace), "already started");
            Assert.IsFalse(ExternalStartRules.MayBeginExternal(RaceState.Racing, RaceMode.OnlineRace));
            Assert.IsFalse(ExternalStartRules.MayBeginExternal(RaceState.Results, RaceMode.OnlineRace), "a rematch resets first");
            Assert.IsFalse(ExternalStartRules.MayBeginExternal(RaceState.Ready, (RaceMode)9), "an unknown mode is never raced");
        }
    }
}
