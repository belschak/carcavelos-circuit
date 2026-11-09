using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // Extrudes OpenStreetMap footprints into the campus buildings: long white wings with glass bands,
    // the leaning Grand Auditorium, the Academic Hall cube, the entrance building with red stairs.
    public static class NovaBuildings
    {
        // The smoothed lap, set by NovaCampusBuilder before the buildings are made (so facades can face the road).
        public static Vector3[] Route;

        public static void Add(NovaBuilding b, NovaBatch batch, NovaLook look)
        {
            if (b.pts == null || b.pts.Length < 3)
            {
                return;
            }
            Vector2[] outline = CounterClockwise(b.pts);
            float bottom = b.baseY - 2f; // reach into the ground so slopes never show a gap
            float top = b.baseY + Mathf.Max(3f, b.height);
            string kind = b.kind == null ? "" : b.kind;

            // Campus wings: white + glass ribbons. Off-campus blocks: off-white with punched windows. Hall + auditorium: plain white.
            Material walls = kind == "wing" || kind == "entrance" ? look.Facade : (kind == "plain" ? look.Residential : look.PlainWhite);
            if (kind == "auditorium")
            {
                AddAuditorium(b, outline, batch, look);
                return;
            }

            float u = 0f;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i];
                Vector2 c = outline[(i + 1) % outline.Length];
                Vector3 a0 = new Vector3(a.x, bottom, a.y);
                Vector3 c0 = new Vector3(c.x, bottom, c.y);
                Vector3 a1 = new Vector3(a.x, top, a.y);
                Vector3 c1 = new Vector3(c.x, top, c.y);
                float length = Vector2.Distance(a, c);
                // UV tile 4 m = one floor of the facade texture.
                batch.For(walls).Quad(a0, a1, c1, c0, new Vector2(u / 4f, (bottom - b.baseY) / 4f), new Vector2(u / 4f, (top - b.baseY) / 4f), new Vector2((u + length) / 4f, (top - b.baseY) / 4f), new Vector2((u + length) / 4f, (bottom - b.baseY) / 4f));
                u += length;
            }

            // Flat roof (terrace concrete) and a white parapet with a metal railing, like the campus roof terraces.
            Vector2[] roof = outline;
            batch.For(look.Concrete).FlatPolygon(roof, top, 4f);
            if (kind == "wing" || kind == "entrance" || kind == "plain")
            {
                for (int i = 0; i < roof.Length; i++)
                {
                    Vector2 a = roof[i];
                    Vector2 c = roof[(i + 1) % roof.Length];
                    Vector3 mid = new Vector3((a.x + c.x) * 0.5f, top, (a.y + c.y) * 0.5f);
                    float length = Vector2.Distance(a, c);
                    if (length < 0.5f)
                    {
                        continue;
                    }
                    Quaternion along = Quaternion.LookRotation(new Vector3(c.x - a.x, 0f, c.y - a.y));
                    batch.For(look.PlainWhite).Box(mid + Vector3.up * 0.3f, new Vector3(0.3f, 0.6f, length), along, 4f);
                    batch.For(look.Metal).Box(mid + Vector3.up * 1.05f, new Vector3(0.05f, 0.05f, length), along, 4f);
                    if (kind == "wing" || kind == "entrance")
                    {
                        // The Nova signature: a thin white roof plate that overhangs the top floor by about 1.5 m.
                        Vector2 o = Outward(a, c);
                        batch.For(look.PlainWhite).Box(mid + new Vector3(o.x, 0f, o.y) * 0.75f + Vector3.up * 0.18f, new Vector3(2.2f, 0.35f, length + 1.5f), along, 4f);
                    }
                }
            }

            if (kind == "wing")
            {
                AddWingGroundFloor(b, outline, batch, look);
                AddWingTopFloor(b, outline, top, batch, look);
            }
            if (kind == "hall")
            {
                AddHallFacades(b, outline, top, batch, look);
            }
            if (kind == "entrance")
            {
                // Wide terracotta stairs on the longest side, the main entrance look.
                int edge = LongestEdge(outline);
                Vector2 a = outline[edge];
                Vector2 c = outline[(edge + 1) % outline.Length];
                Vector2 outward = Outward(a, c);
                const int steps = 12;
                for (int step = 0; step < steps; step++)
                {
                    // Broad salmon stair climbing to the first floor; the lowest three steps flare out as seating.
                    Vector2 m = (a + c) * 0.5f + outward * (0.4f + step * 0.4f);
                    Quaternion along = Quaternion.LookRotation(new Vector3(c.x - a.x, 0f, c.y - a.y));
                    float rise = (steps - step) * 0.25f;
                    batch.For(look.Terracotta).Box(new Vector3(m.x, b.baseY + rise * 0.5f - 0.1f, m.y), new Vector3(0.4f, rise, step >= steps - 3 ? 14f : 10f), along, 1f);
                }

                // Deep white slab on slender round white columns along the entrance side (the campus colonnade).
                float edgeLength = Vector2.Distance(a, c);
                Vector2 dir = (c - a).normalized;
                Quaternion edgeRot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
                float slabY = b.baseY + Mathf.Min(b.height, 8f) * 0.5f;
                for (float s = 3f; s < edgeLength - 2f; s += 6f)
                {
                    float fromMiddle = Mathf.Abs(s - edgeLength * 0.5f);
                    if (fromMiddle < 6f)
                    {
                        continue; // keep the stair opening free
                    }
                    Vector2 p = a + dir * s + outward * 3f;
                    batch.For(look.PlainWhite).Prism(new Vector3(p.x, b.baseY - 0.5f, p.y), 0.2f, slabY - b.baseY + 0.5f, 10);
                }
                Vector2 slabMid = (a + c) * 0.5f + outward * 1.75f;
                batch.For(look.PlainWhite).Box(new Vector3(slabMid.x, slabY + 0.2f, slabMid.y), new Vector3(3.5f, 0.4f, edgeLength), edgeRot, 4f);
            }
        }

        // Set-back top storey (video IMG_7723 f0001-f0012): a dark louvred box 5 m in from the facade on the roof
        // terrace, 3.2 m tall, with its own thin white roof plate. Skipped where the footprint is too narrow.
        private static void AddWingTopFloor(NovaBuilding b, Vector2[] outline, float top, NovaBatch batch, NovaLook look)
        {
            if (b.height < 7.5f)
            {
                return;
            }
            Vector2[] inner = MiterInset(outline, 5f);
            if (!InsetIsClean(outline, inner, 4.5f))
            {
                return;
            }
            float roofY = top + 3.2f;
            float u = 0f;
            for (int i = 0; i < inner.Length; i++)
            {
                Vector2 a = inner[i];
                Vector2 c = inner[(i + 1) % inner.Length];
                float length = Vector2.Distance(a, c);
                Vector3 a0 = new Vector3(a.x, top, a.y);
                Vector3 c0 = new Vector3(c.x, top, c.y);
                Vector3 a1 = new Vector3(a.x, roofY, a.y);
                Vector3 c1 = new Vector3(c.x, roofY, c.y);
                batch.For(look.Louvre).Quad(a0, a1, c1, c0, new Vector2(u / 4f, 0f), new Vector2(u / 4f, 0.8f), new Vector2((u + length) / 4f, 0.8f), new Vector2((u + length) / 4f, 0f));
                if (length > 0.5f)
                {
                    Vector2 o = Outward(a, c);
                    Vector3 mid = new Vector3((a.x + c.x) * 0.5f + o.x * 0.6f, roofY + 0.15f, (a.y + c.y) * 0.5f + o.y * 0.6f);
                    Quaternion along = Quaternion.LookRotation(new Vector3(c.x - a.x, 0f, c.y - a.y));
                    batch.For(look.PlainWhite).Box(mid, new Vector3(1.4f, 0.3f, length + 1.2f), along, 4f);
                }
                u += length;
            }
            batch.For(look.Concrete).FlatPolygon(inner, roofY, 4f);
        }

        // Moves every edge of a counter-clockwise outline inwards by exactly "by" (mitred corners, capped at 3x).
        private static Vector2[] MiterInset(Vector2[] outline, float by)
        {
            int n = outline.Length;
            Vector2[] result = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = outline[(i - 1 + n) % n];
                Vector2 cur = outline[i];
                Vector2 next = outline[(i + 1) % n];
                Vector2 n0 = -Outward(prev, cur);
                Vector2 n1 = -Outward(cur, next);
                Vector2 bis = n0 + n1;
                if (bis.sqrMagnitude < 0.0001f)
                {
                    result[i] = cur + n0 * by;
                    continue;
                }
                bis.Normalize();
                float cos = Mathf.Max(Vector2.Dot(bis, n0), 0.34f);
                result[i] = cur + bis * (by / cos);
            }
            return result;
        }

        // True when the inset outline is a simple polygon that stays at least minGap inside every original edge.
        private static bool InsetIsClean(Vector2[] outline, Vector2[] inner, float minGap)
        {
            if (NovaTriangulator.SignedArea(inner) < 60f)
            {
                return false;
            }
            for (int i = 0; i < inner.Length; i++)
            {
                for (int k = 0; k < outline.Length; k++)
                {
                    if (SegmentDistance(inner[i], outline[k], outline[(k + 1) % outline.Length]) < minGap)
                    {
                        return false;
                    }
                }
                for (int j = i + 2; j < inner.Length; j++)
                {
                    if (i == 0 && j == inner.Length - 1)
                    {
                        continue;
                    }
                    if (SegmentsCross(inner[i], inner[(i + 1) % inner.Length], inner[j], inner[(j + 1) % inner.Length]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 c)
        {
            Vector2 ac = c - a;
            float t = ac.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ac) / ac.sqrMagnitude);
            return Vector2.Distance(p, a + ac * t);
        }

        private static bool SegmentsCross(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            float d1 = Cross(q2 - q1, p1 - q1);
            float d2 = Cross(q2 - q1, p2 - q1);
            float d3 = Cross(p2 - p1, q1 - p1);
            float d4 = Cross(p2 - p1, q2 - p1);
            return d1 * d2 < 0f && d3 * d4 < 0f;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        // Wing ground floor (video IMG_7723): the glass sits in the shade of the first-floor slab, which rests on
        // slender white columns about every 7 m; the slab nose sticks out 0.6 m.
        private static void AddWingGroundFloor(NovaBuilding b, Vector2[] outline, NovaBatch batch, NovaLook look)
        {
            if (b.height < 6f)
            {
                return; // single-storey annexes have no colonnade
            }
            float u = 0f;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i];
                Vector2 c = outline[(i + 1) % outline.Length];
                float length = Vector2.Distance(a, c);
                if (length >= 8f)
                {
                    Vector2 o = Outward(a, c) * 0.03f;
                    float lo = b.baseY - 1f;
                    float hi = b.baseY + 4f;
                    Vector3 a0 = new Vector3(a.x + o.x, lo, a.y + o.y);
                    Vector3 c0 = new Vector3(c.x + o.x, lo, c.y + o.y);
                    Vector3 a1 = new Vector3(a.x + o.x, hi, a.y + o.y);
                    Vector3 c1 = new Vector3(c.x + o.x, hi, c.y + o.y);
                    batch.For(look.FacadeGround).Quad(a0, a1, c1, c0, new Vector2(u / 4f, -0.25f), new Vector2(u / 4f, 1f), new Vector2((u + length) / 4f, 1f), new Vector2((u + length) / 4f, -0.25f));

                    Vector2 dir = (c - a) / length;
                    Vector2 out1 = Outward(a, c);
                    Quaternion along = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
                    Vector2 mid = (a + c) * 0.5f + out1 * 0.3f;
                    batch.For(look.PlainWhite).Box(new Vector3(mid.x, b.baseY + 4.375f, mid.y), new Vector3(0.6f, 0.75f, length), along, 4f);
                    int count = Mathf.Max(1, Mathf.RoundToInt(length / 7f));
                    float step = length / count;
                    for (int k = 0; k <= count; k++)
                    {
                        Vector2 p = a + dir * Mathf.Clamp(k * step, 0.3f, length - 0.3f) + out1 * 0.35f;
                        batch.For(look.PlainWhite).Box(new Vector3(p.x, b.baseY + 1.75f, p.y), new Vector3(0.3f, 4.5f, 0.3f), along, 4f);
                    }
                }
                u += length;
            }
        }

        // Grand Auditorium: a white wedge whose plaza-side wall leans far out over a dark glazed base, the roof
        // rising towards the plaza (photos IMG_6270-6277, video IMG_7723).
        private static void AddAuditorium(NovaBuilding b, Vector2[] outline, NovaBatch batch, NovaLook look)
        {
            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < outline.Length; i++)
            {
                centroid += outline[i] / outline.Length;
            }
            Vector2 toRoad = NearestRoutePoint(centroid) - centroid;
            int longest = LongestEdge(outline);
            Vector2 dir = toRoad.sqrMagnitude > 0.01f ? toRoad.normalized : Outward(outline[longest], outline[(longest + 1) % outline.Length]);
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < outline.Length; i++)
            {
                float d = Vector2.Dot(outline[i], dir);
                min = Mathf.Min(min, d);
                max = Mathf.Max(max, d);
            }
            const float plinth = 2.6f;
            float baseY = b.baseY;
            int n = outline.Length;
            Vector3[] low = new Vector3[n];
            Vector3[] high = new Vector3[n];
            float[] tops = new float[n];
            Vector2[] roof = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float t = max - min < 0.01f ? 0f : (Vector2.Dot(outline[i], dir) - min) / (max - min); // 0 back, 1 plaza side
                Vector2 lean = dir * (b.height * 0.3f * t * t);
                tops[i] = baseY + b.height * (0.65f + 0.45f * t);
                roof[i] = outline[i] + lean;
                low[i] = new Vector3(outline[i].x, baseY + plinth, outline[i].y);
                high[i] = new Vector3(roof[i].x, tops[i], roof[i].y);
            }
            float u = 0f;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float length = Vector2.Distance(outline[i], outline[j]);
                batch.For(look.PlainWhite).Quad(low[i], high[i], high[j], low[j], new Vector2(u / 4f, 0f), new Vector2(u / 4f, 1f), new Vector2((u + length) / 4f, 1f), new Vector2((u + length) / 4f, 0f));
                u += length;
            }
            batch.For(look.PlainWhite).SlopedPolygon(roof, tops, 4f);
            batch.For(look.PlainWhite).FlatPolygonDown(outline, baseY + plinth, 4f); // underside of the white volume

            // Dark glazed base, set back 1.5 m, from below the lawn up to the white volume.
            Vector2[] inset = Inset(outline, 1.5f);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 a = new Vector3(inset[i].x, 0f, inset[i].y);
                Vector3 c = new Vector3(inset[j].x, 0f, inset[j].y);
                batch.For(look.GlassMat).Wall(a, c, baseY - 1f, baseY - 1f, baseY + plinth, baseY + plinth, 0f, 4f);
            }
        }

        // Cascais Academic Hall and siblings: white box with a long dark window band on every long side and, on the
        // side facing the road, a hollow white portal frame around a full-height glass entrance (photo IMG_6239).
        private static void AddHallFacades(NovaBuilding b, Vector2[] outline, float top, NovaBatch batch, NovaLook look)
        {
            int face = -1;
            float faceDistance = float.MaxValue;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i];
                Vector2 c = outline[(i + 1) % outline.Length];
                float length = Vector2.Distance(a, c);
                if (length < 12f)
                {
                    continue;
                }
                Vector2 mid = (a + c) * 0.5f;
                Vector2 o = Outward(a, c);
                Quaternion along = Quaternion.LookRotation(new Vector3(c.x - a.x, 0f, c.y - a.y));
                // Window band: dark glass ribbon with a thin white sill, slightly proud of the wall.
                float bandY = b.baseY + Mathf.Min(b.height * 0.55f, 6.5f);
                Vector2 bandMid = mid + o * 0.12f;
                batch.For(look.GlassMat).Box(new Vector3(bandMid.x, bandY, bandMid.y), new Vector3(0.2f, 2.2f, length * 0.78f), along, 4f);
                batch.For(look.PlainWhite).Box(new Vector3(bandMid.x + o.x * 0.1f, bandY - 1.2f, bandMid.y + o.y * 0.1f), new Vector3(0.4f, 0.2f, length * 0.78f), along, 4f);
                float distance = Vector2.Distance(mid, NearestRoutePoint(mid));
                if (distance < faceDistance)
                {
                    faceDistance = distance;
                    face = i;
                }
            }
            if (face < 0)
            {
                return;
            }
            Vector2 fa = outline[face];
            Vector2 fc = outline[(face + 1) % outline.Length];
            Vector2 fo = Outward(fa, fc);
            Vector2 fd = (fc - fa).normalized;
            Quaternion rot = Quaternion.LookRotation(new Vector3(fd.x, 0f, fd.y));
            float faceLength = Vector2.Distance(fa, fc);
            Vector2 at = fa + fd * Mathf.Min(faceLength * 0.3f, 18f) + fo * 1.5f; // frame sticks out 3 m (centre 1.5 m out)
            float frameTop = top + 4f;
            float h = frameTop - b.baseY;
            Vector3 c0 = new Vector3(at.x, b.baseY + h * 0.5f, at.y);
            Vector3 side = new Vector3(fd.x, 0f, fd.y);
            // Hollow tube: two side walls and a lintel, 1 m thick, 6 m wide outside, 3 m deep.
            batch.For(look.PlainWhite).Box(c0 - side * 2.5f, new Vector3(3f, h, 1f), rot, 4f);
            batch.For(look.PlainWhite).Box(c0 + side * 2.5f, new Vector3(3f, h, 1f), rot, 4f);
            batch.For(look.PlainWhite).Box(new Vector3(at.x, frameTop - 0.5f, at.y), new Vector3(3f, 1f, 6f), rot, 4f);
            // Full-height dark glass entrance inside the frame, and a short sandstone stair in front of it.
            Vector2 glass = at - fo * 1.3f;
            batch.For(look.GlassMat).Box(new Vector3(glass.x, b.baseY + (top - b.baseY) * 0.5f, glass.y), new Vector3(0.3f, top - b.baseY, 4f), rot, 4f);
            for (int step = 0; step < 5; step++)
            {
                Vector2 m = at + fo * (1.7f + step * 0.35f);
                float rise = (5 - step) * 0.17f;
                batch.For(look.SandstoneTop).Box(new Vector3(m.x, b.baseY + rise * 0.5f - 0.05f, m.y), new Vector3(0.35f, rise, 4f), rot, 1f);
            }
            // Raised sign plate on the wall beside the frame (no real lettering: a blank grey plate).
            Vector2 sign = fa + fd * Mathf.Min(faceLength * 0.3f + 9f, faceLength - 4f) + fo * 0.15f;
            batch.For(look.Metal).Box(new Vector3(sign.x, b.baseY + 4f, sign.y), new Vector3(0.15f, 1.1f, 7f), rot, 4f);
        }

        private static Vector2 NearestRoutePoint(Vector2 p)
        {
            if (Route == null || Route.Length == 0)
            {
                return p;
            }
            Vector2 best = new Vector2(Route[0].x, Route[0].z);
            for (int i = 1; i < Route.Length; i++)
            {
                Vector2 q = new Vector2(Route[i].x, Route[i].z);
                if ((q - p).sqrMagnitude < (best - p).sqrMagnitude)
                {
                    best = q;
                }
            }
            return best;
        }

        // Moves every corner of a counter-clockwise outline inwards (approximate, fine for our footprints).
        private static Vector2[] Inset(Vector2[] outline, float by)
        {
            int n = outline.Length;
            Vector2[] result = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = outline[(i - 1 + n) % n];
                Vector2 cur = outline[i];
                Vector2 next = outline[(i + 1) % n];
                Vector2 inward = -(Outward(prev, cur) + Outward(cur, next));
                result[i] = cur + (inward.sqrMagnitude > 0.0001f ? inward.normalized * by : Vector2.zero);
            }
            return result;
        }

        // Unity draws a face from one side only; walls need the outline counter-clockwise (seen from above).
        public static Vector2[] CounterClockwise(NovaPoint[] pts)
        {
            int n = pts.Length;
            if (n > 3 && Mathf.Approximately(pts[0].x, pts[n - 1].x) && Mathf.Approximately(pts[0].z, pts[n - 1].z))
            {
                n--; // OSM repeats the first point at the end
            }
            Vector2[] outline = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                outline[i] = new Vector2(pts[i].x, pts[i].z);
            }
            if (NovaTriangulator.SignedArea(outline) < 0f)
            {
                System.Array.Reverse(outline);
            }
            return outline;
        }

        private static int LongestEdge(Vector2[] outline)
        {
            int best = 0;
            float bestLength = 0f;
            for (int i = 0; i < outline.Length; i++)
            {
                float length = Vector2.Distance(outline[i], outline[(i + 1) % outline.Length]);
                if (length > bestLength)
                {
                    bestLength = length;
                    best = i;
                }
            }
            return best;
        }

        // For a counter-clockwise outline the outside is on the right of each edge.
        private static Vector2 Outward(Vector2 a, Vector2 c)
        {
            Vector2 d = (c - a).normalized;
            return new Vector2(d.y, -d.x);
        }
    }
}
