using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R10): which names may go on the leaderboard.
    public class PlayerNameRulesTests
    {
        private static NameProblem Check(string raw)
        {
            string cleaned;
            return PlayerNameRules.Check(raw, out cleaned);
        }

        private static string Cleaned(string raw)
        {
            string cleaned;
            PlayerNameRules.Check(raw, out cleaned);
            return cleaned;
        }

        [TestCase("Ana")]
        [TestCase("Jo")]                 // exactly 2
        [TestCase("Sixteen_Chars_16")]   // exactly 16
        [TestCase("Speedy Luc")]
        [TestCase("Kart-Queen_99")]
        [TestCase("José")]
        [TestCase("Müller")]
        [TestCase("Françoise")]
        [TestCase("Łukasz")]             // Latin Extended-A
        [TestCase("ÅSA ØRSTED")]
        [TestCase("Dickens")]            // a blocked short word inside a longer one is fine
        [TestCase("Essex Racer")]
        [TestCase("Hancock")]
        public void GoodNames_AreAccepted(string name)
        {
            Assert.AreEqual(NameProblem.None, Check(name), name);
        }

        [Test]
        public void Spaces_AreTrimmed_AndDoubleSpacesBecomeOne()
        {
            Assert.AreEqual("Ana Rita", Cleaned("  Ana    Rita  "));
            Assert.AreEqual(NameProblem.None, Check("  Ana    Rita  "));
        }

        [Test]
        public void Empty_Null_AndOnlySpaces_AreEmpty()
        {
            Assert.AreEqual(NameProblem.Empty, Check(null));
            Assert.AreEqual(NameProblem.Empty, Check(""));
            Assert.AreEqual(NameProblem.Empty, Check("      "));
            Assert.AreEqual("", Cleaned(null));
        }

        [Test]
        public void OneCharacter_IsTooShort()
        {
            Assert.AreEqual(NameProblem.TooShort, Check("A"));
            Assert.AreEqual(NameProblem.TooShort, Check("  A  "), "spaces do not count");
        }

        [Test]
        public void SeventeenCharacters_AreTooLong()
        {
            Assert.AreEqual(NameProblem.TooLong, Check("Seventeen_Chars17"));
            Assert.AreEqual(NameProblem.TooLong, Check(new string('a', 5000)), "a pasted wall of text");
        }

        [TestCase("Ana😀")]               // emoji
        [TestCase("'; drop table lap_times; --")] // looks like SQL
        [TestCase("Robert'); --")]
        [TestCase("<b>Ana</b>")]          // HTML / rich text
        [TestCase("<color=red>X")]
        [TestCase("Ana\tRita")]           // a tab is not a space
        [TestCase("Ana\nRita")]
        [TestCase("Ana.Rita")]
        [TestCase("Ana@home")]
        [TestCase("١٢٣٤")]                // digits of another script
        [TestCase("Ana×2")]               // the multiplication sign sits between the accented letters
        public void OddCharacters_AreRefused(string name)
        {
            Assert.AreEqual(NameProblem.BadCharacter, Check(name), name);
        }

        [TestCase("fuck")]
        [TestCase("FuckYou")]             // anywhere in the name
        [TestCase("f u c k")]             // spaced out
        [TestCase("Sh1t")]                // digit tricks are undone
        [TestCase("sh-it")]
        [TestCase("Big Dick")]            // a short word as a whole word
        [TestCase("Caralho")]             // Portuguese
        [TestCase("puta")]
        [TestCase("Scheiße")]             // German, ß folded to ss
        [TestCase("Arschloch99")]
        [TestCase("kut")]                 // Dutch
        [TestCase("Klootzak")]
        public void RudeWords_AreBlocked(string name)
        {
            Assert.AreEqual(NameProblem.Blocked, Check(name), name);
        }

        [Test]
        public void StoredNames_MustAlreadyBeClean()
        {
            Assert.IsTrue(PlayerNameRules.IsValidStoredName("Ana Rita"));
            Assert.IsFalse(PlayerNameRules.IsValidStoredName(" Ana"), "a server row with spaces around it is not ours");
            Assert.IsFalse(PlayerNameRules.IsValidStoredName("Ana  Rita"));
            Assert.IsFalse(PlayerNameRules.IsValidStoredName(null));
            Assert.IsFalse(PlayerNameRules.IsValidStoredName("<b>x</b>"));
        }

        [Test]
        public void SameName_IgnoresUpperAndLowerCase()
        {
            Assert.IsTrue(PlayerNameRules.SameName("Ana", "ANA"));
            Assert.IsFalse(PlayerNameRules.SameName("Ana", "Anna"));
            Assert.IsFalse(PlayerNameRules.SameName("Ana", null));
        }

        [Test]
        public void EveryProblem_HasItsOwnMessage_AndNoneHasNone()
        {
            Assert.AreEqual("", PlayerNameRules.Message(NameProblem.None));
            NameProblem[] problems = { NameProblem.Empty, NameProblem.TooShort, NameProblem.TooLong, NameProblem.BadCharacter, NameProblem.Blocked };
            for (int i = 0; i < problems.Length; i++)
            {
                Assert.IsNotEmpty(PlayerNameRules.Message(problems[i]), problems[i].ToString());
                for (int j = 0; j < i; j++)
                {
                    Assert.AreNotEqual(PlayerNameRules.Message(problems[j]), PlayerNameRules.Message(problems[i]));
                }
            }
        }
    }
}
