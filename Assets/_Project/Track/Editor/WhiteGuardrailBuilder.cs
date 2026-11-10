using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // T7 (Role 2): low white wooden-style fence (posts + two flat boards) along both road edges of the campus world.
    // Reads the route from Role 3's track.json and saves everything into Role 2's own folders:
    // the prefab Track/Prefabs/WhiteGuardrails.prefab, its meshes and one white material.
    // The prefab sits at (0, 0, 0): drag it into a scene at the origin and the fence lines up with the road.
    // No fence inside the tunnel (it ends with a post right at the tunnel mouth) and none at the terrace jump.
    // Inside the Nova hall the route data does not fit the building: there the fence follows the course of the hall's
    // own railing (copied by HallRailingCourse), and the outside fence stops where that course begins.
    // Rerun after Role 3 exports a new track.json; the prefab keeps its GUID, so scenes keep their reference.
    public static class WhiteGuardrailBuilder
    {
        private const string DataPath = "Assets/_Project/Visuals/CampusWorld/Data/track.json";
        private const string PrefabFolder = "Assets/_Project/Track/Prefabs";
        private const string MeshFolder = "Assets/_Project/Track/Meshes/Guardrails";
        private const string MaterialPath = "Assets/_Project/Track/Materials/GuardrailWhite.mat";
        private const string PrefabPath = PrefabFolder + "/WhiteGuardrails.prefab";

        // TUNING (metres). Low and slim on purpose: you see it, but it does not shout.
        private const float EdgeOffset = 0.3f;      // fence stands this far outside the road edge
        private const float PostSpacing = 2.5f;     // distance between two posts
        private const float PostHeight = 0.75f;     // top of the posts above the road
        private const float PostSize = 0.09f;       // square posts
        private const float LowBoardY = 0.33f;      // middle of the lower board above the road
        private const float HighBoardY = 0.62f;     // middle of the upper board
        private const float BoardHeight = 0.10f;    // boards are flat planks, not round pipes
        private const float BoardThickness = 0.035f;
        private const float TunnelEndShift = 0f;    // plus = the last post moves further into the tunnel mouth
        private const float WallHeight = 1.0f;      // invisible collision wall behind the fence
        private const float WallThickness = 0.3f;
        private const string JumpZone = "kicker";    // zone name of the terrace jump in track.json
        private const string HallZone = "atrium";    // zone name of the Nova hall in track.json
        private const float HallJoin = 2f;          // the outside fence stops this close to a post of the hall course

        // Same field names as Role 3's track.json, so JsonUtility can read it.
        [System.Serializable]
        private class TrackPoint
        {
            public float x, y, z, width;
            public string zone;
            public bool tunnel;
        }

        [System.Serializable]
        private class TrackData
        {
            public TrackPoint[] points;
        }

        private static TrackPoint[] points;
        private static HallRailingCourse.Course hall;

        [MenuItem("Carcavelos Circuit/Build White Guardrails (Role 2)")]
        public static void Build()
        {
            if (!File.Exists(DataPath))
            {
                Debug.LogError("WhiteGuardrailBuilder: " + DataPath + " not found.");
                return;
            }
            points = JsonUtility.FromJson<TrackData>(File.ReadAllText(DataPath)).points;
            hall = HallRailingCourse.Load();
            EnsureFolder(PrefabFolder);
            EnsureFolder(MeshFolder);

            NovaMesh fence = new NovaMesh();
            NovaMesh wall = new NovaMesh();
            int posts = 0;
            posts += AddSide(fence, wall, 1f);
            posts += AddSide(fence, wall, -1f);
            posts += AddHall(fence, wall);

            Mesh fenceMesh = SaveMesh(fence.ToMesh("WhiteGuardrails_Fence"), MeshFolder + "/WhiteGuardrails_Fence.asset");
            Mesh wallMesh = SaveMesh(wall.ToMesh("WhiteGuardrails_Wall"), MeshFolder + "/WhiteGuardrails_Wall.asset");

            GameObject root = new GameObject("WhiteGuardrails");
            root.AddComponent<MeshFilter>().sharedMesh = fenceMesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = WhiteMaterial();
            root.AddComponent<MeshCollider>().sharedMesh = wallMesh; // simple solid wall, so the kart cannot slip between the boards
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("WhiteGuardrailBuilder: saved " + PrefabPath + " with " + posts + " posts (" + hall.posts.Length
                + " of them in the hall). Drag it into the scene at position 0, 0, 0.");
        }

        // The fence inside the hall: a post where each post of the hall's own railing stands, boards between neighbours.
        private static int AddHall(NovaMesh fence, NovaMesh wall)
        {
            for (int k = 0; k + 1 < hall.links.Length; k += 2)
            {
                Vector3 a = hall.posts[hall.links[k]];
                Vector3 b = hall.posts[hall.links[k + 1]];
                AddBoards(fence, a, b);
                AddWall(wall, a, b, Vector3.zero);
            }
            for (int p = 0; p < hall.posts.Length; p++)
            {
                AddPost(fence, hall.posts[p]);
            }
            return hall.posts.Length;
        }

        // True if p is close to a post of the hall course (there the hall fence takes over).
        private static bool NearHallCourse(Vector3 p)
        {
            for (int k = 0; k < hall.posts.Length; k++)
            {
                if (Vector3.Distance(p, hall.posts[k]) < HallJoin)
                {
                    return true;
                }
            }
            return false;
        }


        // One side of the road (side = +1 right, -1 left, seen in driving direction). Returns the number of posts.
        private static int AddSide(NovaMesh fence, NovaMesh wall, float side)
        {
            int n = points.Length;
            int posts = 0;
            float sinceLastPost = 0f;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if (!HasFence(i, side))
                {
                    continue;
                }
                Vector3 a = FencePoint(i, side);
                Vector3 b = FencePoint(j, side);
                // Into the tunnel mouth: stop exactly where the tunnel starts (plus the tuning shift).
                if (points[j].tunnel)
                {
                    b += Forward(j) * TunnelEndShift;
                }
                if (points[i].tunnel)
                {
                    a -= Forward(i) * TunnelEndShift;
                }
                bool runStarts = !HasFence((i - 1 + n) % n, side);
                bool runEnds = !HasFence(j, side);

                AddBoards(fence, a, b);
                AddWall(wall, a, b, Right(i) * (side * WallThickness * 0.5f)); // wall just outside the fence line
                posts += AddPosts(fence, a, b, runStarts, runEnds, ref sinceLastPost);
            }
            return posts;
        }

        // Posts from a to b: one at the start of a run, then every PostSpacing metres, and one at the end of a run.
        // sinceLastPost carries the distance to the last post over to the next piece. Returns the number of posts.
        private static int AddPosts(NovaMesh fence, Vector3 a, Vector3 b, bool runStarts, bool runEnds, ref float sinceLastPost)
        {
            int posts = 0;
            float length = Vector3.Distance(a, b);
            if (runStarts)
            {
                AddPost(fence, a);
                posts++;
                sinceLastPost = 0f;
            }
            float next = PostSpacing - sinceLastPost;
            while (next < length)
            {
                if (!runEnds || length - next > PostSpacing * 0.4f) // no two posts squeezed together at the run end
                {
                    AddPost(fence, Vector3.Lerp(a, b, next / length));
                    posts++;
                }
                next += PostSpacing;
            }
            sinceLastPost = length - (next - PostSpacing);
            if (runEnds)
            {
                AddPost(fence, b);
                posts++;
            }
            return posts;
        }

        // A piece of fence from point i to i+1 exists if it is not fully inside the tunnel
        // and neither end stands on another part of the road (where the route crosses or touches itself).
        private static bool HasFence(int i, float side)
        {
            int j = (i + 1) % points.Length;
            if (points[i].tunnel && points[j].tunnel)
            {
                return false;
            }
            if (points[i].zone == JumpZone || points[j].zone == JumpZone)
            {
                return false; // the kart flies off the terrace here; a fence would hang in the air
            }
            if (points[i].zone == HallZone || points[j].zone == HallZone)
            {
                return false; // inside the hall the fence follows the hall course instead
            }
            if (NearHallCourse(FencePoint(i, side)) || NearHallCourse(FencePoint(j, side)))
            {
                return false; // the hall course reaches out through the door: no second fence next to it
            }
            return !OnOtherRoad(i, side) && !OnOtherRoad(j, side);
        }

        // True if the fence point of route point i would stand on the road of a part of the route further away.
        private static bool OnOtherRoad(int i, float side)
        {
            Vector3 p = FencePoint(i, side);
            int n = points.Length;
            for (int k = 0; k < n; k++)
            {
                int apart = Mathf.Abs(k - i);
                apart = Mathf.Min(apart, n - apart);
                if (apart < 15)
                {
                    continue; // the road right next to this point is the road the fence belongs to
                }
                Vector3 q = Pos(k);
                float flat = new Vector2(p.x - q.x, p.z - q.z).magnitude;
                if (flat < points[k].width * 0.5f + 0.8f && Mathf.Abs(p.y - q.y) < 3f)
                {
                    return true;
                }
            }
            return false;
        }

        // Two flat white boards between a and b, tilted with the road.
        private static void AddBoards(NovaMesh fence, Vector3 a, Vector3 b)
        {
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 0.0001f)
            {
                return;
            }
            Quaternion along = Quaternion.LookRotation(dir);
            Vector3 mid = (a + b) * 0.5f;
            Vector3 size = new Vector3(BoardThickness, BoardHeight, dir.magnitude + 0.04f); // tiny overlap hides the joints in curves
            fence.Box(mid + Vector3.up * LowBoardY, size, along, 1f);
            fence.Box(mid + Vector3.up * HighBoardY, size, along, 1f);
        }

        // One square post standing upright, its foot a bit below the road so it never floats.
        private static void AddPost(NovaMesh fence, Vector3 foot)
        {
            float bottom = -0.15f;
            Vector3 centre = foot + Vector3.up * ((PostHeight + bottom) * 0.5f);
            fence.Box(centre, new Vector3(PostSize, PostHeight - bottom, PostSize), Quaternion.identity, 1f);
        }

        // Invisible collision wall along the fence, moved sideways by "outward" (only used by the MeshCollider, never drawn).
        private static void AddWall(NovaMesh wall, Vector3 a, Vector3 b, Vector3 outward)
        {
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 0.0001f)
            {
                return;
            }
            Vector3 mid = (a + b) * 0.5f + outward + Vector3.up * (WallHeight * 0.5f - 0.1f);
            wall.Box(mid, new Vector3(WallThickness, WallHeight, dir.magnitude + 0.1f), Quaternion.LookRotation(dir), 1f);
        }

        private static Vector3 FencePoint(int i, float side)
        {
            return Pos(i) + Right(i) * (side * (points[i].width * 0.5f + EdgeOffset));
        }

        private static Vector3 Pos(int i)
        {
            return new Vector3(points[i].x, points[i].y, points[i].z);
        }

        // Flat driving direction at point i, averaged over 4 points so curves stay smooth.
        private static Vector3 Forward(int i)
        {
            int n = points.Length;
            Vector3 d = Pos((i + 2) % n) - Pos((i - 2 + n) % n);
            d.y = 0f;
            return d.sqrMagnitude > 0.000001f ? d.normalized : Vector3.forward;
        }

        private static Vector3 Right(int i)
        {
            return Vector3.Cross(Vector3.up, Forward(i));
        }

        // Created once, then only updated, so its GUID stays the same.
        private static Material WhiteMaterial()
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, MaterialPath);
            }
            m.SetColor("_BaseColor", new Color(0.95f, 0.95f, 0.93f));
            m.SetFloat("_Smoothness", 0.25f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        // Overwrites the mesh asset in place (same GUID) instead of deleting and recreating it.
        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            return existing;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
