using CarcavelosCircuit.Driving;
using CarcavelosCircuit.Shared;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Tests for the "which input wins" rule (D8): input off, bot source, keyboard, clamping.
    public class InputPickerTests
    {
        // A stand-in bot that always returns the same input and counts how often it was asked.
        private class FakeSource : IKartInputSource
        {
            public KartInput Answer;
            public int Reads;

            public KartInput ReadInput()
            {
                Reads++;
                return Answer;
            }
        }

        private static KartInput Keys()
        {
            return new KartInput(1f, -1f, true); // gas, left, drift held
        }

        [Test]
        public void NoSource_UsesTheKeyboard()
        {
            KartInput result = InputPicker.Pick(true, null, Keys());
            Assert.AreEqual(1f, result.Throttle, 0.0001f);
            Assert.AreEqual(-1f, result.Steer, 0.0001f);
            Assert.IsTrue(result.Drift);
        }

        [Test]
        public void SourceSet_UsesTheSourceAndIgnoresTheKeyboard()
        {
            FakeSource bot = new FakeSource { Answer = new KartInput(0.5f, 0.25f, false) };
            KartInput result = InputPicker.Pick(true, bot, Keys());
            Assert.AreEqual(0.5f, result.Throttle, 0.0001f);
            Assert.AreEqual(0.25f, result.Steer, 0.0001f);
            Assert.IsFalse(result.Drift, "the keyboard drift key must not count when a bot drives");
        }

        [Test]
        public void SourceDrift_PassesThrough()
        {
            FakeSource bot = new FakeSource { Answer = new KartInput(1f, 1f, true) };
            Assert.IsTrue(InputPicker.Pick(true, bot, KartInput.None).Drift);
        }

        [Test]
        public void InputOff_NoSource_GivesNothing()
        {
            KartInput result = InputPicker.Pick(false, null, Keys());
            Assert.AreEqual(0f, result.Throttle, 0.0001f);
            Assert.AreEqual(0f, result.Steer, 0.0001f);
            Assert.IsFalse(result.Drift);
        }

        [Test]
        public void InputOff_WithSource_GivesNothingAndDoesNotAskTheSource()
        {
            FakeSource bot = new FakeSource { Answer = new KartInput(1f, 1f, true) };
            KartInput result = InputPicker.Pick(false, bot, Keys());
            Assert.AreEqual(0f, result.Throttle, 0.0001f);
            Assert.AreEqual(0f, result.Steer, 0.0001f);
            Assert.IsFalse(result.Drift);
            Assert.AreEqual(0, bot.Reads, "a switched-off kart must not even read the bot");
        }

        [Test]
        public void SourceAskedOncePerPick()
        {
            FakeSource bot = new FakeSource { Answer = KartInput.None };
            InputPicker.Pick(true, bot, Keys());
            Assert.AreEqual(1, bot.Reads);
        }

        [Test]
        public void SourceTooBig_IsClamped()
        {
            FakeSource bot = new FakeSource { Answer = new KartInput(5f, -5f, false) };
            KartInput result = InputPicker.Pick(true, bot, KartInput.None);
            Assert.AreEqual(1f, result.Throttle, 0.0001f);
            Assert.AreEqual(-1f, result.Steer, 0.0001f);
        }

        [Test]
        public void SourceNaN_BecomesZero()
        {
            FakeSource bot = new FakeSource { Answer = new KartInput(float.NaN, float.NaN, false) };
            KartInput result = InputPicker.Pick(true, bot, KartInput.None);
            Assert.AreEqual(0f, result.Throttle, 0.0001f);
            Assert.AreEqual(0f, result.Steer, 0.0001f);
        }

        [Test]
        public void SourceInfinity_IsClamped()
        {
            FakeSource bot = new FakeSource { Answer = new KartInput(float.PositiveInfinity, float.NegativeInfinity, false) };
            KartInput result = InputPicker.Pick(true, bot, KartInput.None);
            Assert.AreEqual(1f, result.Throttle, 0.0001f);
            Assert.AreEqual(-1f, result.Steer, 0.0001f);
        }
    }
}
