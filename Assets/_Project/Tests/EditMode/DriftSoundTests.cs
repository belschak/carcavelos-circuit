using CarcavelosCircuit.Audio;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: the drift-sound maths. Must work for any number of drift stages.
    public class DriftSoundTests
    {
        [TestCase(1, 1f)]
        [TestCase(2, 1.12f)]
        [TestCase(3, 1.24f)]
        [TestCase(0, 1f)] // never below normal pitch
        public void StageUpPitch_RisesWithEveryStage(int stage, float expected)
        {
            Assert.AreEqual(expected, DriftSound.StageUpPitch(stage), 0.0001f);
        }

        [TestCase(0, 0f)]   // no sparks yet: silent
        [TestCase(1, 0.7f)]
        [TestCase(2, 0.85f)]
        [TestCase(3, 1f)]
        [TestCase(5, 1f)]   // never louder than full volume
        public void SparksVolume_GrowsWithStage(int stage, float expected)
        {
            Assert.AreEqual(expected, DriftSound.SparksVolume(stage), 0.0001f);
        }

        [TestCase(1, 1.02f)]
        [TestCase(2, 1.09f)]
        public void SparksPitch_RisesWithStage(int stage, float expected)
        {
            Assert.AreEqual(expected, DriftSound.SparksPitch(stage), 0.0001f);
        }
    }
}
