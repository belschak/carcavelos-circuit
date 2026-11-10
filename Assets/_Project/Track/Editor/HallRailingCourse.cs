using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // T7: copies where the railing inside the Nova hall runs, so the white fence can follow exactly the same course.
    // Select the hall's railing object in the Hierarchy, then Carcavelos Circuit > Copy Hall Railing Course (Role 2).
    // It finds every post of that railing (a small, tall, separate piece of the mesh), links each post to its
    // neighbours and saves posts + links to HallRailingCourse.json. WhiteGuardrailBuilder reads that file.
    public static class HallRailingCourse
    {
        public const string CoursePath = "Assets/_Project/Track/Editor/HallRailingCourse.json";
        private const float MaxPostWidth = 0.5f;   // a post is narrower than this (seen from above) ...
        private const float MinPostHeight = 0.4f;  // ... and taller than this
        private const float MaxLink = 4f;          // two posts further apart than this are never neighbours
        private const string FenceName = "WhiteGuardrails"; // our own fence in the scene, ignored by the floor check

        [System.Serializable]
        public class Course
        {
            public Vector3[] posts = new Vector3[0]; // foot of each post (world position)
            public int[] links = new int[0];          // pairs of post indices: 0-1, 2-3, ... get boards between them
        }

        [MenuItem("Carcavelos Circuit/Copy Hall Railing Course (Role 2)")]
        public static void CopyFromSelection()
        {
            GameObject railing = Selection.activeGameObject;
            if (railing == null)
            {
                Debug.LogError("HallRailingCourse: select the hall railing in the Hierarchy first.");
                return;
            }
            List<Bounds> found = new List<Bounds>();
            foreach (MeshFilter mf in railing.GetComponentsInChildren<MeshFilter>(true))
            {
                FindPosts(mf, found);
            }

            // The white fence replaces it, so switch the old railing off (in the open scene only, undo works).
            // Off first, so the floor check below hits the hall floor and not the old posts.
            Undo.RecordObject(railing, "Switch off hall railing");
            railing.SetActive(false);
            Physics.SyncTransforms();
            List<Vector3> feet = new List<Vector3>();
            for (int i = 0; i < found.Count; i++)
            {
                feet.Add(FloorUnder(found[i]));
            }
            if (feet.Count < 2)
            {
                Debug.LogError("HallRailingCourse: found " + feet.Count + " posts in " + railing.name + ". Is this the railing object?");
                return;
            }
            Course course = new Course();
            course.posts = feet.ToArray();
            course.links = LinkNeighbours(course.posts);
            File.WriteAllText(CoursePath, JsonUtility.ToJson(course, true));
            AssetDatabase.ImportAsset(CoursePath);
            Debug.Log("HallRailingCourse: " + feet.Count + " posts (floor at " + feet[0].y.ToString("F2") + " m), " + (course.links.Length / 2) + " fence pieces saved to " + CoursePath
                + ". " + railing.name + " switched off. Now run Build White Guardrails.");
        }

        public static Course Load()
        {
            if (!File.Exists(CoursePath))
            {
                return new Course();
            }
            return JsonUtility.FromJson<Course>(File.ReadAllText(CoursePath));
        }

        // The old posts reach into the floor, so the white fence stands on the floor right under each post instead.
        // Falls back to the post's own foot if nothing is below it.
        private static Vector3 FloorUnder(Bounds post)
        {
            Vector3 top = new Vector3(post.center.x, post.max.y + 0.5f, post.center.z);
            Vector3 foot = new Vector3(post.center.x, post.min.y, post.center.z);
            bool hitFloor = false;
            foreach (RaycastHit hit in Physics.RaycastAll(top, Vector3.down, post.size.y + 2f))
            {
                if (hit.collider.gameObject.name == FenceName)
                {
                    continue; // our own fence's collision wall is not the floor
                }
                // The highest hit below the start is the floor surface.
                if (!hitFloor || hit.point.y > foot.y)
                {
                    foot = hit.point;
                    hitFloor = true;
                }
            }
            return foot;
        }

        // Splits the mesh into separate pieces (triangles that share corners) and keeps the box of every post-shaped piece.
        private static void FindPosts(MeshFilter mf, List<Bounds> posts)
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh == null)
            {
                return;
            }
            Vector3[] v = mesh.vertices;
            int[] tri = mesh.triangles;
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = mf.transform.TransformPoint(v[i]);
            }

            // Corners at the same spot (1 cm) count as one corner, even if the mesh stores them twice.
            int[] same = new int[v.Length];
            Dictionary<Vector3Int, int> firstAt = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                Vector3Int key = Vector3Int.RoundToInt(v[i] * 100f);
                if (!firstAt.TryGetValue(key, out same[i]))
                {
                    same[i] = i;
                    firstAt[key] = i;
                }
            }

            // Union-find: every triangle glues its three corners into one piece.
            int[] parent = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                parent[i] = i;
            }
            for (int t = 0; t < tri.Length; t += 3)
            {
                Join(parent, same[tri[t]], same[tri[t + 1]]);
                Join(parent, same[tri[t]], same[tri[t + 2]]);
            }

            // One bounding box per piece.
            Dictionary<int, Bounds> pieces = new Dictionary<int, Bounds>();
            for (int i = 0; i < v.Length; i++)
            {
                int root = Root(parent, same[i]);
                if (pieces.TryGetValue(root, out Bounds b))
                {
                    b.Encapsulate(v[i]);
                    pieces[root] = b;
                }
                else
                {
                    pieces[root] = new Bounds(v[i], Vector3.zero);
                }
            }
            foreach (Bounds b in pieces.Values)
            {
                if (b.size.x < MaxPostWidth && b.size.z < MaxPostWidth && b.size.y > MinPostHeight)
                {
                    posts.Add(b);
                }
            }
        }

        // Each post gets a board to its nearest post, and to the nearest one on its other side (so lines never zigzag).
        private static int[] LinkNeighbours(Vector3[] posts)
        {
            List<int> links = new List<int>();
            for (int i = 0; i < posts.Length; i++)
            {
                int first = Nearest(posts, i, -1);
                if (first < 0)
                {
                    continue;
                }
                AddLink(links, i, first);
                int second = Nearest(posts, i, first);
                if (second >= 0)
                {
                    AddLink(links, i, second);
                }
            }
            return links.ToArray();
        }

        // Nearest post to post i within MaxLink. If "away" is given, only posts on the other side of i from "away".
        private static int Nearest(Vector3[] posts, int i, int away)
        {
            int best = -1;
            float bestDistance = MaxLink;
            for (int k = 0; k < posts.Length; k++)
            {
                if (k == i || k == away)
                {
                    continue;
                }
                float d = Vector3.Distance(posts[i], posts[k]);
                if (d >= bestDistance)
                {
                    continue;
                }
                if (away >= 0 && Vector3.Dot(posts[k] - posts[i], posts[away] - posts[i]) > 0f)
                {
                    continue; // same side as the first neighbour
                }
                best = k;
                bestDistance = d;
            }
            return best;
        }

        // Stores the pair once, smaller index first.
        private static void AddLink(List<int> links, int a, int b)
        {
            int low = Mathf.Min(a, b);
            int high = Mathf.Max(a, b);
            for (int k = 0; k < links.Count; k += 2)
            {
                if (links[k] == low && links[k + 1] == high)
                {
                    return;
                }
            }
            links.Add(low);
            links.Add(high);
        }

        private static int Root(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        private static void Join(int[] parent, int a, int b)
        {
            parent[Root(parent, a)] = Root(parent, b);
        }
    }
}
