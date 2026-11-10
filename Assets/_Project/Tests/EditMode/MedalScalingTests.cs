using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: medal times stretch with the track length, and RaceSettings lists the track scenes in order.
    public class MedalScalingTests
    {
        private static float[] ReferenceTimes()
        {
            return new float[] { 12f, 11f, 10.2f, 9.6f }; // the RaceSettings defaults, meant for 158.1 m
        }

        [Test]
        public void LoopLength_SquareOf10Metres_Is40()
        {
            Vector3[] square = { new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f), new Vector3(0f, 0f, 10f) };
            Assert.AreEqual(40f, MedalScaling.LoopLength(square), 0.001f); // includes the way back to the start
        }

        [Test]
        public void LoopLength_PlaceholderLoop_MatchesTheReferenceInRaceSettings()
        {
            // The four checkpoints of RaceFlow_PlaceholderTrack (Role4SceneBuilder.BuildPlaceholderTrackScene).
            Vector3[] placeholder = { new Vector3(0f, 2f, -22.5f), new Vector3(32.5f, 2f, 0f), new Vector3(0f, 2f, 22.5f), new Vector3(-32.5f, 2f, 0f) };
            RaceSettings settings = ScriptableObject.CreateInstance<RaceSettings>();
            Assert.AreEqual(settings.MedalReferenceLapMetres, MedalScaling.LoopLength(placeholder), 0.1f);
            Object.DestroyImmediate(settings);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void LoopLength_LessThanTwoPoints_IsZero(int count)
        {
            Assert.AreEqual(0f, MedalScaling.LoopLength(new Vector3[count]));
            Assert.AreEqual(0f, MedalScaling.LoopLength(null));
        }

        [Test]
        public void ScaleTimes_SameLength_KeepsTheTimes()
        {
            CollectionAssert.AreEqual(ReferenceTimes(), MedalScaling.ScaleTimes(ReferenceTimes(), 158.1f, 158.1f));
        }

        [Test]
        public void ScaleTimes_TrackTwiceAsLong_DoublesEveryTime()
        {
            CollectionAssert.AreEqual(new float[] { 24f, 22f, 20.4f, 19.2f }, MedalScaling.ScaleTimes(ReferenceTimes(), 100f, 200f));
        }

        [Test]
        public void ScaleTimes_RoundsToATenth_AndStaysValid()
        {
            float[] scaled = MedalScaling.ScaleTimes(ReferenceTimes(), 158.1f, 1000f); // about a 1 km campus lap
            Assert.IsTrue(MedalRules.AreValid(scaled));
            for (int i = 0; i < scaled.Length; i++)
            {
                Assert.AreEqual(Mathf.Round(scaled[i] * 10f), scaled[i] * 10f, 0.001f, "tier " + i + " is not on a tenth");
            }
            Assert.AreEqual(75.9f, scaled[0], 0.001f); // 12 s * 1000 / 158.1 = 75.90
        }

        [Test]
        public void ScaleTimes_TinyTrack_KeepsTiersApartInsteadOfRoundingThemTogether()
        {
            float[] scaled = MedalScaling.ScaleTimes(new float[] { 1.2f, 1.15f, 1.1f, 1.05f }, 100f, 10f); // 0.12 .. 0.105 s
            Assert.IsTrue(MedalRules.AreValid(scaled));
        }

        [TestCase(0f, 100f)]
        [TestCase(100f, 0f)]
        [TestCase(-5f, 100f)]
        public void ScaleTimes_NoUsableLength_KeepsTheReferenceTimes(float referenceLength, float trackLength)
        {
            CollectionAssert.AreEqual(ReferenceTimes(), MedalScaling.ScaleTimes(ReferenceTimes(), referenceLength, trackLength));
        }

        [Test]
        public void ScaleTimes_Null_IsNull()
        {
            Assert.IsNull(MedalScaling.ScaleTimes(null, 100f, 200f));
        }

        [Test]
        public void TrackScenes_DefaultOrder_CampusFirstThenTeamScenesThenTestLoop()
        {
            RaceSettings settings = ScriptableObject.CreateInstance<RaceSettings>();
            CollectionAssert.AreEqual(new[] { "Track_Campus", "Track_CampusWorld", "Track_NovaCampus", "RaceFlow_PlaceholderTrack" },
                settings.TrackScenesInOrder());
            Object.DestroyImmediate(settings);
        }
    }
}
