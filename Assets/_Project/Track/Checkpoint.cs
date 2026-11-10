using UnityEngine;

namespace CarcavelosCircuit.Track
{
    // One gate on the lap. Needs a collider with Is Trigger on this object or on a child:
    // RaceFlow listens to that trigger and tells RaceRules which checkpoint the kart crossed.
    // Index 0 = start/finish line, then 1, 2, 3 ... in driving order, each index used once.
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField, Tooltip("0 = start/finish line, then 1, 2, 3 ... in driving order")]
        private int index;

        [SerializeField, Tooltip("Safe pose where the kart is put back when the player presses R after this checkpoint")]
        private Transform respawnPoint;

        public int Index
        {
            get { return index; }
        }

        public Transform RespawnPoint
        {
            get { return respawnPoint; }
        }

        // Draws the gate in the Scene view so the order can be checked without playing.
        private void OnDrawGizmos()
        {
            Gizmos.color = index == 0 ? Color.green : Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(8f, 4f, 0.5f));
        }
    }
}
