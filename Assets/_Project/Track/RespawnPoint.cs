using UnityEngine;

namespace CarcavelosCircuit.Track
{
    // Marks the safe pose after one checkpoint (child of that Checkpoint). Used when the player presses R.
    // It only draws an arrow in the Scene view; the Checkpoint holds the reference.
    public class RespawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 3f));
            Gizmos.DrawLine(new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.5f, 3f));
        }
    }
}
