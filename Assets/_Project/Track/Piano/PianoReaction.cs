using UnityEngine;

namespace CarcavelosCircuit.Track
{
    // Plain C# (Edit Mode tested): decides from distance, speed and time whether the piano plays, and how loud.
    // Every sound has a pause after it, so a kart parked next to the piano does not play it every frame.
    public class PianoReaction
    {
        private readonly float passRadius;     // metres from the piano's centre that count as "driving past"
        private readonly float hitRadius;      // metres from the centre that count as touching it
        private readonly float minPassSpeed;   // m/s; slower karts drive past in silence
        private readonly float minHitSpeed;    // m/s; a gentle touch makes no crash
        private readonly float passPause;      // seconds before the next pass-by chord
        private readonly float hitPause;       // seconds before the next crash

        private float lastPassTime = float.NegativeInfinity;
        private float lastHitTime = float.NegativeInfinity;

        public PianoReaction(float passRadius, float hitRadius, float minPassSpeed, float minHitSpeed, float passPause, float hitPause)
        {
            this.passRadius = Mathf.Max(0f, passRadius);
            this.hitRadius = Mathf.Clamp(hitRadius, 0f, this.passRadius);
            this.minPassSpeed = Mathf.Max(0f, minPassSpeed);
            this.minHitSpeed = Mathf.Max(0f, minHitSpeed);
            this.passPause = Mathf.Max(0f, passPause);
            this.hitPause = Mathf.Max(0f, hitPause);
        }

        // Call for every kart near the piano. Returns what to play now and remembers it for the pauses.
        // A hit wins over a pass-by. Speed counts forwards and backwards. NaN input = None.
        public PianoSoundKind Decide(float distance, float speed, float now)
        {
            if (float.IsNaN(distance) || float.IsNaN(speed) || float.IsNaN(now))
            {
                return PianoSoundKind.None;
            }
            float absSpeed = Mathf.Abs(speed);
            if (distance <= hitRadius && absSpeed >= minHitSpeed && now - lastHitTime >= hitPause)
            {
                lastHitTime = now;
                lastPassTime = now; // no chord right after the crash
                return PianoSoundKind.Hit;
            }
            if (distance <= passRadius && absSpeed >= minPassSpeed && now - lastPassTime >= passPause)
            {
                lastPassTime = now;
                return PianoSoundKind.PassBy;
            }
            return PianoSoundKind.None;
        }

        // Faster crash = louder: 0.5 at the slowest hit, 1.0 at the kart's top speed or above.
        public static float HitVolume(float speed, float maxSpeed)
        {
            if (float.IsNaN(speed) || maxSpeed <= 0f || float.IsNaN(maxSpeed))
            {
                return 0.5f;
            }
            return 0.5f + 0.5f * Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed);
        }
    }
}
