using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: HUD number texts with fixed-width digits (the <mspace> tag), so the timer does not wobble.
    public class HudFormatTests
    {
        private const string Open = "<mspace=" + HudFormat.DigitWidth + ">";
        private const string Close = "</mspace>";

        [Test]
        public void TimeMain_IsMinutesAndSeconds_WithEachDigitRunWrapped()
        {
            Assert.AreEqual(Open + "1" + Close + ":" + Open + "23" + Close, HudFormat.TimeMain(83456));
        }

        [Test]
        public void TimeFraction_IsTheMilliseconds_WithTheDotOutsideTheTag()
        {
            Assert.AreEqual("." + Open + "456" + Close, HudFormat.TimeFraction(83456));
            Assert.AreEqual(".007", HudFormat.StripTags(HudFormat.TimeFraction(1007)));
        }

        [Test]
        public void Time_ReadsLikeTheLapTimer()
        {
            Assert.AreEqual("1:23.456", HudFormat.StripTags(HudFormat.Time(83456)));
            Assert.AreEqual("0:05.020", HudFormat.StripTags(HudFormat.Time(5020)));
            Assert.AreEqual("60:00.000", HudFormat.StripTags(HudFormat.Time(3600000)));
        }

        [Test]
        public void NegativeTime_ShowsAsZero()
        {
            Assert.AreEqual("0:00.000", HudFormat.StripTags(HudFormat.Time(-5)));
            Assert.AreEqual("0:00", HudFormat.StripTags(HudFormat.TimeMain(-5)));
            Assert.AreEqual(".000", HudFormat.StripTags(HudFormat.TimeFraction(-5)));
        }

        [Test]
        public void Time_MatchesRaceTimerFormat_ForManyValues()
        {
            // The HUD and the plain-text format in RaceTimer must never disagree about a time.
            int[] values = { 0, 1, 999, 1000, 59999, 60000, 83456, 599999 };
            for (int i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(RaceTimer.FormatMilliseconds(values[i]), HudFormat.StripTags(HudFormat.Time(values[i])));
            }
        }

        [Test]
        public void MainPlusFraction_IsTheWholeTime()
        {
            string joined = HudFormat.StripTags(HudFormat.TimeMain(83456)) + HudFormat.StripTags(HudFormat.TimeFraction(83456));
            Assert.AreEqual("1:23.456", joined);
        }

        [Test]
        public void Delta_HasASign_AndOptionallyAUnit()
        {
            Assert.AreEqual("-1.204", HudFormat.StripTags(HudFormat.Delta(-1204, false)));
            Assert.AreEqual("+0.380 s", HudFormat.StripTags(HudFormat.Delta(380, true)));
            Assert.AreEqual("+0.000", HudFormat.StripTags(HudFormat.Delta(0, false)));
        }

        [Test]
        public void Delta_MatchesRaceTimerFormatDelta()
        {
            int[] values = { -61234, -1204, -1, 0, 1, 380, 12000 };
            for (int i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(RaceTimer.FormatDeltaMilliseconds(values[i]), HudFormat.StripTags(HudFormat.Delta(values[i], false)));
            }
        }

        [Test]
        public void Whole_IsTheNumber_AndNeverNegative()
        {
            Assert.AreEqual(Open + "87" + Close, HudFormat.Whole(87));
            Assert.AreEqual("0", HudFormat.StripTags(HudFormat.Whole(-3)));
        }

        [Test]
        public void StripTags_RemovesOnlyTags()
        {
            Assert.AreEqual("1:23", HudFormat.StripTags("<mspace=0.6em>1</mspace>:<b>23</b>"));
            Assert.AreEqual("no tags", HudFormat.StripTags("no tags"));
        }
    }
}
