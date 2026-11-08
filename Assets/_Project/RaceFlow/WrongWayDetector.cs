using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Decides when the HUD shows "WRONG WAY". Plain C# (no MonoBehaviour), so Edit Mode tests can feed it fake numbers.
    // Idea: compare the direction the kart is moving in with the direction to the next checkpoint (seen from above).
    // If they point away from each other for a while, the player drives the wrong way.
    // Hysteresis = two different thresholds and two timers, so the warning does not flicker on and off in a corner:
    // it needs about 1 s of clearly wrong driving to appear and about 0.4 s of clearly right driving to go away.
    public class WrongWayDetector
    {
        private readonly float enterSeconds;
        private readonly float exitSeconds;
        private readonly float minSpeed;

        // Dot product of two directions: 1 = same direction, 0 = at a right angle, -1 = opposite.
        private const float EnterDot = -0.25f; // clearly away from the checkpoint (more than about 105 degrees)
        private const float ExitDot = 0.15f;   // clearly towards it again (less than about 80 degrees)

        private float wrongTime;
        private float rightTime;

        public bool IsWrongWay { get; private set; }

        // enterSeconds: how long wrong driving must last before the warning shows.
        // exitSeconds: how long right driving must last before it hides again.
        // minSpeed (m/s): below this the kart counts as standing still and nothing changes.
        public WrongWayDetector(float enterSeconds = 1f, float exitSeconds = 0.4f, float minSpeed = 2f)
        {
            this.enterSeconds = enterSeconds;
            this.exitSeconds = exitSeconds;
            this.minSpeed = minSpeed;
        }

        // Call once per frame while racing.
        // kartForward: the kart's forward direction. signedSpeed: forward speed in m/s (negative = reversing).
        // toNextCheckpoint: next checkpoint position minus kart position. Returns IsWrongWay.
        public bool Update(Vector3 kartForward, float signedSpeed, Vector3 toNextCheckpoint, float deltaTime)
        {
            // Standing still or crawling: keep the current answer (a kart parked backwards still faces the wrong way).
            if (Mathf.Abs(signedSpeed) < minSpeed)
            {
                return IsWrongWay;
            }

            // Moving direction: forward when driving, backwards when reversing. Flat (y = 0), so ramps do not matter.
            Vector3 moving = kartForward * Mathf.Sign(signedSpeed);
            Vector2 move2 = new Vector2(moving.x, moving.z);
            Vector2 target2 = new Vector2(toNextCheckpoint.x, toNextCheckpoint.z);
            if (move2.sqrMagnitude < 0.0001f || target2.sqrMagnitude < 0.0001f)
            {
                return IsWrongWay;
            }
            float dot = Vector2.Dot(move2.normalized, target2.normalized);

            if (dot < EnterDot)
            {
                wrongTime += deltaTime;
                rightTime = 0f;
                if (wrongTime >= enterSeconds)
                {
                    IsWrongWay = true;
                }
            }
            else if (dot > ExitDot)
            {
                rightTime += deltaTime;
                wrongTime = 0f;
                if (rightTime >= exitSeconds)
                {
                    IsWrongWay = false;
                }
            }
            // In between (roughly sideways): nothing changes. That gap is the hysteresis band.
            return IsWrongWay;
        }

        // After Retry, Recover or a new lap.
        public void Reset()
        {
            wrongTime = 0f;
            rightTime = 0f;
            IsWrongWay = false;
        }
    }
}
