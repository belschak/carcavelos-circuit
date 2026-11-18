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
            StringAssert.Contains(".hud--compact #driver-picker", uss);
            StringAssert.Contains(".hud--boxy #mode-picker", uss);
            StringAssert.Contains(".hud--boxy #start-menu", uss);
            StringAssert.Contains(".hud--boxy .ts-footer", uss);
        }

        [Test]
        public void CompactLayout_DriverSelectFitsAtFiveToFour()
        {
            // UI/DriverPicker: 4 cards of 184 px + 2 x 6 px margin each; ts-layout padding 64 left / 56 right.
            // Drawn at 80 % from its left edge (StartScreen.uss), so the row must end inside the narrowest compact window.
            const float width = 900f;
            float rowEnd = 64f + 4f * (184f + 12f) * 0.8f;
            Assert.Less(rowEnd, width - 56f, "the 4th driver card must stay inside a 5:4 window");
        }
    }
}
