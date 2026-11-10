using UnityEngine;

namespace CarcavelosCircuit.Track
{
    // Marks the start grid. Its position and rotation (blue axis = forward) are where the kart stands before GO.
    // It only draws an arrow in the Scene view; TrackInfo holds the reference.
    public class SpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 3f));
            Gizmos.DrawLine(new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.5f, 3f));
        }
    }
}
