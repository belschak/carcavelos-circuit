using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R7): where the rows of item boxes go, measured from the checkpoints.
    public class ItemBoxLayoutTests
    {
        [TestCase(0, 2, true)]
        [TestCase(1, 2, false)]
        [TestCase(2, 2, true)]
        [TestCase(4, 2, true)]
        [TestCase(3, 3, true)]
        [TestCase(5, 1, true)]
        [TestCase(2, 0, false)]  // 0 = no rows at all
        [TestCase(-1, 2, false)]
        public void HasRow(int index, int every, bool expected)
        {
            Assert.AreEqual(expected, ItemBoxLayout.HasRow(index, every));
        }

        [Test]
        public void FlatDirection_IgnoresHeight()
        {
            Vector3 d = ItemBoxLayout.FlatDirection(Vector3.zero, new Vector3(0f, 5f, 10f));
            Assert.AreEqual(0f, d.y, 1e-5f);
            Assert.AreEqual(1f, d.z, 1e-5f);
        }

        [Test]
        public void FlatDirection_SamePoint_IsForward()
        {
            Assert.AreEqual(Vector3.forward, ItemBoxLayout.FlatDirection(Vector3.one, Vector3.one + Vector3.up));
        }

        [Test]
        public void RowCentre_IsAheadMetresAlongTheRoad()
        {
            Vector3 c = ItemBoxLayout.RowCentre(Vector3.zero, new Vector3(100f, 0f, 0f), 14f);
            Assert.AreEqual(14f, c.x, 1e-4f);
            Assert.AreEqual(0f, c.z, 1e-4f);
        }

        [Test]
        public void RowCentre_OnAShortGap_StaysWithin40Percent()
        {
            Vector3 c = ItemBoxLayout.RowCentre(Vector3.zero, new Vector3(20f, 0f, 0f), 14f);
            Assert.AreEqual(8f, c.x, 1e-4f);
        }

        [Test]
        public void RowPositions_AreSpacedAcrossTheRoad_AndCentred()
        {
            Vector3 from = Vector3.zero;
            Vector3 to = new Vector3(0f, 0f, 50f); // driving towards +Z, so the row runs along X
            Vector3 centre = new Vector3(0f, 0f, 14f);
            Vector3[] p = ItemBoxLayout.RowPositions(centre, from, to, 3, 2.6f);
            Assert.AreEqual(3, p.Length);
            Assert.AreEqual(-2.6f, p[0].x, 1e-4f);
            Assert.AreEqual(0f, p[1].x, 1e-4f);
            Assert.AreEqual(2.6f, p[2].x, 1e-4f);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(14f, p[i].z, 1e-4f); // all on one line across the road
            }
        }

        [Test]
        public void RowPositions_AreAtRightAnglesToTheDrivingDirection()
        {
            Vector3 from = Vector3.zero;
            Vector3 to = new Vector3(30f, 0f, 30f);
            Vector3[] p = ItemBoxLayout.RowPositions(Vector3.zero, from, to, 2, 3f);
            Vector3 across = p[1] - p[0];
            Assert.AreEqual(0f, Vector3.Dot(across, ItemBoxLayout.FlatDirection(from, to)), 1e-4f);
            Assert.AreEqual(3f, across.magnitude, 1e-4f);
        }

        [Test]
        public void RowPositions_ZeroBoxes_IsEmpty()
        {
            Assert.AreEqual(0, ItemBoxLayout.RowPositions(Vector3.zero, Vector3.zero, Vector3.forward, 0, 2f).Length);
        }

        [Test]
        public void RowDirection_UsesTheRoadForward_NotTheShortcutToTheNextCheckpoint()
        {
            // the next checkpoint is straight ahead (+Z), but the road leaves this checkpoint towards +X (a bend)
            Vector3 dir = ItemBoxLayout.RowDirection(Vector3.zero, new Vector3(0f, 0f, 50f), new Vector3(2f, 0f, 0f));
            Assert.AreEqual(1f, dir.x, 0.0001f);
            Assert.AreEqual(0f, dir.z, 0.0001f);
        }

        [Test]
        public void RowDirection_IgnoresHeight_AndFallsBackToTheNextCheckpoint()
        {
            Vector3 tilted = ItemBoxLayout.RowDirection(Vector3.zero, Vector3.forward, new Vector3(0f, 5f, 1f));
            Assert.AreEqual(0f, tilted.y, 0.0001f);
            Assert.AreEqual(1f, tilted.z, 0.0001f);
            Vector3 none = ItemBoxLayout.RowDirection(Vector3.zero, new Vector3(30f, 0f, 0f), Vector3.zero);
            Assert.AreEqual(1f, none.x, 0.0001f); // no respawn point: the line to the next checkpoint
            Vector3 straightUp = ItemBoxLayout.RowDirection(Vector3.zero, new Vector3(30f, 0f, 0f), Vector3.up);
            Assert.AreEqual(1f, straightUp.x, 0.0001f); // pointing straight up says nothing about the road
        }

        [Test]
        public void RowAlongADirection_IsAheadOnTheRoad_AndAcrossIt()
        {
            Vector3 centre = ItemBoxLayout.RowCentre(Vector3.zero, new Vector3(0f, 0f, 100f), Vector3.right, 14f);
            Assert.AreEqual(14f, centre.x, 0.0001f);
            Vector3[] row = ItemBoxLayout.RowPositions(centre, Vector3.right, 3, 2f);
            Assert.AreEqual(3, row.Length);
            Assert.AreEqual(14f, row[0].x, 0.0001f); // across the road = along Z when driving along X
            Assert.AreEqual(2f, Mathf.Abs(row[2].z - row[1].z), 0.0001f);
        }
    }
}
