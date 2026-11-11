using CarcavelosCircuit.Ambience;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R12): the surfer state machine. A fake sea decides what the surfer sees.
    public class SurferBrainTests
    {
        // A sea that answers exactly what the test sets.
        private class FakeSea : ISurfWaves
        {
            public bool Incoming;            // FindIncoming finds a wave
            public bool Alive = true;        // TryGetWave still sees it
            public WaveSample Wave;
            public float Surface;

            public float SurfaceHeight(float along, float distance) { return Surface; }

            public bool FindIncoming(float along, float distance, float window, float minHeight, out WaveSample wave)
            {
                wave = Wave;
                return Incoming;
            }

            public bool TryGetWave(int id, float along, out WaveSample wave)
            {
                wave = Wave;
                return Alive && id == Wave.Id;
            }
        }

        private static SurferTuning Tuning()
        {
            var t = new SurferTuning();
            t.LineupDistance = 30f;
            t.PaddleSpeed = 2f;
            t.MinRideHeight = 1f;
            t.RideSpeedMin = 6f;
            t.RideSpeedMax = 6f;
            t.FallChance = 0f;
            t.FallSeconds = 1f;
            t.RespawnSeconds = 2f;
            t.KickOutDistance = 10f;
            t.BeachHalfLength = 80f;
            return t;
        }

        private static FakeSea BigWaveAt(float crest)
        {
            var sea = new FakeSea();
            sea.Wave.Id = 5;
            sea.Wave.Crest = crest;
            sea.Wave.Height = 2f;
            sea.Wave.Broken = 0f;
            sea.Wave.PeakAlong = -20f;     // the peak is on the left, so the surfer rides to the right
            sea.Wave.Speed = 4f;
            return sea;
        }

        // Steps until the surfer waits at the lineup (or gives up after a minute of game time).
        private static void PaddleToLineup(SurferBrain brain, ISurfWaves sea)
        {
            for (int i = 0; i < 600 && brain.State != SurferState.Waiting; i++)
            {
                brain.Step(0.1f, sea);
            }
            Assert.AreEqual(SurferState.Waiting, brain.State);
        }

        [Test]
        public void StartsPaddlingOutBehindTheBreak()
        {
            var brain = new SurferBrain(Tuning(), 0f, 1);
            Assert.AreEqual(SurferState.Paddling, brain.State);
            Assert.Greater(brain.Distance, 30f);
            Assert.IsTrue(brain.Visible);
        }

        [Test]
        public void PaddlesToTheLineup_ThenWaits()
        {
            var brain = new SurferBrain(Tuning(), 5f, 1);
            PaddleToLineup(brain, new FakeSea());
            Assert.AreEqual(30f, brain.Distance, 1f);
            Assert.AreEqual(5f, brain.Along, 1f);
        }

        [Test]
        public void NoWave_KeepsWaiting()
        {
            var brain = new SurferBrain(Tuning(), 0f, 1);
            var calm = new FakeSea();
            PaddleToLineup(brain, calm);
            for (int i = 0; i < 100; i++)
            {
                brain.Step(0.1f, calm);
            }
            Assert.AreEqual(SurferState.Waiting, brain.State);
        }

        [Test]
        public void NullSea_IsACalmSea_NeverThrows()
        {
            var brain = new SurferBrain(Tuning(), 0f, 1);
            for (int i = 0; i < 300; i++)
            {
                brain.Step(0.1f, null);
            }
            Assert.AreEqual(SurferState.Waiting, brain.State);
            Assert.AreEqual(0f, brain.Height);
        }

        [Test]
        public void FullRide_Catch_Ride_KickOut_PaddleBack()
        {
            var brain = new SurferBrain(Tuning(), 0f, 1);
            FakeSea sea = BigWaveAt(36f);
            PaddleToLineup(brain, new FakeSea());
            sea.Incoming = true;
            brain.Step(0.1f, sea);
            Assert.AreEqual(SurferState.Catching, brain.State, "a big wave is coming: turn and paddle");

            sea.Wave.Crest = brain.Distance;             // the crest reaches the surfer
            brain.Step(0.1f, sea);
            Assert.AreEqual(SurferState.Riding, brain.State);
            Assert.AreEqual(1f, brain.RideDirection, "rides away from the peak");

            float startAlong = brain.Along;
            sea.Wave.Crest = 20f;
            brain.Step(0.5f, sea);                         // long frames are cut to 0.1 s
            Assert.Greater(brain.Along, startAlong, "moves along the wave");
            Assert.Less(brain.Along - startAlong, 0.61f, "at most 0.1 s x 6 m/s");
            Assert.Less(brain.Distance, 20f, "on the face, in front of the crest");

            sea.Wave.Crest = 9f;                           // close to the beach: the ride ends
            brain.Step(0.1f, sea);
            Assert.AreEqual(SurferState.Finished, brain.State);
            for (int i = 0; i < 20; i++)
            {
                brain.Step(0.1f, sea);
            }
            Assert.AreEqual(SurferState.Paddling, brain.State, "paddles back out after a short rest");
        }

        [Test]
        public void TooSmallWave_IsLetThrough()
        {
            var brain = new SurferBrain(Tuning(), 0f, 1);
            FakeSea sea = BigWaveAt(34f);
            PaddleToLineup(brain, new FakeSea());
            sea.Incoming = true;
            brain.Step(0.1f, sea);
            Assert.AreEqual(SurferState.Catching, brain.State);
            sea.Wave.Height = 0.5f;                      // smaller than MinRideHeight where the surfer is
            sea.Wave.Crest = brain.Distance;
            brain.Step(0.1f, sea);
            Assert.AreEqual(SurferState.Paddling, brain.State);
        }

        [Test]
        public void WaveGoneWhileCatching_BackToPaddling()
        {
            var brain = new SurferBrain(Tuning(), 0f, 1);
            FakeSea sea = BigWaveAt(36f);
            PaddleToLineup(brain, new FakeSea());
            sea.Incoming = true;
            brain.Step(0.1f, sea);
            sea.Alive = false;
            brain.Step(0.1f, sea);
            Assert.AreEqual(SurferState.Paddling, brain.State);
        }

        [Test]
        public void CaughtByTheFoam_Falls_Disappears_ComesBackOutAtSea()
        {
            var brain = new SurferBrain(Tuning(), 0f, 1);
            FakeSea sea = BigWaveAt(36f);
            PaddleToLineup(brain, new FakeSea());
            sea.Incoming = true;
            brain.Step(0.1f, sea);
            sea.Wave.Crest = brain.Distance;
            brain.Step(0.1f, sea);
            Assert.AreEqual(SurferState.Riding, brain.State);

            sea.Wave.Broken = 1f;                         // the break caught up with the surfer
            brain.Step(0.1f, sea);
            Assert.AreEqual(SurferState.Falling, brain.State);
            for (int i = 0; i < 11; i++)
            {
                brain.Step(0.1f, sea);
            }
            Assert.AreEqual(SurferState.Hidden, brain.State);
            Assert.IsFalse(brain.Visible);
            for (int i = 0; i < 21; i++)
            {
                brain.Step(0.1f, sea);
            }
            Assert.AreEqual(SurferState.Paddling, brain.State);
            Assert.IsTrue(brain.Visible);
            Assert.Greater(brain.Distance, 30f, "appears behind the break, out at sea");
        }

        [Test]
        public void FallChanceOne_EveryRideEndsInAFall()
        {
            SurferTuning t = Tuning();
            t.FallChance = 1f;
            var brain = new SurferBrain(t, 0f, 3);
            FakeSea sea = BigWaveAt(36f);
            PaddleToLineup(brain, new FakeSea());
            sea.Incoming = true;
            brain.Step(0.1f, sea);
            sea.Wave.Crest = brain.Distance;
            brain.Step(0.1f, sea);
            sea.Wave.Crest = 25f;
            for (int i = 0; i < 60 && brain.State == SurferState.Riding; i++)
            {
                brain.Step(0.1f, sea);
            }
            Assert.AreEqual(SurferState.Falling, brain.State);
        }

        [Test]
        public void ReachingTheEndOfTheBeach_EndsTheRide()
        {
            SurferTuning t = Tuning();
            t.BeachHalfLength = 1f;
            var brain = new SurferBrain(t, 0f, 1);
            FakeSea sea = BigWaveAt(36f);
            PaddleToLineup(brain, new FakeSea());
            sea.Incoming = true;
            brain.Step(0.1f, sea);
            sea.Wave.Crest = brain.Distance;
            brain.Step(0.1f, sea);
            for (int i = 0; i < 5; i++)
            {
                brain.Step(0.1f, sea);
            }
            Assert.AreEqual(SurferState.Finished, brain.State);
        }

        [Test]
        public void ZeroSpeeds_NoNaN_NoMovement()
        {
            SurferTuning t = Tuning();
            t.PaddleSpeed = 0f;
            t.RideSpeedMin = 0f;
            t.RideSpeedMax = 0f;
            var brain = new SurferBrain(t, 0f, 1);
            float start = brain.Distance;
            for (int i = 0; i < 50; i++)
            {
                brain.Step(0.1f, new FakeSea());
            }
            Assert.AreEqual(start, brain.Distance, 0.0001f, "cannot paddle at zero speed");
            Assert.IsFalse(float.IsNaN(brain.Along) || float.IsNaN(brain.Heading) || float.IsNaN(brain.Height));
        }

        [Test]
        public void BadTimeSteps_ChangeNothing()
        {
            var brain = new SurferBrain(Tuning(), 0f, 1);
            float along = brain.Along;
            float distance = brain.Distance;
            brain.Step(0f, null);
            brain.Step(-1f, null);
            brain.Step(float.NaN, null);
            brain.Step(float.PositiveInfinity, null);
            Assert.AreEqual(along, brain.Along);
            Assert.AreEqual(distance, brain.Distance);
            Assert.AreEqual(0f, brain.StateTime);
        }

        [Test]
        public void RuleTable_AllowsTheLifeCycle()
        {
            Assert.IsTrue(SurferBrain.IsAllowed(SurferState.Paddling, SurferState.Waiting));
            Assert.IsTrue(SurferBrain.IsAllowed(SurferState.Waiting, SurferState.Catching));
            Assert.IsTrue(SurferBrain.IsAllowed(SurferState.Catching, SurferState.Riding));
            Assert.IsTrue(SurferBrain.IsAllowed(SurferState.Riding, SurferState.Falling));
            Assert.IsTrue(SurferBrain.IsAllowed(SurferState.Riding, SurferState.Finished));
            Assert.IsTrue(SurferBrain.IsAllowed(SurferState.Falling, SurferState.Hidden));
            Assert.IsTrue(SurferBrain.IsAllowed(SurferState.Hidden, SurferState.Paddling));
            Assert.IsTrue(SurferBrain.IsAllowed(SurferState.Finished, SurferState.Paddling));
        }

        [Test]
        public void RuleTable_RefusesShortcuts()
        {
            Assert.IsFalse(SurferBrain.IsAllowed(SurferState.Waiting, SurferState.Riding), "no riding without catching the wave");
            Assert.IsFalse(SurferBrain.IsAllowed(SurferState.Paddling, SurferState.Riding));
            Assert.IsFalse(SurferBrain.IsAllowed(SurferState.Hidden, SurferState.Riding));
            Assert.IsFalse(SurferBrain.IsAllowed(SurferState.Riding, SurferState.Catching));
            Assert.IsFalse(SurferBrain.IsAllowed(SurferState.Falling, SurferState.Riding), "a fall always ends out of sight");
            Assert.IsFalse(SurferBrain.IsAllowed(SurferState.Finished, SurferState.Riding));
            Assert.IsFalse(SurferBrain.IsAllowed(SurferState.Riding, SurferState.Riding), "no change to the same state");
        }

        [Test]
        public void TryChange_RefusedChange_KeepsTheState()
        {
            var brain = new SurferBrain(Tuning(), 0f, 1);
            Assert.IsFalse(brain.TryChange(SurferState.Riding));
            Assert.AreEqual(SurferState.Paddling, brain.State);
            Assert.IsTrue(brain.TryChange(SurferState.Waiting));
            Assert.AreEqual(SurferState.Waiting, brain.State);
        }

        [Test]
        public void NullTuningOrNaNHome_StillWorks()
        {
            var brain = new SurferBrain(null, float.NaN, 1);
            Assert.AreEqual(0f, brain.Along);
            brain.Step(0.1f, null);
            Assert.IsFalse(float.IsNaN(brain.Distance));
        }

        [Test]
        public void SameSeed_SameSurfing()
        {
            var a = new SurferBrain(Tuning(), 0f, 42);
            var b = new SurferBrain(Tuning(), 0f, 42);
            FakeSea sea = BigWaveAt(36f);
            sea.Wave.PeakAlong = 0f;                     // at the peak the side is "random"
            PaddleToLineup(a, new FakeSea());
            PaddleToLineup(b, new FakeSea());
            sea.Incoming = true;
            a.Step(0.1f, sea);
            b.Step(0.1f, sea);
            sea.Wave.Crest = a.Distance;
            a.Step(0.1f, sea);
            b.Step(0.1f, sea);
            Assert.AreEqual(a.RideDirection, b.RideDirection);
        }
    }
}
