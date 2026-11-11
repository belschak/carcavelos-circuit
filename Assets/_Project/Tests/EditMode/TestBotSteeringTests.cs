using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Shared;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R8): the dev test bot steers at its aim point and never sends broken input.
    public class TestBotSteeringTests
    {
        [Test]
        public void TargetStraightAhead_FullGas_NoSteer()
        {
            KartInput input = TestBot.Steering(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 10f), 30f);
            Assert.AreEqual(1f, input.Throttle, 0.001f);
            Assert.AreEqual(0f, input.Steer, 0.001f);
            Assert.IsFalse(input.Drift);
        }

        [Test]
        public void TargetToTheRight_SteersRight_ToTheLeft_SteersLeft()
        {
            Assert.AreEqual(1f, TestBot.Steering(Vector3.zero, Vector3.forward, new Vector3(10f, 0f, 10f), 30f).Steer, 0.001f);
            Assert.AreEqual(-1f, TestBot.Steering(Vector3.zero, Vector3.forward, new Vector3(-10f, 0f, 10f), 30f).Steer, 0.001f);
            float gentle = TestBot.Steering(Vector3.zero, Vector3.forward, new Vector3(Mathf.Tan(15f * Mathf.Deg2Rad) * 10f, 0f, 10f), 30f).Steer;
            Assert.AreEqual(0.5f, gentle, 0.01f, "15 degrees of 30 = half steer");
        }

        [Test]
        public void SharpTurns_SlowDown()
        {
            Assert.AreEqual(0.6f, TestBot.Steering(Vector3.zero, Vector3.forward, new Vector3(10f, 0f, 2f), 30f).Throttle, 0.001f);
            Assert.AreEqual(0.25f, TestBot.Steering(Vector3.zero, Vector3.forward, new Vector3(1f, 0f, -10f), 30f).Throttle, 0.001f, "target behind");
        }

        [Test]
        public void HeightDifference_IsIgnored()
        {
            KartInput input = TestBot.Steering(Vector3.zero, Vector3.forward, new Vector3(0f, 50f, 10f), 30f);
            Assert.AreEqual(0f, input.Steer, 0.001f);
            Assert.AreEqual(1f, input.Throttle, 0.001f);
        }

        [Test]
        public void TargetOnTheKart_NaN_OrNoForward_GiveNoInput()
        {
            AssertNone(TestBot.Steering(Vector3.zero, Vector3.forward, Vector3.zero, 30f));
            AssertNone(TestBot.Steering(Vector3.zero, Vector3.forward, new Vector3(float.NaN, 0f, 1f), 30f));
            AssertNone(TestBot.Steering(Vector3.zero, Vector3.up, new Vector3(0f, 0f, 10f), 30f));
        }

        [Test]
        public void ZeroFullSteerAngle_DoesNotDivideByZero()
        {
            KartInput input = TestBot.Steering(Vector3.zero, Vector3.forward, new Vector3(10f, 0f, 10f), 0f);
            Assert.AreEqual(1f, input.Steer, 0.001f);
        }

        private static void AssertNone(KartInput input)
        {
            Assert.AreEqual(0f, input.Throttle);
            Assert.AreEqual(0f, input.Steer);
            Assert.IsFalse(input.Drift);
        }
    }
}
