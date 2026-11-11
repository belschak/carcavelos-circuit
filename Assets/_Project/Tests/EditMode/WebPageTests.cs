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
    }
}
