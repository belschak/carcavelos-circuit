using System.IO;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // T7: lines the route up with the doors of the Nova hall. On the last metres before each door the route runs
    // straight along the door's axis, as wide as the door minus the fence gap (so the fence lands on the door frame),
    // and one route point sits exactly on the facade (so the road can end there with a clean edge).
    // The doors come from the hall generator (HallDoors.json in the hall test folder). Without that file nothing changes.
    // Used by WhiteGuardrailBuilder (the fence) and by the hall test scene builder (new road pieces at the doors).
    public static class HallDoorApproach
    {
        public const string DoorsPath = "Assets/_Preview/HallDoors.json";
        private const float Reach = 14f;      // metres in front of the door where the route starts to line up
        private const float Full = 3f;        // ... fully lined up from this close to the door
        private const float FenceGap = 0.3f;  // same as WhiteGuardrailBuilder.EdgeOffset: fence this far outside the road
        private const int Run = 10;           // route points on each side of the door that may be moved

        [System.Serializable]
        private class Door
        {
            public string name;
            public float x, z, nx, nz, width; // centre on the facade, normal into the building, opening width
        }

        [System.Serializable]
        private class DoorList
        {
            public Door[] doors = new Door[0];
        }

        public static bool HasDoors()
        {
            return File.Exists(DoorsPath);
        }

        // Moves route centres and widths in place (only outside the building). Points just behind a door, inside the
        // building, are marked in inHall (may be null): the hall's own fence is there. Returns how many points moved.
        public static int Apply(Vector3[] centre, float[] width, bool[] inHall)
        {
            return Apply(centre, width, inHall, null);
        }

        // Same, and also lists the route point that now sits on the facade of each door (where a new road piece ends).
        public static int Apply(Vector3[] centre, float[] width, bool[] inHall, System.Collections.Generic.List<int> onFacadeList)
        {
            if (!HasDoors())
            {
                return 0;
            }
            Door[] doors = JsonUtility.FromJson<DoorList>(File.ReadAllText(DoorsPath)).doors;
            int n = centre.Length;
            int moved = 0;
            foreach (Door d in doors)
            {
                Vector3 c = new Vector3(d.x, 0f, d.z);
                Vector3 inward = new Vector3(d.nx, 0f, d.nz);
                Vector3 along = new Vector3(d.nz, 0f, -d.nx); // along the facade

                // The route point closest to the door is where the route goes through it.
                int door = 0;
                for (int i = 1; i < n; i++)
                {
                    if (Flat(centre[i] - c).magnitude < Flat(centre[door] - c).magnitude)
                    {
                        door = i;
                    }
                }
                if (Flat(centre[door] - c).magnitude > 6f)
                {
                    Debug.LogWarning("HallDoorApproach: the route does not pass door " + d.name);
                    continue;
                }

                // Pull the nearby points onto the door's axis and to the door's width.
                int onFacade = door;
                for (int k = -Run; k <= Run; k++)
                {
                    int i = (door + k + n) % n;
                    Vector3 rel = Flat(centre[i] - c);
                    float depth = Vector3.Dot(rel, inward);      // minus = outside the building
                    if (depth > 0.05f)
                    {
                        if (inHall != null)
                        {
                            inHall[i] = true;
                        }
                        continue;                                // inside: the hall floor and the hall fence take over
                    }
                    float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-Reach, -Full, depth));
                    // ... and fades out along the route too, so a sharp turn right after the door leaves no kink
                    weight *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Run, Run / 2, Mathf.Abs(k)));
                    centre[i] -= along * (Vector3.Dot(rel, along) * weight);
                    width[i] = Mathf.Lerp(width[i], d.width - 2f * FenceGap, weight);
                    if (Mathf.Abs(depth) < Mathf.Abs(Vector3.Dot(Flat(centre[onFacade] - c), inward)))
                    {
                        onFacade = i;
                    }
                    moved++;
                }
                // One point exactly on the facade line, in the middle of the door.
                centre[onFacade] = new Vector3(d.x, centre[onFacade].y, d.z);
                if (onFacadeList != null)
                {
                    onFacadeList.Add(onFacade);
                }
            }
            return moved;
        }

        private static Vector3 Forward(Vector3[] c, int i)
        {
            int n = c.Length;
            Vector3 d = Flat(c[(i + 1) % n] - c[(i - 1 + n) % n]);
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
        }

        private static Vector3 Flat(Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }
    }
}
