using System.IO;
using System.Text.RegularExpressions;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Phone QA 17.11: one tap = one click, and the menu targets are thumb-sized on touch. The real taps are checked in
    // a Web build (by hand and with browser scripts on phone profiles, docs/verification.md "Not covered").
    public class PhoneTapTests
    {
        [Test]
        public void Page_DoesNotTurnTapsIntoMouseClicks()
        {
            // The game reads the touch screen itself; a page click on top gave every menu button two clicks.
            string html = File.ReadAllText(Path.Combine(Application.dataPath, "WebGLTemplates", "CarcavelosCircuit", "index.html"));
            StringAssert.DoesNotContain("clickAt(", html);
            StringAssert.DoesNotContain("new MouseEvent(", html);
        }

        [Test]
        public void TouchMenuTargets_AreAtLeast78HudPixelsHigh()
        {
            // 78 HUD px = 36.8 CSS px on an iPhone in landscape with the browser bars (340 px high).
            string uss = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "RaceFlow", "UI", "RaceHud.uss"));
            Assert.GreaterOrEqual(Height(uss, ".hud--touch .menu-pill"), 78);
            Assert.GreaterOrEqual(Height(uss, ".hud--touch .retry-button"), 78);
        }

        [Test]
        public void DriverSelectButtons_AreThumbSizedOnTouch_AndKeepTheirSizeOnAComputer()
        {
            Assert.GreaterOrEqual(DriverPicker.ButtonHeight(true, true), 78f, "READY on touch");
            Assert.GreaterOrEqual(DriverPicker.ButtonHeight(false, true), 78f, "BACK on touch");
            Assert.AreEqual(64f, DriverPicker.ButtonHeight(true, false), "READY with a mouse");
            Assert.AreEqual(48f, DriverPicker.ButtonHeight(false, false), "BACK with a mouse");
        }

        [Test]
        public void UiRectLog_Flag_IsReadFromTheAddress()
        {
            Assert.IsTrue(UiRectLog.HasTestFlag("https://x.io/game/?test=uirects", "uirects"));
            Assert.IsTrue(UiRectLog.HasTestFlag("https://x.io/?net=local&test=itemrows,UIRects#top", "uirects"));
            Assert.IsFalse(UiRectLog.HasTestFlag("https://x.io/?test=itemrows", "uirects"));
            Assert.IsFalse(UiRectLog.HasTestFlag("https://x.io/#test=uirects", "uirects"));
            Assert.IsFalse(UiRectLog.HasTestFlag("https://x.io/?uirects=1", "uirects"));
            Assert.IsFalse(UiRectLog.HasTestFlag(null, "uirects"));
        }

        private static int Height(string uss, string selector)
        {
            Match block = Regex.Match(uss, Regex.Escape(selector) + @"\s*\{([^}]*)\}");
            Assert.IsTrue(block.Success, selector + " block");
            Match height = Regex.Match(block.Groups[1].Value, @"(?<![-\w])height:\s*(\d+)px");
            Assert.IsTrue(height.Success, selector + " height");
            return int.Parse(height.Groups[1].Value);
        }
    }
}
