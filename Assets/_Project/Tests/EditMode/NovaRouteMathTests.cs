using CarcavelosCircuit.Track;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Tests for the route maths used by the Nova campus track builder (help for Role 2).
    public class NovaRouteMathTests
    {
        private static Vector3[] Square(float side)
        {
            return new[] { new Vector3(0f, 0f, 0f), new Vector3(side, 0f, 0f), new Vector3(side, 0f, side), new Vector3(0f, 0f, side) };
        }

        [Test]
        public void LoopLength_IncludesClosingSegment()
        {
            Assert.AreEqual(400f, NovaRouteMath.LoopLength(Square(100f)), 0.01f);
        }

        [Test]
        public void TurnRadius_OfPointsOnACircle_IsTheCircleRadius()
        {
            float r = 12f;
            Vector3 a = new Vector3(r, 0f, 0f);
            Vector3 b = new Vector3(0f, 0f, r);
            Vector3 c = new Vector3(-r, 0f, 0f);
            Assert.AreEqual(r, NovaRouteMath.TurnRadius(a, b, c), 0.01f);
        }

        [Test]
        public void TurnRadius_OfAStraightLine_IsHuge()
        {
            Assert.AreEqual(float.MaxValue, NovaRouteMath.TurnRadius(Vector3.zero, Vector3.right, Vector3.right * 2f));
        }

        [Test]
        public void MaxSlope_MeasuresRiseOverRun()
        {
            Vector3[] ramp = { new Vector3(0f, 0f, 0f), new Vector3(0f, 1f, 10f), new Vector3(0f, 1f, 20f) };
            Assert.AreEqual(10f, NovaRouteMath.MaxSlopePercent(ramp), 0.01f, "1 m up over 10 m = 10 %");
        }

        [Test]
        public void Right_IsPerpendicularAndFlat()
        {
            Vector3[] line = { new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f) };
            Vector3 right = NovaRouteMath.Right(line, 1);
            Assert.AreEqual(1f, right.x, 0.001f, "driving north, right is east");
            Assert.AreEqual(0f, right.y, 0.001f);
        }

        [Test]
        public void Smooth_PassesThroughTheOriginalPoints()
        {
            Vector3[] square = Square(40f);
            Vector3[] smooth = NovaRouteMath.Smooth(square, 5f);
            Assert.Greater(smooth.Length, square.Length);
            Assert.AreEqual(square[0], smooth[0]);
            float length = NovaRouteMath.LoopLength(smooth);
            Assert.Greater(length, 160f, "the curve bulges a little outside the straight sides");
            Assert.Less(length, 185f, "but stays close to the original loop");
        }

        [Test]
        public void ClosestIndex_FindsNearestPoint()
        {
            Assert.AreEqual(2, NovaRouteMath.ClosestIndex(Square(100f), new Vector3(90f, 5f, 95f)));
        }
    }
}
