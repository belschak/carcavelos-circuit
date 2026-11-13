using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R9): any track size fits the minimap, keeps its shape and is turned start-up.
    public class MinimapFitTests
    {
        private static readonly Vector3[] Square =
        {
            new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f), new Vector3(100f, 0f, 100f), new Vector3(0f, 0f, 100f)
        };

        private static void AssertPoint(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.01f, "x");
            Assert.AreEqual(expected.y, actual.y, 0.01f, "y");
        }

        [Test]
        public void Square_FillsTheMapInsideThePadding_NorthUp()
        {
            MinimapFit fit = new MinimapFit(Square, Vector3.forward, 168f, 168f, 18f);
            Assert.IsTrue(fit.IsValid);
            Assert.AreEqual(132f / 100f, fit.Scale, 0.0001f);
            AssertPoint(new Vector2(18f, 150f), fit.ToMap(new Vector3(0f, 0f, 0f)));    // south-west = bottom left (y down)
            AssertPoint(new Vector2(150f, 18f), fit.ToMap(new Vector3(100f, 0f, 100f))); // north-east = top right
            AssertPoint(new Vector2(84f, 84f), fit.ToMap(new Vector3(50f, 0f, 50f)));   // centre
        }

        [Test]
        public void HugeAndTinyTracks_FitTheSameMap()
        {
            List<Vector3> big = new List<Vector3>();
            List<Vector3> small = new List<Vector3>();
            for (int i = 0; i < Square.Length; i++)
            {
                big.Add(Square[i] * 40f);    // 4 km
                small.Add(Square[i] * 0.1f); // 10 m
            }
            MinimapFit bigFit = new MinimapFit(big, Vector3.forward, 168f, 168f, 18f);
            MinimapFit smallFit = new MinimapFit(small, Vector3.forward, 168f, 168f, 18f);
            AssertPoint(new Vector2(150f, 18f), bigFit.ToMap(new Vector3(4000f, 0f, 4000f)));
            AssertPoint(new Vector2(150f, 18f), smallFit.ToMap(new Vector3(10f, 0f, 10f)));
        }

        [Test]
        public void WideTrack_KeepsItsShape_AndIsCentred()
        {
            Vector3[] wide = { new Vector3(0f, 0f, 0f), new Vector3(200f, 0f, 0f), new Vector3(200f, 0f, 50f), new Vector3(0f, 0f, 50f) };
            MinimapFit fit = new MinimapFit(wide, Vector3.forward, 168f, 168f, 18f);
            Assert.AreEqual(132f / 200f, fit.Scale, 0.0001f, "the long side decides");
            Vector2 a = fit.ToMap(new Vector3(0f, 0f, 0f));
            Vector2 b = fit.ToMap(new Vector3(0f, 0f, 50f));
            Assert.AreEqual(18f, a.x, 0.01f);
            Assert.AreEqual(84f + 16.5f, a.y, 0.01f, "vertically centred: 33 px tall around 84");
            Assert.AreEqual(84f - 16.5f, b.y, 0.01f);
        }

        [Test]
        public void StartDirection_PointsUp()
        {
            // Start faces +X: then +X is up on the map and +Z is left.
            MinimapFit fit = new MinimapFit(Square, Vector3.right, 168f, 168f, 18f);
            Vector2 centre = fit.ToMap(new Vector3(50f, 0f, 50f));
            Vector2 east = fit.ToMap(new Vector3(100f, 0f, 50f));
            Vector2 north = fit.ToMap(new Vector3(50f, 0f, 100f));
            Assert.Less(east.y, centre.y, "east (the start direction) is up");
            Assert.AreEqual(centre.x, east.x, 0.01f);
            Assert.Less(north.x, centre.x, "north is to the left");
            Assert.AreEqual(0f, fit.ToMapAngle(Vector3.right), 0.01f);
            Assert.AreEqual(-90f, fit.ToMapAngle(Vector3.forward), 0.01f);
            Assert.AreEqual(90f, fit.ToMapAngle(Vector3.back), 0.01f);
        }

        [Test]
        public void Heading_IsClockwiseFromUp()
        {
            MinimapFit fit = new MinimapFit(Square, Vector3.forward, 168f, 168f, 18f);
            Assert.AreEqual(0f, fit.ToMapAngle(Vector3.forward), 0.01f);
            Assert.AreEqual(90f, fit.ToMapAngle(Vector3.right), 0.01f);
            Assert.AreEqual(180f, Mathf.Abs(fit.ToMapAngle(Vector3.back)), 0.01f);
            Assert.AreEqual(0f, fit.ToMapAngle(Vector3.up), 0.01f, "straight up (no flat direction) = 0");
        }

        [Test]
        public void HeightIsIgnored()
        {
            MinimapFit fit = new MinimapFit(Square, Vector3.forward, 168f, 168f, 18f);
            AssertPoint(fit.ToMap(new Vector3(30f, 0f, 30f)), fit.ToMap(new Vector3(30f, 25f, 30f)));
        }

        [Test]
        public void ZeroOrNaNUp_FallsBackToNorth()
        {
            MinimapFit zero = new MinimapFit(Square, Vector3.zero, 168f, 168f, 18f);
            MinimapFit nan = new MinimapFit(Square, new Vector3(float.NaN, 0f, 1f), 168f, 168f, 18f);
            AssertPoint(new Vector2(150f, 18f), zero.ToMap(new Vector3(100f, 0f, 100f)));
            AssertPoint(new Vector2(150f, 18f), nan.ToMap(new Vector3(100f, 0f, 100f)));
        }

        [Test]
        public void NotEnoughPoints_OrNoSize_IsInvalid()
        {
            Assert.IsFalse(new MinimapFit(null, Vector3.forward, 168f, 168f, 18f).IsValid);
            Assert.IsFalse(new MinimapFit(new Vector3[0], Vector3.forward, 168f, 168f, 18f).IsValid);
            Assert.IsFalse(new MinimapFit(new[] { Vector3.one }, Vector3.forward, 168f, 168f, 18f).IsValid);
            Assert.IsFalse(new MinimapFit(new[] { Vector3.one, Vector3.one, Vector3.one }, Vector3.forward, 168f, 168f, 18f).IsValid, "all on one spot");
            Assert.IsFalse(new MinimapFit(Square, Vector3.forward, 30f, 30f, 18f).IsValid, "map smaller than its padding");
            Vector3[] withNaN = { new Vector3(float.NaN, 0f, 0f), Vector3.zero };
            Assert.IsFalse(new MinimapFit(withNaN, Vector3.forward, 168f, 168f, 18f).IsValid, "NaN points are skipped");
            // An invalid fit never throws and puts everything in the middle.
            AssertPoint(new Vector2(84f, 84f), new MinimapFit(null, Vector3.forward, 168f, 168f, 18f).ToMap(new Vector3(5f, 0f, 5f)));
        }

        [Test]
        public void StraightLine_UsesTheLongSide()
        {
            Vector3[] line = { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 100f) };
            MinimapFit fit = new MinimapFit(line, Vector3.forward, 168f, 168f, 18f);
            Assert.IsTrue(fit.IsValid);
            Assert.AreEqual(1.32f, fit.Scale, 0.0001f);
        }

        [Test]
        public void Clamp_KeepsADotOnTheMap()
        {
            MinimapFit fit = new MinimapFit(Square, Vector3.forward, 168f, 168f, 18f);
            Vector2 far = fit.ToMap(new Vector3(5000f, 0f, -5000f)); // fell off the world
            AssertPoint(new Vector2(163f, 163f), fit.Clamp(far, 5f));
            AssertPoint(new Vector2(40f, 50f), fit.Clamp(new Vector2(40f, 50f), 5f));
        }
    }
}
