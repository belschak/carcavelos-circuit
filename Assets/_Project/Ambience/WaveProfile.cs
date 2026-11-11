using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // The shape of one wave, as plain maths (Edit Mode tested). Coordinates: 'distance' = metres from the waterline
    // out to sea, 'along' = metres along the beach, 'offset' = metres across the wave from its crest, positive towards
    // the beach. Life of a wave: low swell far out -> grows and steepens as the water gets shallow -> breaks at its peak
    // first, and the break runs along the wave both ways ("peels") -> white foam roll that leaves a lacy trail ->
    // thin foam runs up the sand in lobes and soaks away.
    public static class WaveProfile
    {
        // Points across one wave, from the back (sea side) to the front (beach side).
        public const int SectionPoints = 9;

        // Longest foam trail a broken wave leaves behind it, in metres.
        public const float MaxTrail = 28f;

        // Height (m) of the unbroken wave at this distance from the waterline, before the set size is applied.
        public static float UnbrokenHeight(WaveTuning t, float distance)
        {
            if (float.IsNaN(distance))
            {
                return 0f;
            }
            // Fades in over the first 14 m so a swell never pops up out of nowhere.
            float fadeIn = Smooth(t.SpawnDistance, t.SpawnDistance - 14f, distance);
            // Shoaling: the wave grows as the water gets shallow, fastest in the last 40 m before the break.
            float grow = Smooth(t.BreakDistance + 45f, t.BreakDistance, distance);
            float height = Mathf.Lerp(t.SwellHeight, t.BreakHeight, grow * grow);
            // Inside the break line the parts that have not broken yet stand up a little more.
            float inside = Smooth(t.BreakDistance, t.BreakDistance * 0.4f, distance);
            height *= 1f + 0.12f * inside;
            return Mathf.Max(0f, height * fadeIn);
        }

        // How high the crest is along the wave, 0.7..1.05: highest at the peak (where it breaks first), lower on the
        // shoulders, with a slow wobble. Without this a wave looks like a ruler-straight ridge.
        public static float CrestShape(float alongFromPeak, float along, int id)
        {
            if (float.IsNaN(alongFromPeak) || float.IsNaN(along))
            {
                return 1f;
            }
            float fromPeak = alongFromPeak / 45f;
            return 0.76f + 0.24f * Mathf.Exp(-fromPeak * fromPeak) + 0.04f * Mathf.Sin(along * 0.045f + id * 1.3f);
        }

        // Height (m) of the white foam after the break: as tall as the wave right where it breaks (the "explosion"),
        // then it collapses quickly into a lower roll, shrinks towards the sand and is flat at the waterline.
        public static float FoamHeight(WaveTuning t, float distance)
        {
            if (float.IsNaN(distance))
            {
                return 0f;
            }
            float share = Mathf.Clamp01(distance / t.BreakDistance);
            float roll = t.WhitewaterShare * (0.3f + 0.7f * share);
            float burst = Mathf.Max(0f, 0.9f - t.WhitewaterShare) * share * share * share;
            return t.BreakHeight * (roll + burst) * Smooth(0f, 6f, distance);
        }

        // 0 = smooth water, 1 = broken (white foam) at 'alongFromPeak' metres from the wave's peak.
        // The peak breaks when the crest reaches BreakDistance; then the break runs sideways at PeelSpeed.
        // Close to the sand every part has broken (a wave never reaches the beach as smooth water).
        public static float Broken(WaveTuning t, float alongFromPeak, float distance)
        {
            if (float.IsNaN(alongFromPeak) || float.IsNaN(distance))
            {
                return 0f;
            }
            float secondsSinceBreak = (t.BreakDistance - distance) / t.Speed;
            float spread = t.PeelSpeed * secondsSinceBreak;          // metres of the wave that have broken so far
            float side = Mathf.Abs(alongFromPeak);
            float peeled = secondsSinceBreak <= 0f ? 0f : Smooth(spread + 2f, spread - 3f, side);
            float closeOut = Smooth(t.BreakDistance * 0.38f, t.BreakDistance * 0.18f, distance);
            return Mathf.Max(peeled, closeOut);
        }

        // Metres of foam left on the water behind a broken part: it grows from 0 at the moment the break arrives
        // here (the foam stays where it was made while the wave rolls on) up to MaxTrail.
        public static float TrailLength(WaveTuning t, float alongFromPeak, float distance)
        {
            if (float.IsNaN(alongFromPeak) || float.IsNaN(distance))
            {
                return 0f;
            }
            float sincePeel = (t.BreakDistance - distance) / t.Speed - Mathf.Abs(alongFromPeak) / t.PeelSpeed;
            float sinceCloseOut = (t.BreakDistance * 0.28f - distance) / t.Speed;
            float seconds = Mathf.Max(sincePeel, sinceCloseOut);
            return Mathf.Clamp(seconds * t.Speed, 0f, MaxTrail);
        }

        // How far the lip throws forward (0..1). Strongest right next to the breaking part (the "pocket"),
        // a little everywhere once the wave stands up before the break.
        public static float Curl(WaveTuning t, float alongFromPeak, float distance)
        {
            if (float.IsNaN(alongFromPeak) || float.IsNaN(distance))
            {
                return 0f;
            }
            float standUp = Smooth(t.BreakDistance + 16f, t.BreakDistance, distance);
            float secondsSinceBreak = (t.BreakDistance - distance) / t.Speed;
            float pocket = 0f;
            if (secondsSinceBreak > 0f)
            {
                float spread = t.PeelSpeed * secondsSinceBreak;
                float fromEdge = (Mathf.Abs(alongFromPeak) - spread) / 7f;  // 0 at the edge of the break
                pocket = Mathf.Exp(-fromEdge * fromEdge);
            }
            return Mathf.Clamp01(0.3f * standUp + 0.85f * pocket);
        }

        // 0..1 along the beach: waves fade out softly towards both ends of the wave strip.
        public static float EndFade(WaveTuning t, float along)
        {
            if (float.IsNaN(along))
            {
                return 0f;
            }
            float half = t.BeachLength * 0.5f;
            return Smooth(half, half - t.EndFade, Mathf.Abs(along));
        }

        // One point of the cross-section (index 0 = back at sea level .. 8 = front at sea level).
        // height = smooth-water height, foamHeight = foam height, broken / curl 0..1, trail = metres of foam left
        // behind (TrailLength). Out: offset = metres from the crest towards the beach, y = metres above the water,
        // foam = 0..1 white, alpha 0..1.
        public static void Section(int index, float height, float foamHeight, float broken, float curl, float trail,
                                   out float offset, out float y, out float foam, out float alpha)
        {
            float h = Mathf.Max(0f, height);
            float f = Mathf.Max(0f, foamHeight);
            float c = Mathf.Clamp01(curl);
            float b = Mathf.Clamp01(broken);
            float waterX, waterY, waterFoam, waterAlpha;
            float foamX, foamY, foamFoam, foamAlpha;
            switch (index)
            {
                case 0: // long gentle back, starts flat in the sea; behind foam: the end of the lacy trail
                    waterX = -(4f + 4.5f * h); waterY = 0f; waterFoam = 0f; waterAlpha = 0f;
                    foamX = -(4f + Mathf.Max(0f, trail)); foamY = 0f; foamFoam = 1f; foamAlpha = 0f;
                    break;
                case 1:
                    waterX = -(1.8f + 2.2f * h); waterY = 0.28f * h; waterFoam = 0f; waterAlpha = 0.65f;
                    foamX = -(3f + 1.2f * f); foamY = 0.12f * f; foamFoam = 1f; foamAlpha = 0.6f;
                    break;
                case 2:
                    waterX = -(0.5f + 0.75f * h); waterY = 0.78f * h; waterFoam = 0f; waterAlpha = 0.92f;
                    foamX = -(0.5f + 0.55f * f); foamY = 0.8f * f; foamFoam = 1f; foamAlpha = 0.95f;
                    break;
                case 3: // crest; white feathering once it stands up
                    waterX = 0f; waterY = h; waterFoam = 0.7f * Smooth(0.2f, 0.8f, c); waterAlpha = 0.95f;
                    foamX = 0f; foamY = f; foamFoam = 1f; foamAlpha = 1f;
                    break;
                case 4: // lip: throws forward and down when the wave curls
                    waterX = LipStart * h + LipThrow * h * c; waterY = h * (0.97f - 0.28f * c); waterFoam = Smooth(0.05f, 0.55f, c); waterAlpha = 0.95f;
                    foamX = 0.5f * f; foamY = 0.95f * f; foamFoam = 1f; foamAlpha = 1f;
                    break;
                case 5: // under the lip: the face hollows out behind the thrown lip
                    waterX = 0.3f * h + 0.08f * h * c; waterY = h * (0.68f - 0.05f * c); waterFoam = 0f; waterAlpha = 0.95f;
                    foamX = 0.85f * f; foamY = 0.62f * f; foamFoam = 1f; foamAlpha = 1f;
                    break;
                case 6: // face
                    waterX = FaceX * h; waterY = FaceY * h; waterFoam = 0f; waterAlpha = 0.92f;
                    foamX = 1.05f * f + 0.2f; foamY = 0.28f * f; foamFoam = 1f; foamAlpha = 0.95f;
                    break;
                case 7: // toe of the wave
                    waterX = ToeX * h + ToeFlat; waterY = ToeY * h; waterFoam = 0f; waterAlpha = 0.6f;
                    foamX = 1.3f * f + 0.6f; foamY = 0.05f * f; foamFoam = 1f; foamAlpha = 0.7f;
                    break;
                default: // 8: flat again in front
                    waterX = FrontX * h + FrontFlat; waterY = 0f; waterFoam = 0f; waterAlpha = 0f;
                    foamX = 1.7f * f + 1.4f; foamY = 0f; foamFoam = 1f; foamAlpha = 0f;
                    break;
            }
            offset = Mathf.Lerp(waterX, foamX, b);
            y = Mathf.Lerp(waterY, foamY, b);
            foam = Mathf.Lerp(waterFoam, foamFoam, b);
            alpha = Mathf.Lerp(waterAlpha, foamAlpha, b);
        }

        // The face of a smooth wave, as multiples of its height (used by Section and HeightAtOffset, so the surfers
        // stand exactly on the water that is drawn).
        private const float LipStart = 0.2f, LipThrow = 0.45f;
        private const float FaceX = 0.62f, FaceY = 0.36f;
        private const float ToeX = 1.2f, ToeFlat = 0.4f, ToeY = 0.06f;
        private const float FrontX = 1.9f, FrontFlat = 1.6f;

        // Water height (m above the sea) of a smooth wave of this height at 'offset' from its crest. The lip is left
        // out: a surfer floats on the back and rides on the face, not on the thrown lip.
        public static float HeightAtOffset(float height, float offset)
        {
            if (float.IsNaN(height) || float.IsNaN(offset) || height <= 0f)
            {
                return 0f;
            }
            // Back: points 0 -> 1 -> 2 -> 3, front: points 3 -> 6 -> 7 -> 8 (same numbers as Section).
            float h = height;
            float x0 = -(4f + 4.5f * h), x1 = -(1.8f + 2.2f * h), x2 = -(0.5f + 0.75f * h);
            float x6 = FaceX * h, x7 = ToeX * h + ToeFlat, x8 = FrontX * h + FrontFlat;
            if (offset <= x0 || offset >= x8)
            {
                return 0f;
            }
            if (offset < x1)
            {
                return Mathf.Lerp(0f, 0.28f * h, (offset - x0) / (x1 - x0));
            }
            if (offset < x2)
            {
                return Mathf.Lerp(0.28f * h, 0.78f * h, (offset - x1) / (x2 - x1));
            }
            if (offset < 0f)
            {
                return Mathf.Lerp(0.78f * h, h, (offset - x2) / (0f - x2));
            }
            if (offset < x6)
            {
                return Mathf.Lerp(h, FaceY * h, offset / x6);
            }
            if (offset < x7)
            {
                return Mathf.Lerp(FaceY * h, ToeY * h, (offset - x6) / (x7 - x6));
            }
            return Mathf.Lerp(ToeY * h, 0f, (offset - x7) / (x8 - x7));
        }

        // 0..1, changes slowly along the beach and is different for every wave: how far this bit of foam runs up the
        // sand. Gives the foam line its scalloped (lobed) edge instead of a straight strip.
        public static float Lobe(float along, int id)
        {
            if (float.IsNaN(along))
            {
                return 0.5f;
            }
            return 0.5f + 0.5f * Mathf.Sin(along * 0.19f + id * 2.1f) * Mathf.Cos(along * 0.071f - id * 0.9f);
        }

        // The foam that runs up the sand after the wave reaches the waterline (swash 0..1, lobe 0..1 from Lobe).
        // Returns the distance (m, negative = up the sand) of the foam's front edge and of its back edge.
        public static void SwashEdges(WaveTuning t, float swash, float lobe, out float front, out float back)
        {
            float s = Mathf.Clamp01(float.IsNaN(swash) ? 1f : swash);
            float reach = t.RunUp * (0.5f + 0.5f * Mathf.Clamp01(float.IsNaN(lobe) ? 1f : lobe));
            // Runs up fast and slows down (like water losing its push), then stays while it soaks away.
            float run = Mathf.Sin(Mathf.Min(1f, s * 2.2f) * Mathf.PI * 0.5f);
            front = -reach * run - 1.4f * (1f - run);
            back = Mathf.Lerp(3f, front + 1.5f, Mathf.Pow(s, 0.6f));
        }

        // Unity's SmoothStep with the edges in either order: 0 at 'from', 1 at 'to'.
        public static float Smooth(float from, float to, float value)
        {
            if (Mathf.Approximately(from, to))
            {
                return value >= to ? 1f : 0f;
            }
            float x = Mathf.Clamp01((value - from) / (to - from));
            return x * x * (3f - 2f * x);
        }
    }
}
