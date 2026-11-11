using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R15): the words on the results card never contradict each other, never show a broken "0",
    // and the leaderboard avatar colour is stable per name.
    public class ResultsCopyTests
    {
        // ---------- delta chip ----------

        [Test]
        public void DeltaWords_FasterIsANewBest()
        {
            Assert.AreEqual("NEW BEST!", ResultsCopy.DeltaWords(-320));
        }

        [Test]
        public void DeltaWords_SlowerIsShortAndCalm()
        {
            Assert.AreEqual("OFF YOUR BEST", ResultsCopy.DeltaWords(504));
        }

        [Test]
        public void DeltaWords_ZeroIsATie_NeverANewBest()
        {
            Assert.AreEqual("TIED YOUR BEST", ResultsCopy.DeltaWords(0));
        }

        // ---------- medal line ----------

        [Test]
        public void MedalLine_NewMedal_SaysSoAndGivesTheNextTarget()
        {
            Assert.AreEqual("NEW MEDAL!  NEXT  SILVER  0:12.000",
                ResultsCopy.MedalLine(Medal.Bronze, Medal.None, Medal.Bronze, Medal.Silver, "0:12.000"));
        }

        [Test]
        public void MedalLine_SameMedalAgain_OnlyTheNextTarget()
        {
            Assert.AreEqual("NEXT  GOLD  0:10.000",
                ResultsCopy.MedalLine(Medal.Silver, Medal.Silver, Medal.Silver, Medal.Gold, "0:10.000"));
        }

        [Test]
        public void MedalLine_TopTierAgain_NoAdviceThatClashesWithASlowerLap()
        {
            string line = ResultsCopy.MedalLine(Medal.DeansList, Medal.DeansList, Medal.DeansList, Medal.None, "");
            Assert.AreEqual("HIGHEST MEDAL ON THIS TRACK", line);
            StringAssert.DoesNotContain("BEAT", line, "the old line 'beat your own time' sat right above 'slower than your best'");
        }

        [Test]
        public void MedalLine_TopTierFirstTime_IsCelebrated()
        {
            Assert.AreEqual("NEW MEDAL!  TOP TIER REACHED",
                ResultsCopy.MedalLine(Medal.DeansList, Medal.Gold, Medal.DeansList, Medal.None, ""));
        }

        [Test]
        public void MedalLine_WorseThanAnOlderLap_ShowsYourBestMedal()
        {
            Assert.AreEqual("YOUR BEST MEDAL  " + MedalRules.Name(Medal.Gold),
                ResultsCopy.MedalLine(Medal.Bronze, Medal.Gold, Medal.Gold, Medal.Silver, "0:12.000"));
        }

        [Test]
        public void MedalLine_BrokenMedalTimes_SaysNothing()
        {
            Assert.AreEqual("", ResultsCopy.MedalLine(Medal.Bronze, Medal.None, Medal.Bronze, Medal.Silver, ""));
            Assert.AreEqual("", ResultsCopy.MedalLine(Medal.Bronze, Medal.None, Medal.Bronze, Medal.Silver, null));
        }

        // ---------- lap report ----------

        [Test]
        public void Count_ShowsTheNumber_OrADashForNothing()
        {
            Assert.AreEqual("12", HudFormat.StripTags(ResultsCopy.Count(12)));
            Assert.AreEqual("-", ResultsCopy.Count(0), "a zero looks broken: show a dash");
            Assert.AreEqual("-", ResultsCopy.Count(-4), "a negative count is impossible: a dash, not '-4'");
            Assert.IsTrue(ResultsCopy.IsEmptyCount(0));
            Assert.IsFalse(ResultsCopy.IsEmptyCount(1));
        }

        [Test]
        public void AirTime_ShowsSeconds_OrADashForNoRealAirTime()
        {
            Assert.AreEqual("1.4 s", HudFormat.StripTags(ResultsCopy.AirTime(1.4f)));
            Assert.AreEqual("-", ResultsCopy.AirTime(0f));
            Assert.AreEqual("-", ResultsCopy.AirTime(0.04f), "would round to 0.0 s");
            Assert.AreEqual("-", ResultsCopy.AirTime(float.NaN));
            Assert.AreEqual("-", ResultsCopy.AirTime(-2f));
            Assert.IsTrue(ResultsCopy.IsEmptyAirTime(float.NaN));
            Assert.IsFalse(ResultsCopy.IsEmptyAirTime(0.3f));
        }

        // ---------- leaderboard avatar ----------

        [Test]
        public void Avatar_SameNameSameColour_CaseDoesNotMatter()
        {
            Assert.AreEqual(LeaderboardAvatar.ColourIndex("Ana"), LeaderboardAvatar.ColourIndex("Ana"));
            Assert.AreEqual(LeaderboardAvatar.ColourIndex("ana"), LeaderboardAvatar.ColourIndex("ANA"));
        }

        [Test]
        public void Avatar_DifferentNamesSpreadOverTheColours()
        {
            string[] names = { "Rita Speedy", "Luc", "Noah", "Alessandro", "José Müller", "Dean of Nova", "Kart_Queen", "Beach Bum", "Ana-Sofia", "Pedro 99" };
            bool[] used = new bool[LeaderboardAvatar.ColourCount];
            int different = 0;
            for (int i = 0; i < names.Length; i++)
            {
                int index = LeaderboardAvatar.ColourIndex(names[i]);
                Assert.That(index, Is.InRange(0, LeaderboardAvatar.ColourCount - 1));
                if (!used[index])
                {
                    used[index] = true;
                    different++;
                }
            }
            Assert.GreaterOrEqual(different, 4, "ten names should not all look alike");
        }

        [Test]
        public void Avatar_NullEmptyAndLongNames_AreSafe()
        {
            Assert.AreEqual(0, LeaderboardAvatar.ColourIndex(null));
            Assert.AreEqual(0, LeaderboardAvatar.ColourIndex(""));
            int index = LeaderboardAvatar.ColourIndex(new string('z', 5000)); // the hash overflows: still a valid index
            Assert.That(index, Is.InRange(0, LeaderboardAvatar.ColourCount - 1));
        }
    }
}
