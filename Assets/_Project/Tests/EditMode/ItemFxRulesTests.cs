using CarcavelosCircuit.Visuals;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 3 tests (task V34): the item effects' maths (ItemFxRules), the Carcavelos Wave's water round the kart
    // (WaveMesh), the new item box body (RoundedCube) and face (AzulejoTile), and the hotter Espresso flame.
    public class ItemFxRulesTests
    {
        // ---------- envelope ----------

        [Test]
        public void Envelope_ZeroBefore_RisesToOne_HoldsThenFallsToZero()
        {
            Assert.AreEqual(0f, ItemFxRules.Envelope(-0.1f, 0.3f, 2f, 0.5f));
            Assert.AreEqual(0f, ItemFxRules.Envelope(0f, 0.3f, 2f, 0.5f), 1e-5f);
            float early = ItemFxRules.Envelope(0.1f, 0.3f, 2f, 0.5f);
            float later = ItemFxRules.Envelope(0.2f, 0.3f, 2f, 0.5f);
            Assert.Greater(early, 0f);
            Assert.Greater(later, early, "rises during the form time");
            Assert.AreEqual(1f, ItemFxRules.Envelope(1f, 0.3f, 2f, 0.5f), "holds at 1");
            Assert.Less(ItemFxRules.Envelope(2.6f, 0.3f, 2f, 0.5f), 1f, "falls in the collapse");
            Assert.AreEqual(0f, ItemFxRules.Envelope(2.9f, 0.3f, 2f, 0.5f), "gone after form + hold + collapse");
            Assert.AreEqual(2.8f, ItemFxRules.Length(0.3f, 2f, 0.5f), 1e-5f);
        }

        [Test]
        public void Envelope_ZeroFormAndCollapse_IsAHardEdge_NoNaN()
        {
            Assert.AreEqual(1f, ItemFxRules.Envelope(0f, 0f, 1f, 0f));
            Assert.AreEqual(0f, ItemFxRules.Envelope(1.01f, 0f, 1f, 0f));
        }

        // ---------- round the kart ----------

        [Test]
        public void AroundKart_GivesFrontSideBack_AndIsTheSameLeftAndRight()
        {
            Assert.AreEqual(1f, ItemFxRules.AroundKart(0f, 1f, 2f, 3f), 1e-5f);
            Assert.AreEqual(2f, ItemFxRules.AroundKart(90f, 1f, 2f, 3f), 1e-5f);
            Assert.AreEqual(3f, ItemFxRules.AroundKart(180f, 1f, 2f, 3f), 1e-5f);
            Assert.AreEqual(ItemFxRules.AroundKart(60f, 1f, 2f, 3f), ItemFxRules.AroundKart(-60f, 1f, 2f, 3f), 1e-5f);
            Assert.AreEqual(ItemFxRules.AroundKart(135f, 1f, 2f, 3f), ItemFxRules.AroundKart(225f, 1f, 2f, 3f), 1e-5f);
        }

        [Test]
        public void Ellipse_ForwardIsPlusZ_RightIsPlusX()
        {
            Vector3 front = ItemFxRules.Ellipse(0f, 0.6f, 1f);
            Vector3 right = ItemFxRules.Ellipse(90f, 0.6f, 1f);
            Assert.AreEqual(1f, front.z, 1e-5f);
            Assert.AreEqual(0.6f, right.x, 1e-5f);
            Assert.AreEqual(0f, front.y);
        }

        // ---------- the Carcavelos Wave's water ----------

        private static Vector3[] Wave(float rise, float collapse, float time, int segments)
        {
            Vector3[] v = new Vector3[WaveMesh.VertexCount(segments)];
            Vector2[] uv = new Vector2[v.Length];
            WaveMesh.Write(WaveShape.Default, rise, collapse, time, segments, v, uv);
            return v;
        }

        [Test]
        public void WaveMesh_TrianglesUseOnlyItsVertices_AndCoverEveryQuad()
        {
            const int segments = 40;
            int[] triangles = WaveMesh.Triangles(segments);
            int vertexCount = WaveMesh.VertexCount(segments);
            Assert.AreEqual(segments * (WaveMesh.Rows - 1) * 6, triangles.Length, "every quad drawn once per layer");
            foreach (int i in triangles) Assert.That(i, Is.InRange(0, vertexCount - 1));
        }

        [Test]
        public void WaveMesh_SurroundsTheKart_HighestBehind_LowInFront_NoNaN()
        {
            Vector3[] v = Wave(1f, 0f, 0.7f, 40);
            float front = 0f, back = 0f, left = 0f, right = 0f;
            foreach (Vector3 p in v)
            {
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z));
                Assert.GreaterOrEqual(p.y, 0f, "never below the road");
                if (p.z > 1.0f && Mathf.Abs(p.x) < 0.5f) front = Mathf.Max(front, p.y);
                if (p.z < -1.0f && Mathf.Abs(p.x) < 0.5f) back = Mathf.Max(back, p.y);
                if (p.x < -0.7f && Mathf.Abs(p.z) < 0.5f) left = Mathf.Max(left, p.y);
                if (p.x > 0.7f && Mathf.Abs(p.z) < 0.5f) right = Mathf.Max(right, p.y);
            }
            Assert.Greater(front, 0.1f, "water in front");
            Assert.Greater(left, 0.3f, "a wall of water on the left");
            Assert.Greater(right, 0.3f, "and on the right");
            Assert.Greater(back, left, "the big crest is behind");
            Assert.Greater(left, front, "a low bow wave in front, the road ahead stays visible");
        }

        [Test]
        public void WaveMesh_InnerEdge_StaysUnderTheKartFloor()
        {
            Vector3[] v = Wave(1f, 0f, 1.3f, 40);
            for (int c = 0; c <= 40; c++)
            {
                Assert.LessOrEqual(v[c * WaveMesh.Rows].y, WaveShape.Default.MaxInnerHeight + 1e-4f);
            }
        }

        [Test]
        public void WaveMesh_NeverHidesTheKart_FromTheChaseCamera()
        {
            // Chase camera about 5.5 m behind and 2.3 m up, looking at the kart's middle (0.6 m). Every water point
            // behind the kart must stay under that sight line, at any moment of the ripple.
            Vector3 camera = new Vector3(0f, 2.3f, -5.5f);
            Vector3 target = new Vector3(0f, 0.6f, 0f);
            for (float t = 0f; t < 3f; t += 0.13f)
            {
                foreach (Vector3 p in Wave(1f, 0f, t, 40))
                {
                    if (p.z >= -0.2f || Mathf.Abs(p.x) > 1.0f)
                    {
                        continue; // only the water between the camera and the kart's body
                    }
                    float along = (p.z - target.z) / (camera.z - target.z); // 0 at the kart, 1 at the camera
                    float lineHeight = Mathf.Lerp(target.y, camera.y, along);
                    Assert.Less(p.y, lineHeight, "water at z " + p.z.ToString("0.00") + " covers the kart");
                }
            }
        }

        [Test]
        public void WaveMesh_Flat_BeforeItRises_AndAfterItCollapsed()
        {
            foreach (Vector3 p in Wave(0f, 0f, 0.5f, 24)) Assert.AreEqual(0f, p.y, 1e-5f);
            foreach (Vector3 p in Wave(1f, 1f, 0.5f, 24)) Assert.AreEqual(0f, p.y, 1e-5f);
        }

        [Test]
        public void WaveMesh_Collapse_SpreadsTheWaterOutwards()
        {
            float surf = 0f, falling = 0f;
            foreach (Vector3 p in Wave(1f, 0f, 0f, 24)) surf = Mathf.Max(surf, new Vector2(p.x, p.z).magnitude);
            foreach (Vector3 p in Wave(1f, 0.6f, 0f, 24)) falling = Mathf.Max(falling, new Vector2(p.x, p.z).magnitude);
            Assert.Greater(falling, surf);
        }

        // ---------- small rules ----------

        [Test]
        public void Rings_GrowAndFade()
        {
            Assert.AreEqual(0f, ItemFxRules.RingScale(0f, 5f), 1e-5f);
            Assert.AreEqual(5f, ItemFxRules.RingScale(1f, 5f), 1e-5f);
            Assert.Greater(ItemFxRules.RingScale(0.3f, 5f), 2.5f, "fast at first (ease out)");
            Assert.AreEqual(1f, ItemFxRules.RingAlpha(0f), 1e-5f);
            Assert.AreEqual(0f, ItemFxRules.RingAlpha(1f), 1e-5f);
        }

        [Test]
        public void Shield_Blinks_OnlyInTheLastSeconds()
        {
            for (float t = 0f; t < 2f; t += 0.01f)
            {
                Assert.IsTrue(ItemFxRules.ShieldVisible(5f, t, 2f));
            }
            int hidden = 0;
            for (float t = 0f; t < 1f; t += 0.01f)
            {
                if (!ItemFxRules.ShieldVisible(0.5f, t, 2f)) hidden++;
            }
            Assert.Greater(hidden, 10, "blinks in the warning");
            Assert.Less(hidden, 90);
        }

        [Test]
        public void Toss_GoesUpAndComesBack()
        {
            Assert.AreEqual(0f, ItemFxRules.TossHeight(0f, 1f, 2f));
            Assert.AreEqual(0f, ItemFxRules.TossHeight(1f, 1f, 2f));
            Assert.AreEqual(2f, ItemFxRules.TossHeight(0.5f, 1f, 2f), 1e-5f, "the peak in the middle");
        }

        [Test]
        public void GullPath_StartsAndEndsOnItsPoints_AndPassesTheMiddleOnes()
        {
            Vector3[] points = { new Vector3(0, 3, 0), new Vector3(0, 4, 20), new Vector3(2, 4, 40), new Vector3(0, 12, 60) };
            Assert.AreEqual(Vector3.zero, ItemFxRules.PathPoint(points, 0f) - points[0]);
            Assert.Less((ItemFxRules.PathPoint(points, 1f) - points[3]).magnitude, 1e-4f);
            Assert.Less((ItemFxRules.PathPoint(points, 1f / 3f) - points[1]).magnitude, 1e-4f, "flies over ring 1");
            Assert.AreEqual(Vector3.zero, ItemFxRules.PathPoint(new Vector3[0], 0.5f));
            Assert.AreEqual(points[0], ItemFxRules.PathPoint(new[] { points[0] }, 0.5f));
        }

        [Test]
        public void ExitRoll_IsOneFullTurn()
        {
            Assert.AreEqual(0f, ItemFxRules.ExitRoll(0f), 1e-4f);
            Assert.AreEqual(360f, ItemFxRules.ExitRoll(1f), 1e-4f);
            Assert.AreEqual(360f, ItemFxRules.ExitRoll(3f), 1e-4f, "clamped");
        }

        [Test]
        public void EspressoKick_BurnsHotter_OtherBoostsNormal()
        {
            Assert.AreEqual(1f, KartEffectsRules.FlameHeat(false));
            Assert.Greater(KartEffectsRules.FlameHeat(true), 1f);
        }

        // ---------- the new item box ----------

        [Test]
        public void RoundedCube_FitsTheEdge_NormalsUnit_FacesPointOut()
        {
            RoundedCube.Data d = RoundedCube.Build(1f, 0.16f, 8);
            Assert.AreEqual(6 * 9 * 9, d.Vertices.Length);
            Assert.AreEqual(6 * 8 * 8 * 6, d.Triangles.Length);
            foreach (Vector3 p in d.Vertices)
            {
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z)), 0.5f + 1e-4f);
            }
            foreach (Vector3 n in d.Normals)
            {
                Assert.AreEqual(1f, n.magnitude, 1e-4f);
            }
            for (int t = 0; t < d.Triangles.Length; t += 3)
            {
                Vector3 a = d.Vertices[d.Triangles[t]];
                Vector3 b = d.Vertices[d.Triangles[t + 1]];
                Vector3 c = d.Vertices[d.Triangles[t + 2]];
                Vector3 face = Vector3.Cross(b - a, c - a);
                if (face.sqrMagnitude < 1e-12f)
                {
                    continue; // folded corner quads have no area
                }
                Assert.Greater(Vector3.Dot(face, (a + b + c) / 3f), 0f, "front face outwards (clockwise seen from outside)");
            }
        }

        [Test]
        public void RoundedCube_CornersAreRounded()
        {
            RoundedCube.Data d = RoundedCube.Build(1f, 0.16f, 8);
            float far = 0f;
            foreach (Vector3 p in d.Vertices) far = Mathf.Max(far, p.magnitude);
            Assert.Less(far, Mathf.Sqrt(3f) * 0.5f - 0.05f, "no sharp corner sticks out");
            Assert.AreEqual(0.5f, d.Vertices[4 + 4 * 9].z, 1e-4f, "the middle of a face stays flat on the cube");
        }

        [Test]
        public void AzulejoTile_ClassicHasOchreHeartAndCobaltFrame_AndIsSymmetric()
        {
            float px = 1f / 256f;
            Color heart = AzulejoTile.ColourAt(0.5f, 0.5f, AzulejoTile.Variant.Classic, px);
            Color frame = AzulejoTile.ColourAt(0.02f, 0.5f, AzulejoTile.Variant.Classic, px);
            Assert.Greater(heart.r, heart.b, "ochre heart");
            Assert.Greater(frame.b, frame.r + 0.3f, "cobalt frame");
            Random.InitState(7);
            for (int i = 0; i < 50; i++)
            {
                float u = Random.value;
                float v = Random.value;
                Color a = AzulejoTile.ColourAt(u, v, AzulejoTile.Variant.Classic, px);
                Color b = AzulejoTile.ColourAt(1f - u, v, AzulejoTile.Variant.Classic, px);
                Assert.AreEqual(a.r, b.r, 0.05f, "mirror symmetric (the glaze shimmer is not)");
                Assert.AreEqual(1f, a.a);
            }
        }

        [Test]
        public void AzulejoTile_WaveVariant_IsTurquoise_AndDiffersFromClassic()
        {
            Color[] wave = AzulejoTile.Paint(32, AzulejoTile.Variant.Wave);
            Color[] classic = AzulejoTile.Paint(32, AzulejoTile.Variant.Classic);
            Assert.AreEqual(32 * 32, wave.Length);
            int different = 0;
            for (int i = 0; i < wave.Length; i++)
            {
                if ((wave[i] - classic[i]).maxColorComponent > 0.1f) different++;
            }
            Assert.Greater(different, wave.Length / 4);
            Color frame = AzulejoTile.ColourAt(0.02f, 0.5f, AzulejoTile.Variant.Wave, 1f / 32f);
            Assert.Greater(frame.g, frame.r + 0.15f, "teal frame");
        }
    }
}
