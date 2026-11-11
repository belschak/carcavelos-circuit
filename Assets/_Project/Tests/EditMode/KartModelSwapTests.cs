using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Q14 bridge: the race puts Role 3's kart model into Role 1's empty Visual slot, and only then.
    public class KartModelSwapTests
    {
        private GameObject kart;
        private GameObject model;

        [SetUp]
        public void SetUp()
        {
            kart = new GameObject("Kart");
            GameObject visual = new GameObject(KartModelSwap.VisualSlotName);
            visual.transform.SetParent(kart.transform, false);
            GameObject box = new GameObject(KartModelSwap.StandInName);
            box.transform.SetParent(visual.transform, false);
            model = new GameObject("KartVisual_Test");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(kart);
            Object.DestroyImmediate(model);
        }

        [Test]
        public void EmptySlotGetsTheModelAndTheStandInIsHidden()
        {
            Assert.IsTrue(KartModelSwap.AddModelIfMissing(kart, model));
            Transform slot = kart.transform.Find(KartModelSwap.VisualSlotName);
            Assert.IsNotNull(slot.Find("KartVisual_Test"));
            Assert.IsFalse(slot.Find(KartModelSwap.StandInName).gameObject.activeSelf);
        }

        [Test]
        public void SlotThatAlreadyHasAModelIsLeftAlone()
        {
            Transform slot = kart.transform.Find(KartModelSwap.VisualSlotName);
            new GameObject("KartVisual_FromLuc").transform.SetParent(slot, false);
            Assert.IsFalse(KartModelSwap.AddModelIfMissing(kart, model));
            Assert.AreEqual(2, slot.childCount);
            Assert.IsTrue(slot.Find(KartModelSwap.StandInName).gameObject.activeSelf);
        }

        [Test]
        public void NoModelOrNoSlotDoesNothing()
        {
            Assert.IsFalse(KartModelSwap.AddModelIfMissing(kart, null));
            GameObject bare = new GameObject("PlaceholderKart");
            Assert.IsFalse(KartModelSwap.AddModelIfMissing(bare, model));
            Assert.AreEqual(0, bare.transform.childCount);
            Object.DestroyImmediate(bare);
        }
    }
}
