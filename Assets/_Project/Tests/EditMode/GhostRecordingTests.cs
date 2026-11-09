using System.Globalization;
using System.Threading;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: the ghost recording stores a pose every 0.05 s even when frames come at uneven times, blends
    // smoothly between samples, jumps (not glides) on a teleport, and survives the save text round trip in a size
    // that fits the browser's PlayerPrefs.
    public class GhostRecordingTests
    {
        private const string Track = "Track_Campus";
        private const int Checkpoints = 8;

        private static GhostRecording NewRecording()
        {
            return new GhostRecording(Track, Checkpoints);
        }

        // A kart driving a bumpy circle (radius 30 m, 25 m/s, leaning in, small bumps), recorded at about 60 fps
        // with uneven frame times like a real game.
        private static GhostRecording DriveCircle(float seconds)
        {
            GhostRecording recording = NewRecording();
            float time = 0f;
            int frame = 0;
            while (time <= seconds)
            {
                float angle = time * 25f / 30f; // radians
                Vector3 position = new Vector3(Mathf.Cos(angle) * 30f - 12.34f, 1.5f + Mathf.Sin(time * 7f) * 0.2f, Mathf.Sin(angle) * 30f + 4.56f);
                float yaw = -angle * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.Euler(Mathf.Sin(time * 3f) * 4f, yaw, Mathf.Sin(time * 2f) * 6f);
                recording.Record(time, position, rotation);
                frame++;
                time += frame % 3 == 0 ? 0.021f : 0.0155f;
            }
            return recording;
        }

        [Test]
        public void EmptyRecording_HasNoPose()
        {
            GhostRecording recording = NewRecording();
            Vector3 position;
            Quaternion rotation;
            Assert.IsFalse(recording.Sample(1f, out position, out rotation));
            Assert.AreEqual(0, recording.Count);
            Assert.AreEqual(-1f, recording.Duration);
            Assert.AreEqual(Vector3.zero, recording.EndVelocity());
        }

        [Test]
        public void UnevenFrames_GiveSamplesExactlyEveryFiftyMilliseconds()
        {
            // Kart moving 10 m/s along x, frames at uneven times: samples must sit at 0, 0.05 and 0.10 s.
            GhostRecording recording = NewRecording();
            float[] frameTimes = { 0f, 0.016f, 0.04f, 0.07f, 0.1f, 0.13f };
            for (int i = 0; i < frameTimes.Length; i++)
            {
                recording.Record(frameTimes[i], new Vector3(frameTimes[i] * 10f, 0f, 0f), Quaternion.identity);
            }
            Assert.AreEqual(3, recording.Count);
            Assert.AreEqual(0.1f, recording.Duration, 0.0001f);
            AssertX(recording, 0f, 0f);
            AssertX(recording, 0.05f, 0.5f);
            AssertX(recording, 0.1f, 1f);
        }

        [Test]
        public void Sample_HalfwayBetweenTwoSamples_IsHalfwayThere()
        {
            GhostRecording recording = NewRecording();
            recording.Record(0f, Vector3.zero, Quaternion.identity);
            recording.Record(0.05f, new Vector3(1f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            Vector3 position;
            Quaternion rotation;
            Assert.IsTrue(recording.Sample(0.025f, out position, out rotation));
            Assert.AreEqual(0.5f, position.x, 0.0001f);
            Assert.AreEqual(45f, rotation.eulerAngles.y, 0.01f);
        }

        [Test]
        public void Sample_BeforeTheStartAndAfterTheEnd_IsClamped()
        {
            GhostRecording recording = NewRecording();
            recording.Record(0f, new Vector3(2f, 0f, 0f), Quaternion.identity);
            recording.Record(0.05f, new Vector3(3f, 0f, 0f), Quaternion.identity);
            recording.Record(0.1f, new Vector3(4f, 0f, 0f), Quaternion.identity);
            AssertX(recording, -5f, 2f);
            AssertX(recording, 99f, 4f);
        }

        [Test]
        public void OneSample_IsReturnedAtAnyTime()
        {
            GhostRecording recording = NewRecording();
            recording.Record(0f, new Vector3(7f, 0f, 0f), Quaternion.identity);
            Assert.AreEqual(0f, recording.Duration);
            AssertX(recording, 3f, 7f);
        }

        [Test]
        public void Teleport_BetweenSamples_JumpsInsteadOfGliding()
        {
            GhostRecording recording = NewRecording();
            recording.Record(0f, Vector3.zero, Quaternion.identity);
            recording.Record(0.05f, new Vector3(0.5f, 0f, 0f), Quaternion.identity);
            recording.Record(0.1f, new Vector3(100f, 0f, 0f), Quaternion.identity); // R: back to a checkpoint
            AssertX(recording, 0.06f, 0.5f);
            AssertX(recording, 0.09f, 100f);
        }

        [Test]
        public void Teleport_InsideAFrame_NeverStoresAPoseHalfwayThere()
        {
            GhostRecording recording = NewRecording();
            recording.Record(0f, Vector3.zero, Quaternion.identity);
            recording.Record(0.04f, new Vector3(0.4f, 0f, 0f), Quaternion.identity);
            recording.Record(0.06f, new Vector3(50f, 0f, 0f), Quaternion.identity); // the 0.05 s mark lies inside this frame
            AssertX(recording, 0.05f, 50f);
        }

        [Test]
        public void SameTimeTwice_AndOldTimes_AddNothing()
        {
            GhostRecording recording = NewRecording();
            recording.Record(0f, Vector3.zero, Quaternion.identity);
            recording.Record(0.06f, Vector3.one, Quaternion.identity);
            int count = recording.Count;
            recording.Record(0.06f, Vector3.one * 5f, Quaternion.identity);
            recording.Record(0.01f, Vector3.one * 5f, Quaternion.identity);
            recording.Record(-1f, Vector3.one * 5f, Quaternion.identity);
            Assert.AreEqual(count, recording.Count);
        }

        [Test]
        public void LongLap_StopsAtTheMaximum()
        {
            GhostRecording recording = NewRecording();
            recording.Record(0f, Vector3.zero, Quaternion.identity);
            recording.Record(200f, Vector3.zero, Quaternion.identity); // one 200 s frame
            Assert.AreEqual(GhostRecording.MaxSamples, recording.Count);
            Assert.IsTrue(recording.IsFull);
        }

        [Test]
        public void Clear_StartsOver()
        {
            GhostRecording recording = DriveCircle(2f);
            recording.LapSeconds = 2f;
            recording.Clear();
            Assert.AreEqual(0, recording.Count);
            Assert.AreEqual(-1f, recording.LapSeconds);
            recording.Record(0f, new Vector3(9f, 0f, 0f), Quaternion.identity);
            AssertX(recording, 0f, 9f);
        }

        [Test]
        public void EndVelocity_IsTheLastStepPerSecond()
        {
            GhostRecording recording = NewRecording();
            recording.Record(0f, Vector3.zero, Quaternion.identity);
            recording.Record(0.05f, new Vector3(1f, 0f, 0f), Quaternion.identity);
            Assert.AreEqual(20f, recording.EndVelocity().x, 0.001f);
        }

        [Test]
        public void SaveString_RoundTrip_KeepsEveryPose()
        {
            GhostRecording original = DriveCircle(10f);
            original.LapSeconds = 10.125f;
            GhostRecording loaded = GhostRecording.FromSaveString(original.ToSaveString(), Track, Checkpoints);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(original.Count, loaded.Count);
            Assert.AreEqual(10.125f, loaded.LapSeconds, 0.0005f);
            for (float t = 0f; t <= original.Duration; t += 0.05f)
            {
                Vector3 a, b;
                Quaternion ra, rb;
                original.Sample(t, out a, out ra);
                loaded.Sample(t, out b, out rb);
                Assert.Less(Vector3.Distance(a, b), 0.01f, "position at " + t);
                Assert.Less(Quaternion.Angle(ra, rb), 0.2f, "rotation at " + t);
            }
        }

        [Test]
        public void SaveString_YawAcross360_StaysSmall()
        {
            GhostRecording original = NewRecording();
            original.Record(0f, new Vector3(-5f, -1f, -3f), Quaternion.Euler(0f, 359.5f, 0f));
            original.Record(0.05f, new Vector3(-5.5f, -1f, -3f), Quaternion.Euler(0f, 0.5f, 0f));
            string text = original.ToSaveString();
            StringAssert.EndsWith(";-50,0,0,0,10,0", text); // +1 degree, not -359 degrees
            GhostRecording loaded = GhostRecording.FromSaveString(text, Track, Checkpoints);
            Vector3 position;
            Quaternion rotation;
            loaded.Sample(0.05f, out position, out rotation);
            Assert.Less(Quaternion.Angle(rotation, Quaternion.Euler(0f, 0.5f, 0f)), 0.1f);
            Assert.AreEqual(-5.5f, position.x, 0.001f);
        }

        [Test]
        public void SaveString_IsTheSameOnAPortugueseLaptop()
        {
            GhostRecording original = DriveCircle(1f);
            original.LapSeconds = 41.25f;
            string invariant = original.ToSaveString();
            CultureInfo before = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("pt-PT");
                Assert.AreEqual(invariant, original.ToSaveString());
                Assert.IsNotNull(GhostRecording.FromSaveString(invariant, Track, Checkpoints));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = before;
            }
            StringAssert.Contains("|41.250|", invariant);
        }

        [Test]
        public void SixtySecondLap_FitsTheSaveBudget()
        {
            GhostRecording lap = DriveCircle(60f);
            Assert.GreaterOrEqual(lap.Count, 1199); // about 20 samples per second for 60 s
            Assert.Less(lap.ToSaveString().Length, 40000);
        }

        [Test]
        public void OtherTrack_OrOtherCheckpointCount_IsIgnored()
        {
            string text = DriveCircle(1f).ToSaveString();
            Assert.IsNull(GhostRecording.FromSaveString(text, "RaceFlow_PlaceholderTrack", Checkpoints));
            Assert.IsNull(GhostRecording.FromSaveString(text, Track, Checkpoints + 1));
            Assert.IsNotNull(GhostRecording.FromSaveString(text, Track, Checkpoints));
        }

        [Test]
        public void BrokenSaves_GiveNoGhost()
        {
            Assert.IsNull(GhostRecording.FromSaveString(null, Track, Checkpoints));
            Assert.IsNull(GhostRecording.FromSaveString("", Track, Checkpoints));
            Assert.IsNull(GhostRecording.FromSaveString("no ghost recorded yet", Track, Checkpoints));
            Assert.IsNull(GhostRecording.FromSaveString("G2|Track_Campus|8|1.000|1|0,0,0,0,0,0", Track, Checkpoints)); // newer format
            Assert.IsNull(GhostRecording.FromSaveString("G1|Track_Campus|8|1.000|2|0,0,0,0,0,0", Track, Checkpoints)); // count says 2, has 1
            Assert.IsNull(GhostRecording.FromSaveString("G1|Track_Campus|8|1.000|1|0,0,x,0,0,0", Track, Checkpoints));
            Assert.IsNull(GhostRecording.FromSaveString("G1|Track_Campus|8|1.000|1|0,0,0", Track, Checkpoints));
            Assert.IsNotNull(GhostRecording.FromSaveString("G1|Track_Campus|8|1.000|1|0,0,0,0,0,0", Track, Checkpoints));
        }

        private static void AssertX(GhostRecording recording, float time, float expectedX)
        {
            Vector3 position;
            Quaternion rotation;
            Assert.IsTrue(recording.Sample(time, out position, out rotation));
            Assert.AreEqual(expectedX, position.x, 0.0001f, "x at " + time + " s");
        }
    }
}
