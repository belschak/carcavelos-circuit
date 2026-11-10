using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // White guardrails along both road edges, like the white balustrades inside the Nova SBE building:
    // a slim top handrail, a thinner middle rail, square posts and a low white base strip.
    // Visual only: the invisible edge colliders from NovaCampusBuilder.AddEdges still stop the kart.
    public static class NovaGuardrail
    {
        private const float Height = 1.0f;       // top of the handrail above the road
        private const float PostSize = 0.08f;
        private const int PostEvery = 2;      // a post every 2nd route ring (about 4 m)
        private const float OutsideOffset = 0.15f; // rail stands a little outside the road edge

        // "skip[i]" = no guardrail at ring i (tunnel, ramp walls: they have their own walls already).
        public static void Add(NovaBatch batch, NovaLook look, Vector3[] centre, float[] width, bool[] skip)
        {
            int count = centre.Length;
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                for (int k = 0; k < 2; k++)
                {
                    float side = k == 0 ? -1f : 1f;
                    if (skip[i])
                    {
                        continue;
                    }
                    Vector3 a = RailPoint(centre, width, i, side);
                    // Posts: regularly, and also at the end of a gap so a rail never ends in the air.
                    int before = (i + count - 1) % count;
                    if (i % PostEvery == 0 || skip[j] || skip[before])
                    {
                        batch.For(look.GuardrailWhite).Box(a + Vector3.up * (Height * 0.5f), new Vector3(PostSize, Height, PostSize), Quaternion.identity, 1f);
                    }
                    if (skip[j])
                    {
                        continue;
                    }
                    Vector3 b = RailPoint(centre, width, j, side);
                    AddRailSegment(batch, look, a, b);
                }
            }
        }

        // The point on the road edge (pushed slightly outwards) at ring i.
        private static Vector3 RailPoint(Vector3[] centre, float[] width, int i, float side)
        {
            Vector3 right = NovaRouteMath.Right(centre, i);
            return centre[i] + right * (width[i] * 0.5f * side + OutsideOffset * side);
        }

        // Three horizontal pieces between two neighbouring posts: base strip, middle rail, handrail.
        private static void AddRailSegment(NovaBatch batch, NovaLook look, Vector3 a, Vector3 b)
        {
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 0.01f)
            {
                return;
            }
            Quaternion along = Quaternion.LookRotation(dir);
            float length = dir.magnitude + 0.05f; // tiny overlap so no gap shows in curves
            Vector3 mid = (a + b) * 0.5f;
            batch.For(look.GuardrailWhite).Box(mid + Vector3.up * 0.1f, new Vector3(0.06f, 0.2f, length), along, 1f);
            batch.For(look.GuardrailWhite).Box(mid + Vector3.up * (Height * 0.5f), new Vector3(0.04f, 0.05f, length), along, 1f);
            batch.For(look.GuardrailWhite).Box(mid + Vector3.up * (Height - 0.04f), new Vector3(0.08f, 0.08f, length), along, 1f);
        }
    }
}
