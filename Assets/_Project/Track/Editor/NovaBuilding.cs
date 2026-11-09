using System;

namespace CarcavelosCircuit.Track.Editor
{
    // A building footprint from OpenStreetMap, extruded by the builder.
    // kind: "wing" (white with glass bands), "plain" (white, few windows), "auditorium" (leaning wedge),
    // "hall" (Cascais Academic Hall cube), "entrance" (main entrance with red stairs), "other".
    [Serializable]
    public class NovaBuilding
    {
        public string name;
        public string kind;
        public float baseY;
        public float height;
        public NovaPoint[] pts;
    }
}
