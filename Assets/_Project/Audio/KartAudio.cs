using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Audio
{
    // Task R5 (kart part): engine loop with pitch by speed, drift screech loop, drift-stage pings, boost whoosh, landing thud.
    // Lives on the KartAudio prefab, which RaceBootstrap spawns as a child of the kart.
    // It only READS IKartState from its parent, so it works with any kart that follows the contract.
    public class KartAudio : MonoBehaviour
    {
        [SerializeField, Tooltip("Looping engine sound")]
        private AudioSource engineSource;

        [SerializeField, Tooltip("Looping tyre screech, faded in while drifting")]
        private AudioSource driftSource;

        [SerializeField, Tooltip("One-shot sounds: stage pings, boost, landing")]
        private AudioSource effectSource;

        [SerializeField]
        private AudioClip driftStage1Clip;

        [SerializeField]
        private AudioClip driftStage2Clip;

        [SerializeField]
        private AudioClip boostClip;

        [SerializeField]
        private AudioClip landClip;

        [SerializeField, Tooltip("Engine pitch standing still and at top speed")]
        private float minPitch = 0.8f;

        [SerializeField]
        private float maxPitch = 1.8f;

        [SerializeField, Tooltip("Extra pitch while boosting or in the air (engine revs freely)")]
        private float revBonus = 0.15f;

        [SerializeField, Tooltip("Higher = the engine follows speed changes faster")]
        private float pitchSharpness = 6f;

        [SerializeField, Range(0f, 1f)]
        private float idleVolume = 0.35f;

        [SerializeField, Range(0f, 1f)]
        private float fullVolume = 0.7f;

        [SerializeField, Range(0f, 1f)]
        private float driftVolume = 0.45f;

        [SerializeField, Tooltip("Air time needed before landing makes a thud")]
        private float minAirSeconds = 0.3f;

        private IKartState kart;
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
            engineSource.pitch = minPitch;
            engineSource.Play();
            driftSource.volume = 0f;
            driftSource.Play();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float speed01 = EnginePitch.Speed01(kart.Speed, kart.MaxSpeed);

            // Engine: pitch and volume follow speed, smoothed so gear-less jumps do not sound robotic.
            float targetPitch = EnginePitch.FromSpeed01(speed01, minPitch, maxPitch);
            if (kart.IsBoosting || !kart.IsGrounded)
            {
                targetPitch += revBonus;
            }
            engineSource.pitch = EnginePitch.Smooth(engineSource.pitch, targetPitch, pitchSharpness, dt);
            engineSource.volume = Mathf.Lerp(idleVolume, fullVolume, speed01);

            // Drift screech: quick fade in/out instead of a hard cut.
            float driftTarget = kart.IsDrifting && kart.IsGrounded ? driftVolume : 0f;
            driftSource.volume = Mathf.MoveTowards(driftSource.volume, driftTarget, dt * 4f);

            // Stage pings: one sound each time the drift reaches a new stage (blue, then orange).
            int stage = kart.DriftStage;
            if (stage > lastDriftStage)
            {
                PlayOneShot(stage == 2 ? driftStage2Clip : driftStage1Clip, 0.7f);
            }
            lastDriftStage = stage;

            // Boost: whoosh on the frame the boost starts.
            if (kart.IsBoosting && !wasBoosting)
            {
                PlayOneShot(boostClip, 0.9f);
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
                    PlayOneShot(landClip, 0.8f);
                }
                airSeconds = 0f;
            }
            wasGrounded = kart.IsGrounded;
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip != null)
            {
                effectSource.PlayOneShot(clip, volume);
            }
        }
    }
}
