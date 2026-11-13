using CarcavelosCircuit.Audio;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (P4): the music ducking (fast down, hold, slow back up).
    public class DuckEnvelopeTests
    {
        [Test]
        public void StartsWithoutDuck()
        {
            Assert.AreEqual(0f, new DuckEnvelope(200f, 15f).Tick(0.1f), 0.0001f);
        }

        [Test]
        public void GoesDownFast()
        {
            DuckEnvelope duck = new DuckEnvelope(200f, 15f);
            duck.Duck(-12f, 1f);
            Assert.AreEqual(-6f, duck.Tick(0.03f), 0.01f);  // 200 dB/s * 0.03 s
            Assert.AreEqual(-12f, duck.Tick(0.03f), 0.01f); // and stops at the depth
        }

        [Test]
        public void StaysDownForTheHoldThenComesBackSlowly()
        {
            DuckEnvelope duck = new DuckEnvelope(200f, 15f);
            duck.Duck(-6f, 0.5f);
            duck.Tick(0.1f);
            duck.Tick(0.39f);
            Assert.AreEqual(-6f, duck.CurrentDb, 0.01f);   // still held
            duck.Tick(0.02f);                               // hold is over during this frame...
            duck.Tick(0.2f);                                // ...then 15 dB/s back up
            Assert.AreEqual(-3f, duck.CurrentDb, 0.01f);
            duck.Tick(1f);
            Assert.AreEqual(0f, duck.CurrentDb, 0.01f);
        }

        [Test]
        public void TheDeeperDuckWins()
        {
            DuckEnvelope duck = new DuckEnvelope(200f, 15f);
            duck.Duck(-12f, 1f);
            duck.Duck(-3f, 0.2f); // a small duck during a big one must not make the music louder
            duck.Tick(0.1f);
            Assert.AreEqual(-12f, duck.CurrentDb, 0.01f);
        }

        [Test]
        public void ClearRemovesTheDuckAtOnce()
        {
            DuckEnvelope duck = new DuckEnvelope(200f, 15f);
            duck.Duck(-12f, 2f);
            duck.Tick(0.1f);
            duck.Clear();
            Assert.AreEqual(0f, duck.CurrentDb, 0.0001f);
            Assert.AreEqual(0f, duck.Tick(0.1f), 0.0001f);
        }
    }
}
