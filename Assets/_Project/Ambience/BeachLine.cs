using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // The measured beach in world space: a straight base line along the coast plus, at every sample, how far the real
    // waterline is from it (the coast is not straight), how steep the sand is and whether the sea there is open.
    // Turns beach coordinates (along, distance from the waterline, height above the sea) into world points.
    public class BeachLine
    {
        public Vector3 Origin;      // middle of the beach, on the waterline, at sea level
        public Vector3 Along;       // unit, flat: along the coast
        public Vector3 Seaward;     // unit, flat: out to sea
        public float SeaLevel;
        public float Length;        // metres from the first to the last sample
        public float[] Waterline;   // metres seaward of the base line where the water meets the sand, per sample
        public float[] WaterlineFar; // the same, smoothed over about 25 m: far out the waves ignore small bumps of the coast
        public float[] Slope;       // rise of the sand per metre inland, per sample (used where Sand was not measured)
        public float[] Sand;        // measured sand height above the sea, SandSteps values per sample: 0, 1, 2 ... m inland
        public bool SandMeasured;   // false = Sand is empty, the slope is used instead
        public float[] Open;        // 1 = open sea in front of this sample, 0 = rock, pier or fort (no waves)

        // Metres of sand measured inland of the waterline (one height per metre): as far as foam ever runs up.
        public const int SandSteps = 14;

        public BeachLine(int samples)
        {
            int n = samples < 2 ? 2 : samples;
            Waterline = new float[n];
            WaterlineFar = new float[n];
            Slope = new float[n];
            Sand = new float[n * SandSteps];
            Open = new float[n];
        }

        public int Samples { get { return Waterline.Length; } }

        // Metres along the beach of sample i (0 = middle).
        public float AlongOf(int i)
        {
            return -Length * 0.5f + Length * i / (Samples - 1);
        }

        // World point of sample i at 'distance' metres from its waterline and 'height' metres above the water.
        // Up the sand (distance < 0) the point follows the slope of the beach.
        public Vector3 Point(int i, float distance, float height)
        {
            int k = Mathf.Clamp(i, 0, Samples - 1);
            Vector3 p = Origin + Along * AlongOf(k) + Seaward * (WaterlineFor(Waterline[k], WaterlineFar[k], distance) + distance);
            p.y = SeaLevel + height + (distance < 0f ? SandHeight(k, -distance) : 0f);
            return p;
        }

        // Same for any spot along the beach, blended between the two nearest samples.
        public Vector3 PointAt(float along, float distance, float height)
        {
            float index = (along + Length * 0.5f) / Length * (Samples - 1);
            if (float.IsNaN(index))
            {
                index = 0f;
            }
            index = Mathf.Clamp(index, 0f, Samples - 1);
            int a = Mathf.FloorToInt(index);
            int b = Mathf.Min(a + 1, Samples - 1);
            float t = index - a;
            float waterline = WaterlineFor(Mathf.Lerp(Waterline[a], Waterline[b], t), Mathf.Lerp(WaterlineFar[a], WaterlineFar[b], t), distance);
            Vector3 p = Origin + Along * along + Seaward * (waterline + distance);
            float sand = distance < 0f ? Mathf.Lerp(SandHeight(a, -distance), SandHeight(b, -distance), t) : 0f;
            p.y = SeaLevel + height + sand;
            return p;
        }

        // Height of the sand above the sea, 'inland' metres up the beach from sample i's waterline. Foam that runs
        // up the beach lies on this, so it neither sinks into the sand nor floats above it.
        public float SandHeight(int i, float inland)
        {
            int k = Mathf.Clamp(i, 0, Samples - 1);
            if (float.IsNaN(inland) || inland <= 0f)
            {
                return 0f;
            }
            if (!SandMeasured)
            {
                return Slope[k] * inland;
            }
            int last = SandSteps - 1;
            if (inland >= last)
            {
                // Beyond the measured part: carry on at the slope of the last metre.
                float lastRise = Sand[k * SandSteps + last] - Sand[k * SandSteps + last - 1];
                return Sand[k * SandSteps + last] + Mathf.Max(0f, lastRise) * (inland - last);
            }
            int step = Mathf.FloorToInt(inland);
            return Mathf.Lerp(Sand[k * SandSteps + step], Sand[k * SandSteps + step + 1], inland - step);
        }

        // Near the sand a wave follows the real waterline; further out it follows the smoothed coast, so the swell
        // stays a calm line and only bends to the beach as it arrives (like real waves do).
        public static float WaterlineFor(float near, float far, float distance)
        {
            return Mathf.Lerp(near, far, WaveProfile.Smooth(2f, 14f, distance));
        }

        // Fills WaterlineFar from 'source' (the waterline without headlands): average over 'radius' samples to
        // each side. source == null = from Waterline.
        public void SmoothFar(float[] source, int radius)
        {
            if (source == null || source.Length != Samples)
            {
                source = Waterline;
            }
            int n = Samples;
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                int count = 0;
                for (int k = i - radius; k <= i + radius; k++)
                {
                    if (k >= 0 && k < n)
                    {
                        sum += source[k];
                        count++;
                    }
                }
                WaterlineFar[i] = sum / count;
            }
        }

        // 0..1 how open the sea is at this spot (blended).
        public float OpenAt(float along)
        {
            float index = Mathf.Clamp((along + Length * 0.5f) / Length * (Samples - 1), 0f, Samples - 1);
            if (float.IsNaN(index))
            {
                return 0f;
            }
            int a = Mathf.FloorToInt(index);
            int b = Mathf.Min(a + 1, Samples - 1);
            return Mathf.Lerp(Open[a], Open[b], index - a);
        }

        // Board rotation for a heading in the beach frame (0 = facing the beach, PI/2 = facing +Along).
        public Quaternion Rotation(float heading)
        {
            Vector3 forward = Along * Mathf.Sin(heading) - Seaward * Mathf.Cos(heading);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = -Seaward;
            }
            return Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
