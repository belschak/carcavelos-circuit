using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R10/R11): outside a Web build the calls to the page do nothing and never break the game.
    // The browser side (touch buttons hide outside a race, the name prompt) can only be checked in a real Web build.
    public class WebPageTests
    {
        [Test]
        public void UsesNamePrompt_IsOffOutsideAWebBuild()
        {
            Assert.IsFalse(WebPage.UsesNamePrompt, "Editor and desktop keep the game's own name field");
        }

        [TestCase("Ana")]
        [TestCase("")]
        [TestCase("a name far longer than the sixteen letters the board allows")]
        public void PromptName_OutsideAWebBuild_KeepsTheName(string current)
        {
            Assert.AreEqual(current, WebPage.PromptName(current));
        }

        [Test]
        public void PromptName_Null_StaysNull()
        {
            Assert.IsNull(WebPage.PromptName(null), "no name yet: nothing is made up");
        }

        [Test]
        public void SendRaceState_OutsideAWebBuild_DoesNothing()
        {
            Assert.DoesNotThrow(() => WebPage.SendRaceState(true));
            Assert.DoesNotThrow(() => WebPage.SendRaceState(false));
        }
        [Test]
        public void TouchMode_IsOffOutsideAWebBuild()
        {
            Assert.IsFalse(WebPage.TouchMode, "Editor and desktop: the HUD keeps its keyboard layout");
        }

        // Phone round 17.11: the page's touch buttons show in the countdown and the race, but not while paused
        // (the PAUSED card stays free, a tap continues), and never on the start screen or the results.
        [TestCase(RaceState.Ready, false, false)]
        [TestCase(RaceState.Countdown, false, true)]
        [TestCase(RaceState.Racing, false, true)]
        [TestCase(RaceState.Countdown, true, false)]
        [TestCase(RaceState.Racing, true, false)]
        [TestCase(RaceState.Results, false, false)]
        public void PageSeesRacing_OnlyInAnUnpausedRace(RaceState state, bool paused, bool expected)
        {
            Assert.AreEqual(expected, WebPage.PageSeesRacing(state, paused));
        }
    }
}
