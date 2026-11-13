using CarcavelosCircuit.Visuals;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 3 tests (task V13): where the checkpoint banners stand, their shape and how the cloth waves.
    public class CheckpointGateTests
    {
        [Test]
        public void Banners_StandOutsideTheRoadAndTheFence()
        {
            float offset = CheckpointGatePlan.PostOffset(8f);
            Assert.AreEqual(4.9f, offset, 0.0001f);   // 4 m half road + 0.9 m
            Assert.Greater(offset, 4f + 0.3f);        // the white fence stands 0.3 m off the edge
        }

        [Test]
        public void Banners_NarrowLane_StillOutside()
        {
            Assert.AreEqual(3.9f, CheckpointGatePlan.PostOffset(6f), 0.0001f); // the hall's east lane is 6 m wide
        }

        [Test]
        public void Banners_BadWidth_DoesNotPutThemOnTheCentre()
        {
            Assert.AreEqual(CheckpointGatePlan.PostMargin, CheckpointGatePlan.PostOffset(-3f), 0.0001f);
        }

        [Test]
        public void TopEdge_StartsAtThePoleTop_AndFallsOutwards()
        {
            Assert.AreEqual(0f, CheckpointGatePlan.TopEdge(0f), 0.0001f);
            Assert.Less(CheckpointGatePlan.TopEdge(1f), CheckpointGatePlan.TopEdge(0.5f));
            Assert.AreEqual(-CheckpointGatePlan.TopDrop, CheckpointGatePlan.TopEdge(2f), 0.0001f); // clamped
        }

        [Test]
        public void Wave_ThePoleHoldsTheCloth()
        {
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(0f, CheckpointGatePlan.Wave(0f, i / 100f, i * 0.37f, 2f), 0.0001f);
            }
        }

        [Test]
        public void Wave_NeverSwingsFurtherThanTheLimit()
        {
            for (int i = 0; i < 5000; i++)
            {
                float w = CheckpointGatePlan.Wave((i % 11) / 10f, (i % 7) / 6f, i * 0.013f, i % 5);
                Assert.LessOrEqual(System.Math.Abs(w), CheckpointGatePlan.MaxWave + 0.0001f);
            }
        }

        [Test]
        public void Wave_Moves_AndTwoBannersAreNotInStep()
        {
            Assert.AreNotEqual(CheckpointGatePlan.Wave(1f, 0.5f, 1f, 0f), CheckpointGatePlan.Wave(1f, 0.5f, 1.2f, 0f));
            Assert.AreNotEqual(CheckpointGatePlan.Wave(1f, 0.5f, 1f, 0f), CheckpointGatePlan.Wave(1f, 0.5f, 1f, 1f));
        }
    }
}
