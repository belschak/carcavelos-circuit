using UnityEngine;

namespace CarcavelosCircuit.Track
{
    // Sits on the root of the track scene. RaceFlow reads everything it needs about the track from here.
    public class TrackInfo : MonoBehaviour
    {
        [SerializeField, Tooltip("All checkpoints, element i must have Index i (0 = start/finish line)")]
        private Checkpoint[] checkpoints;

        [SerializeField, Tooltip("Start grid: where the kart stands before GO")]
        private Transform spawnPoint;

        public Checkpoint[] Checkpoints
        {
            get { return checkpoints; }
        }

        public Transform SpawnPoint
        {
            get { return spawnPoint; }
        }

        // Safe pose after the given checkpoint. Falls back to the start grid if something is missing.
        public Transform GetRespawnPoint(int lastCheckpointIndex)
        {
            if (checkpoints != null)
            {
                for (int i = 0; i < checkpoints.Length; i++)
                {
                    if (checkpoints[i] != null && checkpoints[i].Index == lastCheckpointIndex
                        && checkpoints[i].RespawnPoint != null)
                    {
                        return checkpoints[i].RespawnPoint;
                    }
                }
            }
            return spawnPoint;
        }
    }
}
