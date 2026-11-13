using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: which ghost drives along. Your own best lap first, the shipped Dean's ghost on a fresh browser,
    // nothing if neither is valid for this track.
    public class GhostLoaderTests
    {
        private const string Track = "Track_Campus";
        private const int Checkpoints = 8;

        // A short straight drive along x, starting at startX (so the tests can tell two ghosts apart).
        private static string GhostText(float startX, string track, int checkpoints)
        {
            return GhostText(startX, track, checkpoints, TrackLayout.Unknown);
        }

        private static string GhostText(float startX, string track, int checkpoints, int layoutStamp)
        {
            GhostRecording recording = new GhostRecording(track, checkpoints, layoutStamp);
            for (int i = 0; i <= 10; i++)
            {
                float time = i * 0.05f;
                recording.Record(time, new Vector3(startX + time * 10f, 0f, 0f), Quaternion.identity);
            }
            recording.LapSeconds = 0.5f;
            return recording.ToSaveString();
        }

        [Test]
        public void OwnGhostFromAnotherRoad_IsSkipped_DeansGhostDrives()
        {
            bool isShipped;
            GhostRecording ghost = GhostLoader.Choose(GhostText(1f, Track, Checkpoints, 1364), GhostText(2f, Track, Checkpoints, 1408),
                Track, Checkpoints, 1408, out isShipped);
            Assert.IsNotNull(ghost);
            Assert.IsTrue(isShipped);
            Assert.AreEqual(2f, StartX(ghost), 0.01f);
            Assert.IsNull(GhostLoader.Choose(GhostText(1f, Track, Checkpoints, 1364), "", Track, Checkpoints, 1408, out isShipped),
                "no ghost at all rather than one through the walls");
            Assert.IsNotNull(GhostLoader.Choose(GhostText(1f, Track, Checkpoints, 1408), "", Track, Checkpoints, 1408, out isShipped));
        }

        private static float StartX(GhostRecording ghost)
        {
            Vector3 position;
            Quaternion rotation;
            ghost.Sample(0f, out position, out rotation);
            return position.x;
        }

        [Test]
        public void OwnGhost_WinsOverTheShippedOne()
        {
            bool isShipped;
            GhostRecording ghost = GhostLoader.Choose(GhostText(1f, Track, Checkpoints), GhostText(2f, Track, Checkpoints), Track, Checkpoints, out isShipped);
            Assert.IsNotNull(ghost);
            Assert.IsFalse(isShipped);
            Assert.AreEqual(1f, StartX(ghost), 0.001f);
        }

        [Test]
        public void FreshBrowser_RacesTheShippedGhost()
        {
            bool isShipped;
            GhostRecording ghost = GhostLoader.Choose("", GhostText(2f, Track, Checkpoints), Track, Checkpoints, out isShipped);
            Assert.IsNotNull(ghost);
            Assert.IsTrue(isShipped);
            Assert.AreEqual(2f, StartX(ghost), 0.001f);
        }

        [Test]
        public void BrokenOrOutdatedOwnGhost_FallsBackToTheShippedOne()
        {
            bool isShipped;
            Assert.IsTrue(GhostLoader.Choose("garbage", GhostText(2f, Track, Checkpoints), Track, Checkpoints, out isShipped) != null && isShipped);
            Assert.IsTrue(GhostLoader.Choose(GhostText(1f, Track, Checkpoints + 1), GhostText(2f, Track, Checkpoints), Track, Checkpoints, out isShipped) != null && isShipped);
        }

        [Test]
        public void NothingValid_GivesNoGhost()
        {
            bool isShipped;
            Assert.IsNull(GhostLoader.Choose("", "", Track, Checkpoints, out isShipped));
            Assert.IsFalse(isShipped);
            Assert.IsNull(GhostLoader.Choose(null, "no ghost recorded yet", Track, Checkpoints, out isShipped));
            // The shipped ghost was recorded on the old track layout (one checkpoint less) or on the test loop.
            Assert.IsNull(GhostLoader.Choose("", GhostText(2f, Track, Checkpoints - 1), Track, Checkpoints, out isShipped));
            Assert.IsNull(GhostLoader.Choose("", GhostText(2f, "RaceFlow_PlaceholderTrack", Checkpoints), Track, Checkpoints, out isShipped));
        }

        [Test]
        public void SaveKey_SitsNextToThePersonalBest()
        {
            Assert.AreEqual("CarcavelosCircuit.Ghost.Track_Campus", GhostLoader.SaveKey(Track));
        }
    }
}
