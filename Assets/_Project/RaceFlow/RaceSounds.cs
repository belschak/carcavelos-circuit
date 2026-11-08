using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R5 (race part): countdown beeps, checkpoint chime, finish jingle, new-record sting.
    // It listens to the RaceManager's events. The sound files live in Audio/Clips.
    // Kart sounds (engine, drift, boost) are in the Audio module (KartAudio), because they only read IKartState.
    public class RaceSounds : MonoBehaviour
    {
        [SerializeField]
        private RaceManager raceManager;

        [SerializeField, Tooltip("A 2D AudioSource (Spatial Blend 0) for interface sounds")]
        private AudioSource source;

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

        [SerializeField, Range(0f, 1f)]
        private float volume = 0.8f;

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

        private void OnCountdownStep(int number)
        {
            Play(number > 0 ? countdownBeep : goBeep);
        }

        private void OnCheckpointPassed(int index)
        {
            Play(checkpointChime);
        }

        private void OnLapFinished(float lapSeconds, bool newRecord)
        {
            Play(finishJingle);
            if (newRecord)
            {
                Play(newRecordSting);
            }
        }

        // PlayOneShot lets several sounds overlap on one AudioSource. A missing clip is simply skipped.
        private void Play(AudioClip clip)
        {
            if (clip != null && source != null)
            {
                source.PlayOneShot(clip, volume);
            }
        }
    }
}
