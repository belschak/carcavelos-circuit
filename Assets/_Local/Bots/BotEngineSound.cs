using CarcavelosCircuit.Audio;
using CarcavelosCircuit.RaceFlow;
using UnityEngine;

namespace CarcavelosCircuit.Bots
{
    // LOCAL PROTOTYPE (bots). The engine sound of one bot kart: one looping AudioSource that sits on the kart.
    // It is a 3D sound: loud next to the player, fading with distance and silent beyond maxDistance.
    // Pitch follows the bot's own speed (same rule as the player's engine, EnginePitch.LowLoopPitch), volume
    // follows its gas pedal. Every bot is tuned a little differently so two engines never sound like one.
    // BotRace decides which bots are heard at all (Audible): only the nearest few, because browsers have few voices.
    public class BotEngineSound : MonoBehaviour
    {
        [Tooltip("Loudest volume of a bot engine (the player's own engine is 0.75)")]
        public float fullVolume = 0.4f;
        [Tooltip("Full volume up to this distance from the camera, in metres")]
        public float minDistance = 5f;
        [Tooltip("Silent beyond this distance, in metres")]
        public float maxDistance = 38f;
        [Tooltip("Seconds to fade in or out when the bot enters or leaves the 'nearest bots' group")]
        public float fadeSeconds = 0.4f;

        public bool Audible { get; set; }

        private AudioSource source;
        private float detune = 1f;   // this bot's own pitch offset
        private float fade;          // 0 = faded out, 1 = fully in
        private float revs;          // smoothed speed 0..1
        private float load = 1f;     // smoothed gas pedal 0..1

        // number = which bot this is, total = how many bots: spreads the pitch offsets from -6 % to +6 %.
        public void SetUp(AudioClip engineLoop, int number, int total)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = engineLoop;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;                        // fully 3D
            source.rolloffMode = AudioRolloffMode.Linear;    // straight fade from minDistance to maxDistance
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;                        // no pitch bending when karts pass: it sounds wrong on a kart
            source.volume = 0f;
            detune = total > 1 ? Mathf.Lerp(0.94f, 1.06f, number / (total - 1f)) : 1f;
        }

        // Called every physics step by BotRace with the bot's speed and what it does with the gas pedal.
        public void Drive(float speed, float maxSpeed, float throttle)
        {
            float dt = Time.fixedDeltaTime;
            revs = Mathf.MoveTowards(revs, EnginePitch.Speed01(speed, maxSpeed), 2.5f * dt);
            load = Mathf.MoveTowards(load, throttle > 0f ? 1f : 0.55f, 3f * dt); // off the gas = quieter
            fade = Mathf.MoveTowards(fade, Audible ? 1f : 0f, dt / fadeSeconds);

            // A voice that is faded out is paused, so it does not use one of the browser's sound channels.
            if (fade <= 0f)
            {
                if (source.isPlaying)
                {
                    source.Pause();
                }
                return;
            }
            if (!source.isPlaying)
            {
                source.Play();
            }
            source.pitch = EnginePitch.LowLoopPitch(revs) * detune;
            source.volume = fullVolume * Mathf.Lerp(0.6f, 1f, revs) * load * fade * SoundLevels.Effects;
        }
    }
}
