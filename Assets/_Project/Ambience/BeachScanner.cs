using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // Finds the beach next to the racing line by walking from the track towards the sea and checking the ground
    // height (IGroundProbe) until it drops under the sea level: there the water meets the sand.
    // Why this way: Luc's sea is one flat 8 km plane under the whole world, so its box alone cannot say where the
    // coast is; the ground can. The sea's box only gives the first guess of the direction. Plain C#, tested with a
    // made-up ground.
    public class BeachScanner
    {
        private readonly IGroundProbe ground;
        private readonly float step;
        private readonly float maxDistance;

        public BeachScanner(IGroundProbe ground, float step, float maxDistance)
        {
            this.ground = ground;
            this.step = float.IsNaN(step) || step < 0.25f ? 0.25f : step;
            this.maxDistance = float.IsNaN(maxDistance) || maxDistance < this.step ? this.step : maxDistance;
        }

        // Why the last Scan returned null (for the one log line), "" after a success.
        public string Problem { get; private set; }

        // The beach closest to the anchors (points on the racing line), or null if there is none within reach.
        // waveReach = how far out to sea the waves start (to check that the sea in front is really open).
        public BeachLine Scan(Bounds sea, float seaLevel, IList<Vector3> anchors, float length, int samples, float waveReach)
        {
            Problem = "";
            if (ground == null)
            {
                Problem = "no ground probe";
                return null;
            }
            Vector3 centre;
            if (!SeaLookup.Centre(anchors, out centre))
            {
                Problem = "no racing-line objects found";
                return null;
            }
            Vector3 seaward;
            if (!SeaLookup.SeawardGuess(sea, centre, out seaward))
            {
                Problem = "cannot tell which way the sea is";
                return null;
            }

            // 1. The anchor with the shortest walk to the water decides which beach.
            float best = float.MaxValue;
            Vector3 waterPoint = Vector3.zero;
            for (int i = 0; i < anchors.Count; i++)
            {
                float d;
                if (WalkToWater(anchors[i], seaward, seaLevel, maxDistance, out d) && d < best)
                {
                    best = d;
                    waterPoint = anchors[i] + seaward * d;
                }
            }
            if (best == float.MaxValue)
            {
                Problem = "no water within " + maxDistance + " m of the racing line";
                return null;
            }
            waterPoint.y = seaLevel;

            // 2. Turn the frame so 'along' follows the coast: measure the waterline a quarter beach to each side.
            Vector3 along = new Vector3(seaward.z, 0f, -seaward.x);
            float quarter = Mathf.Max(5f, length * 0.25f);
            float left, right;
            if (Crossing(waterPoint - along * quarter, seaward, seaLevel, out left) &&
                Crossing(waterPoint + along * quarter, seaward, seaLevel, out right))
            {
                Vector3 coast = along * (2f * quarter) + seaward * (right - left);
                coast.y = 0f;
                if (coast.sqrMagnitude > 1f)
                {
                    along = coast.normalized;
                    seaward = new Vector3(-along.z, 0f, along.x);
                }
            }

            // 3. Measure every sample along the coast.
            var beach = new BeachLine(samples);
            beach.Origin = waterPoint;
            beach.Along = along;
            beach.Seaward = seaward;
            beach.SeaLevel = seaLevel;
            beach.Length = Mathf.Max(1f, length);
            int found = 0;
            for (int i = 0; i < beach.Samples; i++)
            {
                Vector3 basePoint = waterPoint + along * beach.AlongOf(i);
                float c;
                if (Crossing(basePoint, seaward, seaLevel, out c))
                {
                    beach.Waterline[i] = c;
                    beach.Open[i] = IsOpenSea(basePoint + seaward * c, seaward, seaLevel, waveReach) ? 1f : 0f;
                    beach.Slope[i] = SandSlope(basePoint + seaward * c, seaward, seaLevel);
                    found++;
                }
                else
                {
                    beach.Waterline[i] = float.NaN;    // filled from the neighbours below
                    beach.Open[i] = 0f;
                    beach.Slope[i] = 0.05f;
                }
            }
            if (found < 2)
            {
                Problem = "the coast next to the racing line could not be measured";
                return null;
            }
            float open = 0f;
            for (int i = 0; i < beach.Samples; i++)
            {
                open += beach.Open[i];
            }
            if (open < beach.Samples * 0.6f)
            {
                Problem = "the sea in front of the beach is not open (rocks, pier or land)";
                return null;
            }
            FillGaps(beach.Waterline);
            Smooth(beach.Waterline);
            beach.SmoothFar(Mathf.Max(1, Mathf.RoundToInt(30f / Mathf.Max(0.5f, beach.Length / (beach.Samples - 1)))));  // about 30 m each side
            Smooth(beach.Open);
            return beach;
        }

        // Walks from 'start' (on land) in 'direction' until the ground dips under the sea level and stays there:
        // that is the waterline. A spot without ground (a hole in the ground mesh, e.g. where a tunnel is cut in)
        // does not count as water; only 20 m in a row without ground (the edge of the world, open sea) does.
        // False = the walk started in the water, or no water within 'reach'.
        public bool WalkToWater(Vector3 start, Vector3 direction, float seaLevel, float reach, out float distance)
        {
            distance = 0f;
            float lastLand = -1f;
            float lastLandHeight = 0f;
            float gapStart = -1f;
            for (float d = 0f; d <= reach; d += step)
            {
                float h;
                bool hit = HeightAt(start, direction, d, out h);
                if (hit && h >= seaLevel)
                {
                    lastLand = d;
                    lastLandHeight = h;
                    gapStart = -1f;
                    continue;
                }
                if (lastLand < 0f)
                {
                    if (hit)
                    {
                        return false;                   // started in the water: not a beach
                    }
                    continue;                           // no ground yet (a hole): keep looking for land
                }
                if (!hit)
                {
                    if (gapStart < 0f)
                    {
                        gapStart = d;
                    }
                    if (d - gapStart >= 20f)
                    {
                        distance = lastLand + step * 0.5f;
                        return true;
                    }
                    continue;
                }
                if (!StaysWet(start, direction, d, seaLevel))
                {
                    continue;                           // a dip (pool, ditch), not the sea
                }
                // Between the last dry point and this wet one: where does the ground cross the sea level?
                float t = 0.5f;
                if (lastLandHeight - h > 0.0001f)
                {
                    t = (lastLandHeight - seaLevel) / (lastLandHeight - h);
                }
                distance = Mathf.Lerp(lastLand, d, Mathf.Clamp01(t));
                return true;
            }
            return false;
        }

        private bool HeightAt(Vector3 start, Vector3 direction, float d, out float height)
        {
            return ground.TryGetHeight(start.x + direction.x * d, start.z + direction.z * d, out height);
        }

        // The next three steps after a wet spot must not be dry land again.
        private bool StaysWet(Vector3 start, Vector3 direction, float d, float seaLevel)
        {
            for (int k = 1; k <= 3; k++)
            {
                float h;
                if (HeightAt(start, direction, d + k * step, out h) && h >= seaLevel)
                {
                    return false;
                }
            }
            return true;
        }

        // Waterline position near a point on the base line: search from 40 m inland to 80 m out to sea.
        private bool Crossing(Vector3 basePoint, Vector3 seaward, float seaLevel, out float distance)
        {
            const float inland = 40f;
            float d;
            if (WalkToWater(basePoint - seaward * inland, seaward, seaLevel, inland + 80f, out d))
            {
                distance = d - inland;
                return true;
            }
            distance = 0f;
            return false;
        }

        // Open sea in front = still water at half and at the full wave reach (no rocks, pier or fort).
        private bool IsOpenSea(Vector3 waterline, Vector3 seaward, float seaLevel, float reach)
        {
            float r = Mathf.Max(step, reach);
            for (float d = r * 0.25f; d <= r + 0.01f; d += r * 0.25f)
            {
                float h;
                Vector3 p = waterline + seaward * d;
                if (ground.TryGetHeight(p.x, p.z, out h) && h > seaLevel - 0.2f)
                {
                    return false;
                }
            }
            return true;
        }

        // Rise of the sand per metre over the first 6 m inland (for the foam that runs up the beach).
        private float SandSlope(Vector3 waterline, Vector3 seaward, float seaLevel)
        {
            float h;
            Vector3 p = waterline - seaward * 6f;
            if (ground.TryGetHeight(p.x, p.z, out h))
            {
                return Mathf.Clamp((h - seaLevel) / 6f, 0.01f, 0.5f);
            }
            return 0.05f;
        }

        // A sample without a measured waterline takes the nearest measured one.
        public static void FillGaps(float[] values)
        {
            int n = values.Length;
            for (int i = 0; i < n; i++)
            {
                if (!float.IsNaN(values[i]))
                {
                    continue;
                }
                float fill = 0f;
                for (int k = 1; k < n; k++)
                {
                    if (i - k >= 0 && !float.IsNaN(values[i - k]))
                    {
                        fill = values[i - k];
                        break;
                    }
                    if (i + k < n && !float.IsNaN(values[i + k]))
                    {
                        fill = values[i + k];
                        break;
                    }
                }
                values[i] = fill;
            }
        }

        // Three-point average, so one odd ground triangle does not put a kink in the waves.
        public static void Smooth(float[] values)
        {
            int n = values.Length;
            if (n < 3)
            {
                return;
            }
            float previous = values[0];
            for (int i = 1; i < n - 1; i++)
            {
                float current = values[i];
                values[i] = (previous + current + values[i + 1]) / 3f;
                previous = current;
            }
        }
    }
}
