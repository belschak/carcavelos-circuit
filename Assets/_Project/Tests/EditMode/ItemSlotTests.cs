using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R7): the HUD item slot. Roll, land once, use, Triple Bica charges, busy slot, retry.
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
            Assert.IsTrue(slot.TryStartRoll(ItemKind.Onda, 1f));
            Assert.IsTrue(slot.IsRolling);
            Assert.IsFalse(slot.IsReady);
            Assert.IsFalse(slot.Tick(0.5f));
            Assert.IsTrue(slot.Tick(0.6f));   // lands on this frame
            Assert.IsFalse(slot.Tick(0.1f));  // never again
            Assert.IsTrue(slot.IsReady);
            Assert.AreEqual(ItemKind.Onda, slot.ShownIcon);
        }

        [Test]
        public void WhileRolling_TheItemCannotBeUsed()
        {
            ItemSlot slot = new ItemSlot();
            slot.TryStartRoll(ItemKind.Bica, 1f);
            Assert.AreEqual(ItemKind.None, slot.TryUse());
            Assert.AreEqual(ItemKind.Bica, slot.Held); // still in the slot
        }

        [Test]
        public void BusySlot_RefusesANewItem()
        {
            ItemSlot slot = new ItemSlot();
            slot.TryStartRoll(ItemKind.Bica, 1f);
            Assert.IsFalse(slot.TryStartRoll(ItemKind.Onda, 1f)); // rolling
            slot.Tick(2f);
            Assert.IsFalse(slot.TryStartRoll(ItemKind.Onda, 1f)); // ready
            Assert.AreEqual(ItemKind.Bica, slot.Held);
        }

        [Test]
        public void None_IsNeverRolled()
        {
            ItemSlot slot = new ItemSlot();
            Assert.IsFalse(slot.TryStartRoll(ItemKind.None, 1f));
            Assert.IsTrue(slot.IsEmpty);
        }

        [Test]
        public void Bica_IsUsedOnce_ThenTheSlotIsEmpty()
        {
            ItemSlot slot = new ItemSlot();
            slot.TryStartRoll(ItemKind.Bica, 0f); // 0 s = ready at once
            Assert.IsTrue(slot.IsReady);
            Assert.AreEqual(ItemKind.Bica, slot.TryUse());
            Assert.IsTrue(slot.IsEmpty);
            Assert.AreEqual(ItemKind.None, slot.TryUse());
        }

        [Test]
        public void TripleBica_HasThreeUses()
        {
            ItemSlot slot = new ItemSlot();
            slot.TryStartRoll(ItemKind.TripleBica, 0f);
            Assert.AreEqual(3, slot.Charges);
            Assert.AreEqual(ItemKind.TripleBica, slot.TryUse());
            Assert.AreEqual(2, slot.Charges);
            Assert.AreEqual(ItemKind.TripleBica, slot.TryUse());
            Assert.AreEqual(ItemKind.TripleBica, slot.TryUse());
            Assert.IsTrue(slot.IsEmpty);
            Assert.AreEqual(ItemKind.None, slot.TryUse());
        }

        [Test]
        public void TripleBica_WithChargesLeft_StillBlocksANewBox()
        {
            ItemSlot slot = new ItemSlot();
            slot.TryStartRoll(ItemKind.TripleBica, 0f);
            slot.TryUse();
            Assert.IsFalse(slot.TryStartRoll(ItemKind.Onda, 1f));
            Assert.AreEqual(2, slot.Charges);
        }

        [Test]
        public void Clear_EmptiesTheSlot_EvenWhileRolling()
        {
            ItemSlot slot = new ItemSlot();
            slot.TryStartRoll(ItemKind.Onda, 1f);
            slot.Clear();
            Assert.IsTrue(slot.IsEmpty);
            Assert.IsFalse(slot.IsRolling);
            Assert.AreEqual(0, slot.Charges);
            Assert.IsFalse(slot.Tick(5f));
            Assert.IsTrue(slot.TryStartRoll(ItemKind.Bica, 1f)); // free again
        }

        [Test]
        public void RollSteps_GrowFromZeroToAll_AndNeverGoBack()
        {
            ItemSlot slot = new ItemSlot();
            slot.TryStartRoll(ItemKind.Bica, 1.1f);
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
            slot.TryStartRoll(ItemKind.Bica, 1f);
            slot.Tick(0.5f);
            int firstHalf = slot.RollSteps;
            Assert.Greater(firstHalf, ItemSlot.RollIconSteps - firstHalf);
        }

        [Test]
        public void IconForStep_CyclesThroughTheThreeItems()
        {
            Assert.AreEqual(ItemKind.Bica, ItemSlot.IconForStep(0));
            Assert.AreEqual(ItemKind.TripleBica, ItemSlot.IconForStep(1));
            Assert.AreEqual(ItemKind.Onda, ItemSlot.IconForStep(2));
            Assert.AreEqual(ItemKind.Bica, ItemSlot.IconForStep(3));
        }

        [Test]
        public void ChargesFor_EveryItem()
        {
            Assert.AreEqual(0, ItemSlot.ChargesFor(ItemKind.None));
            Assert.AreEqual(1, ItemSlot.ChargesFor(ItemKind.Bica));
            Assert.AreEqual(3, ItemSlot.ChargesFor(ItemKind.TripleBica));
            Assert.AreEqual(1, ItemSlot.ChargesFor(ItemKind.Onda));
        }
    }
}
