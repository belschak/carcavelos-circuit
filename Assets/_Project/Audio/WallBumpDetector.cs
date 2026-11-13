using UnityEngine;

namespace CarcavelosCircuit.Audio
{
    // Guesses a wall hit from the speed alone (audit 11), because IKartState has no "hit a wall" event.
    // A bump = on the ground, allowed to drive, and the speed fell by a lot in ONE frame, much faster than the
    // strongest brake can do it. Plain C#, unit tested. KartAudio plays a thud when Update returns true.
    // - minDrop: at least this many m/s lost in that frame (a scrape along a wall loses less and stays silent).
    // - minDeceleration: the drop divided by the frame time must be above this (m/s per second). The kart brakes
    //   at 30 m/s2 at most, so 60 means "never a brake, even in a long frame of a slow laptop".
    // - cooldownSeconds: a kart grinding along a wall gives one thud, not one per frame.
    public class WallBumpDetector
    {
        private readonly float minDrop;
        private readonly float minDeceleration;
        private readonly float cooldownSeconds;
        private float lastSpeed;
        private bool hasLastSpeed;
        private float cooldownLeft;

        public float LastDrop { get; private set; } // m/s lost in the last bump, for its volume

        public WallBumpDetector(float minDrop, float minDeceleration, float cooldownSeconds)
        {
            this.minDrop = minDrop;
            this.minDeceleration = minDeceleration;
            this.cooldownSeconds = cooldownSeconds;
        }

        // Call once per frame. Returns true on the frame of a bump.
        public bool Update(float speed, bool grounded, bool canDrive, float deltaTime)
        {
            float size = Mathf.Abs(speed); // reversing into a wall counts too
            float drop = hasLastSpeed ? lastSpeed - size : 0f;
            lastSpeed = size;
            hasLastSpeed = true;
            if (cooldownLeft > 0f)
            {
                cooldownLeft -= deltaTime;
            }
            if (!grounded || !canDrive || deltaTime <= 0f || cooldownLeft > 0f)
            {
                return false; // a landing, the countdown or a paused frame is never a bump
            }
            if (drop < minDrop || drop / deltaTime < minDeceleration)
            {
                return false;
            }
            LastDrop = drop;
            cooldownLeft = cooldownSeconds;
            return true;
        }

        // After a teleport (R, fall, retry) the speed jumps to 0 on purpose: forget the old speed.
        public void Reset()
        {
            hasLastSpeed = false;
            cooldownLeft = 0f;
        }

        // Louder for a harder hit: 0.5 at minDrop, 1 at fullDrop and above.
        public static float VolumeFor(float drop, float minDrop, float fullDrop)
        {
            if (fullDrop <= minDrop)
            {
                return 1f;
            }
            return Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(minDrop, fullDrop, drop));
        }
    }
}
