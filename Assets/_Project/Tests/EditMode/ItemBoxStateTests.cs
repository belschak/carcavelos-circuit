using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R7): one item box breaks, is gone for a while, then comes back exactly once.
    public class ItemBoxStateTests
    {
        [Test]
        public void NewBox_IsThere()
        {
            Assert.IsTrue(new ItemBoxState().IsAvailable);
        }

        [Test]
        public void Break_RemovesTheBox_AndASecondHitDoesNothing()
        {
            ItemBoxState box = new ItemBoxState();
            Assert.IsTrue(box.TryBreak(3f));
            Assert.IsFalse(box.IsAvailable);
            Assert.IsFalse(box.TryBreak(3f));
        }

        [Test]
        public void Box_ComesBackAfterTheRespawnTime_Once()
        {
            ItemBoxState box = new ItemBoxState();
            box.TryBreak(3f);
            Assert.IsFalse(box.Tick(2.9f));
            Assert.IsFalse(box.IsAvailable);
            Assert.IsTrue(box.Tick(0.2f));
            Assert.IsTrue(box.IsAvailable);
            Assert.IsFalse(box.Tick(1f));
        }

        [Test]
        public void TickWhileThere_ChangesNothing()
        {
            ItemBoxState box = new ItemBoxState();
            Assert.IsFalse(box.Tick(10f));
            Assert.IsTrue(box.IsAvailable);
        }

        [Test]
        public void Reset_BringsTheBoxBackAtOnce()
        {
            ItemBoxState box = new ItemBoxState();
            box.TryBreak(3f);
            box.Reset();
            Assert.IsTrue(box.IsAvailable);
            Assert.IsFalse(box.Tick(5f)); // no late "came back" event after a reset
        }
    }
}
