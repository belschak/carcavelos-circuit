using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // Ground height from the track scene's colliders (Luc's ground and road meshes have MeshColliders): one ray
    // straight down. Read only: nothing in the track scene is changed. Trigger colliders (checkpoints) are ignored.
    public class PhysicsGroundProbe : IGroundProbe
    {
        private readonly float top;
        private readonly float depth;

        public PhysicsGroundProbe(float rayStartHeight, float rayLength)
        {
            top = rayStartHeight;
            depth = rayLength;
        }

        public bool TryGetHeight(float x, float z, out float height)
        {
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(x, top, z), Vector3.down, out hit, depth, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                height = hit.point.y;
                return true;
            }
            height = 0f;
            return false;
        }
    }
}
