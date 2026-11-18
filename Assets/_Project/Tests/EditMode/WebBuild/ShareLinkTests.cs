using System.IO;
using System.Text.RegularExpressions;
using CarcavelosCircuit.RaceFlow.Editor;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Share preview (18 Nov 2026): the tab icon, the home screen icons and the link preview card of the Web page.
    // Accepted: the public address from the command line, from the repository on GitHub Actions, or the default.
    // Rejected: addresses that are not plain https (http, javascript:, quotes that would break the HTML), malformed
    // repository names. Edge cases: a repository named owner.github.io (served at the root), upper case owners,
    // a missing trailing slash, and the build id stamp that must leave the absolute card address alone.
    public class ShareLinkTests
    {
        private const string TemplateDir = "Assets/WebGLTemplates/CarcavelosCircuit";
        private static readonly string Data = Path.Combine(TemplateDir, "TemplateData");

        // ---------- The one public address ----------

        [TestCase("belschak/carcavelos-circuit", "https://belschak.github.io/carcavelos-circuit/")]
        [TestCase("Carcavelos-Circuit/carcavelos-circuit", "https://carcavelos-circuit.github.io/carcavelos-circuit/")]
        [TestCase("carcavelos-circuit/carcavelos-circuit.github.io", "https://carcavelos-circuit.github.io/")]
        [TestCase("carcavelos-circuit/Carcavelos-Circuit.GitHub.io", "https://carcavelos-circuit.github.io/")]
        [TestCase("  belschak/nova.kart_2  ", "https://belschak.github.io/nova.kart_2/")]
        public void PagesUrl_FollowsTheRepository(string repository, string expected)
        {
            Assert.AreEqual(expected, ShareLinkStamp.PagesUrl(repository));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("carcavelos-circuit")]
        [TestCase("a/b/c")]
        [TestCase("own er/repo")]
        [TestCase("owner/\"><script>")]
        [TestCase("/repo")]
        public void PagesUrl_RejectsMalformedRepositories(string repository)
        {
            Assert.IsNull(ShareLinkStamp.PagesUrl(repository));
        }

        [Test]
        public void SiteUrl_CommandLineWinsOverTheRepository()
        {
            string url = ShareLinkStamp.SiteUrl(new[] { "Unity.exe", "-batchmode", "-siteUrl=https://play.example.org/kart" }, "belschak/carcavelos-circuit");
            Assert.AreEqual("https://play.example.org/kart/", url, "a trailing slash is added");
        }

        [Test]
        public void SiteUrl_UsesTheRepositoryOnGitHubActions()
        {
            Assert.AreEqual("https://carcavelos-circuit.github.io/",
                ShareLinkStamp.SiteUrl(new[] { "Unity.exe" }, "carcavelos-circuit/carcavelos-circuit.github.io"));
        }

        [Test]
        public void SiteUrl_FallsBackToTheDefaultOnALaptop()
        {
            Assert.AreEqual(ShareLinkStamp.DefaultSiteUrl, ShareLinkStamp.SiteUrl(new string[0], null));
            Assert.AreEqual(ShareLinkStamp.DefaultSiteUrl, ShareLinkStamp.SiteUrl(null, ""));
            StringAssert.StartsWith("https://", ShareLinkStamp.DefaultSiteUrl);
            StringAssert.EndsWith("/", ShareLinkStamp.DefaultSiteUrl);
        }

        [TestCase("-siteUrl=http://belschak.github.io/carcavelos-circuit/")]
        [TestCase("-siteUrl=javascript:alert(1)")]
        [TestCase("-siteUrl=https://evil.example/\"><script>alert(1)</script>")]
        [TestCase("-siteUrl=https://exa mple.org/")]
        [TestCase("-siteUrl=")]
        public void SiteUrl_IgnoresUnsafeCommandLineAddresses(string argument)
        {
            Assert.AreEqual("https://belschak.github.io/carcavelos-circuit/",
                ShareLinkStamp.SiteUrl(new[] { argument }, "belschak/carcavelos-circuit"));
        }

        // ---------- The page head ----------

        private static string Head()
        {
            string html = File.ReadAllText(Path.Combine(TemplateDir, "index.html"));
            int end = html.IndexOf("</head>", System.StringComparison.Ordinal);
            Assert.Greater(end, 0, "index.html has a head");
            return html.Substring(0, end);
        }

        [Test]
        public void Head_HasTheLinkPreviewTagsWithAbsoluteAddresses()
        {
            string head = ShareLinkStamp.Stamp(Head(), "https://example.org/game/");
            StringAssert.DoesNotContain(ShareLinkStamp.Placeholder, head);
            StringAssert.Contains("<meta property=\"og:image\" content=\"https://example.org/game/TemplateData/og-card.jpg\">", head);
            StringAssert.Contains("<meta name=\"twitter:image\" content=\"https://example.org/game/TemplateData/og-card.jpg\">", head);
            StringAssert.Contains("<meta property=\"og:url\" content=\"https://example.org/game/\">", head);
            StringAssert.Contains("<meta name=\"twitter:card\" content=\"summary_large_image\">", head);
            StringAssert.Contains("<meta property=\"og:image:width\" content=\"1200\">", head);
            StringAssert.Contains("<meta property=\"og:image:height\" content=\"630\">", head);
            StringAssert.Contains("<meta property=\"og:title\" content=\"Nova Kart: Carcavelos Circuit\">", head);
            // og:image and twitter:image must never be relative: crawlers do not resolve them against the page.
            foreach (Match m in Regex.Matches(head, "(?:og:image|twitter:image)\" content=\"([^\"]*)\""))
            {
                StringAssert.StartsWith("https://", m.Groups[1].Value);
            }
        }

        [Test]
        public void Head_BuildIdStampLeavesTheAbsoluteCardAddressAlone()
        {
            string head = Role4WebBuild.StampHtml(ShareLinkStamp.Stamp(Head(), "https://example.org/"), "0123456789ab", null);
            StringAssert.Contains("content=\"https://example.org/TemplateData/og-card.jpg\"", head);
            StringAssert.Contains("href=\"TemplateData/favicon.svg?v=0123456789ab\"", head, "the tab icon still gets the build id");
        }

        [Test]
        public void Head_LinksEveryTabIconAndTheFilesExist()
        {
            string head = Head();
            foreach (string file in new[] { "favicon.svg", "favicon.ico", "favicon-32.png", "favicon-16.png", "icon-180.png" })
            {
                StringAssert.Contains("href=\"TemplateData/" + file + "\"", head, file + " is linked");
                Assert.IsTrue(File.Exists(Path.Combine(Data, file)), file + " exists");
            }
            Assert.IsTrue(File.Exists(Path.Combine(Data, "favicon-48.png")));
            StringAssert.DoesNotContain("data:image/svg+xml", head, "the old inline diamond is gone");
        }

        // ---------- The files ----------

        [TestCase("og-card.jpg", 1200, 630)]
        [TestCase("icon-180.png", 180, 180)]
        [TestCase("icon-192.png", 192, 192)]
        [TestCase("icon-512.png", 512, 512)]
        [TestCase("icon-maskable-192.png", 192, 192)]
        [TestCase("icon-maskable-512.png", 512, 512)]
        [TestCase("favicon-16.png", 16, 16)]
        [TestCase("favicon-32.png", 32, 32)]
        [TestCase("favicon-48.png", 48, 48)]
        public void Image_HasItsSize(string file, int width, int height)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(Path.Combine(Data, file))), file + " loads");
                Assert.AreEqual(width, texture.width, file);
                Assert.AreEqual(height, texture.height, file);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Card_StaysUnderThreeHundredKilobytes()
        {
            // WhatsApp and some other apps drop large preview images.
            Assert.LessOrEqual(new FileInfo(Path.Combine(Data, "og-card.jpg")).Length, 300 * 1024);
        }

        [Test]
        public void Favicon_IcoHoldsThreeSizes()
        {
            byte[] ico = File.ReadAllBytes(Path.Combine(Data, "favicon.ico"));
            Assert.AreEqual(0, ico[0]);
            Assert.AreEqual(1, ico[2], "type 1 = icon");
            Assert.AreEqual(3, ico[4] | (ico[5] << 8), "16, 32 and 48 px");
        }
    }
}
