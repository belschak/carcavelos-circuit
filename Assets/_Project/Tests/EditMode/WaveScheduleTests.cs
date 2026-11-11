using CarcavelosCircuit.Ambience;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R12): waves come in sets, roll in at a steady speed, and nothing ever turns into NaN.
    public class WaveScheduleTests
    {
        private static WaveTuning Tuning()
        {
            var t = new WaveTuning();
            t.SpawnDistance = 100f;
            t.BreakDistance = 30f;
            t.Speed = 4f;
            t.WavesPerSet = 4;
            t.Period = 8f;
            t.SetLull = 14f;
            t.SwashSeconds = 4f;
            return t;
        }

        [Test]
        public void SpawnTimes_FollowTheSetPattern()
        {
            var schedule = new WaveSchedule(Tuning());
            // One set = 4 waves x 8 s + 14 s calm = 46 s.
            Assert.AreEqual(0f, schedule.SpawnTime(0), 0.0001f);
            Assert.AreEqual(8f, schedule.SpawnTime(1), 0.0001f);
            Assert.AreEqual(24f, schedule.SpawnTime(3), 0.0001f);
            Assert.AreEqual(46f, schedule.SpawnTime(4), 0.0001f, "the next set starts after the calm gap");
        }

        [Test]
        public void NegativeWaveNumbers_LieBeforeTheStart()
        {
            var schedule = new WaveSchedule(Tuning());
            Assert.AreEqual(-46f + 24f, schedule.SpawnTime(-1), 0.0001f, "wave -1 = last wave of the set before");
            Assert.AreEqual(-46f, schedule.SpawnTime(-4), 0.0001f);
        }

        [Test]
        public void AtTheStart_TheSeaIsAlreadyFull()
        {
            var schedule = new WaveSchedule(Tuning());
            var waves = new WaveState[schedule.MaxActiveWaves];
            Assert.Greater(schedule.ActiveWaves(0f, waves), 0, "waves from before the start are already rolling in");
        }

        [Test]
        public void Waves_RollInAtTheirSpeed_AndStopAtTheWaterline()
        {
            var schedule = new WaveSchedule(Tuning());
            Assert.AreEqual(100f, schedule.Describe(0, 0f).Distance, 0.0001f);
            Assert.AreEqual(60f, schedule.Describe(0, 10f).Distance, 0.0001f);
            WaveState onSand = schedule.Describe(0, 27f);   // travel = 25 s, then 2 of 4 s of foam on the sand
            Assert.AreEqual(0f, onSand.Distance, 0.0001f);
            Assert.AreEqual(0.5f, onSand.Swash, 0.0001f);
        }

        [Test]
        public void ActiveWaves_FurthestOutFirst_AndAllAlive()
        {
            var schedule = new WaveSchedule(Tuning());
            var waves = new WaveState[schedule.MaxActiveWaves];
            for (float time = -50f; time < 300f; time += 1.7f)
            {
                int count = schedule.ActiveWaves(time, waves);
                Assert.LessOrEqual(count, waves.Length);
                for (int i = 0; i < count; i++)
                {
                    Assert.GreaterOrEqual(waves[i].Age, 0f);
                    Assert.Less(waves[i].Age, schedule.Tuning.LifeSeconds);
                    Assert.IsFalse(float.IsNaN(waves[i].Distance) || float.IsNaN(waves[i].HeightScale) || float.IsNaN(waves[i].PeakAlong));
                    if (i > 0)
                    {
                        Assert.GreaterOrEqual(waves[i - 1].Distance, waves[i].Distance, "sorted: furthest out first");
                    }
                }
            }
        }

        [Test]
        public void ActiveWaves_NeverMoreThanTheBufferHolds()
        {
            var schedule = new WaveSchedule(Tuning());
            var small = new WaveState[1];
            Assert.AreEqual(1, schedule.ActiveWaves(30f, small), "a too small buffer is filled, never overrun");
        }

        [Test]
        public void DuringTheCalmGap_FewerWaves()
        {
            var schedule = new WaveSchedule(Tuning());
            var waves = new WaveState[schedule.MaxActiveWaves];
            // Life = 25 + 4 = 29 s. At 45 s only the last wave of set 0 (spawned at 24 s) is left, the next set starts at 46 s.
            Assert.AreEqual(1, schedule.ActiveWaves(45f, waves));
            Assert.Greater(schedule.ActiveWaves(30f, waves), 1);
        }

        [Test]
        public void SameTime_SameWaves()
        {
            var a = new WaveSchedule(Tuning());
            var b = new WaveSchedule(Tuning());
            Assert.AreEqual(a.Describe(7, 3f).PeakAlong, b.Describe(7, 3f).PeakAlong);
            Assert.AreEqual(a.Describe(7, 3f).HeightScale, b.Describe(7, 3f).HeightScale);
        }

        [Test]
        public void SetShape_MiddleWavesAreBiggest()
        {
            var schedule = new WaveSchedule(Tuning());
            Assert.Greater(schedule.SetShape(1), schedule.SetShape(0));
            Assert.Greater(schedule.SetShape(2), schedule.SetShape(3));
            Assert.AreEqual(schedule.SetShape(1), schedule.SetShape(5), 0.0001f, "same place in the next set");
            Assert.AreEqual(schedule.SetShape(1), schedule.SetShape(-3), 0.0001f, "negative wave numbers too");
        }

        [Test]
        public void HeightScaleAndPeak_StayInRange()
        {
            var schedule = new WaveSchedule(Tuning());
            float usable = schedule.Tuning.BeachLength * 0.5f - schedule.Tuning.EndFade;
            for (int id = -200; id < 200; id++)
            {
                WaveState w = schedule.Describe(id, 1f);
                Assert.That(w.HeightScale, Is.InRange(0.6f, 1.2f));
                Assert.LessOrEqual(System.Math.Abs(w.PeakAlong), usable);
                Assert.That(WaveSchedule.Hash01(id, 3), Is.InRange(0f, 0.99999f));
            }
        }

        [Test]
        public void NaNOrInfiniteTime_NoWaves()
        {
            var schedule = new WaveSchedule(Tuning());
            var waves = new WaveState[schedule.MaxActiveWaves];
            Assert.AreEqual(0, schedule.ActiveWaves(float.NaN, waves));
            Assert.AreEqual(0, schedule.ActiveWaves(float.PositiveInfinity, waves));
            Assert.AreEqual(0, schedule.ActiveWaves(10f, null));
        }

        [Test]
        public void BadTuning_IsMadeSafe()
        {
            var bad = new WaveTuning();
            bad.Speed = 0f;
            bad.Period = float.NaN;
            bad.WavesPerSet = 0;
            bad.BreakDistance = 500f;
            bad.SpawnDistance = -3f;
            var schedule = new WaveSchedule(bad);
            WaveTuning t = schedule.Tuning;
            Assert.Greater(t.Speed, 0f);
            Assert.Greater(t.Period, 0f);
            Assert.AreEqual(1, t.WavesPerSet);
            Assert.Less(t.BreakDistance, t.SpawnDistance, "a wave must break before it reaches the sand");
            var waves = new WaveState[schedule.MaxActiveWaves];
            int count = schedule.ActiveWaves(12f, waves);
            for (int i = 0; i < count; i++)
            {
                Assert.IsFalse(float.IsNaN(waves[i].Distance));
            }
            Assert.DoesNotThrow(() => new WaveSchedule(null));
        }
    }
}
