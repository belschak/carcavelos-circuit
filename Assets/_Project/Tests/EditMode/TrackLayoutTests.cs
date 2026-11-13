using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Shared;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (help by Luc, 2026-11-11): a ghost or best time only counts on the road it was driven on.
    // The layout stamp is the lap length of the track's own racing line in whole metres (TrackLayout).
    public class TrackLayoutTests
    {
        private GameObject trackRoot;

        [TearDown]
        public void TearDown()
        {
            if (trackRoot != null)
            {
                Object.DestroyImmediate(trackRoot);
            }
        }

        [Test]
        public void Matches_SameRoad_AndWithinTwoMetres()
        {
            Assert.IsTrue(TrackLayout.Matches(1408, 1408));
            Assert.IsTrue(TrackLayout.Matches(1406, 1408), "2 m is the edge, still the same road");
            Assert.IsTrue(TrackLayout.Matches(1410, 1408));
        }

        [Test]
        public void Matches_OtherRoad_IsRejected()
        {
            Assert.IsFalse(TrackLayout.Matches(1364, 1408), "V14 -> V15");
            Assert.IsFalse(TrackLayout.Matches(1405, 1408), "3 m off");
            Assert.IsFalse(TrackLayout.Matches(TrackLayout.Unknown, 1408), "saved before the stamp existed");
        }

        [Test]
        public void Matches_TrackWithoutOwnLine_AcceptsEverything()
        {
            Assert.IsTrue(TrackLayout.Matches(1364, TrackLayout.Unknown));
            Assert.IsTrue(TrackLayout.Matches(TrackLayout.Unknown, TrackLayout.Unknown));
        }

        [Test]
        public void StampOf_IsTheRoundedLapLength_UnknownWithoutAUsableLine()
        {
            ITrackProgress square = new TrackPolyline(new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(100.3f, 0f, 0f), new Vector3(100.3f, 0f, 100f), new Vector3(0f, 0f, 100f)
            });
            Assert.AreEqual(401, TrackLayout.StampOf(square));
            Assert.AreEqual(TrackLayout.Unknown, TrackLayout.StampOf(null));
            Assert.AreEqual(TrackLayout.Unknown, TrackLayout.StampOf(new TrackPolyline(null)));
        }

        [Test]
        public void OwnOf_TrackWithoutTrackProgress_IsNull_NeverTheCheckpointFallback()
        {
            Assert.IsNull(TrackProgressSource.OwnOf(null));
            trackRoot = new GameObject("Track");
            Assert.IsNull(TrackProgressSource.OwnOf(trackRoot.transform));
        }
    }
}
