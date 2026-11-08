using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R5 (race part) + P4 polish: countdown beeps, checkpoint chime, finish jingle, new-record sting,
    // click on retry, whoosh on respawn, warning cue for a missed checkpoint.
    // It listens to the RaceManager's events. The sound files live in Audio/Clips; their loudness is set in the
    // files (tools/audio-synth/process_wavs.py), so every sound plays at full volume here.
    // Kart sounds (engine, drift, boost) are in the Audio module (KartAudio), because they only read IKartState.
    public class RaceSounds : MonoBehaviour
    {
        [SerializeField]
        private RaceManager raceManager;

        [SerializeField, Tooltip("Ducks the music under the important sounds (RaceMusic on the Music object)")]
        private RaceMusic music;

        [SerializeField, Tooltip("A 2D AudioSource (Spatial Blend 0) for interface sounds")]
        private AudioSource source;

        [SerializeField, Tooltip("Second 2D AudioSource for sounds that start a moment later (record sting, retry beep)")]
        private AudioSource delayedSource;

        [SerializeField, Tooltip("Short low beep for 3, 2, 1")]
        private AudioClip countdownBeep;

        [SerializeField, Tooltip("Longer, higher beep for GO")]
        private AudioClip goBeep;

        [SerializeField]
        private AudioClip checkpointChime;

        [SerializeField]
        private AudioClip finishJingle;

        [SerializeField]
        private AudioClip newRecordSting;

        [SerializeField, Tooltip("Short click when the player restarts (Enter or the Retry button)")]
        private AudioClip retryClick;

        [SerializeField, Tooltip("Airy whoosh when the kart is put back on the track (R or a fall)")]
        private AudioClip respawnWhoosh;

        [SerializeField, Tooltip("Two falling notes when the hint 'Missed a checkpoint' appears (sfx_hint_warning)")]
        private AudioClip hintWarning;

        [SerializeField, Range(0f, 1f)]
        private float volume = 1f;

        [SerializeField, Tooltip("The new-record sting waits this long after the finish jingle, so the two do not stack into one muddy sound and the sting lands with the NEW RECORD stamp on the results card")]
        private float recordStingDelay = 1.25f;

        [SerializeField, Tooltip("On a retry the first beep waits this long, so the retry click is not hidden under it")]
        private float retryBeepDelay = 0.1f;

        private RaceState stateLastFrame = RaceState.Ready;
        private float lastHintUntil;

        private void OnEnable()
        {
            raceManager.CountdownStep += OnCountdownStep;
            raceManager.CheckpointPassed += OnCheckpointPassed;
            raceManager.LapFinished += OnLapFinished;
            raceManager.Restarted += OnRestarted;
            raceManager.Recovered += OnRecovered;
        }

        private void OnDisable()
        {
            raceManager.CountdownStep -= OnCountdownStep;
            raceManager.CheckpointPassed -= OnCheckpointPassed;
            raceManager.LapFinished -= OnLapFinished;
            raceManager.Restarted -= OnRestarted;
            raceManager.Recovered -= OnRecovered;
        }

        private void Update()
        {
            // The hint ("Missed a checkpoint! Press R to go back") has no event, but RaceManager pushes its end time
            // forward every time it shows a hint. A later end time = a new hint = play the warning once.
            if (raceManager.HintText != "" && raceManager.HintVisibleUntil > lastHintUntil)
            {
                Play(hintWarning);
                Duck(-4f, hintWarning);
            }
            lastHintUntil = raceManager.HintVisibleUntil;

            // Remembered for OnCountdownStep: a countdown that starts after Racing or Results is a retry.
            stateLastFrame = raceManager.State;
        }

        private void OnCountdownStep(int number)
        {
            if (number == 0)
            {
                Play(goBeep);
                Duck(-3f, goBeep);
                return;
            }

            // Retry: RaceManager fires the first beep and the Restarted click in the same frame, and the beep would
            // hide the click. So on a retry the first beep starts a moment later on the second source.
            // (This also cancels a record sting that was still waiting.)
            bool firstStepOfRetry = stateLastFrame == RaceState.Racing || stateLastFrame == RaceState.Results;
            if (firstStepOfRetry)
            {
                PlayLater(countdownBeep, retryBeepDelay);
            }
            else
            {
                Play(countdownBeep);
            }
            Duck(-3f, countdownBeep);
        }

        private void OnCheckpointPassed(int index)
        {
            Play(checkpointChime);
            Duck(-4f, checkpointChime);
        }

        private void OnLapFinished(float lapSeconds, bool newRecord)
        {
            Play(finishJingle);
            Duck(-12f, finishJingle);
            if (newRecord)
            {
                // With the NEW RECORD stamp, not on top of the jingle's first notes.
                PlayLater(newRecordSting, recordStingDelay);
                if (music != null && newRecordSting != null)
                {
                    music.Duck(-12f, recordStingDelay + newRecordSting.length * 0.6f);
                }
            }
        }

        private void OnRestarted()
        {
            // A sting still waiting from the old lap is stopped by the delayed first beep (PlayLater replaces it).
            Play(retryClick);
        }

        private void OnRecovered()
        {
            Play(respawnWhoosh);
        }

        // PlayOneShot lets several sounds overlap on one AudioSource. A missing clip is simply skipped.
        private void Play(AudioClip clip)
        {
            if (clip != null && source != null)
            {
                source.PlayOneShot(clip, volume);
            }
        }

        // Starts a clip after "delay" seconds on the second source. Only one waiting sound at a time:
        // a new one replaces the old (a retry during the record sting stops the sting).
        private void PlayLater(AudioClip clip, float delay)
        {
            if (clip == null || delayedSource == null)
            {
                Play(clip); // no second source in an old scene: play right away
                return;
            }
            delayedSource.Stop();
            delayedSource.clip = clip;
            delayedSource.volume = volume;
            delayedSource.PlayDelayed(delay);
        }

        // The music dips while this clip plays (hold = 60 % of the clip, then it comes back slowly).
        private void Duck(float db, AudioClip clip)
        {
            if (music != null && clip != null)
            {
                music.Duck(db, clip.length * 0.6f);
            }
        }
    }
}
