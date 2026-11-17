using System.Collections.Generic;
using CarcavelosCircuit.Online;
using CarcavelosCircuit.Shared;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // The CCNET console lines of the online race (M2 scale test): one parseable token per value, the same text on
    // every screen for the same results, and the "starved remote kart" count behind the smoothness numbers.
    public class OnlineTestLogTests
    {
        [Test]
        public void Clean_ReplacesSpacesAndSeparators()
        {
            Assert.AreEqual("Ana_B_x_y_z", OnlineTestLog.Clean("Ana B|x:y=z"));
            Assert.AreEqual("-", OnlineTestLog.Clean(""));
            Assert.AreEqual("-", OnlineTestLog.Clean(null));
        }

        [Test]
        public void Num_IsInvariantAndNanSafe()
        {
            Assert.AreEqual("61.500", OnlineTestLog.Num(61.5, 3));
            Assert.AreEqual("nan", OnlineTestLog.Num(double.NaN, 3));
        }

        [Test]
        public void Result_ListsTheOrderWithTimesRacingAndDnf()
        {
            List<FinishEntry> entries = new List<FinishEntry>
            {
                new FinishEntry(1, "Ana", true, true, 65.2),
                new FinishEntry(2, "Bo Li", true, false, double.NaN),
                new FinishEntry(3, "Cid", false, false, double.NaN),
                new FinishEntry(4, "Dee", true, true, 61.0),
            };
            List<FinishRow> rows = FinishOrderRules.Order(entries);
            string line = OnlineTestLog.Result(TimeOfDay.Sunset, 3, "Ana", rows);
            Assert.AreEqual("RESULT room=sunset race=3 me=Ana order=Dee#4:61.000|Ana#1:65.200|Bo_Li#2:RACING|Cid#3:DNF", line);
        }

        [Test]
        public void Smoothness_CountsPosesOlderThanTheRenderDelay()
        {
            ProxySmoothness smooth = new ProxySmoothness();
            smooth.Add(-1.0, 10.0);                                     // no pose yet: not counted
            smooth.Add(10.0 - 0.1, 10.0);                               // fresh
            smooth.Add(10.0 - PoseBuffer.RenderDelaySeconds - 0.05, 10.0); // starved
            Assert.AreEqual(2, smooth.Samples);
            Assert.AreEqual(1, smooth.Starved);
            Assert.AreEqual(50.0, smooth.StarvedPercent, 0.001);
            Assert.AreEqual(0.27, smooth.MaxAgeSeconds, 0.0001);
            smooth.Reset();
            Assert.AreEqual(0, smooth.Samples);
            Assert.AreEqual("starved_pct=0.0 max_age_ms=0 delay_ms=0 samples=0", smooth.Line());
        }
    }
}
