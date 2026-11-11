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

        [Test]
        public void CornerThrottle_BrakesOnlyWhenFastBeforeABend()
        {
            Assert.AreEqual(1f, TestBot.CornerThrottle(1f, 10f, 1f, 30f, 0.6f), "straight road: full gas");
            Assert.AreEqual(-1f, TestBot.CornerThrottle(1f, 45f, 0.9f, 30f, 0.6f), "fast before a bend: brake");
            Assert.AreEqual(1f, TestBot.CornerThrottle(1f, 45f, 0.5f, 30f, 0.6f), "slow enough: gas again");
            Assert.AreEqual(0.6f, TestBot.CornerThrottle(0.6f, 45f, 0.6f, 30f, 0.6f), "exactly corner speed keeps the throttle");
            Assert.AreEqual(1f, TestBot.CornerThrottle(1f, float.NaN, 1f, 30f, 0.6f), "NaN bend is ignored");
            Assert.AreEqual(1f, TestBot.CornerThrottle(1f, 45f, float.NaN, 30f, 0.6f), "NaN speed is ignored");
        }

        [Test]
        public void LinePointAhead_PicksTheNextPointFarEnoughAhead_AndWrapsOverTheStartLine()
        {
            // Square lap of 400 m, points at 0, 100, 200 and 300 m.
            TrackPolyline track = new TrackPolyline(new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f), new Vector3(100f, 0f, 100f), new Vector3(0f, 0f, 100f)
            });
            Assert.AreEqual(1, TestBot.LinePointAhead(track, 50f, 8f));
            Assert.AreEqual(2, TestBot.LinePointAhead(track, 95f, 8f), "the point 5 m ahead is too close, take the next");
            Assert.AreEqual(1, TestBot.LinePointAhead(track, 92f, 8f), "exactly 8 m ahead counts");
            Assert.AreEqual(0, TestBot.LinePointAhead(track, 350f, 8f), "after the last point the start line is next");
            Assert.AreEqual(1, TestBot.LinePointAhead(track, 0f, 8f), "standing on a point: the next one");
        }

        [Test]
        public void LinePointAhead_WithoutAUsableTrack_IsMinusOne()
        {
            Assert.AreEqual(-1, TestBot.LinePointAhead(null, 10f, 8f));
            Assert.AreEqual(-1, TestBot.LinePointAhead(new TrackPolyline(null), 10f, 8f));
            TrackPolyline track = new TrackPolyline(new[] { new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f), new Vector3(100f, 0f, 100f) });
            Assert.AreEqual(-1, TestBot.LinePointAhead(track, float.NaN, 8f));
            Assert.AreEqual(-1, TestBot.LinePointAhead(track, 10f, 100000f), "nothing is that far ahead on one lap");
        }

        [Test]
        public void ProgressGain_CountsAcrossTheStartLine()
        {
            Assert.AreEqual(30f, TestBot.ProgressGain(100f, 130f, 1180f), 0.001f);
            Assert.AreEqual(10f, TestBot.ProgressGain(1175f, 5f, 1180f), 0.001f, "over the line forwards");
            Assert.AreEqual(-10f, TestBot.ProgressGain(5f, 1175f, 1180f), 0.001f, "over the line backwards");
            Assert.AreEqual(-20f, TestBot.ProgressGain(130f, 110f, 1180f), 0.001f, "driving the wrong way is negative");
            Assert.AreEqual(0f, TestBot.ProgressGain(100f, 100f, 1180f), 0.001f, "standing still");
        }

        [Test]
        public void ProgressGain_BrokenNumbers_GiveZero()
        {
            Assert.AreEqual(0f, TestBot.ProgressGain(float.NaN, 5f, 1180f));
            Assert.AreEqual(0f, TestBot.ProgressGain(5f, float.NaN, 1180f));
            Assert.AreEqual(0f, TestBot.ProgressGain(5f, 50f, 0f));
            Assert.AreEqual(0f, TestBot.ProgressGain(5f, 50f, float.NaN));
        }

        private static void AssertNone(KartInput input)
        {
            Assert.AreEqual(0f, input.Throttle);
            Assert.AreEqual(0f, input.Steer);
            Assert.IsFalse(input.Drift);
        }
    }
}
