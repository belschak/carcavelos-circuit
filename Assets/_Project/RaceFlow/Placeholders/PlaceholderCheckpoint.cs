using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // STAND-IN for Noah's Checkpoint component (Role 2, task T4) until it is merged.
    // Sits on a trigger collider. Same idea as the contract: an Index in driving order and a safe respawn pose.
    public class PlaceholderCheckpoint : MonoBehaviour
    {
        [SerializeField, Tooltip("0 = start/finish line, then 1, 2, 3 ... in driving order")]
        private int index;

        [SerializeField, Tooltip("Where the kart is put back when the player presses R after this checkpoint")]
        private Transform respawnPoint;

        public int Index
        {
            get { return index; }
        }

        public Transform RespawnPoint
        {
            get { return respawnPoint; }
        }
    }
}
