using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Decides what the kart effects show this frame, from IKartState values only (task V5).
    // Plain C# without MonoBehaviour, so every rule has an Edit Mode test (KartEffectsRulesTests).
    // Drift stages as in docs/interfaces.md: 0 = smoke only, 1 = blue sparks, 2 = orange sparks.
    public class KartEffectsRules
    {
        public const int MaxStage = 2;

        public bool ShowSmoke { get; private set; }   // tyre smoke: drifting with the wheels on the ground
        public int SparkStage { get; private set; }   // 0 no sparks, 1 blue, 2 orange
        public bool StageUp { get; private set; }     // true for one frame when the sparks reach a new stage
        public bool ShowFlame { get; private set; }   // boost flame at the exhausts
        public bool Ignition { get; private set; }    // true for one frame when a boost starts (a backfire pop)
        public int FlameStage { get; private set; }   // stage of the drift that gave the boost, 0 = default colour

        private bool wasDrifting;
        private bool wasBoosting;
        private int driftStage;      // stage of the drift that is running now
        private int releasedStage;   // stage the last drift had when it ended

        // Call once per frame with the kart's current state.
        public void Update(bool isDrifting, bool isGrounded, int stage, bool isBoosting)
        {
            stage = Mathf.Clamp(stage, 0, MaxStage);
            StageUp = false;
            Ignition = false;

            if (isDrifting)
            {
                // A higher stage than before in this drift: one burst of sparks (only with the wheels on the ground).
                StageUp = stage > driftStage && isGrounded;
                driftStage = Mathf.Max(driftStage, stage);
            }
            else if (wasDrifting)
            {
                // The drift just ended: remember its stage, the boost it gives takes that colour.
                releasedStage = driftStage;
                driftStage = 0;
                // A drift released while an earlier boost still runs refreshes the boost: new pop, new colour.
                if (isBoosting && wasBoosting && releasedStage > 0)
                {
                    Ignition = true;
                    FlameStage = releasedStage;
                    releasedStage = 0;
                }
            }

            if (isBoosting && !wasBoosting)
            {
                Ignition = true;
                FlameStage = releasedStage;
                releasedStage = 0;
            }

            ShowSmoke = isDrifting && isGrounded;
            SparkStage = ShowSmoke ? driftStage : 0;
            ShowFlame = isBoosting;
            wasDrifting = isDrifting;
            wasBoosting = isBoosting;
        }

        // After a teleport (retry, recovery) everything starts fresh, like IKartState.ResetTo clears drift and boost.
        public void Reset()
        {
            ShowSmoke = false;
            SparkStage = 0;
            StageUp = false;
            ShowFlame = false;
            Ignition = false;
            FlameStage = 0;
            wasDrifting = false;
            wasBoosting = false;
            driftStage = 0;
            releasedStage = 0;
        }
    }
}
