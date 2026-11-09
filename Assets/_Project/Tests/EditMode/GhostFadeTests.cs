using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: the ghost is invisible on top of your kart or the camera, fully visible further away,
    // and fades out after its lap instead of freezing.
    public class GhostFadeTests
    {
        [Test]
        public void ByDistance_HiddenClose_FullFarAway_HalfInTheMiddle()
        {
            Assert.AreEqual(0f, GhostFade.ByDistance(0f, 1.5f, 8f));
            Assert.AreEqual(0f, GhostFade.ByDistance(1.5f, 1.5f, 8f));
            Assert.AreEqual(0.5f, GhostFade.ByDistance(4.75f, 1.5f, 8f), 0.0001f);
            Assert.AreEqual(1f, GhostFade.ByDistance(8f, 1.5f, 8f));
            Assert.AreEqual(1f, GhostFade.ByDistance(50f, 1.5f, 8f));
        }

        [Test]
        public void ByDistance_GetsStrongerTheFurtherAway()
        {
            float previous = -1f;
            for (float d = 0f; d <= 10f; d += 0.25f)
            {
                float alpha = GhostFade.ByDistance(d, 1.5f, 8f);
                Assert.GreaterOrEqual(alpha, previous);
                previous = alpha;
            }
        }

        [Test]
        public void ByDistance_WrongOrderSettings_DoNotBreak()
        {
            Assert.AreEqual(0f, GhostFade.ByDistance(1f, 3f, 3f));
            Assert.AreEqual(1f, GhostFade.ByDistance(4f, 3f, 2f));
        }

        [Test]
        public void ByEnd_FullWhileDriving_GoneAfterTheFade()
        {
            Assert.AreEqual(1f, GhostFade.ByEnd(-3f, 0.8f));
            Assert.AreEqual(1f, GhostFade.ByEnd(0f, 0.8f));
            Assert.AreEqual(0.5f, GhostFade.ByEnd(0.4f, 0.8f), 0.0001f);
            Assert.AreEqual(0f, GhostFade.ByEnd(0.8f, 0.8f));
            Assert.AreEqual(0f, GhostFade.ByEnd(5f, 0.8f));
            Assert.AreEqual(0f, GhostFade.ByEnd(0.1f, 0f));
        }

        [Test]
        public void Combined_TheStrongestFadeWins()
        {
            // Far from the kart, but right at the camera: hidden.
            Assert.AreEqual(0f, GhostFade.Combined(20f, 1f, -1f, 1.5f, 8f, 2f, 5f, 0.8f));
            // Far from both, still driving: full.
            Assert.AreEqual(1f, GhostFade.Combined(20f, 20f, -1f, 1.5f, 8f, 2f, 5f, 0.8f));
            // Far from both, halfway through the end fade: half.
            Assert.AreEqual(0.5f, GhostFade.Combined(20f, 20f, 0.4f, 1.5f, 8f, 2f, 5f, 0.8f), 0.0001f);
            // On top of the kart (the GO moment): hidden.
            Assert.AreEqual(0f, GhostFade.Combined(0f, 20f, -1f, 1.5f, 8f, 2f, 5f, 0.8f));
        }
    }
}
