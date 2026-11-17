using System;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Test flags for the item boxes in the page's address (package C, 17.11.2026). Plain C#, Edit Mode tested.
    //   ?test=itemrows  the camera visits every row and wave box after GO (ItemRowTour) and every box is logged
    //   ?test=itemlog   only the Console lines: where each box stands, every box hit and every item used
    // Several values may share one flag: ?test=itemrows,other. Unknown values are ignored. Nothing changes without one.
    public struct ItemTestFlags
    {
        public bool Tour; // fly the camera over every row
        public bool Log;  // one Console line per box, per hit and per use

        public static ItemTestFlags Parse(string url)
        {
            ItemTestFlags flags = new ItemTestFlags();
            if (string.IsNullOrEmpty(url))
            {
                return flags;
            }
            int hash = url.IndexOf('#');
            if (hash >= 0)
            {
                url = url.Substring(0, hash); // the fragment is not part of the query
            }
            int query = url.IndexOf('?');
            if (query < 0)
            {
                return flags;
            }
            string rest = url.Substring(query + 1);
            string[] pairs = rest.Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                int equals = pairs[i].IndexOf('=');
                if (equals <= 0 || !string.Equals(pairs[i].Substring(0, equals), "test", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string[] values = Uri.UnescapeDataString(pairs[i].Substring(equals + 1)).Split(',');
                for (int v = 0; v < values.Length; v++)
                {
                    string value = values[v].Trim().ToLowerInvariant();
                    if (value == "itemrows")
                    {
                        flags.Tour = true;
                        flags.Log = true;
                    }
                    else if (value == "itemlog")
                    {
                        flags.Log = true;
                    }
                }
            }
            return flags;
        }
    }

    // One row of boxes (or one wave box) as ItemSystem planned it: for the log and the camera tour.
    public struct ItemStop
    {
        public readonly string Label;
        public readonly Vector3 Centre;   // middle of the row on the road
        public readonly Vector3 Forward;  // driving direction there (flat)
        public readonly int Placed;       // boxes that found a place
        public readonly int Planned;      // boxes the row should have

        public ItemStop(string label, Vector3 centre, Vector3 forward, int placed, int planned)
        {
            Label = label;
            Centre = centre;
            Forward = forward;
            Placed = placed;
            Planned = planned;
        }

        // Where the tour camera stands for this stop: behind the row and a little up, low enough for the tunnel.
        public static Vector3 CameraSpot(Vector3 centre, Vector3 forward, float back, float up)
        {
            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
            return centre - flat * back + Vector3.up * up;
        }
    }
}
