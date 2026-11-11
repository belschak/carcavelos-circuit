using UnityEngine;

namespace CarcavelosCircuit.Shared
{
    // Round 2 contract: what a driver does this frame, the same three things the keyboard gives the kart.
    // A bot (Role 3) fills one of these every frame instead of pressing keys (see IKartInputSource).
    public struct KartInput
    {
        public float Throttle; // -1 full brake / reverse .. +1 full gas
        public float Steer;    // -1 full left .. +1 full right
        public bool Drift;     // drift key held

        public KartInput(float throttle, float steer, bool drift)
        {
            Throttle = throttle;
            Steer = steer;
            Drift = drift;
        }

        // Nothing pressed.
        public static KartInput None { get { return new KartInput(0f, 0f, false); } }

        // The same input with Throttle and Steer kept inside -1..1 and NaN turned into 0, so a bot's maths error
        // can never make a kart faster than a player or throw it off the track.
        public KartInput Clamped()
        {
            return new KartInput(Clamp(Throttle), Clamp(Steer), Drift);
        }

        private static float Clamp(float value)
        {
            if (float.IsNaN(value))
            {
                return 0f;
            }
            return Mathf.Clamp(value, -1f, 1f);
        }
    }
}
