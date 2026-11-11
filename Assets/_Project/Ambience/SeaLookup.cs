using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // Plain rules for finding the sea in someone else's scene without touching it (Edit Mode tested).
    public static class SeaLookup
    {
        // True if 'objectName' is one of 'names' (exact, any upper/lower case). Null or empty = no match.
        public static bool NameMatches(string objectName, IList<string> names)
        {
            if (string.IsNullOrEmpty(objectName) || names == null)
            {
                return false;
            }
            for (int i = 0; i < names.Count; i++)
            {
                if (!string.IsNullOrEmpty(names[i]) && string.Equals(objectName, names[i], System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // First guess of "which way is the open sea" from the racing line, flat on the ground (y = 0):
        // if the land point is outside the sea's box, straight towards the box; if the sea is a big plane under the
        // whole world (Luc's is 8 km wide), towards the middle of the sea, which lies out at sea.
        // False = no usable direction (land point on the sea's middle, empty box or NaN).
        public static bool SeawardGuess(Bounds sea, Vector3 land, out Vector3 seaward)
        {
            seaward = Vector3.zero;
            if (float.IsNaN(land.x) || float.IsNaN(land.z) || float.IsNaN(sea.center.x) || sea.size.x <= 0f || sea.size.z <= 0f)
            {
                return false;
            }
            bool inside = land.x > sea.min.x && land.x < sea.max.x && land.z > sea.min.z && land.z < sea.max.z;
            Vector3 target;
            if (inside)
            {
                target = sea.center;
            }
            else
            {
                target = new Vector3(Mathf.Clamp(land.x, sea.min.x, sea.max.x), 0f, Mathf.Clamp(land.z, sea.min.z, sea.max.z));
            }
            Vector3 flat = new Vector3(target.x - land.x, 0f, target.z - land.z);
            if (flat.magnitude < 1f)
            {
                return false;
            }
            seaward = flat.normalized;
            return true;
        }

        // Average of the points (flat), for the land side. False if the list is empty.
        public static bool Centre(IList<Vector3> points, out Vector3 centre)
        {
            centre = Vector3.zero;
            if (points == null || points.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < points.Count; i++)
            {
                centre += points[i];
            }
            centre /= points.Count;
            return true;
        }
    }
}
