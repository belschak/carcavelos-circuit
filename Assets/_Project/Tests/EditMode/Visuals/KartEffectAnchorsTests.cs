using System.Collections.Generic;
using CarcavelosCircuit.Visuals;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Tests for finding the effect anchors on a kart model (V5): rear wheels and exhaust pipe ends.
    public class KartEffectAnchorsTests
    {
        private GameObject kart;

        [SetUp]
        public void MakeKart()
        {
            // Kart > Visual > Body > Wheel_RL / Wheel_RR / Exhaust_0 / Exhaust_1, like our kart FBX export.
            kart = new GameObject("Kart");
            Transform visual = Child(kart.transform, "Visual", Vector3.zero);
            Transform body = Child(visual, "Body", Vector3.zero);
            Child(body, "Wheel_RL", new Vector3(-0.4f, 0.17f, -0.3f));
            Child(body, "Wheel_RR", new Vector3(0.4f, 0.17f, -0.3f));
            Child(body, "Exhaust_0", new Vector3(-0.1f, 0.3f, -0.6f));
            Child(body, "Exhaust_1", new Vector3(0.1f, 0.3f, -0.6f));
        }

        [TearDown]
        public void RemoveKart()
        {
            Object.DestroyImmediate(kart);
        }

        [Test]
        public void FindsRearWheelsAtAnyDepth()
        {
            Transform left = KartEffectAnchors.FindChild(kart.transform, KartEffectAnchors.RearLeftWheel);
            Assert.IsNotNull(left);
            Assert.AreEqual(-0.4f, left.position.x, 0.0001f);
            Assert.IsNotNull(KartEffectAnchors.FindChild(kart.transform, KartEffectAnchors.RearRightWheel));
        }

        [Test]
        public void FindsEveryExhaust()
        {
            List<Transform> pipes = KartEffectAnchors.FindExhausts(kart.transform);
            Assert.AreEqual(2, pipes.Count);
        }

        [Test]
        public void PlaceholderKart_HasNoAnchors()
        {
            GameObject box = new GameObject("Kart");
            Child(box.transform, "Wheel", Vector3.zero);
            Assert.IsNull(KartEffectAnchors.FindChild(box.transform, KartEffectAnchors.RearLeftWheel));
            Assert.AreEqual(0, KartEffectAnchors.FindExhausts(box.transform).Count);
            Object.DestroyImmediate(box);
        }

        [Test]
        public void ContactPoint_IsOnTheRoadJustBehindTheTyre()
        {
            // Wheel centre 0.2 m high = tyre radius 0.2 m; the kart faces +Z, so behind = smaller z.
            Vector3 contact = KartEffectAnchors.ContactBehindWheel(new Vector3(0.6f, 0.2f, -0.6f));
            Assert.AreEqual(0.6f, contact.x, 0.0001f);
            Assert.AreEqual(0.015f, contact.y, 0.0001f);
            Assert.AreEqual(-0.79f, contact.z, 0.0001f);
        }

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }
    }
}
