using System.IO;
using System.Text.RegularExpressions;
using CarcavelosCircuit.RaceFlow.Editor;
using CarcavelosCircuit.Visuals.Editor;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Load-time pass for the Web build (Role 4, 17 Nov 2026): Web-only texture and mesh budgets, the scenes the Web
    // player leaves out, and the build id that keeps the page from mixing a new build with cached old files.
    public class WebLoadTimeTests
    {
        private const string World = "Assets/_Project/Visuals/CampusWorld/Textures/";
        private const string Kart = "Assets/_Project/Visuals/Kart/Textures/";
        private const string TemplatePath = "Assets/WebGLTemplates/CarcavelosCircuit/index.html";

        // ---------- Texture budgets ----------

        [TestCase("Char_Rookie_BaseColor.png", 1024)]
        [TestCase("Char_Tiago_BaseColor.png", 1024)]
        [TestCase("Char_Nova_Normal.png", 512)]
        [TestCase("Char_Amara_MetallicSmoothness.png", 256)]
        [TestCase("f021_head_wetsuit.jpg", 512)]
        [TestCase("m021_body_wetsuit.jpg", 512)]
        [TestCase("KartLOD_Palette_Colour.png", 0)]
        [TestCase("TyreFar_Normal.png", 128)]
        [TestCase("TyreFar_Retro_Colour.png", 128)]
        [TestCase("ground_detail_data.png", 0)]
        [TestCase("C1_hatchback_normal.png", 512)]
        public void MaxWebSize_DriversAndSurfersHaveAWebSize(string file, int expected)
        {
            Assert.AreEqual(expected, WebTextureBudget.MaxWebSize(file));
        }

        [Test]
        public void EveryDriverTexture_HasAWebSize()
        {
            // A new driver (Char_<Name>_BaseColor/Normal/MetallicSmoothness) gets the budget only if it keeps that naming.
            string[] files = Directory.GetFiles(Kart, "Char_*.png");
            Assert.GreaterOrEqual(files.Length, 12, "expected the four drivers' maps in " + Kart);
            foreach (string path in files)
            {
                Assert.Greater(WebTextureBudget.MaxWebSize(Path.GetFileName(path)), 0, path + " has no Web size");
            }
        }

        [Test]
        public void DriverWithoutOverride_GetsTheBudgetSize_NotCrunched()
        {
            WebLoadBudget.WebTextureDecision d = WebLoadBudget.DecideTexture(Kart + "Char_Tiago_BaseColor.png", false, 2048, false);
            Assert.AreEqual(1024, d.Size);
            Assert.IsFalse(d.Crunch, "drivers are seen up close in the driver select: plain DXT");
        }

        [Test]
        public void BudgetSizedWorldTexture_IsCrunched_SizeKept()
        {
            WebLoadBudget.WebTextureDecision d = WebLoadBudget.DecideTexture(World + "C1_hatchback_normal.png", true, 512, false);
            Assert.AreEqual(0, d.Size, "the budget's own override stays");
            Assert.IsTrue(d.Crunch);
        }

        [Test]
        public void HandSetOverride_IsKept()
        {
            WebLoadBudget.WebTextureDecision d = WebLoadBudget.DecideTexture(World + "C1_hatchback_normal.png", true, 1024, false);
            Assert.AreEqual(0, d.Size);
            Assert.IsFalse(d.Crunch);
        }

        [TestCase("sky_panorama.png")]
        [TestCase("veg_leaves_cutout_albedo.png")]
        [TestCase("asphalt_metallic_smoothness.png")]
        public void SkiesMasksAndCutouts_StayPlainDxt(string file)
        {
            int size = WebTextureBudget.MaxWebSize(file);
            Assert.Greater(size, 0);
            Assert.IsFalse(WebLoadBudget.DecideTexture(World + file, true, size, false).Crunch);
        }

        [Test]
        public void AlreadyCrunched_OrNoBudget_NothingChanges()
        {
            Assert.IsFalse(WebLoadBudget.DecideTexture(World + "C3_suv_albedo.jpg", true, 512, true).Crunch);
            WebLoadBudget.WebTextureDecision none = WebLoadBudget.DecideTexture(World + "ground_detail_data.png", false, 2048, false);
            Assert.AreEqual(0, none.Size);
            Assert.IsFalse(none.Crunch);
        }

        // ---------- Mesh compression ----------

        [TestCase("Assets/_Project/Visuals/Kart/Models/Kart_Buggy.fbx", true)]
        [TestCase("Assets/_Project/Visuals/Kart/Models/Char_Amara.fbx", true)]
        [TestCase("Assets/_Project/Visuals/Kart/Models/Tyre_Slick_Far.fbx", true)]
        [TestCase("Assets/_Project/Ambience/ThirdParty/Rocketbox/Sports_Male_01/Sports_Male_01.fbx", true)]
        [TestCase("Assets/_Project/Visuals/CampusWorld/Models/CampusWorld.fbx", false)]
        [TestCase("Assets/_Project/Visuals/Kart/Textures/TyreFar_Normal.png", false)]
        public void MediumMeshCompression_ForKartsDriversTyresSurfers(string path, bool expected)
        {
            Assert.AreEqual(expected, WebLoadBudget.UsesMediumCompression(path));
        }

        // ---------- Scenes ----------

        [Test]
        public void WebScenes_LeaveOutTheEmptyAndTheUnusedTrack_WhenTheWorldIsIn()
        {
            string[] all =
            {
                "Assets/_Project/RaceFlow/Scenes/Main.unity",
                "Assets/_Project/Track/Scenes/Track_Campus.unity",
                Role4WebBuild.WorldScene,
                "Assets/_Project/Track/Scenes/Track_NovaCampus.unity",
                "Assets/_Project/RaceFlow/Scenes/RaceFlow_PlaceholderTrack.unity",
            };
            CollectionAssert.AreEqual(new[] { all[0], all[2], all[4] }, Role4WebBuild.FilterWebScenes(all));
        }

        [Test]
        public void WebScenes_KeepEverything_WithoutTheWorld()
        {
            string[] all = { "Assets/_Project/RaceFlow/Scenes/Main.unity", "Assets/_Project/Track/Scenes/Track_Campus.unity" };
            CollectionAssert.AreEqual(all, Role4WebBuild.FilterWebScenes(all));
        }

        // ---------- Build id ----------

        [Test]
        public void StampHtml_FillsTheIdAndVersionsTemplateDataLinks()
        {
            string html = "<link href=\"TemplateData/style.css\"><img src=\"TemplateData/keyart-1.jpg\">"
                + "<img src=\"TemplateData/a.png?x=1\"><script src=\"Build/abc.loader.js\"></script>"
                + "<script>var buildId = \"" + Role4WebBuild.BuildIdPlaceholder + "\";</script>";
            string stamped = Role4WebBuild.StampHtml(html, "0123456789ab", null);
            StringAssert.Contains("href=\"TemplateData/style.css?v=0123456789ab\"", stamped);
            StringAssert.Contains("src=\"TemplateData/keyart-1.jpg?v=0123456789ab\"", stamped);
            StringAssert.Contains("src=\"TemplateData/a.png?x=1\"", stamped);   // already has a query: left alone
            StringAssert.Contains("src=\"Build/abc.loader.js\"", stamped);      // hashed names need nothing
            StringAssert.Contains("var buildId = \"0123456789ab\"", stamped);
            StringAssert.DoesNotContain(Role4WebBuild.BuildIdPlaceholder, stamped);
        }

        [Test]
        public void StampHtml_FillsThePhoneDataName_OnlyWhenThereIsOne()
        {
            string html = "var phoneDataFile = \"" + Role4WebBuild.PhoneDataPlaceholder + "\";";
            StringAssert.Contains("\"0f0f.data\"", Role4WebBuild.StampHtml(html, "id", "0f0f.data"));
            StringAssert.Contains(Role4WebBuild.PhoneDataPlaceholder, Role4WebBuild.StampHtml(html, "id", null)); // page keeps DXT
        }

        [Test]
        public void StampCss_VersionsRelativeUrls_Only()
        {
            string css = "a{src:url(\"Font.ttf\") format(\"truetype\")} b{mask:url('logo-word.png') 0 0}"
                + " c{background:url(plain.jpg)} d{background:url(\"data:image/svg+xml,%3Csvg%3E\")}"
                + " e{background:url(https://example.org/x.png)}";
            string stamped = Role4WebBuild.StampCss(css, "id1");
            StringAssert.Contains("url(\"Font.ttf?v=id1\")", stamped);
            StringAssert.Contains("url('logo-word.png?v=id1')", stamped);
            StringAssert.Contains("url(plain.jpg?v=id1)", stamped);
            StringAssert.Contains("url(\"data:image/svg+xml,%3Csvg%3E\")", stamped);
            StringAssert.Contains("url(https://example.org/x.png)", stamped);
        }

        [Test]
        public void BuildId_SameFilesSameId_ChangedFileNewId_VersionJsonIgnored()
        {
            string folder = Path.Combine(Path.GetTempPath(), "cc-buildid-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(folder, "TemplateData"));
                File.WriteAllText(Path.Combine(folder, "index.html"), "page");
                File.WriteAllText(Path.Combine(folder, "TemplateData", "style.css"), "css");
                string first = Role4WebBuild.BuildId(folder);
                Assert.IsTrue(Regex.IsMatch(first, "^[0-9a-f]{12}$"), first);
                File.WriteAllText(Path.Combine(folder, "version.json"), Role4WebBuild.VersionJson(first));
                Assert.AreEqual(first, Role4WebBuild.BuildId(folder));
                File.WriteAllText(Path.Combine(folder, "TemplateData", "style.css"), "css 2");
                Assert.AreNotEqual(first, Role4WebBuild.BuildId(folder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void VersionJson_IsTheShapeThePageReads()
        {
            Assert.AreEqual("{\"build\":\"abc\"}\n", Role4WebBuild.VersionJson("abc"));
        }

        [Test]
        public void Template_ChecksForNewerBuilds_AndCachesHashedDataForGood()
        {
            // Guards the loader hunk of index.html against merges that drop it.
            string html = File.ReadAllText(TemplatePath);
            StringAssert.Contains("\"" + Role4WebBuild.BuildIdPlaceholder + "\"", html);
            StringAssert.Contains("version.json?t=", html);
            StringAssert.Contains("cc-build", html);
            StringAssert.Contains("\"immutable\"", html);
            StringAssert.Contains("startDownload();", html); // phones start the download at once
            StringAssert.Contains("\"" + Role4WebBuild.PhoneDataPlaceholder + "\"", html); // ETC2 data for phones
            StringAssert.Contains("dataUrl: pickDataUrl()", html);
        }
    }
}
