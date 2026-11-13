using CarcavelosCircuit.Audio;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Task P4: the music and the overall mix, following the race (it replaced the first, much simpler music script).
    // - Title screen: race music quietly. Countdown: music fades out, so the beeps are alone.
    // - GO: the race music starts again from bar 1, so its first beat lands on GO (120 BPM = the countdown rhythm).
    // - Last checkpoint passed: the music glides up one semitone and gets a bit louder (the "final stretch" lift).
    // - Finish: race music fades out under the jingle, then the calm results loop starts.
    // - Ducking: RaceSounds calls Duck() so the music dips under beeps, chimes and jingles.
    // - The engine and kart effects get quieter on the results screen (KartAudio.SetMixLevels).
    // - Seaside ambience (audit 12): waves and distant gulls loop quietly the whole time, a bit lower while racing
    //   (the engine and the music fill the space) and a bit higher on the results screen (a calm beach).
    // - Browser tab hidden or game window unfocused: all sound pauses (AudioListener.pause).
    // Unity's Web build only supports volume changes (no mixer effects), so every level here is a script fade
    // in dB instead of an AudioMixer snapshot. Numbers from audio-polish-plan.md section 2.2.
    public class RaceMusic : MonoBehaviour
    {
        [SerializeField]
        private RaceManager raceManager;

        [SerializeField, Tooltip("Looping race music (music_campus_loop)")]
        private AudioSource raceMusic;

        [SerializeField, Tooltip("Looping results music (music_results_loop)")]
        private AudioSource resultsMusic;

        [SerializeField, Tooltip("Looping seaside ambience: waves and distant gulls (amb_seaside_loop, audit 12)")]
        private AudioSource ambience;

        [Header("Music level per phase (dB, 0 = the file's own level)")]
        [SerializeField] private float titleDb = -10f;
        [SerializeField] private float racingDb = -8f;
        [SerializeField] private float finalStretchDb = -5f;
        [SerializeField] private float resultsDb = -2f;

        [Header("Seaside ambience per phase (dB, audit 12). Follows the Effects slider")]
        [SerializeField] private float ambienceTitleDb = -20f;
        [SerializeField] private float ambienceRacingDb = -24f;
        [SerializeField] private float ambienceResultsDb = -18f;

        [SerializeField, Tooltip("Seconds of the ambience fades (slow: the beach never jumps)")]
        private float ambienceFadeSeconds = 1.5f;

        [Header("Engine and kart effects per phase (dB)")]
        [SerializeField] private float engineCountdownDb = -3f;
        [SerializeField] private float effectsCountdownDb = -6f;
        [SerializeField] private float engineResultsDb = -14f;
        [SerializeField] private float effectsResultsDb = -14f;

        [Header("Timing")]
        [SerializeField, Tooltip("Seconds the music needs to fade out when the countdown starts")]
        private float countdownFadeSeconds = 0.3f;

        [SerializeField, Tooltip("Seconds of fade-in on GO (short = punchy start)")]
        private float goFadeSeconds = 0.12f;

        [SerializeField, Tooltip("Seconds of the fade to the results mix")]
        private float resultsFadeSeconds = 0.6f;

        [SerializeField, Tooltip("Seconds after the finish before the results loop starts (after the jingle)")]
        private float resultsMusicDelay = 1.8f;

        [SerializeField, Tooltip("Pitch on the final stretch: 1.0595 = one semitone higher")]
        private float finalStretchPitch = 1.0595f;

        [SerializeField, Tooltip("Seconds the pitch needs to glide up to the final-stretch pitch")]
        private float pitchGlideSeconds = 2f;

        [SerializeField, Tooltip("The music files start with 2048 silent samples (loop padding for the Web build). Starting there makes the first beat land exactly on GO. Set 0 if a build ever starts late.")]
        private int musicStartSample = 2048;

        [SerializeField, Tooltip("Pause all sound while the browser tab is hidden or the game window loses focus")]
        private bool pauseWhenUnfocused = true;

        private LevelFade raceLevel;
        private LevelFade resultsLevel;
        private LevelFade engineLevel;
        private LevelFade effectsLevel;
        private LevelFade ambienceLevel;
        private DuckEnvelope duck;
        private KartAudio kartAudio;
        private bool onFinalStretch; // true from the last checkpoint until the next GO
        private bool hasFocus = true;
        private bool isPaused;

        private void Awake()
        {
            raceLevel = new LevelFade(titleDb);
            resultsLevel = new LevelFade(Decibels.Silent);
            engineLevel = new LevelFade(0f);
            effectsLevel = new LevelFade(0f);
            ambienceLevel = new LevelFade(ambienceTitleDb);
            duck = new DuckEnvelope(200f, 15f); // down in about 0.03 s, back up at 15 dB per second
        }

        private void OnEnable()
        {
            raceManager.CountdownStep += OnCountdownStep;
            raceManager.CheckpointPassed += OnCheckpointPassed;
            raceManager.LapFinished += OnLapFinished;
        }

        private void OnDisable()
        {
            raceManager.CountdownStep -= OnCountdownStep;
            raceManager.CheckpointPassed -= OnCheckpointPassed;
            raceManager.LapFinished -= OnLapFinished;
        }

        private void Start()
        {
            // Title screen. Browsers keep sound muted until the first key or click; Unity starts it then.
            if (raceMusic != null)
            {
                raceMusic.loop = true;
                raceMusic.Play();
            }
            if (resultsMusic != null)
            {
                resultsMusic.loop = true;
            }
            if (ambience != null)
            {
                ambience.loop = true;
                ambience.Play(); // runs from the title screen on; only its volume changes, like the kart loops
            }
            Apply();
        }

        // Ducks the music for a moment (negative dB), e.g. -4 for a checkpoint chime. Called by RaceSounds.
        public void Duck(float db, float holdSeconds)
        {
            duck.Duck(db, holdSeconds);
        }

        private void OnCountdownStep(int number)
        {
            if (number > 0)
            {
                // Countdown (first start or retry): music out, kart a little quieter, results music off.
                // A duck left over from the finish jingle is dropped, so GO starts the music cleanly.
                duck.Clear();
                raceLevel.FadeTo(Decibels.Silent, countdownFadeSeconds);
                resultsLevel.FadeTo(Decibels.Silent, countdownFadeSeconds);
                engineLevel.FadeTo(engineCountdownDb, countdownFadeSeconds);
                effectsLevel.FadeTo(effectsCountdownDb, countdownFadeSeconds);
                ambienceLevel.FadeTo(ambienceTitleDb, ambienceFadeSeconds); // a retry from the results: back to the start level
                return;
            }

            // GO: race music from bar 1, normal pitch, full race mix.
            onFinalStretch = false;
            if (resultsMusic != null)
            {
                resultsMusic.Stop();
            }
            if (raceMusic != null && raceMusic.clip != null)
            {
                raceMusic.Stop();
                raceMusic.pitch = 1f;
                raceMusic.timeSamples = Mathf.Clamp(musicStartSample, 0, raceMusic.clip.samples - 1);
                raceMusic.Play();
            }
            raceLevel.FadeTo(racingDb, goFadeSeconds);
            engineLevel.FadeTo(0f, goFadeSeconds);
            effectsLevel.FadeTo(0f, goFadeSeconds);
            ambienceLevel.FadeTo(ambienceRacingDb, ambienceFadeSeconds);
        }

        private void OnCheckpointPassed(int index)
        {
            // Final stretch = the last checkpoint before the finish line. CheckpointCount includes the
            // start/finish line (index 0), so the last real checkpoint has index CheckpointCount - 1.
            if (raceManager.CheckpointCount > 1 && index == raceManager.CheckpointCount - 1)
            {
                onFinalStretch = true;
                raceLevel.FadeTo(finalStretchDb, 1.5f);
            }
        }

        private void OnLapFinished(float lapSeconds, bool newRecord)
        {
            raceLevel.FadeTo(Decibels.Silent, resultsFadeSeconds);
            engineLevel.FadeTo(engineResultsDb, resultsFadeSeconds);
            effectsLevel.FadeTo(effectsResultsDb, resultsFadeSeconds);
            ambienceLevel.FadeTo(ambienceResultsDb, ambienceFadeSeconds);
            if (resultsMusic != null && resultsMusic.clip != null)
            {
                resultsMusic.Stop();
                resultsMusic.timeSamples = Mathf.Clamp(musicStartSample, 0, resultsMusic.clip.samples - 1);
                resultsMusic.PlayDelayed(resultsMusicDelay);
                resultsLevel.FadeTo(resultsDb, 0f); // the loop starts quietly by itself, no fade needed
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            raceLevel.Tick(dt);
            resultsLevel.Tick(dt);
            engineLevel.Tick(dt);
            effectsLevel.Tick(dt);
            ambienceLevel.Tick(dt);
            duck.Tick(dt);

            // Final stretch pitch lift: glide up, never jump (a jump would sound like a broken tape).
            // It stays up while the music fades out after the finish; GO sets it back to normal.
            if (raceMusic != null)
            {
                float targetPitch = onFinalStretch ? finalStretchPitch : 1f;
                float step = (finalStretchPitch - 1f) / Mathf.Max(0.01f, pitchGlideSeconds) * dt;
                raceMusic.pitch = Mathf.MoveTowards(raceMusic.pitch, targetPitch, step);
            }
            Apply();
            UpdateListenerPause(); // every frame: follows the race pause too, so a tab return cannot blip the sound
        }

        // Turns the dB levels into AudioSource volumes (and kart levels).
        private void Apply()
        {
            if (raceMusic != null)
            {
                raceMusic.volume = Decibels.ToVolume(raceLevel.CurrentDb + duck.CurrentDb) * SoundLevels.Music; // settings slider (P7)
            }
            if (resultsMusic != null)
            {
                resultsMusic.volume = Decibels.ToVolume(resultsLevel.CurrentDb + duck.CurrentDb) * SoundLevels.Music;
            }
            if (ambience != null)
            {
                // Not ducked: the sea does not get quieter for a beep. It is a sound effect, so the Effects slider sets it.
                ambience.volume = Decibels.ToVolume(ambienceLevel.CurrentDb) * SoundLevels.Effects;
            }

            // The kart (and its KartAudio child) is spawned at runtime, so we look it up once it exists.
            if (kartAudio == null && raceManager.IsSetUp)
            {
                Component kartComponent = raceManager.Kart as Component;
                if (kartComponent != null)
                {
                    kartAudio = kartComponent.GetComponentInChildren<KartAudio>();
                }
            }
            if (kartAudio != null)
            {
                kartAudio.SetMixLevels(Decibels.ToVolume(engineLevel.CurrentDb) * SoundLevels.Effects,
                    Decibels.ToVolume(effectsLevel.CurrentDb) * SoundLevels.Effects);
            }
        }

        // Tab hidden / window unfocused. Unity's Web player stops updating then (Run In Background is off), but the
        // browser would keep playing the looping engine and music, so we pause every AudioSource.
        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            UpdateListenerPause();
        }

        private void OnApplicationPause(bool paused)
        {
            isPaused = paused;
            UpdateListenerPause();
        }

        private void UpdateListenerPause()
        {
            if (pauseWhenUnfocused)
            {
                // Paused race (P7): stays silent after the tab comes back, until the player clicks to continue.
                AudioListener.pause = !hasFocus || isPaused || raceManager.IsPaused;
            }
        }
    }
}
