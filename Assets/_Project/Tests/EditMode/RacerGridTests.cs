using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R8): the starting grid behind the SpawnPoint.
    public class RacerGridTests
    {
        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.001f, "x");
            Assert.AreEqual(expected.y, actual.y, 0.001f, "y");
            Assert.AreEqual(expected.z, actual.z, 0.001f, "z");
        }

        [Test]
        public void SlotZero_IsTheSpawnPoint()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            AssertVector(Vector3.zero, grid.SlotOffset(0));
        }

        [Test]
        public void Slots_AreStaggered_RightThenLeft_OneRowBackEveryTwo()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            AssertVector(new Vector3(2f, 0f, -3f), grid.SlotOffset(1));
            AssertVector(new Vector3(-2f, 0f, -3f), grid.SlotOffset(2));
            AssertVector(new Vector3(2f, 0f, -6f), grid.SlotOffset(3));
            AssertVector(new Vector3(-2f, 0f, -6f), grid.SlotOffset(4));
            AssertVector(new Vector3(2f, 0f, -9f), grid.SlotOffset(5));
        }

        [Test]
        public void NegativeSlot_IsTreatedAsTheSpawnPoint()
        {
            Assert.AreEqual(Vector3.zero, new RacerGrid(2f, 3f).SlotOffset(-3));
        }

        [Test]
        public void SlotPosition_TurnsWithTheSpawnPoint()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            // Spawn faces +X (turned 90 degrees right): "behind" is -X, "right" is -Z.
            Vector3 position = grid.SlotPosition(1, new Vector3(10f, 1f, 5f), Quaternion.Euler(0f, 90f, 0f));
            AssertVector(new Vector3(7f, 1f, 3f), position);
        }

        [Test]
        public void BrokenSpacing_FallsBackToTheDefaults()
        {
            AssertVector(new Vector3(2f, 0f, -3f), new RacerGrid(float.NaN, -1f).SlotOffset(1));
            AssertVector(new Vector3(2f, 0f, -3f), new RacerGrid(0f, 1000f).SlotOffset(1));
        }

        [Test]
        public void Player_AlwaysGetsSlotZero_BotsGetOneTwoThree()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            Assert.AreEqual(1, grid.Assign(new TestRacer("A", false, false)));
            Assert.AreEqual(0, grid.Assign(new TestRacer("You", true, false)));
            Assert.AreEqual(2, grid.Assign(new TestRacer("B", false, false)));
            Assert.AreEqual(3, grid.Assign(new TestRacer("C", false, false)));
            Assert.AreEqual(4, grid.Count);
        }

        [Test]
        public void Assign_Twice_KeepsTheSameSlot()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            TestRacer bot = new TestRacer("A", false, false);
            Assert.AreEqual(1, grid.Assign(bot));
            Assert.AreEqual(1, grid.Assign(bot));
            Assert.AreEqual(1, grid.Count);
        }

        [Test]
        public void Assign_Null_IsRejected()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            Assert.AreEqual(-1, grid.Assign(null));
            Assert.AreEqual(0, grid.Count);
            Assert.AreEqual(-1, grid.SlotOf(null));
        }

        [Test]
        public void FreedSlot_GoesToTheNextBot_LowestFirst()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            TestRacer a = new TestRacer("A", false, false);
            TestRacer b = new TestRacer("B", false, false);
            TestRacer c = new TestRacer("C", false, false);
            grid.Assign(a);
            grid.Assign(b);
            grid.Assign(c);
            Assert.IsTrue(grid.Release(a));
            Assert.IsFalse(grid.Release(a), "released twice");
            Assert.AreEqual(-1, grid.SlotOf(a));
            TestRacer late = new TestRacer("Late", false, false);
            Assert.AreEqual(1, grid.Assign(late), "the late bot takes the freed slot 1");
            Assert.AreEqual(2, grid.SlotOf(b), "the others keep their slots");
            Assert.AreEqual(3, grid.SlotOf(c));
        }

        [Test]
        public void KeepOnly_FreesTheSlotsOfRacersWhoLeft()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            TestRacer a = new TestRacer("A", false, false);
            TestRacer b = new TestRacer("B", false, false);
            grid.Assign(a);
            grid.Assign(b);
            grid.KeepOnly(new TestRacer[] { b });
            Assert.AreEqual(-1, grid.SlotOf(a));
            Assert.AreEqual(2, grid.SlotOf(b));
            grid.KeepOnly(null);
            Assert.AreEqual(0, grid.Count, "nobody racing = no slots");
        }
    }
}
