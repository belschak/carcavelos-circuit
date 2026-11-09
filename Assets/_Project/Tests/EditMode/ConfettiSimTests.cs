using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task P6b): the physics of the results confetti. Units are HUD pixels, y grows downwards.
    public class ConfettiSimTests
    {
        [Test]
        public void Burst_MakesTheAskedNumberOfPieces_AtTheOrigin()
        {
            ConfettiSim sim = new ConfettiSim();
            sim.Burst(100f, 200f, 30, 7);
            Assert.AreEqual(30, sim.Count);
            Assert.IsFalse(sim.IsDone);
            for (int i = 0; i < sim.Count; i++)
            {
                Assert.AreEqual(100f, sim.X[i], 0.0001f);
                Assert.AreEqual(200f, sim.Y[i], 0.0001f);
                Assert.That(sim.Colour[i], Is.InRange(0, ConfettiSim.ColourCount - 1));
            }
        }

        [Test]
        public void SameSeed_GivesTheSameBurst()
        {
            ConfettiSim a = new ConfettiSim();
            ConfettiSim b = new ConfettiSim();
            a.Burst(0f, 0f, 30, 1234);
            b.Burst(0f, 0f, 30, 1234);
            for (int frame = 0; frame < 20; frame++)
            {
                a.Step(1f / 60f);
                b.Step(1f / 60f);
            }
            for (int i = 0; i < 30; i++)
            {
                Assert.AreEqual(a.X[i], b.X[i], 0.0001f);
                Assert.AreEqual(a.Y[i], b.Y[i], 0.0001f);
                Assert.AreEqual(a.Angle[i], b.Angle[i], 0.0001f);
            }
        }

        [Test]
        public void EveryPiece_StartsGoingUp()
        {
            ConfettiSim sim = new ConfettiSim();
            sim.Burst(0f, 500f, 30, 99);
            sim.Step(1f / 60f);
            for (int i = 0; i < sim.Count; i++)
            {
                Assert.Less(sim.Y[i], 500f, "piece " + i); // up = smaller y
            }
        }

        [Test]
        public void Gravity_PullsEveryPieceDown_InTheEnd()
        {
            ConfettiSim sim = new ConfettiSim();
            sim.Burst(0f, 500f, 30, 5);
            float[] highest = new float[30];
            for (int i = 0; i < 30; i++)
            {
                highest[i] = 500f;
            }
            for (int frame = 0; frame < 100; frame++) // 1.67 s
            {
                sim.Step(1f / 60f);
                for (int i = 0; i < 30; i++)
                {
                    if (sim.Y[i] < highest[i]) highest[i] = sim.Y[i];
                }
            }
            for (int i = 0; i < 30; i++)
            {
                Assert.Greater(sim.Y[i], highest[i] + 1f, "piece " + i + " should be falling");
            }
        }

        [Test]
        public void Alpha_IsOneWhenFresh_ZeroAfterItsLife()
        {
            ConfettiSim sim = new ConfettiSim();
            sim.Burst(0f, 0f, 30, 3);
            for (int i = 0; i < sim.Count; i++)
            {
                Assert.AreEqual(1f, sim.AlphaOf(i), 0.0001f);
            }
            StepFor(sim, ConfettiSim.MaxLifeSeconds - 0.02f);
            for (int i = 0; i < sim.Count; i++)
            {
                Assert.Less(sim.AlphaOf(i), 1f, "piece " + i + " should be fading by now");
            }
        }

        [Test]
        public void IsDone_AfterTheLongestLife()
        {
            ConfettiSim sim = new ConfettiSim();
            sim.Burst(0f, 0f, 30, 11);
            StepFor(sim, ConfettiSim.MaxLifeSeconds - 0.1f);
            Assert.IsFalse(sim.IsDone);
            StepFor(sim, 0.2f);
            Assert.IsTrue(sim.IsDone);
        }

        [Test]
        public void BigStep_IsClamped()
        {
            // A long hitch (alt-tab) must not throw the pieces across the screen or end the burst at once.
            ConfettiSim sim = new ConfettiSim();
            sim.Burst(0f, 0f, 30, 2);
            sim.Step(3f);
            Assert.AreEqual(0.05f, sim.Age, 0.0001f);
            Assert.IsFalse(sim.IsDone);
        }

        [Test]
        public void ZeroOrBrokenStep_ChangesNothing()
        {
            ConfettiSim sim = new ConfettiSim();
            sim.Burst(0f, 0f, 30, 2);
            sim.Step(0f);
            sim.Step(-1f);
            sim.Step(float.NaN);
            Assert.AreEqual(0f, sim.Age, 0.0001f);
            Assert.AreEqual(0f, sim.X[0], 0.0001f);
        }

        [Test]
        public void Clear_RemovesEveryPiece()
        {
            ConfettiSim sim = new ConfettiSim();
            sim.Burst(0f, 0f, 30, 2);
            sim.Clear();
            Assert.AreEqual(0, sim.Count);
            Assert.IsTrue(sim.IsDone);
        }

        [Test]
        public void NegativeCount_GivesNoPieces()
        {
            ConfettiSim sim = new ConfettiSim();
            sim.Burst(0f, 0f, -5, 2);
            Assert.AreEqual(0, sim.Count);
            Assert.IsTrue(sim.IsDone);
        }

        [Test]
        public void NoBurstYet_IsDone()
        {
            Assert.IsTrue(new ConfettiSim().IsDone);
        }

        [Test]
        public void SecondBurst_ReusesTheArrays()
        {
            // No new arrays (no garbage) when the next burst is not bigger.
            ConfettiSim sim = new ConfettiSim();
            sim.Burst(0f, 0f, 30, 1);
            float[] first = sim.X;
            sim.Burst(0f, 0f, 20, 2);
            Assert.AreSame(first, sim.X);
            Assert.AreEqual(20, sim.Count);
        }

        private static void StepFor(ConfettiSim sim, float seconds)
        {
            int frames = (int)(seconds * 60f);
            for (int i = 0; i < frames; i++)
            {
                sim.Step(1f / 60f);
            }
        }
    }
}
