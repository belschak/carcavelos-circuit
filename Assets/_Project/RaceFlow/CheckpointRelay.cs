using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // RaceManager adds one of these to every checkpoint trigger when the race is set up.
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
            // Only the kart counts (it carries IKartState somewhere on its root), not a ball or a prop.
            if (raceManager != null && other.GetComponentInParent<IKartState>() != null)
            {
                raceManager.OnCheckpointEntered(checkpointIndex);
            }
        }
    }
}
