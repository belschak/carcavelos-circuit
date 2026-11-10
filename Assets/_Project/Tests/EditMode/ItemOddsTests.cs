using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R7): catch-up odds. Behind your best lap = better items.
    public class ItemOddsTests
    {
        private const float Level = 0.5f;
        private const float Far = 2f;

        [Test]
        public void EveryRow_AddsUpTo100()
        {
            foreach (float gap in new[] { -3f, 0f, 1f, 5f })
            {
                int[] w = ItemOdds.WeightsFor(true, gap, Level, Far);
                Assert.AreEqual(100, w[0] + w[1] + w[2], "gap " + gap);
            }
        }

        [Test]
        public void NoBestLapYet_UsesLevelOdds()
        {
            Assert.AreEqual(new[] { 45, 35, 20 }, ItemOdds.WeightsFor(false, 99f, Level, Far));
        }

        [TestCase(-2f, 60)]   // well ahead
        [TestCase(-0.51f, 60)]
        [TestCase(-0.5f, 45)] // exactly on the edge counts as level
        [TestCase(0f, 45)]
        [TestCase(0.5f, 45)]
        [TestCase(0.51f, 30)] // behind
        [TestCase(1.99f, 30)]
        [TestCase(2f, 15)]    // far behind from 2 s
        [TestCase(10f, 15)]
        public void Gap_PicksTheRightRow(float behindSeconds, int bicaChance)
        {
            Assert.AreEqual(bicaChance, ItemOdds.WeightsFor(true, behindSeconds, Level, Far)[0]);
        }

        [Test]
        public void FurtherBehind_MeansMoreOnda()
        {
            int ahead = ItemOdds.WeightsFor(true, -1f, Level, Far)[2];
            int level = ItemOdds.WeightsFor(true, 0f, Level, Far)[2];
            int behind = ItemOdds.WeightsFor(true, 1f, Level, Far)[2];
            int far = ItemOdds.WeightsFor(true, 3f, Level, Far)[2];
            Assert.Less(ahead, level);
            Assert.Less(level, behind);
            Assert.Less(behind, far);
        }

        [TestCase(0f, ItemKind.Bica)]
        [TestCase(0.449f, ItemKind.Bica)]
        [TestCase(0.45f, ItemKind.TripleBica)]
        [TestCase(0.799f, ItemKind.TripleBica)]
        [TestCase(0.8f, ItemKind.Onda)]
        [TestCase(0.9999f, ItemKind.Onda)]
        public void Pick_SplitsTheRollByTheWeights(float roll, ItemKind expected)
        {
            Assert.AreEqual(expected, ItemOdds.Pick(new[] { 45, 35, 20 }, roll));
        }

        [Test]
        public void Pick_NeverReturnsNone()
        {
            int[] w = { 15, 40, 45 };
            for (int i = 0; i < 100; i++)
            {
                Assert.AreNotEqual(ItemKind.None, ItemOdds.Pick(w, i / 100f));
            }
        }
    }
}
