using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using CarcavelosCircuit.Track;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 2 tests (T8): the road points become a racing line that starts at the start line and runs the right way.
    public class RacingLineBuilderTests
    {
        // A 40 x 20 rectangle, one point per metre, driven (0,0) -> (40,0) -> (40,20) -> (0,20) -> back. 120 points, 120 m.
        private static List<Vector3> RectangleRoad()
        {
            var points = new List<Vector3>();
            for (int x = 0; x < 40; x++) { points.Add(new Vector3(x, 0f, 0f)); }
            for (int z = 0; z < 20; z++) { points.Add(new Vector3(40f, 0f, z)); }
            for (int x = 40; x > 0; x--) { points.Add(new Vector3(x, 0f, 20f)); }
            for (int z = 20; z > 0; z--) { points.Add(new Vector3(0f, 0f, z)); }
            return points;
        }

        [Test]
        public void StartsAtThePointClosestToTheStartLine()
        {
            List<Vector3> line = RacingLineBuilder.Build(RectangleRoad(), new Vector3(10.2f, 2f, 0.5f), Vector3.right, 0f, 4f);
            Assert.AreEqual(new Vector3(10f, 0f, 0f), line[0]);
            Assert.AreEqual(new Vector3(11f, 0f, 0f), line[1]); // order kept, driving direction +x
            Assert.AreEqual(120, line.Count);
        }

        [Test]
        public void TurnsTheLineRound_WhenItRunsAgainstTheDrivingDirection()
        {
            List<Vector3> line = RacingLineBuilder.Build(RectangleRoad(), new Vector3(10f, 0f, 0f), Vector3.left, 0f, 4f);
            Assert.AreEqual(new Vector3(10f, 0f, 0f), line[0]);
            Assert.AreEqual(new Vector3(9f, 0f, 0f), line[1]);
            Assert.AreEqual(new Vector3(11f, 0f, 0f), line[line.Count - 1]);
        }

        [Test]
        public void NoDirection_KeepsTheBuildersOrder()
        {
            List<Vector3> line = RacingLineBuilder.Build(RectangleRoad(), new Vector3(10f, 0f, 0f), Vector3.zero, 0f, 4f);
            Assert.AreEqual(new Vector3(11f, 0f, 0f), line[1]);
        }

        [Test]
        public void DropsDuplicatePoints_AndTheRepeatedFirstPointAtTheEnd()
        {
            List<Vector3> road = RectangleRoad();
            road.Insert(5, road[5]);             // the same point twice in a row
            road.Add(new Vector3(0f, 0f, 0f));   // loop drawn closed: first point repeated at the end
            List<Vector3> line = RacingLineBuilder.Build(road, Vector3.zero, Vector3.right, 0f, 4f);
            Assert.AreEqual(120, line.Count);
            for (int i = 0; i < line.Count; i++)
            {
                Vector3 next = line[(i + 1) % line.Count];
                Assert.Greater(Vector3.Distance(line[i], next), 0.5f, "duplicate at " + i);
            }
        }

        [Test]
        public void Thinning_KeepsTheCorners_AndTheLapLength()
        {
            List<Vector3> line = RacingLineBuilder.Build(RectangleRoad(), Vector3.zero, Vector3.right, 6f, 4f);
            Assert.Less(line.Count, 40);                         // far fewer than the 120 road points
            Assert.Contains(new Vector3(40f, 0f, 0f), line);     // every corner stays
            Assert.Contains(new Vector3(40f, 0f, 20f), line);
            Assert.Contains(new Vector3(0f, 0f, 20f), line);
            Assert.AreEqual(Vector3.zero, line[0]);              // start line still first
            Assert.AreEqual(120f, new TrackPolyline(line).LapLength, 0.001f);
        }

        [Test]
        public void Thinning_NoGapLongerThanTheSpacingPlusOnePoint()
        {
            List<Vector3> line = RacingLineBuilder.Build(RectangleRoad(), Vector3.zero, Vector3.right, 6f, 4f);
            for (int i = 0; i < line.Count; i++)
            {
                Assert.LessOrEqual(Vector3.Distance(line[i], line[(i + 1) % line.Count]), 7.001f, "gap after " + i);
            }
        }

        [Test]
        public void TooFewPointsOrNull_GiveAnEmptyLine()
        {
            Assert.AreEqual(0, RacingLineBuilder.Build(null, Vector3.zero, Vector3.right, 6f, 4f).Count);
            var two = new List<Vector3> { Vector3.zero, Vector3.right };
            Assert.AreEqual(0, RacingLineBuilder.Build(two, Vector3.zero, Vector3.right, 6f, 4f).Count);
            var sameThreeTimes = new List<Vector3> { Vector3.zero, Vector3.zero, Vector3.zero };
            Assert.AreEqual(0, RacingLineBuilder.Build(sameThreeTimes, Vector3.zero, Vector3.right, 6f, 4f).Count);
        }

        [Test]
        public void TrackProgress_MeasuresFromTheStartLine()
        {
            var go = new GameObject("Track");
            try
            {
                go.transform.position = new Vector3(10f, 0f, 0f); // no checkpoints: the object itself is the start
                go.AddComponent<TrackInfo>();
                TrackProgress progress = go.AddComponent<TrackProgress>();
                progress.SetRoadPoints(RectangleRoad());
                Assert.AreEqual(120f, progress.LapLength, 0.001f);
                Assert.AreEqual(0f, progress.DistanceAlongLap(new Vector3(10f, 1f, 0.3f)), 0.001f);
                Assert.AreEqual(5f, progress.DistanceAlongLap(new Vector3(15f, 0f, -1f)), 0.001f);
                Assert.AreEqual(new Vector3(40f, 0f, 0f), progress.PointAt(30f));
                Assert.AreEqual(Vector3.forward, progress.DirectionAt(31f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TrackProgress_WithoutPoints_HasNoLap()
        {
            var go = new GameObject("Track");
            try
            {
                go.AddComponent<TrackInfo>();
                TrackProgress progress = go.AddComponent<TrackProgress>();
                Assert.AreEqual(0f, progress.LapLength);
                Assert.AreEqual(0f, progress.DistanceAlongLap(Vector3.one));
                Assert.AreEqual(0, progress.RacingLine.Count);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
