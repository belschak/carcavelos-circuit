using CarcavelosCircuit.Audio;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (P4): guessing the throttle (engine load) from the change in speed.
    public class ThrottleGuessTests
    {
        private const float Frame = 1f / 60f;

        // Feeds one second of frames with a constant acceleration (m/s per second) and returns the load.
        private static float LoadAfterOneSecond(ThrottleGuess throttle, float startSpeed, float acceleration)
        {
            float speed = startSpeed;
            float load = throttle.Load;
            for (int i = 0; i < 60; i++)
            {
                speed += acceleration * Frame;
                load = throttle.Tick(speed, Frame);
            }
            return load;
        }

        [Test]
        public void SpeedingUpMeansFullThrottle()
        {
            Assert.AreEqual(1f, LoadAfterOneSecond(new ThrottleGuess(), 5f, 4f), 0.001f);
        }

        [Test]
        public void BrakingMeansThrottleClosed()
        {
            Assert.AreEqual(0f, LoadAfterOneSecond(new ThrottleGuess(), 15f, -6f), 0.001f);
        }

        [Test]
        public void CruisingIsInBetween()
        {
            Assert.AreEqual(0.55f, LoadAfterOneSecond(new ThrottleGuess(), 10f, 0f), 0.001f);
        }

        [Test]
        public void LoadChangesSmoothlyNotInOneFrame()
        {
            ThrottleGuess throttle = new ThrottleGuess();
            throttle.Tick(5f, Frame);
            float load = throttle.Tick(5f + 10f * Frame, Frame); // hard acceleration, but only one frame
            Assert.Less(load, 0.7f);
        }

        [Test]
        public void ZeroFrameTimeChangesNothing()
        {
            ThrottleGuess throttle = new ThrottleGuess();
            Assert.AreEqual(0.55f, throttle.Tick(30f, 0f), 0.0001f);
        }

        [Test]
        public void ResetForgetsATeleport()
        {
            // After a teleport the speed jumps from 20 to 0; after Reset that jump must not count as braking.
            ThrottleGuess throttle = new ThrottleGuess();
            LoadAfterOneSecond(throttle, 20f, 0f);
            throttle.Reset();
            Assert.AreEqual(0.55f, throttle.Tick(0f, Frame), 0.0001f);
        }
    }
}
