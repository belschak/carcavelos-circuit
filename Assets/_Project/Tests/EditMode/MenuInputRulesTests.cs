using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Team test 17 Nov 2026: who owns a key or a click on the start screen, and the Mario Kart order
    // mode -> driver -> go for every mode (RaceFlow/MenuInputRules.cs).
    public class MenuInputRulesTests
    {
        [Test]
        public void AClickOnAButton_BelongsToTheButton()
        {
            Assert.IsTrue(MenuInputRules.ClickBelongsToButton(true, true));
        }

        [Test]
        public void AKeyWhileTheMouseRestsOnAButton_IsStillAKey()
        {
            // The bug: a mouse parked on a mode card swallowed Space and Enter.
            Assert.IsFalse(MenuInputRules.ClickBelongsToButton(false, true));
        }

        [Test]
        public void AClickNextToTheButtons_IsAnyKey()
        {
            Assert.IsFalse(MenuInputRules.ClickBelongsToButton(true, false));
            Assert.IsFalse(MenuInputRules.ClickBelongsToButton(false, false));
        }

        [TestCase(RaceMode.RaceVsBots)]
        [TestCase(RaceMode.TimeAttack)]
        [TestCase(RaceMode.OnlineRace)]
        public void EveryMode_GoesThroughTheDriverSelect(RaceMode mode)
        {
            Assert.IsTrue(MenuInputRules.OpensDriverSelect(mode, true));
        }

        [Test]
        public void OnlineWithoutTheOnlineBuild_NeverReachesTheDriverSelect()
        {
            Assert.IsFalse(MenuInputRules.OpensDriverSelect(RaceMode.OnlineRace, false));
            Assert.IsTrue(MenuInputRules.OpensDriverSelect(RaceMode.RaceVsBots, false), "offline modes do not need it");
            Assert.IsFalse(MenuInputRules.OpensDriverSelect((RaceMode)7, true), "an unknown mode is refused");
        }

        [Test]
        public void Ready_StartsTheCountdownOffline_OpensTheLobbyOnline()
        {
            Assert.IsFalse(MenuInputRules.ReadyOpensLobby(RaceMode.RaceVsBots));
            Assert.IsFalse(MenuInputRules.ReadyOpensLobby(RaceMode.TimeAttack));
            Assert.IsTrue(MenuInputRules.ReadyOpensLobby(RaceMode.OnlineRace));
        }

        [Test]
        public void StartPrompt_PointsAtTheModeCards()
        {
            StringAssert.Contains("MODE", MenuInputRules.StartPrompt(true));
            StringAssert.Contains("TAP", MenuInputRules.StartPrompt(true));
            StringAssert.Contains("CLICK", MenuInputRules.StartPrompt(false));
        }
    }
}
