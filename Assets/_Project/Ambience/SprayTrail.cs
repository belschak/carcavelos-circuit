using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // The short white spray a surfboard throws up behind it while riding. It remembers the last few places of the
    // board's tail; each one rises, spreads and fades within a second. Drawn as one small ribbon in the foam mesh
    // (no particle system, no extra draw call). Plain C#, Edit Mode tested.
    public class SprayTrail
    {
        public const int Points = 7;           // the live point at the tail + 6 remembered ones

        private const float Life = 0.8f;        // seconds until a bit of spray is gone
        private const float Spacing = 0.1f;     // seconds between two remembered points

        private readonly Vector3[] place = new Vector3[Points - 1];
        private readonly Vector3[] sideways = new Vector3[Points - 1];
        private readonly float[] born = new float[Points - 1];
        private int count;
        private int newest = -1;
        private float lastDrop = float.NegativeInfinity;
        private bool riding;
        private Vector3 headPlace;
        private Vector3 headSideways;

        // How many remembered points are still in the air (for tests).
        public int Count { get { return count; } }

        // Call every frame while the surfer rides: tail = where the board's tail is now, across = the board's
        // right-hand direction, time = the game clock.
        public void Ride(Vector3 tail, Vector3 across, float time)
        {
            if (float.IsNaN(time) || float.IsNaN(tail.x) || float.IsNaN(tail.y) || float.IsNaN(tail.z))
            {
                return;
            }
            if (time < lastDrop)
            {
                Clear();                         // the clock went back (retry): forget the old spray
            }
            riding = true;
            headPlace = tail;
            headSideways = across;
            if (time - lastDrop >= Spacing)
            {
                newest = (newest + 1) % place.Length;
                place[newest] = tail;
                sideways[newest] = across;
                born[newest] = time;
                count = Mathf.Min(count + 1, place.Length);
                lastDrop = time;
            }
        }

        // Call every frame while the surfer does NOT ride: the spray left behind still fades out, no new spray.
        public void Rest()
        {
            riding = false;
        }

        public void Clear()
        {
            count = 0;
            newest = -1;
            lastDrop = float.NegativeInfinity;
            riding = false;
        }

        // Point k of the ribbon (0 = at the tail, then older and older). lower = on the water, upper = the top of
        // the spray, alpha = 0..1 how white. False = no such point (draw nothing).
        public bool TryGet(int k, float time, out Vector3 lower, out Vector3 upper, out float alpha)
        {
            lower = headPlace;
            upper = headPlace;
            alpha = 0f;
            if (k < 0 || k >= Points || float.IsNaN(time))
            {
                return false;
            }
            if (k == 0)
            {
                if (!riding)
                {
                    return false;
                }
                upper = headPlace + Vector3.up * 0.2f;
                alpha = 0.85f;
                return true;
            }
            int past = k - 1;
            if (past >= count)
            {
                return false;
            }
            int index = (newest - past + place.Length * 2) % place.Length;
            float age = (time - born[index]) / Life;
            if (age < 0f || age >= 1f)
            {
                return false;
            }
            // Rises quickly, hangs, drops; drifts a little sideways; fades.
            float height = 0.2f + 0.9f * Mathf.Sin(age * Mathf.PI) * (1f - 0.3f * age);
            lower = place[index];
            upper = place[index] + Vector3.up * height + sideways[index] * (0.25f * age);
            alpha = 0.85f * (1f - age) * (1f - age);
            return true;
        }
    }
}
