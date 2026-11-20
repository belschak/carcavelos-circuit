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
        public void TheKeyThatLeftTheRoom_DoesNotAlsoStartARace()
        {
            Assert.IsTrue(ExternalStartRules.IsLeavingFrame(120, 120), "Q on the online results, same frame");
            Assert.IsFalse(ExternalStartRules.IsLeavingFrame(121, 120), "the next key starts as offline");
            Assert.IsFalse(ExternalStartRules.IsLeavingFrame(0, -1), "never left online: offline is untouched");
            Assert.IsFalse(ExternalStartRules.MayStartFromKeys(false, RaceMode.RaceVsBots, ExternalStartRules.IsLeavingFrame(120, 120)));
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
        public void Q_GoesBackToTheMenu_FromTheCountdownTheRaceAndTheResults()
        {
            Assert.IsTrue(ExternalStartRules.MayGoBackToMenu(RaceState.Countdown, false), "wrong mode picked: out before GO");
            Assert.IsTrue(ExternalStartRules.MayGoBackToMenu(RaceState.Racing, false), "mid-race, no lap needed");
            Assert.IsTrue(ExternalStartRules.MayGoBackToMenu(RaceState.Results, false), "the results card, as before");
        }

        [Test]
        public void Q_IsRefused_OnTheStartScreen_AndWhileTheLobbyOwnsTheRace()
        {
            Assert.IsFalse(ExternalStartRules.MayGoBackToMenu(RaceState.Ready, false), "already on the start screen: Q is any key");
            Assert.IsFalse(ExternalStartRules.MayGoBackToMenu(RaceState.Racing, true), "online, Q leaves the room instead");
            Assert.IsFalse(ExternalStartRules.MayGoBackToMenu(RaceState.Countdown, true), "online countdown on the shared clock");
            Assert.IsFalse(ExternalStartRules.MayGoBackToMenu(RaceState.Results, true), "online results belong to the lobby");
            Assert.IsFalse(ExternalStartRules.MayGoBackToMenu(RaceState.Ready, true));
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
