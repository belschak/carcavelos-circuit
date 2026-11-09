using System;

namespace CarcavelosCircuit.Track.Editor
{
    // Everything the Nova campus builder needs, read from NovaTrackData.json (made from OpenStreetMap + the campus photos).
    [Serializable]
    public class NovaTrackData
    {
        public float campusY;        // height of the campus plateau
        public float beachY;         // height of the promenade / beach side
        public float seaZ;           // the sea starts south of this line
        public NovaPoint[] marginal; // centre line of the Avenida Marginal (campus is north of it, beach south)
        public NovaPoint[] route;    // closed loop, driving order
        public int[] checkpoints;    // route indices, [0] = start/finish
        public int spawn;            // route index of the start grid
        public NovaBuilding[] buildings;
        public NovaZone[] zones;
        public NovaThing[] things;
    }
}
