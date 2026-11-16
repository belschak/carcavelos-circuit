using System.Collections.Generic;
using CarcavelosCircuit.Online;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // The online race time limit (review F5): one player who stops driving never holds up the room.
    public class OnlineRaceLimitRulesTests
    {
        private const double StartAt = 1000.0;

        [Test]
        public void NobodyFinished_TheRaceClosesAtTheNoFinisherLimit()
        {
            List<FinishEntry> entries = new List<FinishEntry>
            {
                new FinishEntry(1, "Ana", true, false, double.NaN),
                new FinishEntry(2, "Bo", true, false, double.NaN),
            };
            Assert.IsTrue(double.IsNaN(OnlineRaceLimitRules.FirstFinishSeconds(entries)));
            Assert.AreEqual(OnlineRaceLimitRules.NoFinisherLimitSeconds, OnlineRaceLimitRules.EndSeconds(double.NaN), 0.0);
            bool timeUp;
            double end;
            OnlineRaceLimitRules.Apply(entries, StartAt + 179.0, StartAt, out timeUp, out end);
            Assert.IsFalse(timeUp, "still inside the limit");
            List<FinishRow> rows = FinishOrderRules.Order(OnlineRaceLimitRules.Apply(entries, StartAt + 180.0, StartAt, out timeUp, out end));
            Assert.IsTrue(timeUp);
            Assert.IsTrue(FinishOrderRules.AllDone(rows), "everyone out of time = the race is over");
            Assert.AreEqual(FinishState.Dnf, rows[0].State);
            Assert.AreEqual(FinishState.Dnf, rows[1].State);
        }

        [Test]
        public void AfterTheFirstFinish_TheOthersGetTheGrace_ThenAreDnf()
        {
            List<FinishEntry> entries = new List<FinishEntry>
            {
                new FinishEntry(1, "Ana", true, true, 50.0),
                new FinishEntry(2, "Bo", true, false, double.NaN), // stopped driving, tab still open
                new FinishEntry(3, "Cid", true, true, 95.0),
            };
            Assert.AreEqual(50.0, OnlineRaceLimitRules.FirstFinishSeconds(entries), 0.0);
            bool timeUp;
            double end;
            List<FinishRow> before = FinishOrderRules.Order(OnlineRaceLimitRules.Apply(entries, StartAt + 109.0, StartAt, out timeUp, out end));
            Assert.AreEqual(110.0, end, 0.0);
            Assert.IsFalse(timeUp);
            Assert.IsFalse(FinishOrderRules.AllDone(before), "Bo is still racing inside the grace");

            List<FinishRow> after = FinishOrderRules.Order(OnlineRaceLimitRules.Apply(entries, StartAt + 110.0, StartAt, out timeUp, out end));
            Assert.IsTrue(timeUp);
            Assert.IsTrue(FinishOrderRules.AllDone(after));
            Assert.AreEqual(1, after[0].Id);
            Assert.AreEqual(1, after[0].Place);
            Assert.AreEqual(3, after[1].Id, "a finish inside the grace counts");
            Assert.AreEqual(2, after[1].Place);
            Assert.AreEqual(2, after[2].Id);
            Assert.AreEqual(FinishState.Dnf, after[2].State);
        }

        [Test]
        public void ALapSlowerThanTheLimit_IsNoFinish_OnEveryScreen()
        {
            List<FinishEntry> entries = new List<FinishEntry>
            {
                new FinishEntry(1, "Ana", true, true, 50.0),
                new FinishEntry(2, "Bo", true, true, 111.0), // 61 s after Ana: outside the grace
            };
            bool timeUp;
            double end;
            List<FinishRow> early = FinishOrderRules.Order(OnlineRaceLimitRules.Apply(entries, StartAt + 60.0, StartAt, out timeUp, out end));
            List<FinishRow> late = FinishOrderRules.Order(OnlineRaceLimitRules.Apply(entries, StartAt + 500.0, StartAt, out timeUp, out end));
            Assert.AreEqual(FinishState.Racing, early[1].State, "the clock is not up yet, Bo still counts as on the road");
            Assert.AreEqual(FinishState.Dnf, late[1].State, "results are final: the slow lap never becomes a place");
            Assert.AreEqual(0, late[1].Place);
        }

        [Test]
        public void ALapAfterTheNoFinisherLimit_NeverReopensTheRace()
        {
            List<FinishEntry> entries = new List<FinishEntry>
            {
                new FinishEntry(1, "Ana", true, true, 185.0),
                new FinishEntry(2, "Bo", true, false, double.NaN),
            };
            Assert.IsTrue(double.IsNaN(OnlineRaceLimitRules.FirstFinishSeconds(entries)), "too late to be a first finish");
            bool timeUp;
            double end;
            List<FinishRow> rows = FinishOrderRules.Order(OnlineRaceLimitRules.Apply(entries, StartAt + 200.0, StartAt, out timeUp, out end));
            Assert.IsTrue(timeUp);
            Assert.AreEqual(OnlineRaceLimitRules.NoFinisherLimitSeconds, end, 0.0);
            Assert.IsTrue(FinishOrderRules.AllDone(rows));
            Assert.AreEqual(FinishState.Dnf, rows[0].State);
        }

        [Test]
        public void Leavers_StayDnf_BadTimesAreIgnored()
        {
            List<FinishEntry> entries = new List<FinishEntry>
            {
                new FinishEntry(1, "Ana", false, false, double.NaN),   // left
                new FinishEntry(2, "Bo", true, true, double.NaN),      // broken report
                new FinishEntry(3, "Cid", true, true, -4.0),
            };
            Assert.IsTrue(double.IsNaN(OnlineRaceLimitRules.FirstFinishSeconds(entries)));
            Assert.IsTrue(double.IsNaN(OnlineRaceLimitRules.FirstFinishSeconds(null)));
            bool timeUp;
            double end;
            List<FinishRow> rows = FinishOrderRules.Order(OnlineRaceLimitRules.Apply(entries, StartAt + 10.0, StartAt, out timeUp, out end));
            Assert.IsFalse(timeUp);
            Assert.AreEqual(FinishState.Racing, rows[0].State);
            Assert.AreEqual(FinishState.Racing, rows[1].State);
            Assert.AreEqual(FinishState.Dnf, rows[2].State, "Ana left");
            Assert.AreEqual(0, OnlineRaceLimitRules.Apply(null, 0.0, 0.0, out timeUp, out end).Count);
        }

        [Test]
        public void SecondsLeft_AndTheLine()
        {
            Assert.AreEqual(42, OnlineRaceLimitRules.SecondsLeft(StartAt + 68.2, StartAt, 110.0));
            Assert.AreEqual(1, OnlineRaceLimitRules.SecondsLeft(StartAt + 109.9, StartAt, 110.0));
            Assert.AreEqual(0, OnlineRaceLimitRules.SecondsLeft(StartAt + 110.0, StartAt, 110.0));
            Assert.AreEqual(0, OnlineRaceLimitRules.SecondsLeft(double.NaN, StartAt, 110.0));
            Assert.AreEqual("RACE CLOSES IN 42 S", OnlineRaceLimitRules.ClosesLine(42));
            Assert.AreEqual("RACE CLOSES IN 0 S", OnlineRaceLimitRules.ClosesLine(-3));
        }
    }
}
