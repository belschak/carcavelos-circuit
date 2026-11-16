using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Finish cinematic (Luc, 16 Nov 2026): after the line the PLAYER's kart drives on by itself, like a bot on a
    // lap of honour, while the camera swings round to its front. BotSquad hands this to the player's kart as its
    // IKartDrivable.InputSource at the finish and takes it back on Retry or on the way back to the menu.
    // A calm bot: the same steering and corner-speed rules as BotDriver (BotRules, BotTrackMap), but in the middle
    // of the racing line, at a share of top speed, no drifts, no overtaking. It is NOT an IRacer (the player stays
    // the player). Decide() is plain maths, so Edit Mode tests can drive it without a scene (FinishCruiseTests).
    public class FinishCruise : IKartInputSource
    {
        public const float DefaultSpeedShare = 0.55f; // a relaxed roll-out, slower than racing, quick enough to look alive
        public const float CorneringShare = 0.8f;     // a little gentler in bends than the bots (they use about 0.9)

        private readonly Transform root;
        private readonly IKartState kart;
        private readonly BotTrackMap map;
        private readonly BotSettings settings;
        private readonly float speedShare;
        private int lineIndex = -1;  // nearest racing line point, -1 = find it again
        private float steer;         // smoothed steering, so taking over from the player's hands is not a jerk
        private KartInput current = KartInput.None;

        public FinishCruise(Transform root, IKartState kart, BotTrackMap map, BotSettings settings, float speedShare)
        {
            this.root = root;
            this.kart = kart;
            this.map = map;
            this.settings = settings;
            this.speedShare = speedShare;
        }

        // The kart asks this once per frame while its input is on.
        public KartInput ReadInput()
        {
            return current;
        }

        // Start fresh from where the kart is now (called when the cruise takes over), keeping the player's steering.
        public void Begin()
        {
            lineIndex = -1;
            steer = kart != null ? kart.Steer : 0f;
        }

        // Decides this frame's input (BotSquad calls it every frame while the cruise drives).
        public void Tick(float deltaTime)
        {
            if (root == null || kart == null || map == null || !map.IsValid || settings == null)
            {
                current = KartInput.None;
                return;
            }
            float wanted;
            current = Decide(map, settings, ref lineIndex, root.position, root.forward, kart.Speed, kart.MaxSpeed, speedShare, out wanted);
            steer = Mathf.MoveTowards(steer, wanted, deltaTime / Mathf.Max(0.01f, settings.SteerSeconds));
            current = new KartInput(current.Throttle, steer, false).Clamped();
        }

        // One decision: aim at the racing line ahead, keep below the cruise speed and the speed the next bend allows.
        // lineIndex is the nearest racing line point from last frame (-1 = search the whole line). wantedSteer is the
        // unsmoothed steering, the returned input already carries it. Never drifts.
        public static KartInput Decide(BotTrackMap map, BotSettings settings, ref int lineIndex, Vector3 position,
            Vector3 heading, float speed, float maxSpeed, float speedShare, out float wantedSteer)
        {
            wantedSteer = 0f;
            if (map == null || !map.IsValid || settings == null || float.IsNaN(speed) || float.IsNaN(maxSpeed))
            {
                return KartInput.None;
            }
            lineIndex = lineIndex < 0 ? map.NearestAnywhere(position) : map.NearestNear(position, lineIndex, 4, 12);

            float look = settings.LookAheadMetres + settings.LookAheadSeconds * Mathf.Max(0f, speed);
            int aim = lineIndex + Mathf.Max(1, Mathf.RoundToInt(look / map.Step));
            wantedSteer = BotRules.SteerTowards(position, heading, map.Point(aim), speed,
                settings.TurnDegreesPerSecond, settings.FullSteerSpeed);

            float cruise = CruiseSpeed(maxSpeed, speedShare);
            float allowed = map.AllowedSpeed(lineIndex, cruise, CorneringShare, settings.TurnDegreesPerSecond,
                settings.MinCornerSpeed, settings.PlanBraking, settings.PlanMetres);
            float throttle = BotRules.Throttle(speed, Mathf.Min(cruise, allowed), settings.BrakeMargin);
            return new KartInput(throttle, wantedSteer, false).Clamped();
        }

        // Top speed of the roll-out in m/s: a share (0..1) of the kart's top speed. Bad values give 0 (stand still).
        public static float CruiseSpeed(float maxSpeed, float share)
        {
            if (float.IsNaN(maxSpeed) || float.IsNaN(share) || maxSpeed <= 0f)
            {
                return 0f;
            }
            return maxSpeed * Mathf.Clamp01(share);
        }
    }
}
