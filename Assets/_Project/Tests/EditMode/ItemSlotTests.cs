using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R7): the HUD item slot. Roll, land once, use, one use per item, busy slot, retry.
    public class ItemSlotTests
    {
        [Test]
        public void NewSlot_IsEmptyAndNotReady()
        {
            ItemSlot slot = new ItemSlot();
            Assert.IsTrue(slot.IsEmpty);
            Assert.IsFalse(slot.IsReady);
            Assert.AreEqual(ItemKind.None, slot.TryUse());
        }

        [Test]
        public void Roll_LandsExactlyOnce_ThenTheItemIsReady()
        {
            ItemSlot slot = new ItemSlot();
            Assert.IsTrue(slot.TryStartRoll(ItemKind.CarcavelosWave, 1f));
            Assert.IsTrue(slot.IsRolling);
            Assert.IsFalse(slot.IsReady);
            Assert.IsFalse(slot.Tick(0.5f));
            Assert.IsTrue(slot.Tick(0.6f));   // lands on this frame
            Assert.IsFalse(slot.Tick(0.1f));  // never again
            Assert.IsTrue(slot.IsReady);
            Assert.AreEqual(ItemKind.CarcavelosWave, slot.ShownIcon);
        }

        [Test]
        public void WhileRolling_TheItemCannotBeUsed()
        {
            ItemSlot slot = new ItemSlot();
            slot.TryStartRoll(ItemKind.Espresso, 1f);
            Assert.AreEqual(ItemKind.None, slot.TryUse());
            Assert.AreEqual(ItemKind.Espresso, slot.Held); // still in the slot
        }

        [Test]
        public void BusySlot_RefusesANewItem()
        {
            ItemSlot slot = new ItemSlot();
            slot.TryStartRoll(ItemKind.Espresso, 1f);
            Assert.IsFalse(slot.TryStartRoll(ItemKind.CarcavelosWave, 1f)); // rolling
            slot.Tick(2f);
            Assert.IsFalse(slot.TryStartRoll(ItemKind.CarcavelosWave, 1f)); // ready
            Assert.AreEqual(ItemKind.Espresso, slot.Held);
        }

        [Test]
        public void None_IsNeverRolled()
        {
            ItemSlot slot = new ItemSlot();
            Assert.IsFalse(slot.TryStartRoll(ItemKind.None, 1f));
            Assert.IsTrue(slot.IsEmpty);
        }

        [Test]
        public void Espresso_IsUsedOnce_ThenTheSlotIsEmpty()
        {
            ItemSlot slot = new ItemSlot();
            slot.TryStartRoll(ItemKind.Espresso, 0f); // 0 s = ready at once
            Assert.IsTrue(slot.IsReady);
            Assert.AreEqual(ItemKind.Espresso, slot.TryUse());
            Assert.IsTrue(slot.IsEmpty);
            Assert.AreEqual(ItemKind.None, slot.TryUse());
        }

        [TestCase(ItemKind.Espresso)]
        [TestCase(ItemKind.SeagullDelivery)]
        [TestCase(ItemKind.CarcavelosWave)]
        [TestCase(ItemKind.DeansList)]
        public void EveryItem_IsOneUse(ItemKind kind)
        {
            ItemSlot slot = new ItemSlot();
            slot.TryStartRoll(kind, 0f);
            Assert.AreEqual(1, slot.Charges);
            Assert.AreEqual(kind, slot.TryUse());
            Assert.IsTrue(slot.IsEmpty);
        }

        [Test]
        public void Clear_EmptiesTheSlot_EvenWhileRolling()
        {
            ItemSlot slot = new ItemSlot();
            slot.TryStartRoll(ItemKind.CarcavelosWave, 1f);
            slot.Clear();
            Assert.IsTrue(slot.IsEmpty);
            Assert.IsFalse(slot.IsRolling);
            Assert.AreEqual(0, slot.Charges);
            Assert.IsFalse(slot.Tick(5f));
            Assert.IsTrue(slot.TryStartRoll(ItemKind.Espresso, 1f)); // free again
        }

        [Test]
        public void RollSteps_GrowFromZeroToAll_AndNeverGoBack()
        {
            ItemSlot slot = new ItemSlot();
            slot.TryStartRoll(ItemKind.Espresso, 1.1f);
            Assert.AreEqual(0, slot.RollSteps);
            int last = 0;
            for (int i = 0; i < 12; i++)
            {
                slot.Tick(0.1f);
                Assert.GreaterOrEqual(slot.RollSteps, last);
                last = slot.RollSteps;
            }
            Assert.AreEqual(ItemSlot.RollIconSteps, slot.RollSteps);
        }

        [Test]
        public void RollSteps_SlowDownTowardsTheEnd()
        {
            // Ease-out: the first half of the time shows more icon changes than the second half.
            ItemSlot slot = new ItemSlot();
            slot.TryStartRoll(ItemKind.Espresso, 1f);
            slot.Tick(0.5f);
            int firstHalf = slot.RollSteps;
            Assert.Greater(firstHalf, ItemSlot.RollIconSteps - firstHalf);
        }

        [Test]
        public void IconForStep_CyclesThroughTheFourItems()
        {
            Assert.AreEqual(ItemKind.Espresso, ItemSlot.IconForStep(0));
            Assert.AreEqual(ItemKind.SeagullDelivery, ItemSlot.IconForStep(1));
            Assert.AreEqual(ItemKind.CarcavelosWave, ItemSlot.IconForStep(2));
            Assert.AreEqual(ItemKind.DeansList, ItemSlot.IconForStep(3));
            Assert.AreEqual(ItemKind.Espresso, ItemSlot.IconForStep(4));
        }

        [Test]
        public void ChargesFor_EveryItem()
        {
            Assert.AreEqual(0, ItemSlot.ChargesFor(ItemKind.None));
            Assert.AreEqual(1, ItemSlot.ChargesFor(ItemKind.Espresso));
            Assert.AreEqual(1, ItemSlot.ChargesFor(ItemKind.SeagullDelivery));
            Assert.AreEqual(1, ItemSlot.ChargesFor(ItemKind.CarcavelosWave));
            Assert.AreEqual(1, ItemSlot.ChargesFor(ItemKind.DeansList));
        }
    }
}
