using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // Builds the points and colours of every wave (one draw call for the water, one for the foam lace on top).
    // Each wave = a strip along the beach: 'samples' cross-sections of WaveProfile.SectionPoints points each.
    // The triangles never change; every frame only the points and colours move (cheap enough for phones).
    // Two layers share the same points and triangles:
    //  - water: vertex colours with transparency, the foot of each wave fades into Luc's sea (no seam); foam is only
    //    a soft milky white here;
    //  - foam: white with the lacy FoamPattern picture, strong where the wave has broken. Soft layer + crisp lace on
    //    top is what makes foam look like foam instead of a white strip.
    public class WaveMeshWriter
    {
        private const float Lift = 0.06f;        // metres above the sea plane, so the two never flicker ("z-fight")
        private const float SandLift = 0.05f;    // metres above the sand for the thin foam sheet
        private const float Tile = 6f;           // metres of beach covered by one copy of the foam picture
        private const float Boil = 0.1f;         // how fast the lace drifts through the foam, picture copies per second

        // The thin foam sheet on the sand, per cross-section point: where it sits between the sheet's back (0) and
        // front (1) edge, how strong the soft layer is and how strong the lace. Bright lacy line near the front edge.
        private static readonly float[] SheetPlace = { 0f, 0f, 0.2f, 0.4f, 0.58f, 0.74f, 0.86f, 0.95f, 1f };
        private static readonly float[] SheetSoft = { 0f, 0.08f, 0.2f, 0.22f, 0.25f, 0.3f, 0.55f, 0.7f, 0f };
        private static readonly float[] SheetLace = { 0f, 0.4f, 0.5f, 0.55f, 0.6f, 0.7f, 1f, 1f, 0f };

        // Strip p joins cross-section point p and p + 1 (see WaveProfile.Section). Painter's order for a camera on
        // the beach looking out to sea (where the race camera always is): per wave first the back, then the face
        // from its foot up, the lip last. With see-through water that keeps the near parts on top of the far parts.
        private static readonly int[] StripOrder = { 0, 1, 2, 7, 6, 5, 4, 3 };

        private readonly int slots;
        private readonly int samples;
        private readonly Color deep, face, crest, softFoam;   // linear colour space values (what the shader expects)
        private readonly Color32 white = new Color32(255, 255, 255, 255);

        public readonly Vector3[] Vertices;     // shared by both layers
        public readonly Color32[] Colours;      // water layer
        public readonly Color32[] FoamColours;  // foam layer: white, alpha = how much lace
        public readonly Vector2[] FoamUV;       // foam layer: where in the foam picture
        public readonly int[] Indices;          // shared by both layers

        public WaveMeshWriter(int waveSlots, int beachSamples, Color deepWater, Color faceWater, Color crestWater, Color foamColour, bool linear)
        {
            slots = Mathf.Max(1, waveSlots);
            samples = Mathf.Max(2, beachSamples);
            deep = linear ? deepWater.linear : deepWater;
            face = linear ? faceWater.linear : faceWater;
            crest = linear ? crestWater.linear : crestWater;
            Color foam = linear ? foamColour.linear : foamColour;
            softFoam = Color.Lerp(crest, foam, 0.72f);
            int k = WaveProfile.SectionPoints;
            Vertices = new Vector3[slots * samples * k];
            Colours = new Color32[Vertices.Length];
            FoamColours = new Color32[Vertices.Length];
            FoamUV = new Vector2[Vertices.Length];
            Indices = new int[slots * (samples - 1) * (k - 1) * 6];
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

        public int Slots { get { return slots; } }

        private int Index(int slot, int sample, int point)
        {
            return (slot * samples + sample) * WaveProfile.SectionPoints + point;
        }

        // Fills the arrays for the waves in 'field' on 'beach'. Unused slots are folded flat and invisible.
        public void Write(WaveField field, BeachLine beach)
        {
            if (field == null || beach == null)
            {
                return;
            }
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
                            Set(Index(s, i, p), flat, deep, 0f, 0f, Vector2.zero);
                        }
                        continue;
                    }
                    if (w.Swash > 0f)
                    {
                        WriteSwash(field, w, beach, s, i, sample, along);
                    }
                    else
                    {
                        WriteRolling(field, w, beach, s, i, sample, along);
                    }
                }
            }
        }

        // A wave still rolling in: smooth water, the curling lip, or the foam roll with its trail.
        private void WriteRolling(WaveField field, WaveState w, BeachLine beach, int s, int i, int sample, float along)
        {
            WaveTuning t = field.Tuning;
            float open = beach.Open[sample];
            float fade = WaveProfile.EndFade(t, along) * open;
            float fromPeak = along - w.PeakAlong;
            float broken = WaveProfile.Broken(t, fromPeak, w.Distance);
            float curl = WaveProfile.Curl(t, fromPeak, w.Distance) * (1f - broken);
            float height = field.HeightOf(w, along) * open;
            // The top of the foam is bumpy and keeps changing (billows), bigger waves make more of it.
            float billow = 0.8f + 0.35f * Billow(along, w.Id, w.Age);
            float foamHeight = WaveProfile.FoamHeight(t, w.Distance) * billow * (0.5f + 0.5f * w.HeightScale) * fade;
            float trail = WaveProfile.TrailLength(t, fromPeak, w.Distance);
            // Far out a swell is only a soft line on the sea; it gets clearer as it stands up.
            // A wave that is just appearing (or fading at the ends of the beach) is still flat: draw nothing of it.
            float body = height + foamHeight * broken;
            float clarity = Mathf.Lerp(0.45f, 1f, WaveProfile.Smooth(0.35f, 1.3f, body)) * WaveProfile.Smooth(0f, 0.3f, body);
            float v0 = w.Age * Boil + w.Id * 0.37f;
            for (int p = 0; p < WaveProfile.SectionPoints; p++)
            {
                float offset, y, foamShare, alpha;
                WaveProfile.Section(p, height, foamHeight, broken, curl, trail, out offset, out y, out foamShare, out alpha);
                Vector3 position = beach.Point(sample, w.Distance - offset, y + Lift);
                Color colour = Color.Lerp(WaterColour(p), softFoam, foamShare);
                // Where it is foam the water layer is only a soft white (thinner still in the trail behind).
                float soft = alpha * fade * clarity * Mathf.Lerp(1f, p <= 1 ? 0.45f : 0.8f, foamShare);
                float lace = alpha * fade * foamShare;
                Set(Index(s, i, p), position, colour, soft, lace, new Vector2(along / Tile, -offset / Tile + v0));
            }
        }

        // The wave has reached the waterline: its foam flattens into a thin sheet that runs up the sand in lobes,
        // stops and soaks away. It starts exactly as the rolling foam ended (no jump) and changes over the first fifth.
        private void WriteSwash(WaveField field, WaveState w, BeachLine beach, int s, int i, int sample, float along)
        {
            WaveTuning t = field.Tuning;
            float fade = WaveProfile.EndFade(t, along) * beach.Open[sample];
            float front, back;
            WaveProfile.SwashEdges(t, w.Swash, WaveProfile.Lobe(along, w.Id), out front, out back);
            float trail = WaveProfile.TrailLength(t, along - w.PeakAlong, 0f);
            float sheet = WaveProfile.Smooth(0f, 0.2f, w.Swash);
            // Stays for the first 40 % (the run up the sand), then soaks away smoothly.
            float fadeOut = (1f - WaveProfile.Smooth(0.4f, 1f, w.Swash)) * fade;
            float v0 = t.TravelSeconds * Boil + w.Id * 0.37f;
            for (int p = 0; p < WaveProfile.SectionPoints; p++)
            {
                float offset, y, foamShare, alpha;
                WaveProfile.Section(p, 0f, 0f, 1f, 0f, trail, out offset, out y, out foamShare, out alpha);
                float distance = -offset;                          // where the rolling foam left this point
                if (p > 0)
                {
                    distance = Mathf.Lerp(distance, Mathf.Lerp(back, front, SheetPlace[p]), sheet);
                }
                float soft = Mathf.Lerp(alpha * (p <= 1 ? 0.45f : 0.8f), SheetSoft[p], sheet) * fadeOut;
                float lace = Mathf.Lerp(alpha, SheetLace[p], sheet) * fadeOut;
                Vector3 position = beach.Point(sample, distance, distance < 0f ? SandLift : Lift);
                Set(Index(s, i, p), position, softFoam, soft, lace, new Vector2(along / Tile, distance / Tile + v0));
            }
        }

        private void Set(int index, Vector3 position, Color colour, float softAlpha, float laceAlpha, Vector2 uv)
        {
            Vertices[index] = position;
            Colours[index] = ToColour32(colour, softAlpha);
            Color32 lace = white;
            lace.a = (byte)(Clamp01(laceAlpha) * 255f);
            FoamColours[index] = lace;
            FoamUV[index] = uv;
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
                case 4: return Color.Lerp(crest, softFoam, 0.35f);
                case 5: return Color.Lerp(face, crest, 0.7f);
                case 6: return face;
                default: return Color.Lerp(face, deep, 0.4f);
            }
        }

        // 0..1: a slow moving bumpy pattern along the foam roll, different for every wave.
        public static float Billow(float along, int id, float age)
        {
            float a = Mathf.Sin(along * 0.9f + id * 3.1f + age * 1.7f);
            float b = Mathf.Sin(along * 0.41f - age * 0.9f + id);
            return Clamp01(0.5f + 0.5f * a * b);
        }

        private static float Clamp01(float value)
        {
            return float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
        }

        private static Color32 ToColour32(Color c, float alpha)
        {
            return new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(Clamp01(alpha) * 255f));
        }
    }
}
