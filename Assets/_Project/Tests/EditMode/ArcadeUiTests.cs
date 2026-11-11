using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests for the arcade UI restyle (task R15): the plate pictures, the overshoot easing, the start prompt,
    // the leaderboard row order and the "7/16" name counter. Accepted, rejected and edge cases for each rule.
    public class ArcadeUiTests
    {
        // ---------- PlateArt: rounded rectangle coverage ----------

        [Test]
        public void RoundedRect_MiddleIsCovered_FarOutsideIsNot()
        {
            Assert.AreEqual(1f, PlateArt.RoundedRectCoverage(50f, 50f, 0f, 0f, 100f, 100f, 20f), 0.001f);
            Assert.AreEqual(0f, PlateArt.RoundedRectCoverage(150f, 50f, 0f, 0f, 100f, 100f, 20f), 0.001f);
        }

        [Test]
        public void RoundedRect_CornerIsCutAway_ButASquareCornerIsNot()
        {
            // Pixel (0,0) is inside the square box but outside the rounded corner.
            Assert.AreEqual(0f, PlateArt.RoundedRectCoverage(0f, 0f, 0f, 0f, 100f, 100f, 20f), 0.001f);
            Assert.AreEqual(1f, PlateArt.RoundedRectCoverage(0f, 0f, 0f, 0f, 100f, 100f, 0f), 0.001f);
        }

        [Test]
        public void RoundedRect_EdgeIsSoft_ForAntiAliasing()
        {
            // A pixel whose centre sits exactly on the edge is half covered.
            float edge = PlateArt.RoundedRectCoverage(99.5f, 50f, 0f, 0f, 100f, 100f, 20f);
            Assert.That(edge, Is.GreaterThan(0.2f).And.LessThan(0.8f));
        }

        [Test]
        public void RoundedRect_WithNoSizeOrHugeRadius_StaysSafe()
        {
            Assert.AreEqual(0f, PlateArt.RoundedRectCoverage(0f, 0f, 0f, 0f, 0f, 10f, 4f), "zero width covers nothing");
            Assert.AreEqual(0f, PlateArt.RoundedRectCoverage(0f, 0f, 0f, 0f, -5f, 10f, 4f), "negative width covers nothing");
            // A radius bigger than half the box is clamped: the middle is still covered (a circle).
            Assert.AreEqual(1f, PlateArt.RoundedRectCoverage(50f, 50f, 0f, 0f, 100f, 100f, 500f), 0.001f);
        }

        [Test]
        public void Shield_IsFullAtTheTop_AndPointedAtTheBottom()
        {
            Assert.AreEqual(1f, PlateArt.ShieldCoverage(20f, 10f, 0f, 0f, 40f, 46f), 0.001f, "upper middle");
            Assert.AreEqual(0f, PlateArt.ShieldCoverage(1f, 44f, 0f, 0f, 40f, 46f), 0.001f, "bottom corner is cut away (the point)");
            Assert.Greater(PlateArt.ShieldCoverage(19.5f, 44f, 0f, 0f, 40f, 46f), 0f, "the tip is in the middle");
            Assert.AreEqual(0f, PlateArt.ShieldCoverage(5f, 5f, 0f, 0f, 0f, 46f), "no size covers nothing");
        }

        // ---------- PlateArt: whole pictures ----------

        [Test]
        public void Plate_HasFillInTheMiddle_ShadowAtTheBottom_ClearCorners()
        {
            PlateLook look = new PlateLook(Color.red, Color.red, Color.white, Color.black, 10f, 2f, 0f, 6f);
            Color[] pixels = PlateArt.Plate(40, 46, look);
            Assert.AreEqual(40 * 46, pixels.Length);
            Color middle = pixels[20 * 40 + 20];
            Assert.AreEqual(1f, middle.r, 0.01f, "fill colour in the middle");
            Assert.AreEqual(0f, middle.g, 0.01f);
            Color shadow = pixels[44 * 40 + 20]; // 2 rows from the bottom, below the body
            Assert.AreEqual(0f, shadow.r, 0.01f, "the hard shadow is below the body");
            Assert.Greater(shadow.a, 0.9f);
            Assert.AreEqual(0f, pixels[0].a, 0.01f, "the top-left corner is rounded off (see-through)");
        }

        [Test]
        public void Plate_WithNoSize_IsEmpty_NotAnError()
        {
            PlateLook look = new PlateLook(Color.red, Color.red, Color.white, Color.black, 10f, 2f, 0f, 6f);
            Assert.AreEqual(0, PlateArt.Plate(0, 10, look).Length);
            Assert.AreEqual(0, PlateArt.Shield(-1, 10, look).Length);
            Assert.AreEqual(0, PlateArt.AzulejoFrame(0, Color.blue, Color.blue, Color.white, Color.black).Length);
        }

        [Test]
        public void Over_BlendsLikeNormalTransparency()
        {
            Color result = PlateArt.Over(Color.white, new Color(0f, 0f, 0f, 0.5f));
            Assert.AreEqual(0.5f, result.r, 0.01f, "half black over white = grey");
            Assert.AreEqual(1f, result.a, 0.01f);
            Color nothing = PlateArt.Over(new Color(0f, 0f, 0f, 0f), new Color(1f, 1f, 1f, 0f));
            Assert.AreEqual(0f, nothing.a, 0.0001f, "clear over clear stays clear (no division by zero)");
        }

        // ---------- HudEasing.OutBack ----------

        [Test]
        public void OutBack_StartsAtZero_EndsAtOne_AndOvershoots()
        {
            Assert.AreEqual(0f, HudEasing.OutBack(0f), 0.0001f);
            Assert.AreEqual(1f, HudEasing.OutBack(1f), 0.0001f);
            Assert.Greater(HudEasing.OutBack(0.7f), 1f, "goes a little past the end before it settles");
        }

        [Test]
        public void OutBack_ClampsOutsideZeroToOne()
        {
            Assert.AreEqual(0f, HudEasing.OutBack(-3f), 0.0001f);
            Assert.AreEqual(1f, HudEasing.OutBack(9f), 0.0001f);
        }

        // ---------- Start prompt ----------

        [Test]
        public void StartPrompt_FewWords_PhoneOrKeyboard()
        {
            Assert.AreEqual("TAP TO START", RaceHud.StartPrompt(true));
            Assert.AreEqual("PRESS ANY KEY", RaceHud.StartPrompt(false));
        }

        // ---------- Leaderboard: who slides in when ----------

        [Test]
        public void RowDelay_RowsComeOneAfterAnother()
        {
            Assert.AreEqual(0, LeaderboardPanel.RowDelayMs(0, -1, 10));
            Assert.AreEqual(45, LeaderboardPanel.RowDelayMs(1, -1, 10));
            Assert.AreEqual(405, LeaderboardPanel.RowDelayMs(9, -1, 10));
        }

        [Test]
        public void RowDelay_YourOwnRowComesLast_EvenAtTheTop()
        {
            int own = LeaderboardPanel.RowDelayMs(0, 0, 10);
            Assert.Greater(own, LeaderboardPanel.RowDelayMs(9, 0, 10), "your row (1st place) still lands after the 10th row");
            Assert.AreEqual(10 * 45 + 120, own);
        }

        [Test]
        public void RowDelay_BadRowNumber_IsZero()
        {
            Assert.AreEqual(0, LeaderboardPanel.RowDelayMs(-1, -1, 10));
        }

        // ---------- Name counter ----------

        [Test]
        public void CountLine_CountsTheLetters()
        {
            Assert.AreEqual("7/16", SaveTimeForm.CountLine("Ana Rui"));
            Assert.AreEqual("16/16", SaveTimeForm.CountLine("Sixteen Letters!"));
        }

        [Test]
        public void CountLine_EndSpacesDoNotCount_EmptyAndNullAreZero()
        {
            Assert.AreEqual("3/16", SaveTimeForm.CountLine("  Ana  "), "spaces at the ends are removed when saving");
            Assert.AreEqual("0/16", SaveTimeForm.CountLine(""));
            Assert.AreEqual("0/16", SaveTimeForm.CountLine(null));
        }

        [Test]
        public void CountLine_TooLong_ShowsTheRealCount()
        {
            // The phone prompt can return more than the field allows: the counter tells the truth, the rules refuse it.
            Assert.AreEqual("20/16", SaveTimeForm.CountLine("Twenty letters here!"));
        }
    }
}
