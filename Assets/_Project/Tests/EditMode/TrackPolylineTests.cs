using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: the racing line maths behind positions, the minimap and the bots (round 2 contract).
    public class TrackPolylineTests
    {
        // A 100 x 50 rectangle driven in this order: lap = 300 m, start at the origin.
        private static TrackPolyline Rectangle()
        {
            return new TrackPolyline(new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(100f, 0f, 0f),
                new Vector3(100f, 0f, 50f),
                new Vector3(0f, 0f, 50f),
            });
        }

        [Test]
        public void LapLength_IsTheClosedLoop()
        {
            Assert.AreEqual(300f, Rectangle().LapLength, 0.001f);
        }

        [Test]
        public void DistanceAlongLap_ProjectsOntoTheClosestPiece()
        {
            TrackPolyline line = Rectangle();
            Assert.AreEqual(40f, line.DistanceAlongLap(new Vector3(40f, 0f, 3f)), 0.001f);   // first straight, a bit off the line
            Assert.AreEqual(125f, line.DistanceAlongLap(new Vector3(102f, 0f, 25f)), 0.001f); // second piece
            Assert.AreEqual(290f, line.DistanceAlongLap(new Vector3(-1f, 0f, 10f)), 0.001f);  // last piece, back towards the start
        }

        [Test]
        public void DistanceAlongLap_IgnoresHeight()
        {
            TrackPolyline line = Rectangle();
            Assert.AreEqual(40f, line.DistanceAlongLap(new Vector3(40f, 12f, 0f)), 0.001f); // in the air over the road
        }

        [Test]
        public void PointAt_WrapsNegativeAndLongDistances()
        {
            TrackPolyline line = Rectangle();
            Assert.That(Vector3.Distance(new Vector3(50f, 0f, 0f), line.PointAt(50f)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(new Vector3(50f, 0f, 0f), line.PointAt(350f)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(new Vector3(0f, 0f, 10f), line.PointAt(-10f)), Is.LessThan(0.001f));
        }

        [Test]
        public void DirectionAt_FollowsDrivingOrder()
        {
            TrackPolyline line = Rectangle();
            Assert.That(Vector3.Distance(Vector3.right, line.DirectionAt(10f)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(Vector3.forward, line.DirectionAt(110f)), Is.LessThan(0.001f));
        }

        [Test]
        public void TooFewPoints_IsInvalid_AndNeverThrows()
        {
            TrackPolyline line = new TrackPolyline(new List<Vector3> { Vector3.zero, Vector3.one });
            Assert.IsFalse(line.IsValid);
            Assert.AreEqual(0f, line.DistanceAlongLap(new Vector3(5f, 0f, 5f)));
            Assert.AreEqual(Vector3.forward, line.DirectionAt(3f));
            Assert.IsFalse(new TrackPolyline(null).IsValid);
        }

        [Test]
        public void DuplicatePoints_AreSkipped()
        {
            TrackPolyline line = new TrackPolyline(new List<Vector3>
            {
                Vector3.zero, Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f),
            });
            Assert.AreEqual(3, line.RacingLine.Count);
        }

        [Test]
        public void Wrap_TurnsNaNIntoZero()
        {
            Assert.AreEqual(0f, Rectangle().Wrap(float.NaN));
            Assert.AreEqual(0f, Rectangle().Wrap(float.PositiveInfinity));
        }

        [Test]
        public void KartInput_Clamped_KeepsBotsInsideTheKeyboardRange()
        {
            KartInput wild = new KartInput(3f, float.NaN, true).Clamped();
            Assert.AreEqual(1f, wild.Throttle);
            Assert.AreEqual(0f, wild.Steer);
            Assert.IsTrue(wild.Drift);
            Assert.AreEqual(-1f, new KartInput(-7f, -2f, false).Clamped().Steer);
        }
    }
}
