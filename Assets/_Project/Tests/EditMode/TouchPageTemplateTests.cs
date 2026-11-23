using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Phone round 17.11 (Role 4): the Web page's touch buttons and home screen files. The page is plain HTML + JS
    // (Assets/WebGLTemplates/CarcavelosCircuit), so these tests read the files and check the contract with the game:
    // every button presses a key the page's script knows, every key the game needs on a phone has a button, the
    // home screen manifest is valid and its icons exist, and the HUD has the touch layout the buttons rely on.
    // How it looks and feels is checked in a real Web build (by hand and with browser scripts, docs/verification.md).
    public class TouchPageTemplateTests
    {
        private static string TemplateDir
        {
            get { return Path.Combine(Application.dataPath, "WebGLTemplates", "CarcavelosCircuit"); }
        }

        private static string IndexHtml()
        {
            return File.ReadAllText(Path.Combine(TemplateDir, "index.html"));
        }

        [Test]
        public void EveryTouchButtonKey_IsInTheKeyTable()
        {
            string html = IndexHtml();
            int buttons = 0;
            foreach (Match button in Regex.Matches(html, "data-keys=\"([^\"]+)\""))
            {
                foreach (string code in button.Groups[1].Value.Split(' '))
                {
                    StringAssert.IsMatch("\\n\\s*" + code + ": \\{ key:", html, code + " is pressed by a button but missing in KEYS");
                }
                buttons++;
            }
            Assert.GreaterOrEqual(buttons, 10, "steer x2, gas, brake, drift, item, trick, recover, retry, pause");
        }

        [TestCase("KeyA", "Steer left")]
        [TestCase("KeyD", "Steer right")]
        [TestCase("KeyW", "Gas")]
        [TestCase("KeyS", "Brake and reverse")]
        [TestCase("KeyW Space", "Drift")]
        [TestCase("KeyE", "Use item")]
        [TestCase("ShiftLeft", "Trick (in the air)")]
        [TestCase("KeyR", "Recover to the last checkpoint")]
        [TestCase("Enter", "Retry from the start")]
        [TestCase("KeyP", "Pause")]
        public void EveryGameKey_HasATouchButton(string keys, string label)
        {
            StringAssert.Contains("data-keys=\"" + keys + "\"", IndexHtml(), label + " needs a touch button");
            StringAssert.Contains("aria-label=\"" + label + "\"", IndexHtml());
        }

        [Test]
        public void HomeScreen_ManifestIsValidAndItsIconsExist()
        {
            string data = Path.Combine(TemplateDir, "TemplateData");
            string json = File.ReadAllText(Path.Combine(data, "manifest.webmanifest"));
            Manifest manifest = JsonUtility.FromJson<Manifest>(json);
            Assert.AreEqual("landscape", manifest.orientation);
            Assert.AreEqual("../", manifest.start_url, "the manifest sits in TemplateData, the game one folder up");
            StringAssert.IsMatch("^(fullscreen|standalone)$", manifest.display);
            Assert.GreaterOrEqual(manifest.icons.Length, 2);
            foreach (ManifestIcon icon in manifest.icons)
            {
                Assert.IsTrue(File.Exists(Path.Combine(data, icon.src)), icon.src + " is missing");
            }
            Assert.IsTrue(File.Exists(Path.Combine(data, "icon-180.png")), "apple-touch-icon");
            string html = IndexHtml();
            StringAssert.Contains("rel=\"manifest\" href=\"TemplateData/manifest.webmanifest\"", html);
            StringAssert.Contains("apple-mobile-web-app-capable", html);
        }

        [Test]
        public void Hud_HasTheTouchLayoutTheButtonsLeaveRoomFor()
        {
            string uss = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "RaceFlow", "UI", "RaceHud.uss"));
            StringAssert.Contains(".hud--touch .drift-panel", uss);
            StringAssert.Contains(".hud--touch .speed-gauge", uss);
            StringAssert.Contains(".hud--touch .controls-hint", uss);
            string css = File.ReadAllText(Path.Combine(TemplateDir, "TemplateData", "style.css"));
            StringAssert.Contains("var(--cc-r", css, "touch buttons are sized in the HUD's own unit");
        }

        // iPhone report 23.11: fast taps on ITEM zoomed the page in (iOS ignores user-scalable=no).
        [TestCase("gesturestart")]
        [TestCase("gesturechange")]
        [TestCase("dblclick")]
        public void TouchPage_CancelsSafarisZoomGestures(string eventName)
        {
            StringAssert.Contains("addEventListener(\"" + eventName + "\", noZoom, { passive: false })", IndexHtml(),
                eventName + " must be cancelled (a passive listener cannot cancel it)");
        }

        [Test]
        public void TouchPage_EveryElementSaysNoTouchZoom_ButErrorDetailsStillScroll()
        {
            string css = File.ReadAllText(Path.Combine(TemplateDir, "TemplateData", "style.css"));
            StringAssert.IsMatch("\\.cc-touch-mode \\* \\{\\s*touch-action: none;", css, "the canvas and the gaps, not only html/body");
            StringAssert.IsMatch("\\.cc-touch-mode \\.cc-details p \\{\\s*touch-action: pan-y;", css, "the error text can still scroll");
        }

        [Test]
        public void TouchPage_ZoomGuards_LeaveNormalTouchesAlone()
        {
            string html = IndexHtml();
            // Rejected case: two thumbs moving on Android/Chrome have no event.scale, so their moves are never cancelled.
            StringAssert.Contains("typeof event.scale === \"number\" && event.scale !== 1", html);
            // Edge cases: the unzoom runs only in touch mode, only when zoomed, at most once a second, after the last finger.
            StringAssert.Contains("window.visualViewport.scale <= 1.01", html);
            StringAssert.Contains("Date.now() - lastUnzoom < 1000", html);
            StringAssert.Contains("if (event.touches.length === 0) {\n          setTimeout(undoZoom", html.Replace("\r\n", "\n"));
        }

        [System.Serializable]
        private class Manifest
        {
            public string start_url;
            public string display;
            public string orientation;
            public ManifestIcon[] icons;
        }

        [System.Serializable]
        private class ManifestIcon
        {
            public string src;
        }
    }
}
