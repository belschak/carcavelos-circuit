using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Records every lap the player drives (kart position + rotation, 20 times per second). When the lap is a new
    // personal best, the recording is saved in PlayerPrefs next to the best time; GhostPlayer replays it next run.
    // Thin glue: the recording itself is the plain C# class GhostRecording.
    public class GhostRecorder : MonoBehaviour
    {
        [SerializeField]
        private RaceManager raceManager;

        private GhostRecording recording;
        private bool wasRacing;

        private void OnEnable()
        {
            raceManager.LapFinished += OnLapFinished;
        }

        private void OnDisable()
        {
            raceManager.LapFinished -= OnLapFinished;
        }

        // LateUpdate: after the kart has moved this frame, so we store the pose the player actually sees.
        private void LateUpdate()
        {
            if (!raceManager.IsSetUp || raceManager.KartTransform == null)
            {
                return;
            }
            if (recording == null)
            {
                recording = new GhostRecording(raceManager.TrackName, raceManager.CheckpointCount, raceManager.LayoutStamp);
            }

            // A new attempt starts on GO (also after a retry): throw the old recording away.
            bool racing = raceManager.State == RaceState.Racing;
            if (racing && !wasRacing)
            {
                recording.Clear();
            }
            wasRacing = racing;

            if (racing)
            {
                Transform kart = raceManager.KartTransform;
                recording.Record(raceManager.LapSeconds, kart.position, kart.rotation);
            }
        }

        // The RaceManager has already saved the new best time when this event comes; the ghost goes right next to it.
        private void OnLapFinished(float lapSeconds, bool newRecord)
        {
            if (!newRecord || recording == null)
            {
                return;
            }
            string key = GhostLoader.SaveKey(raceManager.TrackName);
            if (recording.IsFull || recording.Count < 2)
            {
                // Lap too long to keep (save size). Delete an older ghost, so the ghost never belongs to a slower lap
                // than the best time on screen; the shipped Dean's ghost drives instead.
                PlayerPrefs.DeleteKey(key);
            }
            else
            {
                recording.LapSeconds = lapSeconds;
                PlayerPrefs.SetString(key, recording.ToSaveString());
            }
            PlayerPrefs.Save(); // in a Web build this writes to the browser right away
        }
    }
}
