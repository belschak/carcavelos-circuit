using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // STAND-IN for Noah's TrackInfo (Role 2, task T4) until it is merged.
    // Same public members as the TrackInfo contract: Checkpoints, SpawnPoint, GetRespawnPoint.
    public class PlaceholderTrack : MonoBehaviour
    {
        [SerializeField, Tooltip("All checkpoints, index 0 = start/finish line")]
        private PlaceholderCheckpoint[] checkpoints;

        [SerializeField, Tooltip("Start grid: where the kart stands before GO")]
        private Transform spawnPoint;

        public PlaceholderCheckpoint[] Checkpoints
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
            for (int i = 0; i < checkpoints.Length; i++)
            {
                if (checkpoints[i] != null && checkpoints[i].Index == lastCheckpointIndex && checkpoints[i].RespawnPoint != null)
                {
                    return checkpoints[i].RespawnPoint;
                }
            }
            return spawnPoint;
        }
    }
}
