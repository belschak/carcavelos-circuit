using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R9): counting start line crossings from the distance along the lap.
    public class LapProgressTests
    {
        [Test]
        public void JustBehindTheStartLine_AtTheStart_IsNotALapAhead()
        {
            LapProgress kart = new LapProgress(1000f);
            kart.Feed(995f); // grid: 5 m before the line
            Assert.AreEqual(-1, kart.LapsDone);
            Assert.AreEqual(-5f, kart.Total, 0.001f);
        }

        [Test]
        public void JustAfterTheStartLine_AtTheStart_IsOnLapZero()
        {
            LapProgress kart = new LapProgress(1000f);
            kart.Feed(3f);
            Assert.AreEqual(0, kart.LapsDone);
            Assert.AreEqual(3f, kart.Total, 0.001f);
        }

        [Test]
        public void CrossingTheLineForwards_CountsALap()
        {
            LapProgress kart = new LapProgress(1000f);
            kart.Feed(995f);
            kart.Feed(2f);
            Assert.AreEqual(0, kart.LapsDone);
            kart.Feed(500f);
            kart.Feed(990f);
            kart.Feed(4f);
            Assert.AreEqual(1, kart.LapsDone);
            Assert.AreEqual(1004f, kart.Total, 0.001f);
        }

        [Test]
        public void WrongWay_BackOverTheLine_TakesTheLapAway()
        {
            LapProgress kart = new LapProgress(1000f);
            kart.Feed(10f);
            kart.Feed(2f);
            kart.Feed(996f); // reversed over the line
            Assert.AreEqual(-1, kart.LapsDone);
            Assert.AreEqual(-4f, kart.Total, 0.001f);
        }

        [Test]
        public void NaN_Infinity_AndNoLapLength_AreIgnored()
        {
            LapProgress kart = new LapProgress(1000f);
            kart.Feed(float.NaN);
            Assert.IsFalse(kart.HasValue);
            kart.Feed(100f);
            kart.Feed(float.PositiveInfinity);
            Assert.AreEqual(100f, kart.Distance, 0.001f);
            LapProgress broken = new LapProgress(float.NaN);
            broken.Feed(10f);
            Assert.IsFalse(broken.HasValue);
            LapProgress zero = new LapProgress(0f);
            zero.Feed(10f);
            Assert.IsFalse(zero.HasValue);
        }

        [Test]
        public void DistanceOutsideTheLap_IsWrapped()
        {
            LapProgress kart = new LapProgress(1000f);
            kart.Feed(1010f);
            Assert.AreEqual(10f, kart.Distance, 0.001f);
            kart.Feed(-20f); // = 980, a jump backwards over the line
            Assert.AreEqual(980f, kart.Distance, 0.001f);
            Assert.AreEqual(-1, kart.LapsDone);
        }

        [Test]
        public void Reset_StartsAgainFromTheGrid()
        {
            LapProgress kart = new LapProgress(1000f);
            kart.Feed(990f);
            kart.Feed(5f);
            kart.Feed(400f);
            kart.Reset();
            Assert.IsFalse(kart.HasValue);
            kart.Feed(994f);
            Assert.AreEqual(-1, kart.LapsDone);
        }

        [Test]
        public void IsAheadOf_LapsFirst_ThenDistance_TiesAreNotAhead()
        {
            LapProgress a = new LapProgress(1000f);
            LapProgress b = new LapProgress(1000f);
            a.Feed(100f);
            b.Feed(100f);
            Assert.IsFalse(a.IsAheadOf(b), "level = not ahead");
            Assert.IsFalse(b.IsAheadOf(a));
            b.Feed(150f);
            Assert.IsTrue(b.IsAheadOf(a));
            LapProgress behindTheLine = new LapProgress(1000f);
            behindTheLine.Feed(990f); // lap -1, distance 990: a bigger number, but still behind the line
            Assert.IsTrue(a.IsAheadOf(behindTheLine), "a lap more beats a bigger distance");
        }

        [Test]
        public void WithoutAValue_IsBehindEveryone()
        {
            LapProgress empty = new LapProgress(1000f);
            LapProgress racing = new LapProgress(1000f);
            racing.Feed(1f);
            Assert.IsTrue(racing.IsAheadOf(empty));
            Assert.IsFalse(empty.IsAheadOf(racing));
            Assert.IsFalse(empty.IsAheadOf(new LapProgress(1000f)));
            Assert.IsTrue(racing.IsAheadOf(null));
        }
    }
}
