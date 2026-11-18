using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R10/R11): outside a Web build the calls to the page do nothing and never break the game.
    // The browser side (touch buttons hide outside a race, the name prompt) can only be checked in a real Web build.
    public class WebPageTests
    {
        [Test]
        public void WorldStream_OutsideAWebBuild_DoesNothing()
        {
            // Load time P8: without a world file (Editor, tests) RaceBootstrap loads the world scene as before.
            Assert.AreEqual("", WebPage.WorldBundleUrl);
            System.Collections.IEnumerator load = WorldStream.Load();
            Assert.IsFalse(load.MoveNext(), "nothing to wait for");
            Assert.IsFalse(WorldStream.Failed);
            Assert.IsFalse(WorldStream.IsUsed);
            WebPage.SendWorldState(WebPage.WorldReady, 1f); // no page: nothing happens
            WorldStream.Release();
        }

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
        [TestCase(RaceState.Ready, false, WebPage.PageMenus)]
        [TestCase(RaceState.Ready, true, WebPage.PageMenus)]
        [TestCase(RaceState.Countdown, false, WebPage.PageRacing)]
        [TestCase(RaceState.Racing, false, WebPage.PageRacing)]
        [TestCase(RaceState.Countdown, true, WebPage.PagePaused)]
        [TestCase(RaceState.Racing, true, WebPage.PagePaused)]
        [TestCase(RaceState.Results, false, WebPage.PageMenus)]
        public void PageRaceState_ButtonsOnlyInARunningRace(RaceState state, bool paused, int expected)
        {
            Assert.AreEqual(expected, WebPage.PageRaceState(state, paused));
        }

        // Phone QA 17.11: online the page hides Pause and Retry (the room cannot be paused or restarted by hand).
        [TestCase(RaceState.Racing, false, WebPage.PageRacing + WebPage.PageOnline)]
        [TestCase(RaceState.Countdown, false, WebPage.PageRacing + WebPage.PageOnline)]
        [TestCase(RaceState.Ready, false, WebPage.PageMenus)]
        [TestCase(RaceState.Results, false, WebPage.PageMenus)]
        public void PageRaceState_TellsThePageAnOnlineRace(RaceState state, bool paused, int expected)
        {
            Assert.AreEqual(expected, WebPage.PageRaceState(state, paused, true));
            Assert.AreEqual(WebPage.PageRaceState(state, paused), WebPage.PageRaceState(state, paused, true) & 3,
                "the low bits stay the plain race state the page already reads");
        }

        [Test]
        public void SendRaceState_WithAPageState_OutsideAWebBuild_DoesNothing()
        {
            Assert.DoesNotThrow(() => WebPage.SendRaceState(WebPage.PagePaused));
        }
    }
}
