using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Shared;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R7): where the Seagull Delivery's rings stand. They follow the road round a corner.
    public class RingPathTests
    {
        // An L-shaped road: 20 m north, then 30 m east.
        private static readonly Vector3[] Corner = { Vector3.zero, new Vector3(0f, 0f, 20f), new Vector3(30f, 0f, 20f) };

        private static void AssertNear(Vector3 expected, Vector3 actual, string what)
        {
            Assert.Less(Vector3.Distance(expected, actual), 0.001f, what + ": expected " + expected + " got " + actual);
        }

        [Test]
        public void OnTheFirstPiece_GoesStraightAhead()
        {
            Vector3 position;
            Vector3 facing;
            RingPath.PointAt(Corner, 12f, out position, out facing);
            AssertNear(new Vector3(0f, 0f, 12f), position, "position");
            AssertNear(Vector3.forward, facing, "facing");
        }

        [Test]
        public void AfterTheCorner_FollowsTheSecondPiece()
        {
            Vector3 position;
            Vector3 facing;
            RingPath.PointAt(Corner, 26f, out position, out facing);
            AssertNear(new Vector3(6f, 0f, 20f), position, "position");
            AssertNear(Vector3.right, facing, "facing");
        }

        [Test]
        public void BeyondTheEnd_KeepsGoingStraightOn()
        {
            Vector3 position;
            Vector3 facing;
            RingPath.PointAt(Corner, 60f, out position, out facing);
            AssertNear(new Vector3(40f, 0f, 20f), position, "position");
            AssertNear(Vector3.right, facing, "facing");
        }

        [Test]
        public void OnASlope_TheRingStillStandsUpright()
        {
            Vector3[] hill = { Vector3.zero, new Vector3(0f, 5f, 20f) };
            Vector3 position;
            Vector3 facing;
            RingPath.PointAt(hill, 10f, out position, out facing);
            Assert.AreEqual(0f, facing.y, 0.0001f);
            AssertNear(Vector3.forward, facing, "facing");
        }

        [Test]
        public void Place_FillsOneSpotPerRing()
        {
            float[] distances = { 18f, 32f, 46f };
            Vector3[] positions = new Vector3[3];
            Vector3[] facings = new Vector3[3];
            RingPath.Place(Corner, distances, positions, facings);
            AssertNear(new Vector3(0f, 0f, 18f), positions[0], "ring 1");
            AssertNear(new Vector3(12f, 0f, 20f), positions[1], "ring 2");
            AssertNear(new Vector3(26f, 0f, 20f), positions[2], "ring 3");
        }

        [Test]
        public void KartOnTopOfTheCheckpoint_SkipsTheEmptyPiece()
        {
            // The kart stands exactly on the next checkpoint: the first piece has no length.
            Vector3[] path = { new Vector3(0f, 0f, 20f), new Vector3(0f, 0f, 20f), new Vector3(30f, 0f, 20f) };
            Vector3 position;
            Vector3 facing;
            RingPath.PointAt(path, 10f, out position, out facing);
            AssertNear(new Vector3(10f, 0f, 20f), position, "position");
            AssertNear(Vector3.right, facing, "facing");
        }

        // A square racing line, 100 m a side, driven east, north, west, south (start line at the origin).
        private static readonly ITrackProgress Square = new TrackPolyline(new[]
        {
            new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f), new Vector3(100f, 0f, 100f), new Vector3(0f, 0f, 100f)
        });

        [Test]
        public void AlongTrack_RingsFollowTheBend()
        {
            // 8 m before the corner, driving east: the third ring (46 m) is already 38 m up the north side.
            Vector3[] path = RingPath.AlongTrack(Square, new Vector3(92f, 0f, 0f), Vector3.right, 50f);
            Assert.IsNotNull(path);
            float[] distances = { 18f, 32f, 46f };
            Vector3[] positions = new Vector3[3];
            Vector3[] facings = new Vector3[3];
            RingPath.Place(path, distances, positions, facings);
            AssertNear(new Vector3(100f, 0f, 10f), positions[0], "ring 1, round the corner");
            AssertNear(new Vector3(100f, 0f, 38f), positions[2], "ring 3");
            AssertNear(Vector3.forward, facings[0], "ring 1 faces north, along the road");
        }

        [Test]
        public void AlongTrack_KartBesideTheLine_RingsStillOnTheLine()
        {
            Vector3[] path = RingPath.AlongTrack(Square, new Vector3(20f, 0f, 4f), Vector3.right, 30f);
            Assert.IsNotNull(path, "4 m beside the line is still on the road");
            AssertNear(new Vector3(20f, 0f, 0f), path[0], "starts on the line next to the kart");
        }

        [Test]
        public void AlongTrack_RunsOverTheStartLine()
        {
            // On the last side, driving south towards the start line: the line wraps round to the first side.
            Vector3[] path = RingPath.AlongTrack(Square, new Vector3(0f, 0f, 12f), Vector3.back, 30f);
            Vector3 position;
            Vector3 facing;
            RingPath.PointAt(path, 20f, out position, out facing);
            AssertNear(new Vector3(8f, 0f, 0f), position, "8 m past the start line");
        }

        [Test]
        public void AlongTrack_OffTheLine_WrongWay_OrNoLine_GivesNull()
        {
            Assert.IsNull(RingPath.AlongTrack(Square, new Vector3(50f, 0f, 15f), Vector3.right, 30f), "15 m off (a shortcut)");
            Assert.IsNotNull(RingPath.AlongTrack(Square, new Vector3(50f, 0f, 10f), Vector3.right, 30f), "10 m off is the edge, still on");
            Assert.IsNull(RingPath.AlongTrack(Square, new Vector3(50f, 0f, 0f), Vector3.left, 30f), "driving the wrong way");
            Assert.IsNull(RingPath.AlongTrack(null, Vector3.zero, Vector3.right, 30f), "track without its own line");
            Assert.IsNull(RingPath.AlongTrack(Square, new Vector3(50f, 0f, 0f), Vector3.right, 0f), "no rings wanted");
        }

        [Test]
        public void GroundNearLine_RingOnTheRoad_NotOverTheJumpGap()
        {
            Assert.IsTrue(RingPath.IsGroundNearLine(14.5f, 14.5f), "road right under the line");
            Assert.IsTrue(RingPath.IsGroundNearLine(14.5f, 13.0f), "1.5 m lower is the edge, still fine");
            Assert.IsFalse(RingPath.IsGroundNearLine(14.5f, 12.9f), "lower than 1.5 m: a gap");
            Assert.IsFalse(RingPath.IsGroundNearLine(14.5f, 6f), "the beach under the jump");
            Assert.IsFalse(RingPath.IsGroundNearLine(14.5f, 16.1f), "a roof above the line, not the road");
        }
    }
}
