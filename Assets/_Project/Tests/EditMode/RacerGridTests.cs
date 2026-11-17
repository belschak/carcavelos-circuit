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

        // Online rooms hold 15 (OnlineRooms.MaxPlayers). The straight behind the SpawnPoint ends about 13 m back, then
        // the U-turn starts, so slots 10 .. 14 stand in a middle column between the box columns, half a row behind each
        // box row. start_plan.py carries the same table (SLOT_TABLE_MIDDLE_2_3).
        private const int OnlineRoom = 15;

        [Test]
        public void OnlineSlots10To14_StandInAMiddleColumn_BetweenTheBoxes()
        {
            float[] back = { -1.5f, -4.5f, -7.5f, -10.5f, -13.5f };
            RacerGrid grid = new RacerGrid(2f, 3f);
            for (int i = 0; i < back.Length; i++)
            {
                AssertVector(new Vector3(0f, 0f, back[i]), grid.SlotOffset(RacerGrid.PaintedSlots + i));
            }
            Assert.AreEqual(OnlineRoom, RacerGrid.PaintedSlots + RacerGrid.MiddleSlots, "one slot per online seat");
        }

        // A kart is about 1.4 x 2.0 m (the boxes are 1.7 x 2.3); checked with 1.5 x 2.2 to keep some air.
        [Test]
        public void NoTwoSlots_Overlap_EvenWithMoreThanAnOnlineRoom()
        {
            const float kartWidth = 1.5f;
            const float kartLength = 2.2f;
            RacerGrid grid = new RacerGrid(2f, 3f);
            for (int a = 0; a < OnlineRoom + 5; a++)
            {
                for (int b = a + 1; b < OnlineRoom + 5; b++)
                {
                    Vector3 d = grid.SlotOffset(a) - grid.SlotOffset(b);
                    bool apart = Mathf.Abs(d.x) >= kartWidth || Mathf.Abs(d.z) >= kartLength;
                    Assert.IsTrue(apart, "slots " + a + " and " + b + " overlap: " + d);
                }
            }
        }

        // Every online slot stays on the straight: no further back than the last box row plus half a row (13.5 m, the
        // U-turn starts at about 13 m to 14 m), and no wider than the box columns (the road is 8 m wide).
        [Test]
        public void AllOnlineSlots_StayOnTheStraight_BehindTheSpawnPoint()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            for (int slot = 0; slot < OnlineRoom; slot++)
            {
                Vector3 offset = grid.SlotOffset(slot);
                Assert.LessOrEqual(offset.z, 0f, "slot " + slot + " is not ahead of the front row");
                Assert.GreaterOrEqual(offset.z, -13.5f, "slot " + slot + " is not in the U-turn");
                Assert.LessOrEqual(Mathf.Abs(offset.x), 2f, "slot " + slot + " is inside the box columns");
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

        // M2 online race: the room gives the player its slot (index in the roster), the same on every screen.
        [Test]
        public void Online_ThePlayerTakesTheSlotTheRoomGave()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            TestRacer you = new TestRacer("You", true, false);
            Assert.AreEqual(RacerGrid.NoOverride, grid.PlayerSlotOverride, "off by default");
            grid.PlayerSlotOverride = 4;
            Assert.AreEqual(4, grid.PlayerSlot, "alone on the grid, still slot 4");
            Assert.AreEqual(4, grid.Assign(you));
            Assert.AreEqual(4, grid.SlotOf(you));
            AssertVector(new Vector3(-2f, 0f, -6f), grid.SlotOffset(grid.SlotOf(you))); // box 5 = third row left
            grid.PlayerSlotOverride = 0;
            Assert.AreEqual(0, grid.SlotOf(you), "slot 0 is a real slot, not 'off'");
        }

        [Test]
        public void Online_TheOverrideBeatsTheBots_AndOffPutsThePlayerBehindThemAgain()
        {
            RacerGrid grid = new RacerGrid(2f, 3f);
            TestRacer you = new TestRacer("You", true, false);
            grid.Assign(new TestRacer("A", false, false));
            grid.Assign(new TestRacer("B", false, false));
            grid.Assign(you);
            Assert.AreEqual(2, grid.SlotOf(you), "behind the two bots");
            grid.PlayerSlotOverride = 1;
            Assert.AreEqual(1, grid.SlotOf(you), "the room's slot wins, even next to a bot (online has no bots)");
            grid.PlayerSlotOverride = -1;
            Assert.AreEqual(RacerGrid.NoOverride, grid.PlayerSlotOverride);
            Assert.AreEqual(2, grid.SlotOf(you), "off = behind the bots as usual");
            grid.PlayerSlotOverride = -7;
            Assert.AreEqual(RacerGrid.NoOverride, grid.PlayerSlotOverride, "any negative number = off");
            Assert.AreEqual(2, grid.SlotOf(you));
        }
    }
}
