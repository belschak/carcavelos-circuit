using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Track
{
    // Task T10: the grand piano by the west glass of the Nova hall plays when a kart drives past or into it.
    // The piano you see is part of the hall model (help PR #45); this object only sits at the same spot with a
    // trigger sphere around it. Any kart (player or bot) inside the sphere is checked with PianoReaction, and the
    // notes come from PianoTone (made in code, no sound file).
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(SphereCollider))]
    public class AtriumPiano : MonoBehaviour
    {
        [SerializeField, Tooltip("Metres from the piano's centre where a passing kart makes it play a chord")]
        private float passRadius = 8f;

        [SerializeField, Tooltip("Metres from the piano's centre that count as driving into it (piano body is about 2 x 1.4 m)")]
        private float hitRadius = 2.2f;

        [SerializeField, Tooltip("Slowest speed (m/s) that still plays the pass-by chord")]
        private float minPassSpeed = 3f;

        [SerializeField, Tooltip("Slowest speed (m/s) that still makes the crash")]
        private float minHitSpeed = 1f;

        [SerializeField, Tooltip("Seconds of quiet after a pass-by chord")]
        private float passPause = 3f;

        [SerializeField, Tooltip("Seconds of quiet after a crash")]
        private float hitPause = 1f;

        [SerializeField, Range(0f, 1f), Tooltip("Volume of the pass-by chord")]
        private float passVolume = 0.6f;

        // Keys (0 = A4): rising C major, F major and G major chords for passing, a low cluster for the crash.
        private static readonly int[][] PassChords =
        {
            new[] { -9, -5, -2, 3 },   // C4 E4 G4 C5
            new[] { -4, 0, 3, 8 },     // F4 A4 C5 F5
            new[] { -2, 2, 5, 10 },    // G4 B4 D5 G5
        };
        private static readonly int[] CrashKeys = { -33, -32, -30, -27, -26, -21 }; // low keys pressed at once

        private AudioSource source;
        private PianoReaction reaction;
        private AudioClip[] passClips;
        private AudioClip crashClip;
        private int nextChord;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            SphereCollider zone = GetComponent<SphereCollider>();
            zone.isTrigger = true;           // karts drive through it; the hall model's collider is the solid piano
            zone.radius = passRadius;
            reaction = new PianoReaction(passRadius, hitRadius, minPassSpeed, minHitSpeed, passPause, hitPause);
            passClips = new AudioClip[PassChords.Length];
            for (int i = 0; i < PassChords.Length; i++)
            {
                passClips[i] = MakeClip("Piano chord " + i, PassChords[i], new[] { 0f, 0.09f, 0.18f, 0.27f }, 2.2f);
            }
            crashClip = MakeClip("Piano crash", CrashKeys, new float[CrashKeys.Length], 2.5f);
        }

        // Called every physics step for each collider inside the sphere; only karts count.
        private void OnTriggerStay(Collider other)
        {
            IKartState kart = other.GetComponentInParent<IKartState>();
            if (kart == null)
            {
                return;
            }
            Vector3 offset = other.transform.position - transform.position;
            offset.y = 0f;                   // flat distance: the kart's height does not matter
            PianoSoundKind sound = reaction.Decide(offset.magnitude, kart.Speed, Time.time);
            if (sound == PianoSoundKind.Hit)
            {
                source.PlayOneShot(crashClip, PianoReaction.HitVolume(kart.Speed, kart.MaxSpeed));
            }
            else if (sound == PianoSoundKind.PassBy)
            {
                source.PlayOneShot(passClips[nextChord], passVolume);
                nextChord = (nextChord + 1) % passClips.Length; // a different chord each time
            }
        }

        // Turns PianoTone's samples into a sound Unity can play (mono).
        private static AudioClip MakeClip(string clipName, int[] keys, float[] starts, float seconds)
        {
            float[] samples = PianoTone.Notes(keys, starts, seconds);
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, PianoTone.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // Inspector change: keep the trigger sphere as big as the pass radius.
        private void OnValidate()
        {
            SphereCollider zone = GetComponent<SphereCollider>();
            if (zone != null)
            {
                zone.isTrigger = true;
                zone.radius = passRadius;
            }
        }

        // Scene view: yellow = pass-by area, red = counts as a hit.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, passRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, hitRadius);
        }
    }
}
