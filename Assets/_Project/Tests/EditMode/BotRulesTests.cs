using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using CarcavelosCircuit.Visuals;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 3 tests (task V10): the bots' driving decisions (BotRules), the stuck rule (BotStuckCheck) and the
    // pre-computed racing line (BotTrackMap). Accepted, rejected and edge cases, NaN-safe.
    public class BotRulesTests
    {
        private const float Turn = 110f;      // KartTuning: degrees per second at full steer
        private const float FullSteer = 5f;   // KartTuning: full steering from this speed (m/s)
        private const float Top = 18f;        // KartTuning: top speed (m/s)

        // ---------- steering ----------

        [Test]
        public void Steer_TargetRight_SteersRight_TargetLeft_SteersLeft()
        {
            float right = BotRules.SteerTowards(Vector3.zero, Vector3.forward, new Vector3(3f, 0f, 10f), 10f, Turn, FullSteer);
            float left = BotRules.SteerTowards(Vector3.zero, Vector3.forward, new Vector3(-3f, 0f, 10f), 10f, Turn, FullSteer);
            Assert.Greater(right, 0f);
            Assert.Less(left, 0f);
            Assert.AreEqual(right, -left, 0.0001f, "left and right are mirror images");
        }

        [Test]
        public void Steer_TargetStraightAhead_NoSteering()
        {
            Assert.AreEqual(0f, BotRules.SteerTowards(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 12f), 15f, Turn, FullSteer), 0.0001f);
        }

        [Test]
        public void Steer_FasterNeedsMoreLockForTheSameArc()
        {
            Vector3 target = new Vector3(1f, 0f, 10f);
            float slow = BotRules.SteerTowards(Vector3.zero, Vector3.forward, target, 6f, Turn, FullSteer);
            float fast = BotRules.SteerTowards(Vector3.zero, Vector3.forward, target, 16f, Turn, FullSteer);
            Assert.Greater(fast, slow);
        }

        [Test]
        public void Steer_TargetBehind_FullLockTowardsIt()
        {
            Assert.AreEqual(1f, BotRules.SteerTowards(Vector3.zero, Vector3.forward, new Vector3(1f, 0f, -5f), 5f, Turn, FullSteer));
            Assert.AreEqual(-1f, BotRules.SteerTowards(Vector3.zero, Vector3.forward, new Vector3(-1f, 0f, -5f), 5f, Turn, FullSteer));
        }

        [Test]
        public void Steer_NeverBeyondFullLock()
        {
            float sharp = BotRules.SteerTowards(Vector3.zero, Vector3.forward, new Vector3(5f, 0f, 1f), 18f, Turn, FullSteer);
            Assert.AreEqual(1f, sharp);
        }

        [Test]
        public void Steer_BadInput_DoesNothing()
        {
            Assert.AreEqual(0f, BotRules.SteerTowards(Vector3.zero, Vector3.forward, new Vector3(float.NaN, 0f, 5f), 10f, Turn, FullSteer));
            Assert.AreEqual(0f, BotRules.SteerTowards(Vector3.zero, Vector3.zero, new Vector3(3f, 0f, 5f), 10f, Turn, FullSteer), "no heading");
            Assert.AreEqual(0f, BotRules.SteerTowards(Vector3.zero, Vector3.forward, Vector3.zero, 10f, Turn, FullSteer), "already at the target");
            Assert.AreEqual(0f, BotRules.SteerTowards(Vector3.zero, Vector3.forward, new Vector3(3f, 0f, 5f), 10f, 0f, FullSteer), "a kart that cannot turn");
            float nanSpeed = BotRules.SteerTowards(Vector3.zero, Vector3.forward, new Vector3(1f, 0f, 10f), float.NaN, Turn, FullSteer);
            Assert.IsFalse(float.IsNaN(nanSpeed));
        }

        // ---------- corners ----------

        [Test]
        public void Curvature_Straight_IsZero_CircleRadius10_IsOneTenth()
        {
            Assert.AreEqual(0f, BotRules.Curvature(Vector3.zero, new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, 10f)), 0.0001f);
            // Three points on a circle with radius 10 around (10, 0, 0), driving forwards (+z) and turning right (+x).
            Vector3 a = new Vector3(0f, 0f, 0f);
            Vector3 b = new Vector3(10f - 10f * Mathf.Cos(0.5f), 0f, 10f * Mathf.Sin(0.5f));
            Vector3 c = new Vector3(10f - 10f * Mathf.Cos(1f), 0f, 10f * Mathf.Sin(1f));
            Assert.AreEqual(0.1f, BotRules.Curvature(a, b, c), 0.001f, "right turn = +");
            Assert.AreEqual(-0.1f, BotRules.Curvature(c, b, a), 0.001f, "driven the other way it is a left turn = -");
        }

        [Test]
        public void Curvature_PointsOnTopOfEachOther_IsZero()
        {
            Assert.AreEqual(0f, BotRules.Curvature(Vector3.one, Vector3.one, Vector3.one));
            Assert.AreEqual(0f, BotRules.Curvature(new Vector3(float.NaN, 0f, 0f), Vector3.one, Vector3.zero));
        }

        [Test]
        public void CornerSpeed_SharpBendIsSlowerThanAWideOne()
        {
            float hairpin = BotRules.CornerSpeed(1f / 8f, Turn, 0.95f, 6f, Top);   // radius 8 m
            float sweeper = BotRules.CornerSpeed(1f / 9f, Turn, 0.95f, 6f, Top);   // radius 9 m
            Assert.Less(hairpin, sweeper);
            Assert.Less(hairpin, Top, "a hairpin needs braking");
            // turn rate 110 deg/s = 1.92 rad/s, x radius 8 m x 0.95 = 14.6 m/s
            Assert.AreEqual(110f * Mathf.Deg2Rad * 8f * 0.95f, hairpin, 0.01f);
        }

        [Test]
        public void CornerSpeed_StraightRoad_TopSpeed_AndNeverBelowMinimum()
        {
            Assert.AreEqual(Top, BotRules.CornerSpeed(0f, Turn, 0.9f, 6f, Top));
            Assert.AreEqual(Top, BotRules.CornerSpeed(1f / 500f, Turn, 0.9f, 6f, Top), "a very wide bend is a straight");
            Assert.AreEqual(6f, BotRules.CornerSpeed(10f, Turn, 0.9f, 6f, Top), "a 10 cm radius still gives the minimum, not 0");
            Assert.AreEqual(Top, BotRules.CornerSpeed(float.NaN, Turn, 0.9f, 6f, Top));
        }

        [Test]
        public void SpeedBeforeCorner_FurtherAwayMayStillBeFaster()
        {
            Assert.AreEqual(8f, BotRules.SpeedBeforeCorner(8f, 0f, 12f), 0.0001f, "in the corner: the corner speed");
            Assert.AreEqual(Mathf.Sqrt(64f + 2f * 12f * 10f), BotRules.SpeedBeforeCorner(8f, 10f, 12f), 0.0001f);
            Assert.AreEqual(8f, BotRules.SpeedBeforeCorner(8f, -5f, 12f), 0.0001f, "a corner behind us gives no extra speed");
            Assert.AreEqual(0f, BotRules.SpeedBeforeCorner(float.NaN, 5f, 12f));
        }

        // ---------- pedals ----------

        [Test]
        public void Throttle_GasWhenSlow_RollWhenALittleFast_BrakeWhenTooFast()
        {
            Assert.AreEqual(1f, BotRules.Throttle(10f, 15f, 1.5f));
            Assert.AreEqual(0f, BotRules.Throttle(16f, 15f, 1.5f));
            Assert.AreEqual(-1f, BotRules.Throttle(17f, 15f, 1.5f));
        }

        [Test]
        public void Throttle_StandingOrRollingBack_AlwaysGas_NeverReverses()
        {
            Assert.AreEqual(1f, BotRules.Throttle(0f, 0f, 1.5f), "standing with a 0 limit: gas, not reverse");
            Assert.AreEqual(1f, BotRules.Throttle(-2f, 6f, 1.5f));
            Assert.AreEqual(0f, BotRules.Throttle(float.NaN, 10f, 1.5f));
            Assert.AreEqual(0f, BotRules.Throttle(10f, float.NaN, 1.5f));
        }

        // ---------- rubber band ----------

        [Test]
        public void RubberBand_FarBehindFaster_FarAheadSlower_LevelNoChange()
        {
            Assert.AreEqual(1f, BotRules.RubberBand(0f, 80f, 0.06f, 0.1f), 0.0001f);
            Assert.AreEqual(1.06f, BotRules.RubberBand(-80f, 80f, 0.06f, 0.1f), 0.0001f);
            Assert.AreEqual(1.03f, BotRules.RubberBand(-40f, 80f, 0.06f, 0.1f), 0.0001f, "half way = half the correction");
            Assert.AreEqual(0.9f, BotRules.RubberBand(80f, 80f, 0.06f, 0.1f), 0.0001f);
            Assert.AreEqual(1.06f, BotRules.RubberBand(-5000f, 80f, 0.06f, 0.1f), 0.0001f, "a lap behind: still only the full correction");
        }

        [Test]
        public void RubberBand_BadInput_NoCorrection()
        {
            Assert.AreEqual(1f, BotRules.RubberBand(float.NaN, 80f, 0.06f, 0.1f));
            Assert.AreEqual(1f, BotRules.RubberBand(-50f, 0f, 0.06f, 0.1f));
        }

        [Test]
        public void PaceShare_NeverAboveTheKartsTopSpeed_NeverNegative()
        {
            Assert.AreEqual(1f, BotRules.PaceShare(0.99f, 1.06f), "a boosted fast bot is capped at 1.0");
            Assert.AreEqual(0.95f * 0.9f, BotRules.PaceShare(0.95f, 0.9f), 0.0001f);
            Assert.AreEqual(0.3f, BotRules.PaceShare(-1f, 1f), "never negative, never standing");
            Assert.AreEqual(1f, BotRules.PaceShare(float.NaN, 1f));
        }

        // ---------- traffic ----------

        [Test]
        public void LaneToPass_KartInMyLane_GoesToTheSideWithMoreRoom()
        {
            Assert.AreEqual(-1.5f, BotRules.LaneToPass(0.2f, 0.3f, 1.8f, 2.2f), 0.0001f, "it is right of the middle: pass left");
            Assert.AreEqual(1.3f, BotRules.LaneToPass(-0.6f, -0.5f, 1.8f, 2.2f), 0.0001f, "it is left: pass right");
        }

        [Test]
        public void LaneToPass_AlreadyBesideIt_KeepsMyLane_AndStaysOnTheRoad()
        {
            Assert.AreEqual(-1f, BotRules.LaneToPass(-1f, 1f, 1.8f, 2.2f));
            Assert.AreEqual(-2.2f, BotRules.LaneToPass(2f, 2f, 5f, 2.2f), "never further out than maxLane");
            Assert.AreEqual(0f, BotRules.LaneToPass(float.NaN, 1f, 1.8f, 2.2f));
        }

        [Test]
        public void LaneBeside_KartOnMyRight_StayLeftOfIt_OnMyLeft_StayRightOfIt()
        {
            // Grid: I stand 2 m left (-2), the player 2 m right (+2); I would like +1, that is into the player.
            Assert.AreEqual(0.2f, BotRules.LaneBeside(1f, -2f, 2f, 1.8f), 0.0001f, "at most 1.8 m left of it");
            Assert.AreEqual(-1f, BotRules.LaneBeside(-1f, -2f, 2f, 1.8f), 0.0001f, "a lane away from it is fine");
            Assert.AreEqual(1.3f, BotRules.LaneBeside(-1f, 1f, -0.5f, 1.8f), 0.0001f, "it is on my left: stay right of it");
        }

        [Test]
        public void LaneBeside_BadNumbers_KeepTheWantedLane()
        {
            Assert.AreEqual(0.5f, BotRules.LaneBeside(0.5f, float.NaN, 1f, 1.8f));
            Assert.AreEqual(0f, BotRules.LaneBeside(float.NaN, 0f, 1f, 1.8f));
        }

        // ---------- drifting ----------

        [Test]
        public void Drift_OnlyBeforeAGoodBend_FastEnough_OnTheGround_ForDriftingBots()
        {
            Assert.IsTrue(BotRules.WantsDrift(true, true, 80f, 0.8f, true, false, 50f, 0.7f));
            Assert.IsFalse(BotRules.WantsDrift(false, true, 80f, 0.8f, true, false, 50f, 0.7f), "this bot never drifts");
            Assert.IsFalse(BotRules.WantsDrift(true, false, 80f, 0.8f, true, false, 50f, 0.7f), "no good bend ahead");
            Assert.IsFalse(BotRules.WantsDrift(true, true, 80f, 0.5f, true, false, 50f, 0.7f), "too slow");
            Assert.IsFalse(BotRules.WantsDrift(true, true, 80f, 0.8f, false, false, 50f, 0.7f), "in the air");
            Assert.IsFalse(BotRules.WantsDrift(true, true, float.NaN, 0.8f, true, false, 50f, 0.7f));
        }

        [Test]
        public void Drift_RunningDrift_HeldWhileTheBendLasts_ReleasedWhenItOpens()
        {
            Assert.IsTrue(BotRules.WantsDrift(true, false, -30f, 0.6f, true, true, 50f, 0.7f), "half the bend (left) still ahead: hold");
            Assert.IsFalse(BotRules.WantsDrift(true, false, 10f, 0.6f, true, true, 50f, 0.7f), "bend over: let go for the boost");
        }

        [Test]
        public void DriftBend_LongEvenBend_Yes_TightCorner_Kink_Ramp_No()
        {
            BotTrackMap wide = new BotTrackMap(Circle(25f), 2f, 6f);  // 24 m on a 25 m circle turn 55 degrees
            Assert.IsTrue(wide.IsDriftBend(3, 24f, 50f, 130f, 14f, 0.4f));
            BotTrackMap tight = new BotTrackMap(Circle(8f), 2f, 6f);  // 10 m on an 8 m circle turn 72 degrees, radius 8 < 14
            Assert.IsFalse(tight.IsDriftBend(3, 10f, 50f, 130f, 14f, 0.4f), "a tight corner is not a drift bend");
            Assert.IsFalse(wide.IsDriftBend(3, 8f, 50f, 130f, 14f, 0.4f), "8 m of it turn only 18 degrees: a kink");
            BotTrackMap ramp = new BotTrackMap(Circle(25f, 0.05f), 2f, 6f); // the same bend climbing 5 cm per point
            Assert.IsFalse(ramp.IsDriftBend(3, 24f, 50f, 130f, 14f, 0.4f), "no drift up a ramp");
            Assert.IsFalse(new BotTrackMap(null).IsDriftBend(0, 24f, 50f, 130f, 14f, 0.4f));
        }

        // ---------- stuck ----------

        [Test]
        public void Stuck_NotBeforeTwoSeconds_ThenReverse()
        {
            BotStuckCheck check = new BotStuckCheck(2f, 1.5f, 6f, 1.2f, 3);
            Assert.AreEqual(BotRecovery.Drive, check.Update(0.5f, 1.9f), "1.9 s against a wall is not stuck yet");
            Assert.AreEqual(BotRecovery.Reverse, check.Update(0.5f, 0.2f), "2.1 s: back up");
            Assert.IsTrue(check.IsReversing);
            Assert.AreEqual(1, check.Tries);
        }

        [Test]
        public void Stuck_ReverseLastsItsTime_ThenDrivesAgain()
        {
            BotStuckCheck check = new BotStuckCheck(2f, 1.5f, 6f, 1.2f, 3);
            Assert.AreEqual(BotRecovery.Reverse, check.Update(0f, 2.1f));
            Assert.AreEqual(BotRecovery.Reverse, check.Update(-2f, 1.1f), "still backing up after 1.1 s");
            Assert.AreEqual(BotRecovery.Reverse, check.Update(-2f, 0.2f), "the last bit of the 1.2 s");
            Assert.AreEqual(BotRecovery.Drive, check.Update(3f, 0.02f), "1.2 s of reversing done: forwards again");
        }

        [Test]
        public void Stuck_DrivingNormally_NeverStuck_SlowCornerIsNotStuck()
        {
            BotStuckCheck check = new BotStuckCheck(2f, 1.5f, 6f, 1.2f, 3);
            Assert.AreEqual(BotRecovery.Drive, Run(check, 14f, 30f));
            Assert.AreEqual(BotRecovery.Drive, Run(check, 2f, 10f), "2 m/s through a hairpin is slow but moving");
        }

        [Test]
        public void Stuck_ThreeFailedReverses_PutBackOnTheLine()
        {
            BotStuckCheck check = new BotStuckCheck(2f, 1.5f, 6f, 1.2f, 3);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(BotRecovery.Reverse, check.Update(0f, 2.1f), "try " + (i + 1));
                check.Update(0f, 1.3f); // backing up did not move it
            }
            Assert.AreEqual(BotRecovery.PutBack, check.Update(0f, 2.1f), "fourth time stuck: give up and put it back");
            Assert.AreEqual(0, check.Tries, "fresh start after being put back");
            Assert.IsFalse(check.IsReversing);
        }

        [Test]
        public void Stuck_GettingGoingAgain_ForgetsTheTries()
        {
            BotStuckCheck check = new BotStuckCheck(2f, 1.5f, 6f, 1.2f, 3);
            check.Update(0f, 2.1f);
            check.Update(-2f, 1.3f);
            Assert.AreEqual(1, check.Tries);
            Assert.AreEqual(BotRecovery.Drive, check.Update(8f, 0.5f), "free again");
            Assert.AreEqual(0, check.Tries);
        }

        [Test]
        public void Stuck_BadNumbers_CountAsStanding_NeverThrow()
        {
            BotStuckCheck check = new BotStuckCheck(2f, 1.5f, 6f, 1.2f, 3);
            Assert.AreEqual(BotRecovery.Drive, check.Update(float.NaN, float.NaN), "NaN time = no time passed");
            Assert.AreEqual(BotRecovery.Drive, check.Update(5f, -3f), "negative time = no time passed");
            Assert.AreEqual(BotRecovery.Reverse, check.Update(float.NaN, 2.1f), "NaN speed = not known to move");
            check.Reset();
            Assert.IsFalse(check.IsReversing);
            Assert.AreEqual(0, check.Tries);
        }

        // ---------- the racing line map ----------

        [Test]
        public void TrackMap_Circle_KnowsItsBendsAndLimitsTheSpeed()
        {
            BotTrackMap map = new BotTrackMap(Circle(6f), 2f, 6f);
            Assert.IsTrue(map.IsValid);
            Assert.AreEqual(Mathf.FloorToInt(2f * Mathf.PI * 6f / 2f), map.Count);
            // A clockwise circle seen from above = always turning right: curvature +1/6.
            Assert.AreEqual(1f / 6f, map.CurvatureAt(5), 0.01f);
            float allowed = map.AllowedSpeed(5, Top, 0.95f, Turn, 6f, 12f, 40f);
            float corner = BotRules.CornerSpeed(1f / 6f, Turn, 0.95f, 6f, Top); // 1.92 rad/s x 6 m x 0.95 = 10.9 m/s
            Assert.Less(corner, Top);
            Assert.AreEqual(corner, allowed, 0.3f, "in the circle: the corner speed");
            Assert.Greater(map.BendAhead(0, 10f), 30f, "10 m on a 6 m circle turns about 95 degrees, to the right (+)");
        }

        [Test]
        public void TrackMap_LongStraight_TopSpeed_BrakesBeforeTheEnd()
        {
            BotTrackMap map = new BotTrackMap(Stadium(200f, 8f), 2f, 6f);
            Assert.AreEqual(Top, map.AllowedSpeed(map.Wrap(10), Top, 0.95f, Turn, 6f, 12f, 40f), 0.0001f, "the straight's start: no bend within 40 m");
            // The 8 m half circle runs from 200 m to 225 m. Its corner speed: 1.92 rad/s x 8 m x 0.95 = 14.6 m/s.
            float corner = BotRules.CornerSpeed(1f / 8f, Turn, 0.95f, 6f, Top);
            // 12 m before the full bend (194 m) the kart may still be at top speed: sqrt(14.6^2 + 2 x 12 x 12) > 18.
            Assert.AreEqual(Top, map.AllowedSpeed(97, Top, 0.95f, Turn, 6f, 12f, 40f), 0.0001f, "far enough to brake later");
            // 4 m before it (202 m): sqrt(14.6^2 + 2 x 12 x 4) = 17.6 m/s, so brake now.
            Assert.Less(map.AllowedSpeed(101, Top, 0.95f, Turn, 6f, 12f, 40f), Top, "just before the bend: slow down");
            Assert.AreEqual(corner, map.AllowedSpeed(104, Top, 0.95f, Turn, 6f, 12f, 40f), 0.3f, "in the bend: the corner speed");
        }

        [Test]
        public void TrackMap_NearestNear_DoesNotJumpToTheOtherSideOfAUTurn()
        {
            // A long thin loop: two 100 m straights only 4 m apart (like the start plaza's U-turn).
            BotTrackMap map = new BotTrackMap(Stadium(100f, 2f), 2f, 6f);
            int onTheWayOut = 20; // 40 m along the first straight
            Vector3 drifting = map.Point(onTheWayOut) + map.Right(onTheWayOut) * 2.3f; // nearer the other straight (4 m away) than its own line
            int found = map.NearestNear(drifting, onTheWayOut - 1, 4, 12);
            Assert.That(map.Wrap(found - onTheWayOut), Is.LessThanOrEqualTo(2), "stays on its own straight");
        }

        [Test]
        public void TrackMap_NoTrack_IsInvalid_NeverThrows()
        {
            BotTrackMap none = new BotTrackMap(null);
            Assert.IsFalse(none.IsValid);
            Assert.AreEqual(Vector3.zero, none.Point(5));
            Assert.AreEqual(0f, none.CurvatureAt(3));
            Assert.AreEqual(0, none.NearestAnywhere(Vector3.one));
            BotTrackMap tooShort = new BotTrackMap(new TrackPolyline(new List<Vector3> { Vector3.zero, Vector3.right }));
            Assert.IsFalse(tooShort.IsValid);
        }

        // ---------- helpers ----------

        private static BotRecovery Run(BotStuckCheck check, float speed, float seconds)
        {
            BotRecovery last = BotRecovery.Drive;
            for (float t = 0f; t < seconds - 0.0001f; t += 0.05f)
            {
                last = check.Update(speed, Mathf.Min(0.05f, seconds - t));
            }
            return last;
        }

        // A circle driven clockwise seen from above (always turning right), first point at (0, 0, 0).
        // climbPerPoint > 0 makes the first half climb and the second half drop again (a ramp in the bend).
        private static ITrackProgress Circle(float radius, float climbPerPoint = 0f)
        {
            List<Vector3> points = new List<Vector3>();
            for (int i = 0; i < 72; i++)
            {
                float a = i * Mathf.PI * 2f / 72f;
                float y = climbPerPoint * (i < 36 ? i : 72 - i);
                points.Add(new Vector3(radius - radius * Mathf.Cos(a), y, radius * Mathf.Sin(a)));
            }
            return new TrackPolyline(points);
        }

        // Two straights (forwards +z, back -z) joined by half circles of `radius`: a stadium, clockwise.
        private static ITrackProgress Stadium(float straight, float radius)
        {
            List<Vector3> points = new List<Vector3>();
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
