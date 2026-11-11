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
                Assert.AreEqual(4, w.Length, "one weight per item");
                Assert.AreEqual(100, w[0] + w[1] + w[2] + w[3], "gap " + gap);
            }
        }

        [Test]
        public void NoBestLapYet_UsesLevelOdds()
        {
            Assert.AreEqual(new[] { 40, 30, 22, 8 }, ItemOdds.WeightsFor(false, 99f, Level, Far));
        }

        [TestCase(-2f, 60)]   // well ahead
        [TestCase(-0.51f, 60)]
        [TestCase(-0.5f, 40)] // exactly on the edge counts as level
        [TestCase(0f, 40)]
        [TestCase(0.5f, 40)]
        [TestCase(0.51f, 25)] // behind
        [TestCase(1.99f, 25)]
        [TestCase(2f, 10)]    // far behind from 2 s
        [TestCase(10f, 10)]
        public void Gap_PicksTheRightRow(float behindSeconds, int espressoChance)
        {
            Assert.AreEqual(espressoChance, ItemOdds.WeightsFor(true, behindSeconds, Level, Far)[0]);
        }

        [TestCase(2)] // Carcavelos Wave
        [TestCase(3)] // Dean's List
        public void FurtherBehind_MeansMoreOfTheBigItems(int item)
        {
            int ahead = ItemOdds.WeightsFor(true, -1f, Level, Far)[item];
            int level = ItemOdds.WeightsFor(true, 0f, Level, Far)[item];
            int behind = ItemOdds.WeightsFor(true, 1f, Level, Far)[item];
            int far = ItemOdds.WeightsFor(true, 3f, Level, Far)[item];
            Assert.Less(ahead, level);
            Assert.Less(level, behind);
            Assert.Less(behind, far);
        }

        [Test]
        public void DeansList_NeverComesWhenYouAreAhead()
        {
            int[] ahead = ItemOdds.WeightsFor(true, -1f, Level, Far);
            for (int i = 0; i < 1000; i++)
            {
                Assert.AreNotEqual(ItemKind.DeansList, ItemOdds.Pick(ahead, i / 1000f));
            }
        }

        [TestCase(0f, ItemKind.Espresso)]
        [TestCase(0.399f, ItemKind.Espresso)]
        [TestCase(0.4f, ItemKind.SeagullDelivery)]
        [TestCase(0.699f, ItemKind.SeagullDelivery)]
        [TestCase(0.7f, ItemKind.CarcavelosWave)]
        [TestCase(0.919f, ItemKind.CarcavelosWave)]
        [TestCase(0.92f, ItemKind.DeansList)]
        [TestCase(0.9999f, ItemKind.DeansList)]
        public void Pick_SplitsTheRollByTheWeights(float roll, ItemKind expected)
        {
            Assert.AreEqual(expected, ItemOdds.Pick(new[] { 40, 30, 22, 8 }, roll));
        }

        [Test]
        public void Pick_NeverReturnsNone()
        {
            int[] w = { 10, 25, 35, 30 };
            for (int i = 0; i < 100; i++)
            {
                Assert.AreNotEqual(ItemKind.None, ItemOdds.Pick(w, i / 100f));
            }
        }
    }
}
