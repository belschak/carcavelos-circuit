using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Shared;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R9): live positions on a square test lap of 400 m.
    // The line starts at (0,0) and runs along +X first: (0,0) -> (100,0) -> (100,100) -> (0,100) -> back to the start.
    public class RaceStandingsTests
    {
        private TrackPolyline track;
        private readonly List<TestRacer> made = new List<TestRacer>();

        [SetUp]
        public void SetUp()
        {
            track = new TrackPolyline(new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f), new Vector3(100f, 0f, 100f), new Vector3(0f, 0f, 100f)
            });
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < made.Count; i++)
            {
                made[i].Destroy();
            }
            made.Clear();
        }

        private TestRacer Racer(string name, Vector3 position)
        {
            TestRacer racer = new TestRacer(name, false, true);
            racer.MoveTo(position);
            made.Add(racer);
            return racer;
        }

        [Test]
        public void FurtherAlongTheLap_IsAhead()
        {
            TestRacer a = Racer("A", new Vector3(20f, 0f, 0f));
            TestRacer b = Racer("B", new Vector3(60f, 0f, 0f));
            RaceStandings standings = new RaceStandings();
            standings.Update(new IRacer[] { a, b }, track);
            Assert.AreEqual(1, standings.PositionOf(b));
            Assert.AreEqual(2, standings.PositionOf(a));
            Assert.AreSame(b, standings.RacerAt(0));
        }

        [Test]
        public void GridBehindTheStartLine_IsNotALapAhead()
        {
            // The player 2 m after the line, a bot 3 m before it (on the last piece of the lap, distance 397 m).
            TestRacer player = Racer("You", new Vector3(2f, 0f, 0f));
            TestRacer bot = Racer("Bot", new Vector3(0f, 0f, 3f));
            RaceStandings standings = new RaceStandings();
            standings.Update(new IRacer[] { bot, player }, track);
            Assert.AreEqual(1, standings.PositionOf(player), "397 m on the grid must not beat 2 m");
            Assert.AreEqual(2, standings.PositionOf(bot));
        }

        [Test]
        public void MoreLaps_BeatsMoreDistance()
        {
            TestRacer a = Racer("A", new Vector3(100f, 0f, 50f)); // 150 m on lap 0
            TestRacer b = Racer("B", new Vector3(10f, 0f, 0f));   // 10 m
            RaceStandings standings = new RaceStandings();
            IRacer[] both = { a, b };
            standings.Update(both, track);
            Assert.AreEqual(1, standings.PositionOf(a));
            // B drives the rest of the lap and crosses the line again, A stays at 150 m.
            b.MoveTo(new Vector3(100f, 0f, 80f)); // 180 m
            standings.Update(both, track);
            b.MoveTo(new Vector3(50f, 0f, 100f)); // 250 m
            standings.Update(both, track);
            b.MoveTo(new Vector3(0f, 0f, 40f));   // 360 m
            standings.Update(both, track);
            b.MoveTo(new Vector3(5f, 0f, 0f)); // crossed: lap 1, 5 m
            standings.Update(both, track);
            Assert.AreEqual(1, standings.ProgressOf(b).LapsDone);
            Assert.AreEqual(1, standings.PositionOf(b));
        }

        [Test]
        public void ExactTies_KeepTheirOrder()
        {
            TestRacer a = Racer("A", new Vector3(10f, 0f, 0f));
            TestRacer b = Racer("B", new Vector3(10f, 0f, 0f));
            TestRacer c = Racer("C", new Vector3(10f, 0f, 0f));
            RaceStandings standings = new RaceStandings();
            IRacer[] all = { a, b, c };
            for (int frame = 0; frame < 5; frame++)
            {
                standings.Update(all, track);
                Assert.AreSame(a, standings.RacerAt(0));
                Assert.AreSame(b, standings.RacerAt(1));
                Assert.AreSame(c, standings.RacerAt(2));
            }
        }

        [Test]
        public void WrongWay_OverTheLine_DropsBack()
        {
            TestRacer a = Racer("A", new Vector3(5f, 0f, 0f));
            TestRacer b = Racer("B", new Vector3(3f, 0f, 0f));
            RaceStandings standings = new RaceStandings();
            IRacer[] both = { a, b };
            standings.Update(both, track);
            Assert.AreEqual(1, standings.PositionOf(a));
            a.MoveTo(new Vector3(0f, 0f, 10f)); // reversed over the line: lap -1, 390 m
            standings.Update(both, track);
            Assert.AreEqual(2, standings.PositionOf(a));
            Assert.AreEqual(-1, standings.ProgressOf(a).LapsDone);
        }

        [Test]
        public void JoiningAndLeaving()
        {
            TestRacer a = Racer("A", new Vector3(10f, 0f, 0f));
            TestRacer b = Racer("B", new Vector3(50f, 0f, 0f));
            RaceStandings standings = new RaceStandings();
            standings.Update(new IRacer[] { a }, track);
            Assert.AreEqual(1, standings.Count);
            standings.Update(new IRacer[] { a, b }, track);
            Assert.AreEqual(2, standings.Count, "B joined");
            Assert.AreEqual(1, standings.PositionOf(b));
            standings.Update(new IRacer[] { a }, track);
            Assert.AreEqual(1, standings.Count, "B left");
            Assert.AreEqual(0, standings.PositionOf(b));
            Assert.IsNull(standings.ProgressOf(b));
            Assert.AreEqual(1, standings.PositionOf(a));
        }

        [Test]
        public void NullListNullRacerNullRoot_AndNoTrack_DoNotThrow()
        {
            TestRacer a = Racer("A", new Vector3(10f, 0f, 0f));
            TestRacer noRoot = new TestRacer("NoRoot", false, false);
            RaceStandings standings = new RaceStandings();
            standings.Update(null, track);
            Assert.AreEqual(0, standings.Count);
            standings.Update(new IRacer[] { noRoot, null, a }, track);
            Assert.AreEqual(2, standings.Count, "null is skipped");
            Assert.AreEqual(1, standings.PositionOf(a), "a racer without a position is last");
            Assert.AreEqual(2, standings.PositionOf(noRoot));
            standings.Update(new IRacer[] { noRoot, a }, null);
            Assert.AreEqual(2, standings.Count, "no track: the list is still kept");
            Assert.IsNull(standings.RacerAt(5));
            Assert.IsNull(standings.RacerAt(-1));
        }

        [Test]
        public void Reset_ForgetsTheLaps_TheGridIsRecountedFromTheLine()
        {
            TestRacer a = Racer("A", new Vector3(10f, 0f, 0f));
            RaceStandings standings = new RaceStandings();
            IRacer[] one = { a };
            standings.Update(one, track);
            a.MoveTo(new Vector3(100f, 0f, 50f));
            standings.Update(one, track);
            standings.Reset();
            a.MoveTo(new Vector3(0f, 0f, 2f)); // back on the grid behind the line
            standings.Update(one, track);
            Assert.AreEqual(-1, standings.ProgressOf(a).LapsDone);
        }
    }
}
