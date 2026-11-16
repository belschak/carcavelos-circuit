using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using CarcavelosCircuit.Visuals;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 3 (Luc, 16 Nov 2026): the finish cruise that drives the player's kart on after the line.
    public class FinishCruiseTests
    {
        private const float TopSpeed = 20f; // m/s

        [Test]
        public void StandingOnTheStraight_GivesGas_DrivesStraight()
        {
            KartInput input = Decide(new Vector3(0f, 0f, 20f), Vector3.forward, 0f);
            Assert.AreEqual(1f, input.Throttle, "gas from standstill");
            Assert.AreEqual(0f, input.Steer, 0.05f, "on the line, facing along it");
            Assert.IsFalse(input.Drift, "a lap of honour never drifts");
        }

        [Test]
        public void FasterThanTheCruise_DoesNotGiveGas()
        {
            KartInput input = Decide(new Vector3(0f, 0f, 20f), Vector3.forward, TopSpeed);
            Assert.LessOrEqual(input.Throttle, 0f, "full racing speed is above the roll-out speed");
            KartInput cruising = Decide(new Vector3(0f, 0f, 20f), Vector3.forward, FinishCruise.CruiseSpeed(TopSpeed, FinishCruise.DefaultSpeedShare) * 0.5f);
            Assert.AreEqual(1f, cruising.Throttle, "below the roll-out speed: gas");
        }

        [Test]
        public void RightOfTheLine_SteersLeft_LeftOfIt_SteersRight()
        {
            Assert.Less(Decide(new Vector3(3f, 0f, 20f), Vector3.forward, 8f).Steer, 0f);
            Assert.Greater(Decide(new Vector3(-3f, 0f, 20f), Vector3.forward, 8f).Steer, 0f);
        }

        [Test]
        public void BadInput_StandsStill()
        {
            int index = -1;
            float wanted;
            BotSettings settings = ScriptableObject.CreateInstance<BotSettings>();
            KartInput none = FinishCruise.Decide(null, settings, ref index, Vector3.zero, Vector3.forward, 5f, TopSpeed, 0.5f, out wanted);
            Assert.AreEqual(0f, none.Throttle);
            Assert.AreEqual(0f, none.Steer);
            BotTrackMap map = new BotTrackMap(Stadium());
            KartInput nan = FinishCruise.Decide(map, settings, ref index, Vector3.zero, Vector3.forward, float.NaN, TopSpeed, 0.5f, out wanted);
            Assert.AreEqual(0f, nan.Throttle, "a NaN speed never drives");
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CruiseSpeed_IsAShareOfTopSpeed_Clamped()
        {
            Assert.AreEqual(11f, FinishCruise.CruiseSpeed(20f, 0.55f), 0.001f);
            Assert.AreEqual(20f, FinishCruise.CruiseSpeed(20f, 3f), 0.001f, "never above top speed");
            Assert.AreEqual(0f, FinishCruise.CruiseSpeed(20f, -1f));
            Assert.AreEqual(0f, FinishCruise.CruiseSpeed(0f, 0.5f));
            Assert.AreEqual(0f, FinishCruise.CruiseSpeed(float.NaN, 0.5f));
            Assert.AreEqual(0f, FinishCruise.CruiseSpeed(20f, float.NaN));
        }

        private static KartInput Decide(Vector3 position, Vector3 heading, float speed)
        {
            BotSettings settings = ScriptableObject.CreateInstance<BotSettings>();
            BotTrackMap map = new BotTrackMap(Stadium());
            int index = -1;
            float wanted;
            KartInput input = FinishCruise.Decide(map, settings, ref index, position, heading, speed, TopSpeed,
                FinishCruise.DefaultSpeedShare, out wanted);
            Object.DestroyImmediate(settings);
            return input;
        }

        // A 200 m straight along +z at x = 0, back along x = 16, joined by half circles: a clockwise stadium.
        private static ITrackProgress Stadium()
        {
            List<Vector3> points = new List<Vector3>();
            const float straight = 200f;
            const float radius = 8f;
            for (float z = 0f; z < straight; z += 2f)
            {
                points.Add(new Vector3(0f, 0f, z));
            }
            for (int i = 0; i < 18; i++)
            {
                float a = i * Mathf.PI / 18f;
                points.Add(new Vector3(radius - radius * Mathf.Cos(a), 0f, straight + radius * Mathf.Sin(a)));
            }
            for (float z = straight; z > 0f; z -= 2f)
            {
                points.Add(new Vector3(2f * radius, 0f, z));
            }
            for (int i = 0; i < 18; i++)
            {
                float a = i * Mathf.PI / 18f;
                points.Add(new Vector3(radius + radius * Mathf.Cos(a), 0f, -radius * Mathf.Sin(a)));
            }
            return new TrackPolyline(points);
        }
    }
}
