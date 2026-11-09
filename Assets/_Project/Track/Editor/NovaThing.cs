using System;

namespace CarcavelosCircuit.Track.Editor
{
    // A single prop: kind = "pine", "olive", "palm", "shrub", "lamp", "bollard", "lounge", "parasol", "car", "bench".
    [Serializable]
    public class NovaThing
    {
        public string kind;
        public float x;
        public float y;
        public float z;
        public float yaw;
        public float size;
    }
}
