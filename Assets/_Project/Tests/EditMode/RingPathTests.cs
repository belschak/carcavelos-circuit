using CarcavelosCircuit.RaceFlow;
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
    }
}
