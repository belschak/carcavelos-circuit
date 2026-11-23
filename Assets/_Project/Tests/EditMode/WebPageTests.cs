using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

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

        // Android name fix (23 Nov): the page's name box. Outside a Web build every call does nothing and reads "".
        [TestCase("Ana")]
        [TestCase("")]
        [TestCase(null)]
        public void NameBox_OutsideAWebBuild_DoesNothing(string current)
        {
            Rect field = new Rect(10f, 10f, 300f, 52f);
            Rect screen = new Rect(0f, 0f, 1280f, 720f);
            Assert.DoesNotThrow(() => WebPage.ShowNameBox(1, field, screen, current, 16, true));
            Assert.AreEqual("", WebPage.NameBoxValue(1), "no page: never a made-up name");
            Assert.IsFalse(WebPage.NameBoxFocused(1));
            Assert.DoesNotThrow(() => WebPage.HideNameBox(1));
        }

        [Test]
        public void NameBox_FieldNotOnScreen_KeepsTheFieldAsItWas()
        {
            // A field that is not on any panel (no screen yet): the box stays away, the typed name is untouched.
            TextField field = new TextField();
            field.value = "Ana";
            PageNameBox box = new PageNameBox(field);
            Assert.IsFalse(box.HasFocus);
            Assert.AreEqual("Ana", field.value);
            Assert.IsTrue(field.focusable, "desktop and Editor keep the game's own field");
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
        [TestCase(RaceState.Results, false, WebPage.PageMenus + WebPage.PageOnline)] // 23.11: ROOM RESULTS on screen
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
