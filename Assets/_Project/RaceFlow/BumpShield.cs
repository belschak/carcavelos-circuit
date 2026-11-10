namespace CarcavelosCircuit.RaceFlow
{
    // The Dean's List shield (task R7). Plain C#, no Unity objects, so the rules are Edit Mode tested.
    // RaceFlow cannot see the kart's collisions (IKartState has no hit event, docs/interfaces.md), but it can see
    // the speed. A wall bump is a sudden drop: in ONE frame the speed falls by at least minDrop m/s AND by at least
    // dropShare of the speed. Braking or letting go of the throttle loses much less per frame, so it never counts.
    // The shield waits for one bump, then reports Blocked (ItemSystem gives the speed back as a boost) and is gone.
    // If no bump comes within its time it reports Expired.
    public class BumpShield
    {
        public enum Result { Nothing, Blocked, Expired }

        public bool IsActive { get; private set; }
        public float SecondsLeft { get; private set; }

        private float lastSpeed = -1f; // speed seen last frame, -1 = none yet (just switched on, or after a teleport)

        // Dean's List used: the shield is on for this many seconds.
        public void Activate(float seconds)
        {
            IsActive = seconds > 0f;
            SecondsLeft = seconds;
            lastSpeed = -1f;
        }

        // The kart was teleported (recover R, or a fall): its speed is 0 on purpose, that is not a bump.
        public void NoteTeleport()
        {
            lastSpeed = -1f;
        }

        // Retry: no shield.
        public void Clear()
        {
            IsActive = false;
            SecondsLeft = 0f;
            lastSpeed = -1f;
        }

        // Called every frame while racing with the kart's speed (m/s). Returns what happened in this frame.
        public Result Tick(float deltaSeconds, float speed, float minDrop, float dropShare)
        {
            if (!IsActive)
            {
                return Result.Nothing;
            }
            float before = lastSpeed;
            lastSpeed = speed;
            if (IsBump(before, speed, minDrop, dropShare))
            {
                Clear();
                return Result.Blocked;
            }
            SecondsLeft -= deltaSeconds;
            if (SecondsLeft <= 0f)
            {
                Clear();
                return Result.Expired;
            }
            return Result.Nothing;
        }

        // True if going from "before" to "now" in one frame is a bump. before < 0 = no earlier frame to compare with.
        public static bool IsBump(float before, float now, float minDrop, float dropShare)
        {
            if (before <= 0f)
            {
                return false;
            }
            float drop = before - now;
            return drop >= minDrop && drop >= before * dropShare;
        }
    }
}
