namespace CarcavelosCircuit.Ambience
{
    // "How high is the ground here?" The game asks the physics colliders (PhysicsGroundProbe); tests use a made-up
    // ground, so the beach search can be tested without a scene.
    public interface IGroundProbe
    {
        // False = no ground found at this spot (open sea, or outside the world).
        bool TryGetHeight(float x, float z, out float height);
    }
}
