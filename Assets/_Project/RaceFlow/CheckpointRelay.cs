using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // RaceManager adds one of these to every checkpoint trigger collider when the race is set up
    // (on the object that owns the collider, because only that object gets OnTriggerEnter).
    // It only tells the RaceManager "the kart touched checkpoint N". The rules decide if that counts.
    // This way the Track module does not need to know anything about RaceFlow.
    public class CheckpointRelay : MonoBehaviour
    {
        private RaceManager raceManager;
        private int checkpointIndex;

        public void SetUp(RaceManager manager, int index)
        {
            raceManager = manager;
            checkpointIndex = index;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Only the kart counts, not a ball or a prop. The RaceManager knows the kart's colliders and Rigidbodies.
            if (raceManager != null && raceManager.IsKartCollider(other))
            {
                raceManager.OnCheckpointEntered(checkpointIndex);
            }
        }
    }
}
