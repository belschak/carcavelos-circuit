using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: best lap, checkpoint splits and saving them as text.
    public class PersonalBestTests
    {
        [Test]
        public void FirstLap_IsAlwaysARecord()
        {
            PersonalBest best = new PersonalBest(3);
            Assert.IsFalse(best.HasRecord);
            Assert.IsTrue(best.FinishRun(45f));
            Assert.AreEqual(45f, best.BestSeconds);
        }

        [Test]
        public void SlowerLap_IsNotARecord_FasterLapIs()
        {
            PersonalBest best = new PersonalBest(3);
            best.FinishRun(45f);
            Assert.IsFalse(best.FinishRun(46f));
            Assert.IsFalse(best.FinishRun(45f), "a tie is not a new record");
            Assert.IsTrue(best.FinishRun(44.2f));
            Assert.AreEqual(44.2f, best.BestSeconds, 0.0001f);
        }

        [Test]
        public void Split_HasNoDelta_WithoutABestLap()
        {
            PersonalBest best = new PersonalBest(3);
            best.StartRun();
            float delta;
            Assert.IsFalse(best.RecordSplit(1, 10f, out delta));
        }

        [Test]
        public void Split_ComparesWithTheBestLap()
        {
            PersonalBest best = new PersonalBest(3);
            best.StartRun();
            float delta;
            best.RecordSplit(1, 10f, out delta);
            best.RecordSplit(2, 20f, out delta);
            best.FinishRun(30f);

            best.StartRun();
            Assert.IsTrue(best.RecordSplit(1, 9.5f, out delta));
            Assert.AreEqual(-0.5f, delta, 0.0001f, "half a second ahead");
            Assert.IsTrue(best.RecordSplit(2, 21f, out delta));
            Assert.AreEqual(1f, delta, 0.0001f, "one second behind");
        }

        [Test]
        public void Split_OutOfRange_IsIgnored()
        {
            PersonalBest best = new PersonalBest(3);
            float delta;
            Assert.IsFalse(best.RecordSplit(7, 10f, out delta));
            Assert.IsFalse(best.RecordSplit(-1, 10f, out delta));
        }

        [Test]
        public void SaveString_RoundTrips()
        {
            PersonalBest best = new PersonalBest(3);
            best.StartRun();
            float delta;
            best.RecordSplit(1, 10.25f, out delta);
            best.RecordSplit(2, 20.5f, out delta);
            best.FinishRun(31.125f);

            PersonalBest loaded = PersonalBest.FromSaveString(best.ToSaveString(), 3);
            Assert.AreEqual(31.125f, loaded.BestSeconds, 0.0001f);
            loaded.StartRun();
            Assert.IsTrue(loaded.RecordSplit(1, 10f, out delta));
            Assert.AreEqual(-0.25f, delta, 0.0001f);
        }

        [Test]
        public void SaveString_UsesADot_NotAComma()
        {
            PersonalBest best = new PersonalBest(1);
            best.FinishRun(12.5f);
            StringAssert.StartsWith("12.5|", best.ToSaveString());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("garbage")]
        [TestCase("-5|1;2;3")]
        public void BrokenSave_MeansNoRecord(string text)
        {
            PersonalBest loaded = PersonalBest.FromSaveString(text, 3);
            Assert.IsFalse(loaded.HasRecord);
        }

        [Test]
        public void Save_FromATrackWithOtherCheckpoints_KeepsTimeButDropsSplits()
        {
            PersonalBest oldTrack = new PersonalBest(5);
            oldTrack.FinishRun(40f);
            PersonalBest loaded = PersonalBest.FromSaveString(oldTrack.ToSaveString(), 3);
            Assert.AreEqual(40f, loaded.BestSeconds, 0.0001f);
            float delta;
            Assert.IsFalse(loaded.RecordSplit(1, 10f, out delta));
        }

        [Test]
        public void BestMedal_OnlyGoesUp()
        {
            PersonalBest best = new PersonalBest(3);
            Assert.AreEqual(Medal.None, best.BestMedal);
            Assert.IsTrue(best.NoteMedal(Medal.Silver));
            Assert.IsFalse(best.NoteMedal(Medal.Bronze), "a worse medal does not replace a better one");
            Assert.IsFalse(best.NoteMedal(Medal.Silver), "the same medal is not an improvement");
            Assert.IsTrue(best.NoteMedal(Medal.DeansList));
            Assert.AreEqual(Medal.DeansList, best.BestMedal);
        }

        [Test]
        public void BestMedal_SurvivesTheSaveString()
        {
            PersonalBest best = new PersonalBest(3);
            best.FinishRun(10.1f);
            best.NoteMedal(Medal.Gold);
            PersonalBest loaded = PersonalBest.FromSaveString(best.ToSaveString(), 3);
            Assert.AreEqual(Medal.Gold, loaded.BestMedal);
            Assert.AreEqual(10.1f, loaded.BestSeconds, 0.0001f);
        }

        [Test]
        public void SaveFromBeforeMedals_LoadsWithNoMedal()
        {
            PersonalBest loaded = PersonalBest.FromSaveString("41.25|10.5;22.25;-1", 3);
            Assert.AreEqual(41.25f, loaded.BestSeconds, 0.0001f);
            Assert.AreEqual(Medal.None, loaded.BestMedal);
        }

        [Test]
        public void LayoutStamp_SurvivesTheSaveString_SameRoadKeepsTheRecord()
        {
            PersonalBest best = new PersonalBest(3, 1408);
            best.FinishRun(52.5f);
            StringAssert.EndsWith("|1408", best.ToSaveString());
            PersonalBest loaded = PersonalBest.FromSaveString(best.ToSaveString(), 3, 1408);
            Assert.AreEqual(52.5f, loaded.BestSeconds, 0.0001f);
            Assert.AreEqual(1408, loaded.LayoutStamp);
            Assert.IsTrue(PersonalBest.FromSaveString(best.ToSaveString(), 3, 1406).HasRecord, "2 m off: same road");
        }

        [Test]
        public void RecordFromAnotherRoad_IsDropped_SoTheNextLapIsARecordAgain()
        {
            PersonalBest onV14 = new PersonalBest(3, 1364);
            onV14.FinishRun(51.03f);
            PersonalBest loaded = PersonalBest.FromSaveString(onV14.ToSaveString(), 3, 1408);
            Assert.IsFalse(loaded.HasRecord);
            Assert.AreEqual(1408, loaded.LayoutStamp, "the next save belongs to the road we race on now");
            Assert.IsTrue(loaded.FinishRun(53f), "a slower lap on the longer road is still a new record");
            Assert.IsFalse(PersonalBest.FromSaveString(onV14.ToSaveString(), 3, 1367).HasRecord, "3 m off: another road");
        }

        [Test]
        public void SaveWithoutStamp_KeptWhileTheTrackHasNoLine_DroppedOnceItHasOne()
        {
            Assert.IsTrue(PersonalBest.FromSaveString("41.25|10.5;22.25;-1|2", 3, TrackLayout.Unknown).HasRecord);
            Assert.IsFalse(PersonalBest.FromSaveString("41.25|10.5;22.25;-1|2", 3, 1408).HasRecord);
        }

        [TestCase("41.25|10.5;22.25;-1|2|long")]
        [TestCase("41.25|10.5;22.25;-1|2|")]
        public void BrokenLayoutStamp_MeansNoRecord(string text)
        {
            Assert.IsFalse(PersonalBest.FromSaveString(text, 3, TrackLayout.Unknown).HasRecord);
        }

        [TestCase("41.25|10.5;22.25;-1|9")]
        [TestCase("41.25|10.5;22.25;-1|-3")]
        [TestCase("41.25|10.5;22.25;-1|gold")]
        public void BrokenMedalNumber_MeansNoMedal_ButKeepsTheTime(string text)
        {
            PersonalBest loaded = PersonalBest.FromSaveString(text, 3);
            Assert.AreEqual(Medal.None, loaded.BestMedal);
            Assert.IsTrue(loaded.HasRecord);
        }
    }
}
