using System.Collections.Generic;
using CarcavelosCircuit.Online;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // The online results order (M2): times first, ties by id, then racing, then DNF; the same on every screen.
    public class FinishOrderRulesTests
    {
        [Test]
        public void FinishedByTime_ThenRacing_ThenDnf()
        {
            List<FinishEntry> entries = new List<FinishEntry>
            {
                new FinishEntry(1, "Ana", true, true, 65.2),
                new FinishEntry(2, "Bo", true, false, double.NaN),   // still racing
                new FinishEntry(3, "Cid", false, false, double.NaN), // left the room
                new FinishEntry(4, "Dee", true, true, 61.0),
            };
            List<FinishRow> rows = FinishOrderRules.Order(entries);
            Assert.AreEqual(4, rows.Count);
            Assert.AreEqual(4, rows[0].Id);
            Assert.AreEqual(1, rows[0].Place);
            Assert.AreEqual(FinishState.Finished, rows[0].State);
            Assert.AreEqual(1, rows[1].Id);
            Assert.AreEqual(2, rows[1].Place);
            Assert.AreEqual(2, rows[2].Id);
            Assert.AreEqual(FinishState.Racing, rows[2].State);
            Assert.AreEqual(0, rows[2].Place, "no place while racing");
            Assert.AreEqual(3, rows[3].Id);
            Assert.AreEqual(FinishState.Dnf, rows[3].State);
            Assert.AreEqual(0, rows[3].Place);
        }

        [Test]
        public void Tie_LowerIdFirst()
        {
            List<FinishEntry> entries = new List<FinishEntry>
            {
                new FinishEntry(9, "Nine", true, true, 60.0),
                new FinishEntry(4, "Four", true, true, 60.0),
            };
            List<FinishRow> rows = FinishOrderRules.Order(entries);
            Assert.AreEqual(4, rows[0].Id);
            Assert.AreEqual(9, rows[1].Id);
            Assert.AreEqual(2, rows[1].Place);
        }

        [Test]
        public void LeftAfterFinishing_KeepsTheTime()
        {
            List<FinishEntry> entries = new List<FinishEntry>
            {
                new FinishEntry(1, "Ana", false, true, 70.0), // finished, then closed the tab
            };
            List<FinishRow> rows = FinishOrderRules.Order(entries);
            Assert.AreEqual(FinishState.Finished, rows[0].State);
            Assert.AreEqual(1, rows[0].Place);
        }

        [Test]
        public void BadTime_CountsAsNotFinished()
        {
            List<FinishEntry> entries = new List<FinishEntry>
            {
                new FinishEntry(1, "Ana", true, true, double.NaN),
                new FinishEntry(2, "Bo", true, true, -3.0),
                new FinishEntry(3, "Cid", false, true, double.PositiveInfinity),
            };
            List<FinishRow> rows = FinishOrderRules.Order(entries);
            Assert.AreEqual(FinishState.Racing, rows[0].State);
            Assert.AreEqual(FinishState.Racing, rows[1].State);
            Assert.AreEqual(FinishState.Dnf, rows[2].State);
        }

        [Test]
        public void PlaceOf_AndAllDone()
        {
            List<FinishEntry> entries = new List<FinishEntry>
            {
                new FinishEntry(1, "Ana", true, true, 65.2),
                new FinishEntry(2, "Bo", true, false, double.NaN),
            };
            List<FinishRow> rows = FinishOrderRules.Order(entries);
            Assert.AreEqual(1, FinishOrderRules.PlaceOf(rows, 1));
            Assert.AreEqual(0, FinishOrderRules.PlaceOf(rows, 2), "still racing");
            Assert.AreEqual(0, FinishOrderRules.PlaceOf(rows, 77), "not in the race");
            Assert.IsFalse(FinishOrderRules.AllDone(rows));
            entries[1] = new FinishEntry(2, "Bo", false, false, double.NaN);
            Assert.IsTrue(FinishOrderRules.AllDone(FinishOrderRules.Order(entries)), "finished or gone = done");
        }

        [Test]
        public void Empty_AndNull()
        {
            Assert.AreEqual(0, FinishOrderRules.Order(new List<FinishEntry>()).Count);
            Assert.AreEqual(0, FinishOrderRules.Order(null).Count);
            Assert.IsTrue(FinishOrderRules.AllDone(null));
            Assert.AreEqual(0, FinishOrderRules.PlaceOf(null, 1));
        }
    }
}
