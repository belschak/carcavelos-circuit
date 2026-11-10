using System;

namespace CarcavelosCircuit.Track.Editor
{
    // One point of the Nova track data file (NovaTrackData.json). Metres: +X east, +Z north, Y = height.
    // w = road width at this point, s = section tag (e.g. "06_tunnel"); both only used for route points.
    [Serializable]
    public class NovaPoint
    {
        public float x;
        public float y;
        public float z;
        public float w;
        public string s;
    }
}
