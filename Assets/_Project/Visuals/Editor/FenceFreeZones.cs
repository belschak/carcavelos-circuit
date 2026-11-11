using System.Collections.Generic;
using System.IO;
using CarcavelosCircuit.Track.Editor;
using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Visuals.Editor
{
    // V14 + Nova hall: leaves Noah's white fence (Role 2, #63) out where the V14 world has its own edge or needs the
    // way open: the terrace jump's drop, the terracotta stairs, the left side of the upper plaza (courtyard fence,
    // shortcut kicker and exit), the straight tunnel ramp, the promenade's sea side and the gap in the Marginal's
    // median. The places come from Data/fence_free.json (tools/world_gen/fence_free.py). Noah's prefab, its meshes and
    // his builder are NOT changed: the fence instance inside the campus world gets trimmed copies of its two meshes
    // (what you see, and the invisible wall behind it), and every cut is closed with a post like his.
    // Without the data file or the fence nothing happens.
    public static class FenceFreeZones
    {
        private const string Root = "Assets/_Project/Visuals/CampusWorld";
        private const string DataPath = Root + "/Data/fence_free.json";
        private const string TrackData = Root + "/Data/track.json";
        private const string OutDir = Root + "/Generated/NovaHallCuts";   // NovaHallInsert makes it fresh on every build
        private const string FenceName = "WhiteGuardrails";
        // As in WhiteGuardrailBuilder (Role 2): where the fence stands and what a post looks like.
        private const float EdgeOffset = 0.3f, PostHeight = 0.75f, PostSize = 0.09f, PostBottom = -0.15f;

        [System.Serializable] private class Free { public int from, to, side; public string why; }
        [System.Serializable] private class FreeList { public int points; public Free[] free = new Free[0]; }
        [System.Serializable] private class TrackPoint { public float x, y, z, width; }
        [System.Serializable] private class TrackList { public TrackPoint[] points; }

        private static Vector3[] centre;
        private static float[] width;
        private static bool[] freeRight, freeLeft;

        // Returns a line for the build log, or null when there is nothing to do.
        public static string Apply(GameObject worldRoot)
        {
            if (!File.Exists(DataPath))
            {
                return null;
            }
            Transform fence = null;
            foreach (Transform t in worldRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == FenceName && t.GetComponent<MeshFilter>() != null)
                {
                    fence = t;
                    break;
                }
            }
            if (fence == null)
            {
                return null;
            }
            FreeList list = JsonUtility.FromJson<FreeList>(File.ReadAllText(DataPath));
            TrackPoint[] p = JsonUtility.FromJson<TrackList>(File.ReadAllText(TrackData)).points;
            int n = p.Length;
            if (list.points != n)
            {
                Debug.LogWarning("FenceFreeZones: fence_free.json was made for " + list.points + " route points, track.json has " + n +
                                 ". Run tools/world_gen/fence_free.py after hall_route.py and hall_join.py. Fence left as it is.");
                return null;
            }
            // The fence follows the route as HallDoorApproach lines it up with the hall doors.
            centre = new Vector3[n];
            width = new float[n];
            for (int i = 0; i < n; i++)
            {
                centre[i] = new Vector3(p[i].x, p[i].y, p[i].z);
                width[i] = p[i].width;
            }
            HallDoorApproach.Apply(centre, width, null);
            freeRight = new bool[n];
            freeLeft = new bool[n];
            foreach (Free f in list.free)
            {
                for (int k = Mathf.Max(0, f.from); k < Mathf.Min(n, f.to); k++)
                {
                    freeRight[k] |= f.side >= 0;
                    freeLeft[k] |= f.side <= 0;
                }
            }

            candidates = null;
            MeshFilter mf = fence.GetComponent<MeshFilter>();
            MeshCollider mc = fence.GetComponent<MeshCollider>();
            int cutFence, cutWall = 0, posts;
            Mesh seen = Trim(mf.sharedMesh, fence, true, out cutFence, out posts);
            AssetDatabase.CreateAsset(seen, OutDir + "/WhiteGuardrails_Fence_trimmed.asset");
            mf.sharedMesh = seen;
            if (mc != null && mc.sharedMesh != null)
            {
                Mesh wall = Trim(mc.sharedMesh, fence, false, out cutWall, out _);
                AssetDatabase.CreateAsset(wall, OutDir + "/WhiteGuardrails_Wall_trimmed.asset");
                mc.sharedMesh = wall;
            }
            int pieces = 0;
            for (int k = 0; k < n; k++)
            {
                pieces += (freeRight[k] ? 1 : 0) + (freeLeft[k] ? 1 : 0);
            }
            return "white fence left out on " + pieces + " pieces (" + list.free.Length + " places): " + cutFence + " fence and " + cutWall +
                   " wall triangles off, " + posts + " end posts added";
        }

        // The mesh without the triangles that belong to a free piece; for the visible fence, an end post at every cut.
        private static Mesh Trim(Mesh src, Transform owner, bool addPosts, out int removed, out int posts)
        {
            Vector3[] v = src.vertices;
            int[] tri = src.triangles;
            var keep = new List<int>(tri.Length);
            removed = 0;
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 c = owner.TransformPoint((v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f);
                if (OnFreePiece(c))
                {
                    removed++;
                    continue;
                }
                keep.Add(tri[i]);
                keep.Add(tri[i + 1]);
                keep.Add(tri[i + 2]);
            }
            var verts = new List<Vector3>(v);
            var normals = new List<Vector3>(src.normals);
            var uvs = new List<Vector2>(src.uv);
            bool hasNormals = normals.Count == v.Length, hasUv = uvs.Count == v.Length;
            posts = 0;
            if (addPosts)
            {
                int n = centre.Length;
                for (int k = 0; k < n; k++)
                {
                    int before = (k - 1 + n) % n;
                    foreach (float side in new[] { 1f, -1f })
                    {
                        bool[] free = side > 0 ? freeRight : freeLeft;
                        // Route point k is where a kept piece meets a free one (either way round).
                        // ... and the kept piece really has a fence (none in the tunnel, the hall, at a crossing).
                        int kept = free[k] ? before : k;
                        if (free[k] != free[before] && HadFence(owner, v, tri, kept, side))
                        {
                            AddPost(verts, normals, uvs, keep, owner.InverseTransformPoint(FencePoint(k, side)), hasNormals, hasUv);
                            posts++;
                        }
                    }
                }
            }
            var mesh = new Mesh { name = src.name + "_trimmed", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            if (hasNormals)
            {
                mesh.SetNormals(normals);
            }
            if (hasUv)
            {
                mesh.SetUVs(0, uvs);
            }
            mesh.SetTriangles(keep, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // True if the point belongs to a free piece: the fence piece nearest to it (between the fence points of route
        // points k and k + 1 on one side, exactly where WhiteGuardrailBuilder puts them) is a free one. Measured against
        // the fence points themselves, not the centreline: in front of a hall door HallDoorApproach shifts and narrows
        // the route from point to point, and there a test "so many metres beside the centreline" left pieces standing
        // (a fence across the shortcut's exit at the atrium door: a boosted kart stopped dead on its invisible wall).
        private static bool OnFreePiece(Vector3 c)
        {
            if (candidates == null)
            {
                BuildCandidates();
            }
            float best = Reach * Reach;
            bool free = false;
            foreach (Piece piece in candidates)
            {
                float d = SegmentDistanceSquared(c, piece.a, piece.b);
                // At a shared fence point (a post) a free piece wins over its kept neighbour: a new end post goes there.
                if (d < best - 0.0004f || (d < best + 0.0004f && piece.free && !free))
                {
                    best = Mathf.Min(best, d);
                    free = piece.free;
                }
            }
            return free;
        }

        private struct Piece { public Vector3 a, b; public bool free; }
        private static List<Piece> candidates;
        private const float Reach = 0.55f;    // boards and posts lie on the fence line, the wall up to 0.3 m outside it (and up to 0.9 m up)

        // The free pieces and the kept pieces right before and after each of them.
        private static void BuildCandidates()
        {
            candidates = new List<Piece>();
            int n = centre.Length;
            foreach (float side in new[] { 1f, -1f })
            {
                bool[] free = side > 0 ? freeRight : freeLeft;
                for (int k = 0; k < n; k++)
                {
                    if (free[k] || free[(k + 1) % n] || free[(k - 1 + n) % n])
                    {
                        candidates.Add(new Piece { a = FencePoint(k, side), b = FencePoint((k + 1) % n, side), free = free[k] });
                    }
                }
            }
        }

        // Squared distance from p to the fence piece a-b; heights between the fence's foot and the wall's top count as on it.
        private static float SegmentDistanceSquared(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            Vector3 q = a + ab * t;
            float up = p.y - q.y;
            float dy = up < -0.2f ? up + 0.2f : up > 1.0f ? up - 1.0f : 0f;
            return (p.x - q.x) * (p.x - q.x) + (p.z - q.z) * (p.z - q.z) + dy * dy;
        }

        // True if the untrimmed fence has a board on the piece from route point k to k + 1 on that side: something of
        // the fence lies on the piece's middle half (a post at one of its ends does not count: Noah's fence ends with
        // a post at the tunnel mouth, and the piece inside the tunnel has no fence).
        private static bool HadFence(Transform owner, Vector3[] v, int[] tri, int k, float side)
        {
            Vector3 a = FencePoint(k, side), b = FencePoint((k + 1) % centre.Length, side);
            Vector3 ab = b - a;
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 c = owner.TransformPoint((v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f);
                float t = Vector3.Dot(c - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude);
                if (t > 0.25f && t < 0.75f && SegmentDistanceSquared(c, a, b) < 0.2f * 0.2f)
                {
                    return true;
                }
            }
            return false;
        }

        // WhiteGuardrailBuilder.FencePoint: 0.3 m outside the road edge, direction averaged over 4 points.
        private static Vector3 FencePoint(int i, float side)
        {
            int n = centre.Length;
            Vector3 d = centre[(i + 2) % n] - centre[(i - 2 + n) % n];
            d.y = 0f;
            Vector3 forward = d.sqrMagnitude > 0.000001f ? d.normalized : Vector3.forward;
            return centre[i] + Vector3.Cross(Vector3.up, forward) * (side * (width[i] * 0.5f + EdgeOffset));
        }

        // A square post like WhiteGuardrailBuilder.AddPost (foot a bit below the road).
        private static void AddPost(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris, Vector3 foot,
                                    bool hasNormals, bool hasUv)
        {
            float h = PostSize * 0.5f;
            Vector3 lo = foot + new Vector3(-h, PostBottom, -h), hi = foot + new Vector3(h, PostHeight, h);
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 nrm in dirs)
            {
                Vector3 u = Mathf.Abs(nrm.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(nrm, u);
                Vector3 mid = (lo + hi) * 0.5f + Vector3.Scale(nrm, (hi - lo) * 0.5f);
                Vector3 eu = Vector3.Scale(u, (hi - lo) * 0.5f), ew = Vector3.Scale(w, (hi - lo) * 0.5f);
                int q = verts.Count;
                verts.Add(mid - eu - ew);
                verts.Add(mid + eu - ew);
                verts.Add(mid + eu + ew);
                verts.Add(mid - eu + ew);
                for (int k = 0; k < 4; k++)
                {
                    if (hasNormals)
                    {
                        normals.Add(nrm);
                    }
                    if (hasUv)
                    {
                        uvs.Add(Vector2.zero);
                    }
                }
                // u x w = nrm in Unity's left-handed axes: (0, 1, 2) runs clockwise seen from outside = a front face.
                tris.Add(q); tris.Add(q + 1); tris.Add(q + 2);
                tris.Add(q); tris.Add(q + 2); tris.Add(q + 3);
            }
        }
    }
}
