using CarcavelosCircuit.Driving;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Tests for the name tag rules (D9): who gets a tag (accepted / rejected), which text may appear
    // (names typed by players are cleaned) and how visible a tag is at each distance (edges included).
    // Distances used: hidden up to 3 m, full from 6 m to 45 m, gone at 60 m.
    public class NameTagRulesTests
    {
        private static float Alpha(float distance)
        {
            return NameTagRules.Alpha(distance, 3f, 6f, 45f, 60f);
        }

        [Test]
        public void Bot_WithAName_GetsATag()
        {
            Assert.IsTrue(NameTagRules.ShouldShow(false, "Bot"));
        }

        [Test]
        public void Player_NeverGetsATag()
        {
            Assert.IsFalse(NameTagRules.ShouldShow(true, "You"));
        }

        [Test]
        public void EmptyName_GetsNoTag()
        {
            Assert.IsFalse(NameTagRules.ShouldShow(false, ""));
            Assert.IsFalse(NameTagRules.ShouldShow(false, null));
            Assert.IsFalse(NameTagRules.ShouldShow(false, NameTagRules.CleanName("   ")), "only spaces");
        }

        [Test]
        public void CleanName_KeepsANormalName()
        {
            Assert.AreEqual("Rita", NameTagRules.CleanName("Rita"));
            Assert.AreEqual("Dean's List", NameTagRules.CleanName("Dean's List"));
        }

        [Test]
        public void CleanName_NullBecomesEmpty()
        {
            Assert.AreEqual("", NameTagRules.CleanName(null));
        }

        [Test]
        public void CleanName_TrimsSpacesAtTheEnds()
        {
            Assert.AreEqual("Rita", NameTagRules.CleanName("  Rita  "));
        }

        [Test]
        public void CleanName_RemovesNewLinesAndTabs()
        {
            Assert.AreEqual("AB", NameTagRules.CleanName("A\nB"));
            Assert.AreEqual("AB", NameTagRules.CleanName("A\tB\r"));
        }

        [Test]
        public void CleanName_CutsLongNames()
        {
            string longName = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            Assert.AreEqual(NameTagRules.MaxNameLength, NameTagRules.CleanName(longName).Length);
            Assert.AreEqual("ABCDEFGHIJKLMNOP", NameTagRules.CleanName(longName));
        }

        [Test]
        public void CleanName_ExactlyMaxLength_IsKept()
        {
            string sixteen = "ABCDEFGHIJKLMNOP";
            Assert.AreEqual(sixteen, NameTagRules.CleanName(sixteen));
        }

        [Test]
        public void CleanName_NeverCutsAnEmojiInHalf()
        {
            // 15 letters + one emoji (2 chars) = 17 chars: the emoji would be cut in half, so it is dropped whole.
            string name = "ABCDEFGHIJKLMNO\U0001F600";
            string clean = NameTagRules.CleanName(name);
            Assert.AreEqual("ABCDEFGHIJKLMNO", clean);
        }

        [Test]
        public void CleanName_RichTextIsKeptAsPlainCharacters()
        {
            // The tag switches rich text off, so this shows as typed and cannot resize or recolour the tag.
            Assert.AreEqual("<b>x</b>", NameTagRules.CleanName("<b>x</b>"));
        }

        [Test]
        public void Alpha_TooCloseToTheCamera_IsHidden()
        {
            Assert.AreEqual(0f, Alpha(0f), 0.0001f);
            Assert.AreEqual(0f, Alpha(3f), 0.0001f, "exactly at the near limit");
        }

        [Test]
        public void Alpha_FadesInNearTheCamera()
        {
            Assert.AreEqual(0.5f, Alpha(4.5f), 0.0001f);
        }

        [Test]
        public void Alpha_FullyVisibleInTheMiddle()
        {
            Assert.AreEqual(1f, Alpha(6f), 0.0001f);
            Assert.AreEqual(1f, Alpha(20f), 0.0001f);
            Assert.AreEqual(1f, Alpha(45f), 0.0001f);
        }

        [Test]
        public void Alpha_FadesOutFarAway()
        {
            Assert.AreEqual(0.5f, Alpha(52.5f), 0.0001f);
        }

        [Test]
        public void Alpha_TooFar_IsHidden()
        {
            Assert.AreEqual(0f, Alpha(60f), 0.0001f, "exactly at the far limit");
            Assert.AreEqual(0f, Alpha(500f), 0.0001f);
            Assert.AreEqual(0f, Alpha(float.PositiveInfinity), 0.0001f);
        }

        [Test]
        public void Alpha_BadDistance_IsHidden()
        {
            Assert.AreEqual(0f, Alpha(float.NaN), 0.0001f);
            Assert.AreEqual(0f, Alpha(-5f), 0.0001f);
        }

        [Test]
        public void Alpha_BadSettings_NeverDivideByZero()
        {
            // near fade of zero length: visible right after the near limit, no division by zero
            Assert.AreEqual(1f, NameTagRules.Alpha(10f, 5f, 5f, 45f, 60f), 0.0001f);
            // far fade of zero length: hidden past the far-full distance
            Assert.AreEqual(0f, NameTagRules.Alpha(50f, 3f, 6f, 45f, 45f), 0.0001f);
        }
    }
}
