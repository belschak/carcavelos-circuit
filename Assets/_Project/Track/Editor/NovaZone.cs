using System;

namespace CarcavelosCircuit.Track.Editor
{
    // A flat ground area drawn on top of the grass: "concrete", "calcada", "plaza", "parking", "sand", "road", "terrace".
    [Serializable]
    public class NovaZone
    {
        public string kind;
        public float y;
        public NovaPoint[] pts;
    }
}
