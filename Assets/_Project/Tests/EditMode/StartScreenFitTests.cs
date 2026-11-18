using System.IO;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Start screen fit (review on 18 Nov 2026): the panel always scales to 720 px height, so a window is
    // 720 x aspect reference pixels wide. Accepted: 16:9 (1280) and 16:10 (1152) keep the normal layout. Edge: 4:3
    // (960) and 5:4 (900) get "hud--compact", where the mode cards and the start leaderboard must not overlap.
    // How it looks is checked in a real Web build (screenshots at 800x600 ... 1920x1080 and phone landscape).
    public class StartScreenFitTests
    {
        [TestCase(16f / 9f, false)]
        [TestCase(16f / 10f, false)]
        [TestCase(3f / 2f, false)]
        [TestCase(4f / 3f, true)]
        [TestCase(5f / 4f, true)]
        public void CompactLayout_OnlyForBoxyWindows(float aspect, bool compact)
        {
            Assert.AreEqual(compact, 720f * aspect < RaceHud.CompactHudWidth);
        }

        [TestCase(4f / 3f, false)]
        [TestCase(5f / 4f, true)]
        [TestCase(16f / 9f, false)]
        public void BoxyLayout_OnlyForFiveToFour(float aspect, bool boxy)
        {
            Assert.AreEqual(boxy, 720f * aspect < RaceHud.BoxyHudWidth);
        }

        [Test]
        public void CompactLayout_CardsAndBoardFitAtFiveToFour()
        {
            // Numbers from UI/StartScreen.uss and ModePicker: padding 28 left / 24 right, 3 cards of 214 + 2 x 7 px,
            // drawn at 70 % from the left edge at 5:4 (hud--boxy); board 33 % of the width between the paddings, 280-330 px.
            const float width = 900f;
            float cardsEnd = 28f + 3f * (214f + 14f) * 0.7f;
            float boardStart = width - 24f - Mathf.Clamp((width - 52f) * 0.33f, 280f, 330f);
            Assert.Less(cardsEnd, boardStart, "the board must start right of the ONLINE RACE card");
        }

        [Test]
        public void CompactLayout_RulesExist()
        {
            string uss = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "RaceFlow", "UI", "StartScreen.uss"));
            StringAssert.Contains(".hud--compact #mode-picker", uss);
            StringAssert.Contains(".hud--compact .lb-host--start", uss);
            StringAssert.Contains(".hud--compact .lb-host--start .lb-title", uss);
            StringAssert.Contains(".hud--compact .lb-host--start .lb-source", uss);
            StringAssert.Contains(".hud--boxy #mode-picker", uss);
            StringAssert.Contains(".hud--boxy #start-menu", uss);
            StringAssert.Contains(".hud--boxy .ts-footer", uss);
        }

        [Test]
        public void KartBuilder_FitsAtFiveToFour()
        {
            // UI/KartBuilder (18 Nov, replaced the driver select): padding, the turntable at its smallest, three columns.
            const float width = 900f;
            float builder = 2f * KartBuilder.SidePadding + KartBuilder.StageMinWidth + KartBuilder.StageGap
                            + 3f * KartBuilder.TileWidth + 2f * KartBuilder.ColumnGap;
            Assert.LessOrEqual(builder, width, "the TYRES column must stay inside a 5:4 window");
        }

        [Test]
        public void StartLineup_StaysAboveThePageControlsLegend()
        {
            // UI/StartLineup (first screen): the page's legend starts at 83 % of the height at 1280 x 720 (two rows).
            float bottom = StartLineup.BottomPercent(0.83f);
            Assert.GreaterOrEqual(bottom, 17f + 1f, "the names must end above the legend");
            Assert.AreEqual(StartLineup.MinBottomPercent, StartLineup.BottomPercent(0.95f), "a low legend keeps the default");
            Assert.AreEqual(StartLineup.MinBottomPercent, StartLineup.BottomPercent(1f), "no legend: default");
            Assert.AreEqual(StartLineup.MinBottomPercent, StartLineup.BottomPercent(0f), "bad value: default");
            Assert.LessOrEqual(StartLineup.BottomPercent(0.2f), 40f, "never squeezed to nothing");
        }

        [Test]
        public void StartLineup_NamesShrinkOnNarrowWindows()
        {
            // One name cell = 1/4 of 92 % of the line-up (46 % of the HUD width). 1280 x 720: 1280 HUD px wide;
            // iPad 1024 x 768: 960 HUD px wide (the HUD is 720 high). "ROOKIE" is about 6 letters of ~0.9 em.
            float wide = StartLineup.NameFontSize(1280f * 0.46f * 0.92f / 4f);
            float ipad = StartLineup.NameFontSize(960f * 0.46f * 0.92f / 4f);
            Assert.AreEqual(19.3f, wide, 0.2f);
            Assert.Less(ipad, wide);
            Assert.LessOrEqual(6f * 0.9f * ipad + 16f, 960f * 0.46f * 0.92f / 4f, "the names keep a gap on an iPad");
            Assert.AreEqual(12f, StartLineup.NameFontSize(10f), "never unreadably small");
        }

        [Test]
        public void KartBuilder_SetTitleFitsOneLine()
        {
            // The longest set name under the turntable at 4:3 (960 HUD px wide; caption = stage minus 2 x 18 px): one line.
            string longest = "AMARA / BEACH BUGGY / BALLOON";
            float stage = 960f - 2f * KartBuilder.SidePadding - KartBuilder.StageGap - 3f * KartBuilder.TileWidth - 2f * KartBuilder.ColumnGap;
            float caption = stage - 36f;
            float size = KartBuilder.TitleFontSize(caption, longest.Length);
            Assert.LessOrEqual(longest.Length * 0.8f * size, caption + 0.5f);
            Assert.GreaterOrEqual(size, 14f, "still readable");
            Assert.AreEqual(20f, KartBuilder.TitleFontSize(900f, 12), "short names keep the full size");
            Assert.AreEqual(20f, KartBuilder.TitleFontSize(0f, 12), "no width yet: the default");
        }

        [Test]
        public void StartLineup_KartsStandNearThePictureBottom()
        {
            Assert.AreEqual(0.5f, KartStage.LineupLookHeight(0.4f), 1e-4f, "a wide picture: the karts' middle");
            Assert.AreEqual(2f * 0.82f, KartStage.LineupLookHeight(2f), 1e-4f, "a tall picture: look up, the ground drops to the bottom");
        }

        [Test]
        public void Turntable_KartKeepsStandingOnItsShadowInTallPictures()
        {
            Assert.AreEqual(0.43f, KartStage.SingleLookHeight(1.3f), 1e-4f, "16:9: unchanged");
            Assert.AreEqual(0.43f * 2f, KartStage.SingleLookHeight(2.6f), 1e-4f, "twice the height: the ground stays at the same share");
        }

        [Test]
        public void TileShots_EmptyPicturesAreFound()
        {
            var empty = new Color32[100];
            var drawn = new Color32[100];
            for (int i = 0; i < 10; i++)
            {
                drawn[i] = new Color32(200, 120, 40, 255);
            }
            Assert.IsFalse(KartStage.HasDrawnPixels(empty, 0.005f), "only the see-through background");
            Assert.IsTrue(KartStage.HasDrawnPixels(drawn, 0.005f), "a kart on it");
            Assert.IsFalse(KartStage.HasDrawnPixels(null, 0.005f));
        }
    }
}
