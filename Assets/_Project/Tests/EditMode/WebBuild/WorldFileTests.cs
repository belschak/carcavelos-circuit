using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CarcavelosCircuit.RaceFlow.Editor;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Load time P8 (18 Nov 2026): the campus world as its own file next to the Web build (Role4WebBuild world part,
    // RaceFlow/WorldStream.cs, index.html "Campus world as its own file"). The download itself can only be checked in a
    // real Web build (docs/history/2026-11-18-load-time.md has the browser runs); these guard the build rules and the page hunks.
    public class WorldFileTests
    {
        private const string TemplatePath = "Assets/WebGLTemplates/CarcavelosCircuit/index.html";
        private const string JsLibPath = "Assets/_Project/RaceFlow/Web/CarcavelosWebPage.jslib";

        [Test]
        public void PlayerScenes_LeaveOutOnlyTheWorld()
        {
            string[] web = { "Assets/_Project/RaceFlow/Scenes/Main.unity", Role4WebBuild.WorldScene, "Assets/_Project/RaceFlow/Scenes/RaceFlow_PlaceholderTrack.unity" };
            CollectionAssert.AreEqual(new[] { web[0], web[2] }, Role4WebBuild.PlayerScenes(web));
        }

        [Test]
        public void WorldFileName_IsTheContentHash()
        {
            string a = Role4WebBuild.WorldFileName(Encoding.UTF8.GetBytes("world one"));
            StringAssert.IsMatch(@"^world-[0-9a-f]{32}\.bundle$", a);
            Assert.AreEqual(a, Role4WebBuild.WorldFileName(Encoding.UTF8.GetBytes("world one")), "same world = same name = stays cached");
            Assert.AreNotEqual(a, Role4WebBuild.WorldFileName(Encoding.UTF8.GetBytes("world two")), "a changed world gets a new name");
        }

        [Test]
        public void StampWorldHtml_FillsNamesAndShare()
        {
            string html = File.ReadAllText(TemplatePath);
            var files = new Role4WebBuild.WorldFiles { Dxt = "world-aa.bundle", Etc2 = "world-bb.bundle", Share = 0.6666f };
            string stamped = Role4WebBuild.StampWorldHtml(html, files);
            StringAssert.Contains("dxt: \"world-aa.bundle\"", stamped);
            StringAssert.Contains("etc2: \"world-bb.bundle\"", stamped);
            StringAssert.Contains("var worldShare = 0.667;", stamped);
            StringAssert.DoesNotContain(Role4WebBuild.WorldDxtPlaceholder, stamped);
        }

        [Test]
        public void StampWorldHtml_WithoutEtc2_PhonesUseTheDxtWorld()
        {
            string html = File.ReadAllText(TemplatePath);
            var files = new Role4WebBuild.WorldFiles { Dxt = "world-aa.bundle", Etc2 = null, Share = 0.5f };
            StringAssert.Contains("\"" + Role4WebBuild.WorldEtc2Placeholder + "\"", Role4WebBuild.StampWorldHtml(html, files));
        }

        [Test]
        public void Template_UnstampedPage_HasNoWorldFile()
        {
            // A build without the world file (or an older template) must load exactly as before.
            string html = File.ReadAllText(TemplatePath);
            StringAssert.Contains("dxt: \"" + Role4WebBuild.WorldDxtPlaceholder + "\"", html);
            StringAssert.Contains("var worldShare = " + Role4WebBuild.WorldSharePlaceholder + ";", html);
            StringAssert.Contains("worldStreamed = worldFiles.dxt.indexOf(\"__\") !== 0", html);
        }

        [Test]
        public void Template_CachesWorldFileForGood_AndWaitsForTheTrack()
        {
            string html = File.ReadAllText(TemplatePath);
            Match cache = Regex.Match(html, @"cacheControl: function \(url\) \{(.*?)no-store", RegexOptions.Singleline);
            Assert.IsTrue(cache.Success);
            StringAssert.Contains(@"world-[0-9a-f]{32}\.bundle", cache.Groups[1].Value);
            StringAssert.Contains("window.ccWorldBundleUrl", html);
            StringAssert.Contains("addEventListener(\"cc-world\"", html);
            StringAssert.Contains("createUnityInstance(canvas, config, onGameProgress)", html);
            StringAssert.Contains("waitingInstance = unityInstance", html);
        }

        [Test]
        public void JsLib_HasTheWorldCalls()
        {
            string jslib = File.ReadAllText(JsLibPath);
            StringAssert.Contains("CarcavelosWorldBundleUrl: function", jslib);
            StringAssert.Contains("CarcavelosWorldState: function", jslib);
            StringAssert.Contains("\"cc-world\"", jslib);
        }
    }
}
