using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Shared;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R9): positions and the minimap use the track's own TrackProgress (T8) when it has one,
    // else a line through the checkpoints in driving order.
    public class TrackProgressSourceTests
    {
        private static readonly Vector3[] Checkpoints =
        {
            new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f), new Vector3(100f, 0f, 100f), new Vector3(0f, 0f, 100f)
        };

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
        public void Fallback_FromCheckpoints_MeasuresTheLap_StartLineFirst()
        {
            ITrackProgress progress = TrackProgressSource.FromCheckpoints(Checkpoints);
            Assert.IsNotNull(progress);
            Assert.AreEqual(400f, progress.LapLength, 0.001f);
            Assert.AreEqual(0f, progress.DistanceAlongLap(new Vector3(0f, 0f, 0f)), 0.001f);
            Assert.AreEqual(150f, progress.DistanceAlongLap(new Vector3(103f, 0f, 50f)), 0.001f, "beside the line counts at the closest point");
            Assert.AreEqual(4, progress.RacingLine.Count);
        }

        [Test]
        public void Fallback_TooFewCheckpoints_IsNull()
        {
            Assert.IsNull(TrackProgressSource.FromCheckpoints(null));
            Assert.IsNull(TrackProgressSource.FromCheckpoints(new Vector3[0]));
            Assert.IsNull(TrackProgressSource.FromCheckpoints(new[] { Vector3.zero, Vector3.one }));
        }

        [Test]
        public void TrackWithoutTrackProgress_UsesTheFallback()
        {
            trackRoot = new GameObject("Track");
            ITrackProgress progress = TrackProgressSource.For(trackRoot.transform, Checkpoints);
            Assert.IsNotNull(progress);
            Assert.AreEqual(400f, progress.LapLength, 0.001f);
            Assert.IsNotNull(TrackProgressSource.For(null, Checkpoints), "no track root at all: still the fallback");
            Assert.IsNull(TrackProgressSource.For(null, null), "nothing to measure");
        }

        [Test]
        public void IsUsable_RejectsEmptyAndTinyLines()
        {
            Assert.IsFalse(TrackProgressSource.IsUsable(null));
            Assert.IsFalse(TrackProgressSource.IsUsable(new TrackPolyline(null)));
            Assert.IsFalse(TrackProgressSource.IsUsable(new TrackPolyline(new[] { Vector3.zero, new Vector3(0.1f, 0f, 0f), new Vector3(0.1f, 0f, 0.1f) })));
            Assert.IsTrue(TrackProgressSource.IsUsable(new TrackPolyline(Checkpoints)));
        }
    }
}
