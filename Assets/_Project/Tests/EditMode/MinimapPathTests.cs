using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R9): the smooth minimap line goes through every corner point, starts at the start line.
    public class MinimapPathTests
    {
        private static readonly Vector2[] Square = { new Vector2(0f, 0f), new Vector2(100f, 0f), new Vector2(100f, 100f), new Vector2(0f, 100f) };

        [Test]
        public void Smooth_GoesThroughEveryPoint_StartsAtTheFirst()
        {
            List<Vector2> smooth = MinimapPath.SmoothClosed(Square, 8);
            Assert.AreEqual(32, smooth.Count, "8 points per piece, 4 pieces, no repeated end");
            for (int i = 0; i < Square.Length; i++)
            {
                Assert.AreEqual(Square[i].x, smooth[i * 8].x, 0.001f);
                Assert.AreEqual(Square[i].y, smooth[i * 8].y, 0.001f);
            }
        }

        [Test]
        public void Smooth_BendsOutwardsBetweenCorners_LikeARoad()
        {
            List<Vector2> smooth = MinimapPath.SmoothClosed(Square, 8);
            Vector2 middleOfFirstSide = smooth[4];
            Assert.AreEqual(50f, middleOfFirstSide.x, 0.001f);
            Assert.Less(middleOfFirstSide.y, 0f, "a rounded square bulges a little outwards");
        }

        [Test]
        public void TooFewPoints_ComeBackUnchanged_NullIsEmpty()
        {
            Assert.AreEqual(0, MinimapPath.SmoothClosed(null, 8).Count);
            List<Vector2> two = MinimapPath.SmoothClosed(new[] { Vector2.zero, Vector2.one }, 8);
            Assert.AreEqual(2, two.Count);
        }

        [Test]
        public void Steps_AreClampedToASaneRange()
        {
            Assert.AreEqual(8, MinimapPath.SmoothClosed(Square, 0).Count, "at least 2 per piece");
            Assert.AreEqual(96, MinimapPath.SmoothClosed(Square, 1000).Count, "at most 24 per piece");
        }

        [Test]
        public void Thin_KeepsEveryNthPoint_AndTheFirst()
        {
            List<Vector2> dense = new List<Vector2>();
            for (int i = 0; i < 1000; i++)
            {
                dense.Add(new Vector2(i, 0f));
            }
            List<Vector2> thin = MinimapPath.Thin(dense, 120);
            Assert.LessOrEqual(thin.Count, 120);
            Assert.Greater(thin.Count, 100);
            Assert.AreEqual(0f, thin[0].x);
            Assert.AreEqual(4, MinimapPath.Thin(Square, 120).Count, "a short line stays as it is");
            Assert.AreEqual(4, MinimapPath.Thin(Square, 0).Count, "0 = keep all");
            Assert.AreEqual(0, MinimapPath.Thin(null, 10).Count);
        }

        [Test]
        public void ClosedLength_IncludesTheWayBack()
        {
            Assert.AreEqual(400f, MinimapPath.ClosedLength(Square), 0.001f);
            Assert.AreEqual(0f, MinimapPath.ClosedLength(null));
        }
    }
}
