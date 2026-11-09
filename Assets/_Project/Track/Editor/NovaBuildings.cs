using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // Extrudes OpenStreetMap footprints into the campus buildings: long white wings with glass bands,
    // the leaning Grand Auditorium, the Academic Hall cube, the entrance building with red stairs.
    public static class NovaBuildings
    {
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
            Vector3 lean = Vector3.zero;
            if (kind == "auditorium")
            {
                lean = LongestEdgeOutward(outline) * (b.height * 0.35f); // the wedge leans out over the plaza
            }

            float u = 0f;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i];
                Vector2 c = outline[(i + 1) % outline.Length];
                Vector3 a0 = new Vector3(a.x, bottom, a.y);
                Vector3 c0 = new Vector3(c.x, bottom, c.y);
                Vector3 a1 = new Vector3(a.x, top, a.y) + lean;
                Vector3 c1 = new Vector3(c.x, top, c.y) + lean;
                float length = Vector2.Distance(a, c);
                // UV tile 4 m = one floor of the facade texture.
                batch.For(walls).Quad(a0, a1, c1, c0, new Vector2(u / 4f, (bottom - b.baseY) / 4f), new Vector2(u / 4f, (top - b.baseY) / 4f), new Vector2((u + length) / 4f, (top - b.baseY) / 4f), new Vector2((u + length) / 4f, (bottom - b.baseY) / 4f));
                u += length;
            }

            // Flat roof (terrace concrete) and a white parapet with a metal railing, like the campus roof terraces.
            Vector2[] roof = new Vector2[outline.Length];
            for (int i = 0; i < outline.Length; i++)
            {
                roof[i] = outline[i] + new Vector2(lean.x, lean.z);
            }
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

            if (kind == "hall")
            {
                // Cascais Academic Hall: deep dark entrance slot cut into the white cube.
                Vector2 a = outline[0];
                Vector2 c = outline[1];
                Vector3 mid = new Vector3((a.x + c.x) * 0.5f, b.baseY + b.height * 0.45f, (a.y + c.y) * 0.5f);
                Quaternion along = Quaternion.LookRotation(new Vector3(c.x - a.x, 0f, c.y - a.y));
                batch.For(look.GlassMat).Box(mid, new Vector3(0.6f, b.height * 0.85f, 3.5f), along, 4f);
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
            }
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

        private static Vector3 LongestEdgeOutward(Vector2[] outline)
        {
            int edge = LongestEdge(outline);
            Vector2 o = Outward(outline[edge], outline[(edge + 1) % outline.Length]);
            return new Vector3(o.x, 0f, o.y);
        }
    }
}
