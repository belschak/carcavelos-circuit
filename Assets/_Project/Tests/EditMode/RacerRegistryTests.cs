using CarcavelosCircuit.Shared;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: the shared list of racers (round 2 contract) accepts each racer once and rejects bad calls.
    public class RacerRegistryTests
    {
        private class FakeRacer : IRacer
        {
            public FakeRacer(string name, bool isPlayer) { DisplayName = name; IsPlayer = isPlayer; }
            public string DisplayName { get; private set; }
            public Transform Root { get { return null; } }
            public IKartState Kart { get { return null; } }
            public bool IsPlayer { get; private set; }
            public Color MapColor { get { return Color.white; } }
        }

        [SetUp]
        public void SetUp() { RacerRegistry.Clear(); }

        [TearDown]
        public void TearDown() { RacerRegistry.Clear(); }

        [Test]
        public void Register_AddsRacer_AndFiresChanged()
        {
            int changes = 0;
            RacerRegistry.Changed += () => changes++;
            Assert.IsTrue(RacerRegistry.Register(new FakeRacer("Bot", false)));
            Assert.AreEqual(1, RacerRegistry.Count);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Register_SameRacerTwice_IsRejected()
        {
            FakeRacer racer = new FakeRacer("Bot", false);
            RacerRegistry.Register(racer);
            Assert.IsFalse(RacerRegistry.Register(racer));
            Assert.AreEqual(1, RacerRegistry.Count);
        }

        [Test]
        public void Register_Null_IsRejected()
        {
            Assert.IsFalse(RacerRegistry.Register(null));
            Assert.AreEqual(0, RacerRegistry.Count);
        }

        [Test]
        public void Unregister_UnknownRacer_ReturnsFalse_AndFiresNothing()
        {
            int changes = 0;
            RacerRegistry.Changed += () => changes++;
            Assert.IsFalse(RacerRegistry.Unregister(new FakeRacer("Ghost", false)));
            Assert.IsFalse(RacerRegistry.Unregister(null));
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void Unregister_RemovesRacer()
        {
            FakeRacer racer = new FakeRacer("Bot", false);
            RacerRegistry.Register(racer);
            Assert.IsTrue(RacerRegistry.Unregister(racer));
            Assert.AreEqual(0, RacerRegistry.Count);
        }

        [Test]
        public void FindPlayer_ReturnsThePlayer_OrNull()
        {
            Assert.IsNull(RacerRegistry.FindPlayer());
            RacerRegistry.Register(new FakeRacer("Bot", false));
            FakeRacer player = new FakeRacer("You", true);
            RacerRegistry.Register(player);
            Assert.AreSame(player, RacerRegistry.FindPlayer());
        }

        [Test]
        public void Clear_EmptiesList_AndDropsListeners()
        {
            int changes = 0;
            RacerRegistry.Changed += () => changes++;
            RacerRegistry.Register(new FakeRacer("Bot", false));
            RacerRegistry.Clear();
            RacerRegistry.Register(new FakeRacer("Bot 2", false));
            Assert.AreEqual(1, RacerRegistry.Count);
            Assert.AreEqual(1, changes); // only the first register was heard, the listener was dropped by Clear
        }
    }
}
