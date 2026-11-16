using CarcavelosCircuit.Track;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 2 tests (T10): when the atrium piano plays, and that its code-made notes are a clean sound.
    public class AtriumPianoTests
    {
        // Same numbers as the AtriumPiano component's defaults.
        private static PianoReaction Piano()
        {
            return new PianoReaction(8f, 2.2f, 3f, 1f, 3f, 1f);
        }

        [Test]
        public void FastKartDrivingPast_PlaysAChord()
        {
            Assert.AreEqual(PianoSoundKind.PassBy, Piano().Decide(6f, 12f, 10f));
        }

        [Test]
        public void SlowKartOrFarAway_StaysQuiet()
        {
            PianoReaction piano = Piano();
            Assert.AreEqual(PianoSoundKind.None, piano.Decide(6f, 2f, 10f));   // too slow
            Assert.AreEqual(PianoSoundKind.None, piano.Decide(8.5f, 12f, 11f)); // outside the pass radius
        }

        [Test]
        public void DrivingIntoIt_IsAHit_EvenSlowly()
        {
            Assert.AreEqual(PianoSoundKind.Hit, Piano().Decide(1.5f, 1.2f, 10f));
        }

        [Test]
        public void ReversingIntoIt_CountsToo()
        {
            Assert.AreEqual(PianoSoundKind.Hit, Piano().Decide(1.5f, -4f, 10f));
        }

        [Test]
        public void PassBy_WaitsForItsPause()
        {
            PianoReaction piano = Piano();
            Assert.AreEqual(PianoSoundKind.PassBy, piano.Decide(6f, 12f, 10f));
            Assert.AreEqual(PianoSoundKind.None, piano.Decide(6f, 12f, 12.9f));   // 2.9 s later: still quiet
            Assert.AreEqual(PianoSoundKind.PassBy, piano.Decide(6f, 12f, 13f));   // 3 s later: plays again
        }

        [Test]
        public void Hit_WaitsForItsPause_AndNoChordRightAfterIt()
        {
            PianoReaction piano = Piano();
            Assert.AreEqual(PianoSoundKind.Hit, piano.Decide(1f, 8f, 10f));
            Assert.AreEqual(PianoSoundKind.None, piano.Decide(1f, 8f, 10.5f));   // crash pause
            Assert.AreEqual(PianoSoundKind.None, piano.Decide(5f, 8f, 10.5f));   // no chord straight after the crash
            Assert.AreEqual(PianoSoundKind.Hit, piano.Decide(1f, 8f, 11f));      // next crash after 1 s
        }

        [Test]
        public void BrokenInput_StaysQuiet()
        {
            PianoReaction piano = Piano();
            Assert.AreEqual(PianoSoundKind.None, piano.Decide(float.NaN, 12f, 10f));
            Assert.AreEqual(PianoSoundKind.None, piano.Decide(1f, float.NaN, 10f));
            Assert.AreEqual(PianoSoundKind.None, piano.Decide(1f, 12f, float.NaN));
        }

        [Test]
        public void HitVolume_GrowsWithSpeed_AndStaysBetweenHalfAndFull()
        {
            Assert.AreEqual(0.5f, PianoReaction.HitVolume(0f, 18f), 0.0001f);
            Assert.AreEqual(0.75f, PianoReaction.HitVolume(9f, 18f), 0.0001f);
            Assert.AreEqual(1f, PianoReaction.HitVolume(40f, 18f), 0.0001f);
            Assert.AreEqual(0.5f, PianoReaction.HitVolume(10f, 0f), 0.0001f);     // no top speed known
            Assert.AreEqual(0.5f, PianoReaction.HitVolume(float.NaN, 18f), 0.0001f);
        }

        [Test]
        public void Frequency_A4Is440_AndAnOctaveDoubles()
        {
            Assert.AreEqual(440f, PianoTone.Frequency(0), 0.01f);
            Assert.AreEqual(880f, PianoTone.Frequency(12), 0.01f);
            Assert.AreEqual(261.63f, PianoTone.Frequency(-9), 0.01f); // middle C
        }

        [Test]
        public void Notes_HaveTheRightLength_AndNeverClip()
        {
            float[] samples = PianoTone.Notes(new[] { -9, -5, -2, 3 }, new[] { 0f, 0.09f, 0.18f, 0.27f }, 2f);
            Assert.AreEqual(2 * PianoTone.SampleRate, samples.Length);
            float loudest = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                Assert.IsFalse(float.IsNaN(samples[i]), "NaN at " + i);
                loudest = Mathf.Max(loudest, Mathf.Abs(samples[i]));
            }
            Assert.AreEqual(0.8f, loudest, 0.001f);
        }

        [Test]
        public void Notes_StartSoft_AndFadeOut()
        {
            float[] samples = PianoTone.Notes(new[] { 0 }, new[] { 0f }, 2f);
            Assert.AreEqual(0f, samples[0], 0.0001f);                          // no click at the start
            Assert.Less(Loudest(samples, 88200 - 4410, 88200), Loudest(samples, 0, 4410) * 0.1f); // last 0.1 s much quieter
        }

        [Test]
        public void Notes_WithBrokenInput_AreSilence()
        {
            float[] none = PianoTone.Notes(null, null, 1f);
            Assert.AreEqual(PianoTone.SampleRate, none.Length);
            Assert.AreEqual(0f, Loudest(none, 0, none.Length));
            Assert.AreEqual(1, PianoTone.Notes(new[] { 0 }, new[] { 0f }, -3f).Length); // negative length: one silent sample
        }

        private static float Loudest(float[] samples, int from, int to)
        {
            float loudest = 0f;
            for (int i = from; i < to; i++)
            {
                loudest = Mathf.Max(loudest, Mathf.Abs(samples[i]));
            }
            return loudest;
        }
    }
}
