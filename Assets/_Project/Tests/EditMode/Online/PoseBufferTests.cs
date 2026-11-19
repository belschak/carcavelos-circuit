using CarcavelosCircuit.Online;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Snapshot interpolation of a remote kart (M2 online race): blend between poses, extrapolate a little, then hold.
    public class PoseBufferTests
    {
        private static KartPose At(double time, float x, float speed = 10f)
        {
            KartPose pose = new KartPose();
            pose.Time = time;
            pose.Position = new Vector3(x, 0f, 0f);
            pose.Rotation = Quaternion.identity;
            pose.Speed = speed;
            return pose;
        }

        [Test]
        public void Empty_HasNothingToShow()
        {
            PoseBuffer buffer = new PoseBuffer();
            KartPose unused;
            Assert.IsFalse(buffer.TrySample(1.0, out unused));
            Assert.AreEqual(-1.0, buffer.NewestTime);
        }

        [Test]
        public void Add_NewestWins_OldOrDoubledDropped()
        {
            PoseBuffer buffer = new PoseBuffer();
            Assert.IsTrue(buffer.Add(At(1.0, 0f)));
            Assert.IsFalse(buffer.Add(At(1.0, 5f)), "the same time again is a doubled packet");
            Assert.IsFalse(buffer.Add(At(0.5, 5f)), "a late packet is older than the newest");
            Assert.IsTrue(buffer.Add(At(1.5, 5f)));
            Assert.AreEqual(2, buffer.Count);
        }

        [Test]
        public void Sample_BlendsBetweenTwoPoses()
        {
            PoseBuffer buffer = new PoseBuffer();
            buffer.Add(At(1.0, 0f, 0f));
            buffer.Add(At(2.0, 10f, 20f));
            KartPose shown;
            Assert.IsTrue(buffer.TrySample(1.25, out shown));
            Assert.AreEqual(2.5f, shown.Position.x, 0.001f);
            Assert.AreEqual(5f, shown.Speed, 0.001f);
            Assert.AreEqual(1.25, shown.Time, 0.0);
        }

        [Test]
        public void Sample_BeforeOldest_ShowsOldest()
        {
            PoseBuffer buffer = new PoseBuffer();
            buffer.Add(At(5.0, 3f));
            buffer.Add(At(6.0, 4f));
            KartPose shown;
            Assert.IsTrue(buffer.TrySample(1.0, out shown));
            Assert.AreEqual(3f, shown.Position.x, 0.001f);
        }

        [Test]
        public void Sample_AfterNewest_ExtrapolatesThenHolds()
        {
            PoseBuffer buffer = new PoseBuffer();
            buffer.Add(At(1.0, 0f));
            buffer.Add(At(2.0, 10f)); // 10 m/s
            KartPose shown;
            Assert.IsTrue(buffer.TrySample(2.1, out shown));
            Assert.AreEqual(11f, shown.Position.x, 0.001f, "0.1 s ahead at 10 m/s");
            float most = 10f + 10f * (float)PoseBuffer.MaxExtrapolationSeconds;
            Assert.IsTrue(buffer.TrySample(2.0 + PoseBuffer.MaxExtrapolationSeconds, out shown));
            Assert.AreEqual(most, shown.Position.x, 0.001f, "the most we ever extrapolate");
            Assert.IsTrue(buffer.TrySample(5.0, out shown));
            Assert.AreEqual(most, shown.Position.x, 0.001f, "after that the kart holds still");
        }

        // Luc's review: on campus Wi-Fi a pose can arrive up to ~150 ms after it was sent (trip + jitter). With poses
        // every 1/15 s, the moment we draw (now - RenderDelaySeconds) must still lie between two received poses, so the
        // kart is blended, never extrapolated or frozen.
        [Test]
        public void RenderDelay_CoversSendGapPlusSlowWifi()
        {
            const double sendGap = 1.0 / 15.0;
            const double lateness = 0.15;
            Assert.GreaterOrEqual(PoseBuffer.RenderDelaySeconds, 0.2, "Luc's 0.2 to 0.25 s");
            for (double now = 1.0; now < 3.0; now += 1.0 / 60.0)
            {
                // What has arrived by "now": every pose sent at least "lateness" ago.
                PoseBuffer buffer = new PoseBuffer();
                for (int i = 0; i * sendGap <= now - lateness; i++)
                {
                    buffer.Add(At(i * sendGap, (float)(i * sendGap * 10.0)));
                }
                double drawAt = now - PoseBuffer.RenderDelaySeconds;
                Assert.Less(drawAt, buffer.NewestTime, "at " + now + " s the kart would run out of poses");
            }
        }

        [Test]
        public void OnePose_IsShownAsItIs()
        {
            PoseBuffer buffer = new PoseBuffer();
            buffer.Add(At(1.0, 7f));
            KartPose shown;
            Assert.IsTrue(buffer.TrySample(9.0, out shown));
            Assert.AreEqual(7f, shown.Position.x, 0.001f);
        }

        [Test]
        public void Capacity_ForgetsTheOldest()
        {
            PoseBuffer buffer = new PoseBuffer();
            for (int i = 0; i < PoseBuffer.Capacity + 5; i++)
            {
                buffer.Add(At(i, i));
            }
            Assert.AreEqual(PoseBuffer.Capacity, buffer.Count);
            KartPose shown;
            Assert.IsTrue(buffer.TrySample(0.0, out shown));
            Assert.AreEqual(5f, shown.Position.x, 0.001f, "poses 0..4 are gone, the oldest kept is 5");
        }

        [Test]
        public void Clear_Empties()
        {
            PoseBuffer buffer = new PoseBuffer();
            buffer.Add(At(1.0, 0f));
            buffer.Clear();
            Assert.AreEqual(0, buffer.Count);
        }
    
        // G-audit (19 Nov): one pose stamped far in the future used to block every later pose (the kart froze).
        [Test]
        public void PoseFarInTheFuture_DoesNotFreezeTheKart()
        {
            PoseBuffer buffer = new PoseBuffer();
            buffer.Add(At(10.0, 0f));
            Assert.IsTrue(buffer.Add(At(1e15, 99f)), "a faked time is newer, so it is taken first");
            Assert.IsTrue(buffer.Add(At(10.1, 1f)), "the next real pose starts the buffer over");
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(10.1, buffer.NewestTime);
        }

        [Test]
        public void LatePacket_JustUnderTheRestartGap_IsStillDropped()
        {
            PoseBuffer buffer = new PoseBuffer();
            buffer.Add(At(10.0, 0f));
            Assert.IsFalse(buffer.Add(At(10.0 - PoseBuffer.RestartGapSeconds + 0.01, 0f)));
            Assert.AreEqual(1, buffer.Count);
        }
    }
}
