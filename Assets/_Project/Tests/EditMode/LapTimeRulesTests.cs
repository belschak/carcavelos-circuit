using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R10): which lap times may go on the leaderboard, and which track names.
    public class LapTimeRulesTests
    {
        [Test]
        public void Minimum_Is40PercentOfGold_ButNeverUnder5Seconds()
        {
            Assert.AreEqual(24f, LapTimeRules.MinSecondsFor(60f), 0.0001f);
            Assert.AreEqual(5f, LapTimeRules.MinSecondsFor(10.2f), 0.0001f, "40 % of a short Gold time is under the 5 s floor");
            Assert.AreEqual(5f, LapTimeRules.MinSecondsFor(-1f), "no medal times: only the floor");
            Assert.AreEqual(5f, LapTimeRules.MinSecondsFor(0f));
            Assert.AreEqual(5f, LapTimeRules.MinSecondsFor(float.NaN));
            Assert.AreEqual(5f, LapTimeRules.MinSecondsFor(float.PositiveInfinity));
        }

        [Test]
        public void NormalLaps_AreAccepted_IncludingBothEdges()
        {
            Assert.IsTrue(LapTimeRules.IsAcceptable(62.345f, 60f));
            Assert.IsTrue(LapTimeRules.IsAcceptable(LapTimeRules.MinSecondsFor(60f), 60f), "exactly the minimum");
            Assert.IsTrue(LapTimeRules.IsAcceptable(1800f, 60f), "exactly 30 minutes");
            Assert.IsTrue(LapTimeRules.IsAcceptable(5f, 0f), "exactly the floor without medal times");
        }

        [Test]
        public void TooFast_TooSlow_AndNonNumbers_AreRefused()
        {
            Assert.IsFalse(LapTimeRules.IsAcceptable(23.99f, 60f), "faster than 40 % of Gold");
            Assert.IsFalse(LapTimeRules.IsAcceptable(4.99f, 0f), "under 5 s");
            Assert.IsFalse(LapTimeRules.IsAcceptable(0f, 60f));
            Assert.IsFalse(LapTimeRules.IsAcceptable(-30f, 60f));
            Assert.IsFalse(LapTimeRules.IsAcceptable(1800.01f, 60f), "over 30 minutes");
            Assert.IsFalse(LapTimeRules.IsAcceptable(float.NaN, 60f));
            Assert.IsFalse(LapTimeRules.IsAcceptable(float.PositiveInfinity, 60f));
            Assert.IsFalse(LapTimeRules.IsAcceptable(float.NegativeInfinity, 60f));
        }

        [TestCase("Track_CampusWorld", true)]
        [TestCase("RaceFlow_PlaceholderTrack", true)]
        [TestCase("track-2", true)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("Track Campus", false)]           // a space would break the web address
        [TestCase("Track&limit=1000", false)]       // could add its own filter to the request
        [TestCase("Track_Campus.eq.x", false)]
        [TestCase("Ünicode", false)]
        public void TrackNames_OnlyPlainCharacters(string track, bool ok)
        {
            Assert.AreEqual(ok, LapTimeRules.IsValidTrack(track), track);
        }

        [Test]
        public void TrackNames_AtMost40Characters()
        {
            Assert.IsTrue(LapTimeRules.IsValidTrack(new string('a', 40)));
            Assert.IsFalse(LapTimeRules.IsValidTrack(new string('a', 41)));
        }
    }
}
