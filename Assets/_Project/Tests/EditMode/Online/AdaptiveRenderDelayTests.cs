using CarcavelosCircuit.Online;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // The render delay of a remote kart (scale test, 17 Nov 2026): never below Luc's 0.22 s, rises at once with late
    // poses, sinks slowly when they are on time again, ignores outages, never above 0.8 s.
    public class AdaptiveRenderDelayTests
    {
        private const double Frame = 1.0 / 60.0;

        // Feeds "seconds" of frames at 60 fps; the newest pose is "baseAge" old right after it arrives and ages by one
        // frame per frame until the next pose comes (every 1/15 s).
        private static double Feed(AdaptiveRenderDelay delay, double from, double seconds, double baseAge)
        {
            double t = from;
            double sinceArrival = 0.0;
            while (t < from + seconds)
            {
                delay.Add(t, baseAge + sinceArrival);
                t += Frame;
                sinceArrival += Frame;
                if (sinceArrival >= 1.0 / 15.0)
                {
                    sinceArrival = 0.0;
                }
            }
            return t;
        }

        [Test]
        public void StartsAndStaysAtLucsMinimum_OnAGoodNetwork()
        {
            AdaptiveRenderDelay delay = new AdaptiveRenderDelay();
            Assert.AreEqual(0.22, delay.Seconds, 1e-9);
            Feed(delay, 0.0, 5.0, 0.03);
            Assert.AreEqual(AdaptiveRenderDelay.MinSeconds, delay.Seconds, 1e-9);
        }

        [Test]
        public void RisesToCoverLatePoses()
        {
            AdaptiveRenderDelay delay = new AdaptiveRenderDelay();
            Feed(delay, 0.0, 2.0, 0.30); // poses arrive 0.3 s old, plus up to 1/15 s until the next one
            Assert.Greater(delay.Seconds, 0.30 + 1.0 / 15.0 - 0.02, "covers age + send gap");
            Assert.Less(delay.Seconds, 0.45);
        }

        [Test]
        public void SinksSlowlyWhenPosesAreOnTimeAgain()
        {
            AdaptiveRenderDelay delay = new AdaptiveRenderDelay();
            double t = Feed(delay, 0.0, 3.0, 0.40);
            double high = delay.Seconds;
            t = Feed(delay, t, 3.2, 0.03); // the late samples leave the 3 s window after 3 s
            Assert.Greater(delay.Seconds, high - 0.1, "eases down at FallPerSecond (0.1 s per s), no jump to the minimum");
            Assert.Greater(high, 0.4);
            Feed(delay, t, 5.0, 0.03);
            Assert.AreEqual(AdaptiveRenderDelay.MinSeconds, delay.Seconds, 1e-6);
        }

        [Test]
        public void IgnoresOutages_AndNeverExceedsTheMaximum()
        {
            AdaptiveRenderDelay delay = new AdaptiveRenderDelay();
            Feed(delay, 0.0, 2.0, 3.0); // a sender that stopped for seconds: not jitter
            Assert.AreEqual(AdaptiveRenderDelay.MinSeconds, delay.Seconds, 1e-9);
            Feed(delay, 2.0, 2.0, 1.2); // very late but still "jitter"
            Assert.AreEqual(AdaptiveRenderDelay.MaxSeconds, delay.Seconds, 1e-9);
        }

        [Test]
        public void Reset_GoesBackToTheMinimum()
        {
            AdaptiveRenderDelay delay = new AdaptiveRenderDelay();
            Feed(delay, 0.0, 2.0, 0.5);
            delay.Reset();
            Assert.AreEqual(AdaptiveRenderDelay.MinSeconds, delay.Seconds, 1e-9);
        }
    }
}
