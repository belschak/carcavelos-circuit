using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R8, V23 pairs): the starting grid at the SpawnPoint.
    public class RacerGridTests
    {
        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.001f, "x");
            Assert.AreEqual(expected.y, actual.y, 0.001f, "y");
            Assert.AreEqual(expected.z, actual.z, 0.001f, "z");
        }

        // V23: pairs side by side. Slot 0 = box 1 = front row left, slot 1 = box 2 = front row right.
        [Test]
        public void FrontRow_IsCentredOnTheSpawnPoint()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            AssertVector(new Vector3(-2f, 0f, 0f), grid.SlotOffset(0));
            AssertVector(new Vector3(2f, 0f, 0f), grid.SlotOffset(1));
            AssertVector(Vector3.zero, (grid.SlotOffset(0) + grid.SlotOffset(1)) * 0.5f);
        }

        [Test]
        public void Slots_ArePairsSideBySide_OddBoxNumbersLeft()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            for (int row = 0; row < 5; row++)
            {
                Vector3 left = grid.SlotOffset(row * 2);       // box 1, 3, 5 ...
                Vector3 right = grid.SlotOffset(row * 2 + 1);  // box 2, 4, 6 ...
                Assert.AreEqual(left.z, right.z, 0.001f, "the two boxes of row " + (row + 1) + " are level");
                Assert.AreEqual(-3f * row, left.z, 0.001f, "3 m back per row");
                Assert.AreEqual(-2f, left.x, 0.001f, "odd box number = left");
                Assert.AreEqual(2f, right.x, 0.001f, "even box number = right");
            }
        }

        // The ten boxes painted on the start plaza. tools/world_gen/start_plan.py carries the same table
        // (SLOT_TABLE_2_3) and checks its own slot_offset() against it: if this test must change, change that too.
        [Test]
        public void SlotOffsets_AreTheTableTheGeneratorPaints()
        {
            float[,] table =
            {
                { -2f, 0f }, { 2f, 0f }, { -2f, -3f }, { 2f, -3f }, { -2f, -6f },
                { 2f, -6f }, { -2f, -9f }, { 2f, -9f }, { -2f, -12f }, { 2f, -12f }
            };
            RacerGrid grid = new RacerGrid(2f, 3f);
            for (int slot = 0; slot < 10; slot++)
            {
                AssertVector(new Vector3(table[slot, 0], 0f, table[slot, 1]), grid.SlotOffset(slot));
                AssertVector(grid.SlotOffset(slot), RacerGrid.OffsetOf(slot, 2f, 3f));
            }
        }

        [Test]
        public void NegativeSlot_IsTreatedAsSlotZero()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            AssertVector(grid.SlotOffset(0), grid.SlotOffset(-3));
        }

        [Test]
        public void SlotPosition_TurnsWithTheSpawnPoint()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            // SpawnPoint faces +x (yaw 90): "right" is -z, "behind" is -x. Slot 3 = second row right.
            Vector3 position = grid.SlotPosition(3, new Vector3(10f, 1f, 5f), Quaternion.Euler(0f, 90f, 0f));
            AssertVector(new Vector3(7f, 1f, 3f), position);
        }

        [Test]
        public void BrokenNumbers_FallBackToTheDefaults()
        {
            AssertVector(new Vector3(2f, 0f, -3f), new RacerGrid(float.NaN, -1f).SlotOffset(3));
            AssertVector(new Vector3(2f, 0f, -3f), new RacerGrid(0f, 1000f).SlotOffset(3));
        }

        [Test]
        public void TimeTrial_ThePlayerAloneStartsInSlotZero()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            TestRacer you = new TestRacer("You", true, false);
            Assert.AreEqual(0, RacerGrid.TimeTrialSlot);
            Assert.AreEqual(0, grid.PlayerSlot, "no bots = time trial");
            Assert.AreEqual(0, grid.Assign(you));
            Assert.AreEqual(0, grid.SlotOf(you));
            AssertVector(new Vector3(-2f, 0f, 0f), grid.SlotOffset(grid.SlotOf(you)));
        }

        [Test]
        public void Race_BotsFillFromTheFront_ThePlayerStartsLast()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            TestRacer you = new TestRacer("You", true, false);
            Assert.AreEqual(0, grid.Assign(new TestRacer("A", false, false)));
            Assert.AreEqual(1, grid.Assign(you), "behind the one bot so far");
            Assert.AreEqual(1, grid.Assign(new TestRacer("B", false, false)), "the bot takes the slot, the player moves back");
            Assert.AreEqual(2, grid.Assign(new TestRacer("C", false, false)));
            Assert.AreEqual(3, grid.SlotOf(you), "the player is behind every bot");
            Assert.AreEqual(3, grid.Assign(you), "asking again changes nothing");
            Assert.AreEqual(4, grid.Count);
        }

        [Test]
        public void Race_ThePlayerIsLast_WhateverTheOrderOfRegistering()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            TestRacer you = new TestRacer("You", true, false);
            Assert.AreEqual(0, grid.Assign(you), "registered first, still alone");
            for (int i = 0; i < 9; i++)
            {
                Assert.AreEqual(i, grid.Assign(new TestRacer("Bot " + i, false, false)));
                Assert.AreEqual(i + 1, grid.SlotOf(you), "always one behind the last bot");
            }
            // Nine bots + the player = the ten painted boxes; the player stands in box 10 (fifth row right).
            AssertVector(new Vector3(2f, 0f, -12f), grid.SlotOffset(grid.SlotOf(you)));
        }

        [Test]
        public void Race_ThePlayerMovesUp_WhenTheLastBotLeaves()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            TestRacer you = new TestRacer("You", true, false);
            TestRacer a = new TestRacer("A", false, false);
            TestRacer b = new TestRacer("B", false, false);
            grid.Assign(a);
            grid.Assign(b);
            grid.Assign(you);
            Assert.AreEqual(2, grid.SlotOf(you));
            grid.Release(b);
            Assert.AreEqual(1, grid.SlotOf(you));
            grid.KeepOnly(new TestRacer[] { you });
            Assert.AreEqual(0, grid.SlotOf(you), "alone again = time trial slot");
        }

        [Test]
        public void Bot_KeepsItsSlot_WhenAssignedAgain()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            grid.Assign(new TestRacer("A", false, false));
            TestRacer bot = new TestRacer("Bot", false, false);
            Assert.AreEqual(1, grid.Assign(bot));
            Assert.AreEqual(1, grid.Assign(bot));
            Assert.AreEqual(2, grid.Count);
        }

        [Test]
        public void Null_GetsNoSlot()
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
            Assert.AreEqual(0, grid.Assign(late), "the late bot takes the freed slot 0");
            Assert.AreEqual(1, grid.SlotOf(b), "the others keep their slots");
            Assert.AreEqual(2, grid.SlotOf(c));
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
            Assert.AreEqual(1, grid.SlotOf(b));
            grid.KeepOnly(null);
            Assert.AreEqual(0, grid.Count, "nobody racing = no slots");
        }
    }
}
