using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Audio
{
    // Task R5 (kart part): engine, drift, boost, landing and wind sounds.
    // Lives on the KartAudio prefab, which RaceBootstrap spawns as a child of the kart.
    // It only READS IKartState from its parent, so it works with any kart that follows the contract.
    // Engine and drift recordings are Luc's (Role 3), the rules for pitch and volume follow his handoff.
    public class KartAudio : MonoBehaviour
    {
        [Header("Looping sounds (one AudioSource each)")]
        [SerializeField, Tooltip("Engine at low revs (kart_engine_low)")]
        private AudioSource engineLowSource;

        [SerializeField, Tooltip("Engine at high revs (kart_engine_high)")]
        private AudioSource engineHighSource;

        [SerializeField, Tooltip("Tyre squeal while drifting (kart_drift_squeal)")]
        private AudioSource squealSource;

        [SerializeField, Tooltip("Spark crackle while the drift has sparks (kart_drift_sparks)")]
        private AudioSource sparksSource;

        [SerializeField, Tooltip("Wind rush that grows with speed (amb_wind_loop)")]
        private AudioSource windSource;

        [Header("One-shot sounds")]
        [SerializeField, Tooltip("Own source for the stage-up chime, because its pitch changes per stage")]
        private AudioSource stageSource;

        [SerializeField, Tooltip("Drift start chirp, boost, landing")]
        private AudioSource effectSource;

        [SerializeField]
        private AudioClip driftStartClip;

        [SerializeField]
        private AudioClip stageUpClip;

        [SerializeField]
        private AudioClip boostClip;

        [SerializeField]
        private AudioClip landClip;

        [Header("Mix")]
        [SerializeField, Tooltip("How fast the revs may rise and fall, in 'full range' per second")]
        private float revRisePerSecond = 1.4f;

        [SerializeField]
        private float revFallPerSecond = 2.2f;

        [SerializeField, Tooltip("Extra pitch while boosting or in the air (engine revs freely)")]
        private float revBonus = 0.1f;

        [SerializeField, Range(0f, 1f)]
        private float engineIdleVolume = 0.45f;

        [SerializeField, Range(0f, 1f)]
        private float engineFullVolume = 0.75f;

        [SerializeField, Range(0f, 1f)]
        private float squealVolume = 0.6f;

        [SerializeField, Range(0f, 1f)]
        private float windFullVolume = 0.35f;

        [SerializeField, Tooltip("Air time needed before landing makes a thud")]
        private float minAirSeconds = 0.3f;

        private IKartState kart;
        private float revs; // smoothed 0..1, drives the engine
        private bool wasDrifting;
        private bool wasBoosting;
        private bool wasGrounded = true;
        private int lastDriftStage;
        private float airSeconds;

        private void Start()
        {
            kart = GetComponentInParent<IKartState>();
            if (kart == null)
            {
                Debug.LogWarning("KartAudio: no IKartState found on a parent object. Put the KartAudio prefab under the kart.");
                enabled = false;
                return;
            }
            // All loops run all the time; we only change their volume. Starting a loop late can click.
            StartSilent(engineLowSource);
            StartSilent(engineHighSource);
            StartSilent(squealSource);
            StartSilent(sparksSource);
            StartSilent(windSource);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float speed01 = EnginePitch.Speed01(kart.Speed, kart.MaxSpeed);

            UpdateEngine(speed01, dt);
            UpdateDrift(dt);
            UpdateWind(speed01);
            UpdateBoostAndLanding(dt);
        }

        // Two loops crossfaded by revs. Idle while the player cannot drive (countdown, results).
        private void UpdateEngine(float speed01, float dt)
        {
            float targetRevs = kart.InputEnabled ? speed01 : 0f;
            revs = EnginePitch.MoveLimited(revs, targetRevs, revRisePerSecond, revFallPerSecond, dt);

            float bonus = (kart.IsBoosting || !kart.IsGrounded) ? revBonus : 0f;
            float highMix = EnginePitch.HighLoopMix(revs);
            float volume = Mathf.Lerp(engineIdleVolume, engineFullVolume, revs);

            engineLowSource.pitch = EnginePitch.LowLoopPitch(revs) + bonus;
            engineHighSource.pitch = EnginePitch.HighLoopPitch(revs) + bonus;
            engineLowSource.volume = volume * (1f - highMix);
            engineHighSource.volume = volume * highMix;
        }

        private void UpdateDrift(float dt)
        {
            bool drifting = kart.IsDrifting && kart.IsGrounded;
            int stage = drifting ? kart.DriftStage : 0;

            // Chirp when the drift starts.
            if (drifting && !wasDrifting)
            {
                PlayOneShot(effectSource, driftStartClip, 0.8f);
            }
            wasDrifting = drifting;

            // Squeal fades in and out over a few tenths of a second instead of a hard cut.
            float squealTarget = drifting ? squealVolume : 0f;
            squealSource.volume = Mathf.MoveTowards(squealSource.volume, squealTarget, dt * 3f);

            // Sparks: louder and a bit higher with every stage.
            float sparksTarget = DriftSound.SparksVolume(stage);
            sparksSource.volume = Mathf.MoveTowards(sparksSource.volume, sparksTarget, dt * 4f);
            sparksSource.pitch = DriftSound.SparksPitch(stage);

            // Chime each time the drift reaches a new stage, higher for every stage.
            if (stage > lastDriftStage)
            {
                stageSource.pitch = DriftSound.StageUpPitch(stage);
                PlayOneShot(stageSource, stageUpClip, 0.8f);
            }
            lastDriftStage = stage;
        }

        // Wind: only really audible when fast (speed squared), so it makes speed feel fast.
        private void UpdateWind(float speed01)
        {
            windSource.volume = windFullVolume * speed01 * speed01;
            windSource.pitch = 0.8f + 0.4f * speed01;
        }

        private void UpdateBoostAndLanding(float dt)
        {
            // Boost: whoosh on the frame the boost starts.
            if (kart.IsBoosting && !wasBoosting)
            {
                PlayOneShot(effectSource, boostClip, 0.9f);
            }
            wasBoosting = kart.IsBoosting;

            // Landing after a jump.
            if (!kart.IsGrounded)
            {
                airSeconds += dt;
            }
            else
            {
                if (!wasGrounded && airSeconds >= minAirSeconds)
                {
                    PlayOneShot(effectSource, landClip, 0.8f);
                }
                airSeconds = 0f;
            }
            wasGrounded = kart.IsGrounded;
        }

        private static void StartSilent(AudioSource source)
        {
            source.volume = 0f;
            source.Play();
        }

        // PlayOneShot lets sounds overlap on one AudioSource. A missing clip is simply skipped.
        private static void PlayOneShot(AudioSource source, AudioClip clip, float volume)
        {
            if (clip != null)
            {
                source.PlayOneShot(clip, volume);
            }
        }
    }
}
