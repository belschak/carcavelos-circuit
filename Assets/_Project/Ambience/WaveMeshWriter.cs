using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // Builds the points and colours of every wave into one mesh (one draw call for the whole surf).
    // Each wave = a strip along the beach: 'samples' cross-sections of WaveProfile.SectionPoints points each.
    // The triangles never change; every frame only the points and colours move (cheap enough for phones).
    // Colours are vertex colours with transparency: the foot of each wave fades into Luc's sea, so there is no seam.
    public class WaveMeshWriter
    {
        private readonly int slots;
        private readonly int samples;
        private readonly Color deep, face, crest, foam;   // linear colour space values (what the shader expects)

        public readonly Vector3[] Vertices;
        public readonly Color32[] Colours;
        public readonly int[] Indices;

        public WaveMeshWriter(int waveSlots, int beachSamples, Color deepWater, Color faceWater, Color crestWater, Color foamColour, bool linear)
        {
            slots = Mathf.Max(1, waveSlots);
            samples = Mathf.Max(2, beachSamples);
            deep = linear ? deepWater.linear : deepWater;
            face = linear ? faceWater.linear : faceWater;
            crest = linear ? crestWater.linear : crestWater;
            foam = linear ? foamColour.linear : foamColour;
            int k = WaveProfile.SectionPoints;
            Vertices = new Vector3[slots * samples * k];
            Colours = new Color32[Vertices.Length];
            Indices = new int[slots * (samples - 1) * (k - 1) * 6];
            // Painter's order for a camera on the beach looking out to sea (where the race camera always is):
            // per wave first the back, then the face from its foot up, the lip last. With see-through water that
            // keeps the near parts on top of the far parts.
            int n = 0;
            for (int s = 0; s < slots; s++)
            {
                for (int o = 0; o < StripOrder.Length; o++)
                {
                    int p = StripOrder[o];
                    for (int i = 0; i < samples - 1; i++)
                    {
                        int a = Index(s, i, p);
                        int b = Index(s, i + 1, p);
                        Indices[n++] = a; Indices[n++] = b; Indices[n++] = a + 1;
                        Indices[n++] = b; Indices[n++] = b + 1; Indices[n++] = a + 1;
                    }
                }
            }
        }

        // Strip p joins cross-section point p and p + 1 (see WaveProfile.Section).
        private static readonly int[] StripOrder = { 0, 1, 2, 7, 6, 5, 4, 3 };

        public int Slots { get { return slots; } }

        private int Index(int slot, int sample, int point)
        {
            return (slot * samples + sample) * WaveProfile.SectionPoints + point;
        }

        // Fills Vertices and Colours for the waves in 'field' on 'beach'. Unused slots are folded flat and invisible.
        public void Write(WaveField field, BeachLine beach)
        {
            if (field == null || beach == null)
            {
                return;
            }
            WaveTuning t = field.Tuning;
            int k = WaveProfile.SectionPoints;
            for (int s = 0; s < slots; s++)
            {
                bool used = s < field.Count;
                WaveState w = used ? field.Get(s) : new WaveState();
                for (int i = 0; i < samples; i++)
                {
                    int sample = beach.Samples == samples ? i : Mathf.RoundToInt((float)i / (samples - 1) * (beach.Samples - 1));
                    float along = beach.AlongOf(sample);
                    if (!used)
                    {
                        Vector3 flat = beach.Point(sample, 0f, -1f);
                        for (int p = 0; p < k; p++)
                        {
                            Vertices[Index(s, i, p)] = flat;
                            Colours[Index(s, i, p)] = new Color32(0, 0, 0, 0);
                        }
                        continue;
                    }
                    float fade = WaveProfile.EndFade(t, along) * beach.Open[sample];
                    if (w.Swash > 0f)
                    {
                        WriteSwash(t, w, beach, s, i, sample, along, fade);
                    }
                    else
                    {
                        WriteRolling(t, w, beach, s, i, sample, along, fade);
                    }
                }
            }
        }

        private void WriteRolling(WaveTuning t, WaveState w, BeachLine beach, int s, int i, int sample, float along, float fade)
        {
            float fromPeak = along - w.PeakAlong;
            float broken = WaveProfile.Broken(t, fromPeak, w.Distance);
            float curl = WaveProfile.Curl(t, fromPeak, w.Distance) * (1f - broken);
            float height = WaveProfile.UnbrokenHeight(t, w.Distance) * w.HeightScale * fade;
            float foamHeight = WaveProfile.FoamHeight(t, w.Distance) * Mathf.Lerp(0.8f, 1.1f, w.HeightScale - 0.6f) * fade;
            // Far out a swell is only a soft line on the sea; it gets clearer as it stands up.
            float clarity = Mathf.Lerp(0.35f, 1f, WaveProfile.Smooth(0.35f, 1.3f, height + foamHeight * broken));
            float lace = Lace(along, w.Id, w.Age);
            for (int p = 0; p < WaveProfile.SectionPoints; p++)
            {
                float offset, y, foamShare, alpha;
                WaveProfile.Section(p, height, foamHeight, broken, curl, out offset, out y, out foamShare, out alpha);
                Vertices[Index(s, i, p)] = beach.Point(sample, w.Distance - offset, y + 0.03f);
                Color water = WaterColour(p);
                Color c = Color.Lerp(water, foam, foamShare);
                // Foam is lacy (holes) at its back edge and wherever it is thin.
                float a = alpha * fade * clarity * Mathf.Lerp(1f, lace, foamShare * (p <= 2 || p >= 7 ? 0.9f : 0.25f));
                Colours[Index(s, i, p)] = ToColour32(c, a);
            }
        }

        private void WriteSwash(WaveTuning t, WaveState w, BeachLine beach, int s, int i, int sample, float along, float fade)
        {
            float front, back;
            WaveProfile.SwashEdges(t, w.Swash, out front, out back);
            float fadeOut = Mathf.Pow(1f - w.Swash, 1.3f) * fade;
            float lace = Lace(along * 1.3f, w.Id + 7, w.Age);
            int last = WaveProfile.SectionPoints - 1;
            for (int p = 0; p <= last; p++)
            {
                float u = (float)p / last;
                float distance = Mathf.Lerp(back, front, u);
                // Thin sheet: see-through in the middle, a bright lacy line at the front edge, soft at both ends.
                float alpha = p == 0 || p == last ? 0f : (p >= last - 2 ? 0.95f : 0.38f);
                float lacy = p >= last - 2 ? Mathf.Lerp(0.55f, 1f, lace) : lace;
                Vertices[Index(s, i, p)] = beach.Point(sample, distance, 0.04f);
                Colours[Index(s, i, p)] = ToColour32(Color.Lerp(face, foam, p >= last - 2 ? 1f : 0.75f), alpha * fadeOut * lacy);
            }
        }

        // Colour of each cross-section point of smooth water: the sea's colour at the foot, a dark steep face
        // (it faces away from the bright sky), light where the water is thin near the crest (sun shining through).
        // Dark face + white foam = the contrast that makes a wave readable from the racing line 80 m away.
        private Color WaterColour(int point)
        {
            switch (point)
            {
                case 0: case 1: case 8: return deep;
                case 2: return Color.Lerp(deep, crest, 0.35f);
                case 3: return crest;
                case 4: return Color.Lerp(crest, foam, 0.25f);
                case 5: return Color.Lerp(face, crest, 0.7f);
                case 6: return face;
                default: return Color.Lerp(face, deep, 0.4f);
            }
        }

        // 0.35..1: a cheap moving pattern that breaks foam into lace, different for every wave.
        public static float Lace(float along, int id, float age)
        {
            float a = Mathf.Sin(along * 0.83f + id * 1.7f + age * 0.6f);
            float b = Mathf.Sin(along * 0.29f - id * 2.3f - age * 0.35f);
            float v = 0.5f + 0.25f * a + 0.25f * b;
            return 0.35f + 0.65f * Mathf.Clamp01(v);
        }

        private static Color32 ToColour32(Color c, float alpha)
        {
            float a = float.IsNaN(alpha) ? 0f : Mathf.Clamp01(alpha);
            return new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(a * 255f));
        }
    }
}
