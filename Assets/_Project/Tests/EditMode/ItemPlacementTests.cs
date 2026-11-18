using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Package C (17.11.2026): where a row of item boxes stands on a track with its own racing line, which ray hit is
    // the road under a box, how a blocked box is nudged, and the ?test=itemrows / ?test=itemlog flags.
    // The team saw rows missing (atrium bend), on the beach tunnel roof and in the terrace jump's landing zone.
    public class ItemPlacementTests
    {
        // ---- RowDistance ----

        [Test]
        public void RowDistance_IsAheadMetresAfterTheCheckpoint()
        {
            Assert.AreEqual(114f, ItemBoxLayout.RowDistance(100f, 80f, 14f), 1e-4f);
        }

        [Test]
        public void RowDistance_NeverMoreThan40PercentOfTheGap()
        {
            Assert.AreEqual(108f, ItemBoxLayout.RowDistance(100f, 20f, 14f), 1e-4f);
        }

        [Test]
        public void RowDistance_UnknownGap_KeepsAheadMetres()
        {
            Assert.AreEqual(114f, ItemBoxLayout.RowDistance(100f, 0f, 14f), 1e-4f);
        }

        // ---- OutOfJumpLanding ----

        // The terrace jump in track.json: flat at 14.56 m, the edge at 164 m, the lawn 4.8 m lower from 166 m on.
        private static float Terrace(float d)
        {
            if (d <= 164f) return 14.56f;
            if (d >= 166f) return 10.77f;
            return Mathf.Lerp(14.56f, 10.77f, (d - 164f) / 2f);
        }

        [Test]
        public void OutOfJumpLanding_RowAfterAJumpEdge_MovesClearOfTheLanding()
        {
            // Checkpoint 2 at 158 m, 86 m to checkpoint 3: the old row at 172 m was in the landing zone.
            float row = ItemBoxLayout.OutOfJumpLanding(158f, 172f, 86f, Terrace);
            Assert.GreaterOrEqual(row, 164f + ItemBoxLayout.ClearAfterJumpMetres - 1.01f, "clear of the landing");
            Assert.GreaterOrEqual(row - 164f, 61f, "past the longest flight off the boosted terrace jump (BoostPadPlan)");
            Assert.LessOrEqual(row, 158f + 86f * ItemBoxLayout.MaxShareOfGap, "still before the next checkpoint");
        }

        [Test]
        public void OutOfJumpLanding_SimplifiedLine_StillFindsTheJump()
        {
            // The racing line keeps a point every 6 m on straights: the drop is spread over 6 m (0.8 m per metre).
            System.Func<float, float> height = d => d <= 162f ? 14.56f : d >= 168f ? 10.77f : Mathf.Lerp(14.56f, 10.77f, (d - 162f) / 6f);
            Assert.Greater(ItemBoxLayout.OutOfJumpLanding(158f, 172f, 86f, height), 190f);
        }

        [Test]
        public void OutOfJumpLanding_FlatRoad_KeepsTheRow()
        {
            Assert.AreEqual(172f, ItemBoxLayout.OutOfJumpLanding(158f, 172f, 86f, d => 7.8f), 1e-4f);
        }

        [Test]
        public void OutOfJumpLanding_DownTheStairs_KeepsTheRow()
        {
            // The steepest drop on the lap apart from the jump is under 0.1 m per metre (stairs, the tunnel slope).
            Assert.AreEqual(172f, ItemBoxLayout.OutOfJumpLanding(158f, 172f, 86f, d => 20f - 0.1f * d), 1e-4f);
        }

        [Test]
        public void OutOfJumpLanding_SmallStep_KeepsTheRow()
        {
            // A kerb: steep but only 40 cm.
            System.Func<float, float> kerb = d => d < 165f ? 5.4f : 5f;
            Assert.AreEqual(172f, ItemBoxLayout.OutOfJumpLanding(158f, 172f, 86f, kerb), 1e-4f);
        }

        [Test]
        public void OutOfJumpLanding_JumpAfterTheRow_KeepsTheRow()
        {
            Assert.AreEqual(150f, ItemBoxLayout.OutOfJumpLanding(140f, 150f, 86f, Terrace), 1e-4f);
        }

        [Test]
        public void OutOfJumpLanding_ShortGap_StaysBeforeTheNextCheckpoint()
        {
            float row = ItemBoxLayout.OutOfJumpLanding(158f, 166f, 30f, Terrace);
            Assert.AreEqual(158f + 30f * ItemBoxLayout.MaxShareOfGap, row, 1e-3f);
        }

        // ---- PickGround ----

        [Test]
        public void PickGround_TunnelMouth_TakesTheRoadNotTheRoof()
        {
            // Under the tunnel portal the ray (from 2 m above the line) sees the road; from higher it would also see
            // the roof at 8.4 m. The roof is far from the line height: never picked.
            float[] heights = { 8.4f, 3.9f };
            float[] normals = { 1f, 1f };
            Assert.AreEqual(1, ItemBoxLayout.PickGround(3.9f, heights, normals, ItemBoxLayout.MaxGroundOffLineMetres));
        }

        [Test]
        public void PickGround_OnlyARoofAbove_NoGround()
        {
            float[] heights = { 8.4f };
            float[] normals = { 1f };
            Assert.AreEqual(-1, ItemBoxLayout.PickGround(3.9f, heights, normals, ItemBoxLayout.MaxGroundOffLineMetres));
        }

        [Test]
        public void PickGround_OverTheJumpGap_NoGround()
        {
            float[] heights = { 10.7f }; // the lawn 4.8 m below the line
            float[] normals = { 1f };
            Assert.AreEqual(-1, ItemBoxLayout.PickGround(15.5f, heights, normals, ItemBoxLayout.MaxGroundOffLineMetres));
        }

        [Test]
        public void PickGround_SkipsWallsAndGuardSides()
        {
            float[] heights = { 4.2f, 3.95f };
            float[] normals = { 0.1f, 0.98f }; // a guard's side, then the road
            Assert.AreEqual(1, ItemBoxLayout.PickGround(3.9f, heights, normals, ItemBoxLayout.MaxGroundOffLineMetres));
        }

        [Test]
        public void PickGround_TakesTheHitClosestToTheLine()
        {
            float[] heights = { 5.2f, 4.0f, 3.1f }; // a bench top, the road, a drain
            float[] normals = { 1f, 1f, 1f };
            Assert.AreEqual(1, ItemBoxLayout.PickGround(3.9f, heights, normals, ItemBoxLayout.MaxGroundOffLineMetres));
        }

        [Test]
        public void PickGround_NoLine_AnyHeight()
        {
            float[] heights = { 1.0f };
            float[] normals = { 1f };
            Assert.AreEqual(0, ItemBoxLayout.PickGround(9f, heights, normals, -1f));
        }

        [Test]
        public void PickGround_NoHits_NoGround()
        {
            Assert.AreEqual(-1, ItemBoxLayout.PickGround(3.9f, new float[0], new float[0], 1.5f));
            Assert.AreEqual(-1, ItemBoxLayout.PickGround(3.9f, null, null, 1.5f));
        }

        // ---- Nudges ----

        [Test]
        public void Nudges_StartOnTheSpot_AndStaySmall()
        {
            Assert.AreEqual(Vector2.zero, ItemBoxLayout.Nudges[0], "first try = the spot itself");
            for (int i = 0; i < ItemBoxLayout.Nudges.Length; i++)
            {
                Assert.LessOrEqual(Mathf.Abs(ItemBoxLayout.Nudges[i].x), 6f, "never more than 6 m along the road");
                Assert.That(ItemBoxLayout.Nudges[i].y, Is.InRange(0f, 0.5f), "at most half way to the middle box");
                for (int j = 0; j < i; j++)
                {
                    Assert.AreNotEqual(ItemBoxLayout.Nudges[j], ItemBoxLayout.Nudges[i], "no try twice");
                }
            }
        }

        [Test]
        public void IsClearOfBoxes_KeepsTheDistance()
        {
            List<Vector3> placed = new List<Vector3> { Vector3.zero };
            Assert.IsFalse(ItemBoxLayout.IsClearOfBoxes(new Vector3(1f, 0f, 0f), placed, 1.8f));
            Assert.IsTrue(ItemBoxLayout.IsClearOfBoxes(new Vector3(2.6f, 0f, 0f), placed, 1.8f), "the row spacing is fine");
            Assert.IsTrue(ItemBoxLayout.IsClearOfBoxes(Vector3.one, null, 1.8f));
        }

        // ---- ItemStop.CameraSpot ----

        [Test]
        public void CameraSpot_IsBehindAndAboveTheRow()
        {
            Vector3 spot = ItemStop.CameraSpot(new Vector3(10f, 4f, 0f), new Vector3(1f, 0.3f, 0f), 9f, 2.6f);
            Assert.AreEqual(1f, spot.x, 1e-4f);
            Assert.AreEqual(6.6f, spot.y, 1e-4f);
            Assert.AreEqual(0f, spot.z, 1e-4f);
        }

        // ---- ItemTestFlags ----

        [TestCase("https://belschak.github.io/carcavelos-circuit/", false, false)]
        [TestCase("", false, false)]
        [TestCase(null, false, false)]
        [TestCase("http://localhost:8783/?test=itemrows", true, true)]
        [TestCase("http://localhost:8783/?photo=0&test=itemrows&autopilot=1", true, true)]
        [TestCase("http://localhost:8783/?test=itemlog&net=local", false, true)]
        [TestCase("http://localhost:8783/?test=other%2Citemrows", true, true)]
        [TestCase("http://localhost:8783/?TEST=ItemRows", true, true)]
        [TestCase("http://localhost:8783/?test=itemrowsx", false, false)]
        [TestCase("http://localhost:8783/?contest=itemrows", false, false)]
        [TestCase("http://localhost:8783/#?test=itemrows", false, false)]
        [TestCase("http://localhost:8783/?a=1#test=itemrows", false, false)]
        public void ItemTestFlags_Parse(string url, bool tour, bool log)
        {
            ItemTestFlags flags = ItemTestFlags.Parse(url);
            Assert.AreEqual(tour, flags.Tour, "tour");
            Assert.AreEqual(log, flags.Log, "log");
        }
    }
}
