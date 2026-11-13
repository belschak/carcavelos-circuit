using System.Collections.Generic;
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
    // V21 (Luc's playtest of 13 Nov 2026, done by Luc's Claude):
    //  - in the zones of KartWallZones (start plaza and hall) the fence is the orange and white Nova Kart wall
    //    (NovaKartWall, child object "NovaKartWalls" of the prefab); everywhere else the white fence stays,
    //  - the wall runs in one straight line from 24 m before the hall's west door to the door frame, on both sides,
    //  - nothing to see in the zones of HiddenFenceZones (terrace deck: the terrace has its own metal railings);
    //    only the invisible collision wall stays there, because those railings have no collider of their own,
    //  - extra walls along the lines of the optional "barriers" list in track.json (child object "NovaKartBarriers").
    // V22w (Luc's reference models, done by Luc's Claude):
    //  - the Nova Kart wall has a new shape (NovaKartWall: foot, orange stripe, rounded top, end pieces),
    //  - barrier lines whose name is in TyreBarriers are a row of tyre stacks instead of the wall
    //    (TrackProps.TyreWall, child object "NovaKartTyres"); their collision wall stays, on the tyres' road side.
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
        private const string HallZone = "atrium";    // zone name of the Nova hall in track.json
        private const string JumpZone = "kicker";    // zone name of the terrace jump in older track.json files
        // Zones where no fence is drawn but the invisible collision wall stays (V21, Luc: "leave out the white fences,
        // there are already fences outside"). The terrace's metal railings stand 0.3 m behind this wall and have no
        // collider, so without the wall a kart would drive through them and fall off the deck.
        private static readonly string[] HiddenFenceZones = { "terrace deck" };
        // Zones where the fence is the Nova Kart wall instead of the white wooden fence. Add a zone name to extend it.
        private static readonly string[] KartWallZones = { "upper plaza", "atrium" };
        private const int StraightPieces = 12;      // the wall is one straight line over this many route points before a hall door
        public const string KartWallName = "NovaKartWalls";       // child object: the walls along the route and in the hall
        public const string BarrierName = "NovaKartBarriers";     // child object: the walls along track.json "barriers"
        public const string TyreName = "NovaKartTyres";           // child object: the tyre stacks along some of those lines
        // Barrier lines (by their name in track.json) that are tyre stacks instead of the Nova Kart wall.
        private static readonly string[] TyreBarriers = { "uturn_outside" };
        private const float HallJoin = 2f;          // the outside fence stops this close to a post of the hall course
        private const float HallReach = 6f;         // ... and then gets one more board to the hall's end post, if this near

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
        private static bool[] inHall;   // route points just inside a hall door (HallDoorApproach)
        private static NovaKartWall kartWall;                     // collects the Nova Kart wall pieces of this build
        private static Dictionary<int, Vector3> straightRight;    // fence points on the straight line to a hall door,
        private static Dictionary<int, Vector3> straightLeft;     // by route point (right and left side of the road)

        [MenuItem("Carcavelos Circuit/Build White Guardrails (Role 2)")]
        public static void Build()
        {
            if (!File.Exists(DataPath))
            {
                Debug.LogError("WhiteGuardrailBuilder: " + DataPath + " not found.");
                return;
            }
            string json = File.ReadAllText(DataPath);
            points = JsonUtility.FromJson<TrackData>(json).points;
            hall = HallRailingCourse.Load();
            LineUpWithHallDoors();
            StraightenDoorApproach();
            EnsureFolder(PrefabFolder);
            EnsureFolder(MeshFolder);

            NovaMesh fence = new NovaMesh();
            NovaMesh wall = new NovaMesh();
            kartWall = new NovaKartWall();
            int posts = 0;
            posts += AddSide(fence, wall, 1f);
            posts += AddSide(fence, wall, -1f);
            posts += AddHall(fence, wall);
            float kartMetres = kartWall.Build();

            // Extra walls from track.json "barriers" (for example the median of the start plaza). They get their own
            // object and their own collision wall, in the middle of the line because karts pass on both sides.
            // A line named in TyreBarriers is a row of tyre stacks instead; its collision wall stands on the side of
            // the tyres that faces the road, because a tyre is wider than the wall.
            NovaKartWall barriers = new NovaKartWall();
            TrackProps.TyreWall tyres = new TrackProps.TyreWall();
            NovaMesh barrierWall = new NovaMesh();
            List<string> barrierNames = new List<string>();
            List<Vector3[]> barrierLines = TrackBarriers.Read(json, barrierNames);
            for (int l = 0; l < barrierLines.Count; l++)
            {
                Vector3[] line = barrierLines[l];
                bool asTyres = System.Array.IndexOf(TyreBarriers, barrierNames[l]) >= 0 && tyres.AddLine(line);
                if (!asTyres)
                {
                    barriers.AddLine(line);
                }
                for (int k = 0; k + 1 < line.Length; k++)
                {
                    Vector3 shift = asTyres ? TowardsRoad(line[k], line[k + 1]) * (TrackProps.TyreRadius + TrackProps.TyreInward - WallThickness * 0.5f) : Vector3.zero;
                    AddWall(barrierWall, line[k], line[k + 1], shift);
                }
            }
            float barrierMetres = barriers.Build();

            Mesh fenceMesh = SaveMesh(fence.ToMesh("WhiteGuardrails_Fence"), MeshFolder + "/WhiteGuardrails_Fence.asset");
            Mesh wallMesh = SaveMesh(wall.ToMesh("WhiteGuardrails_Wall"), MeshFolder + "/WhiteGuardrails_Wall.asset");

            GameObject root = new GameObject("WhiteGuardrails");
            root.AddComponent<MeshFilter>().sharedMesh = fenceMesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = WhiteMaterial();
            root.AddComponent<MeshCollider>().sharedMesh = wallMesh; // simple solid wall, so the kart cannot slip between the boards
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
            if (kartWall.VertexCount > 0)
            {
                // Its collision wall is part of the root's wall mesh, like the white fence's.
                AddChild(root, KartWallName, SaveMesh(kartWall.ToMesh(KartWallName), MeshFolder + "/" + KartWallName + ".asset"),
                    null, NovaKartWall.Material());
            }
            if (barrierWall.VertexCount > 0)
            {
                // The collision wall of all barrier lines, and the walls you see (none if every line is tyres).
                AddChild(root, BarrierName,
                    barriers.VertexCount > 0 ? SaveMesh(barriers.ToMesh(BarrierName), MeshFolder + "/" + BarrierName + ".asset") : null,
                    SaveMesh(barrierWall.ToMesh(BarrierName + "_Wall"), MeshFolder + "/" + BarrierName + "_Wall.asset"), NovaKartWall.Material());
            }
            if (tyres.VertexCount > 0)
            {
                AddChild(root, TyreName, SaveMesh(tyres.ToMesh(TyreName), MeshFolder + "/" + TyreName + ".asset"), null, TrackProps.Material());
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("WhiteGuardrailBuilder: saved " + PrefabPath + " with " + posts + " white fence posts, "
                + kartMetres.ToString("F0") + " m of Nova Kart wall (" + hall.posts.Length + " hall posts followed) and "
                + barrierLines.Count + " barrier lines (" + barrierMetres.ToString("F0") + " m of wall, " + tyres.Stacks
                + " tyre stacks). Drag it into the scene at position 0, 0, 0.");
        }

        // A child of the fence object that draws one mesh (if given) with this material, with its own collider if given.
        private static void AddChild(GameObject root, string name, Mesh mesh, Mesh colliderMesh, Material material)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            if (mesh != null)
            {
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                child.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            if (colliderMesh != null)
            {
                child.AddComponent<MeshCollider>().sharedMesh = colliderMesh;
            }
            GameObjectUtility.SetStaticEditorFlags(child, StaticEditorFlags.BatchingStatic);
        }

        // Flat unit vector at right angles to the piece a-b, pointing to the side where the race road is
        // (towards the nearest route point). Zero if the piece has no length.
        private static Vector3 TowardsRoad(Vector3 a, Vector3 b)
        {
            Vector3 along = b - a;
            along.y = 0f;
            if (along.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }
            Vector3 sideways = Vector3.Cross(Vector3.up, along.normalized);
            Vector3 mid = (a + b) * 0.5f;
            int nearest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                float d = (Pos(i) - mid).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = i;
                }
            }
            return Vector3.Dot(Pos(nearest) - mid, sideways) >= 0f ? sideways : -sideways;
        }

        // V21 (Luc: "the fence is curved for no reason, make it straight up to the entrance"): where the route enters
        // the hall from a Nova Kart wall zone, the wall of each side is one straight line over the last StraightPieces
        // route points, ending on the door frame (the end post of the hall course there, if the hall has one).
        private static void StraightenDoorApproach()
        {
            straightRight = new Dictionary<int, Vector3>();
            straightLeft = new Dictionary<int, Vector3>();
            int n = points.Length;
            for (int door = 0; door < n; door++)
            {
                bool entersHall = points[door].zone != HallZone && points[(door + 1) % n].zone == HallZone;
                if (!entersHall || !IsKartWallZone(points[door].zone))
                {
                    continue;
                }
                int from = (door - StraightPieces + n) % n;
                for (float side = 1f; side >= -1f; side -= 2f)
                {
                    Vector3 start = FencePoint(from, side);
                    Vector3 end = FencePoint(door, side);
                    int post = NearestHallEnd(end);
                    if (post >= 0)
                    {
                        end = hall.posts[post];
                    }
                    Dictionary<int, Vector3> straight = side > 0f ? straightRight : straightLeft;
                    for (int k = 1; k <= StraightPieces; k++)
                    {
                        straight[(from + k) % n] = Vector3.Lerp(start, end, k / (float)StraightPieces);
                    }
                }
            }
        }

        private static bool IsKartWallZone(string zone)
        {
            return System.Array.IndexOf(KartWallZones, zone) >= 0;
        }

        private static bool IsHiddenFenceZone(string zone)
        {
            return System.Array.IndexOf(HiddenFenceZones, zone) >= 0;
        }

        // Where the hall test has door data, the route runs straight into each door at the door's width, so the fence
        // reaches the door frame exactly (the hall test scene moves the road the same way).
        private static void LineUpWithHallDoors()
        {
            Vector3[] centre = new Vector3[points.Length];
            float[] width = new float[points.Length];
            inHall = new bool[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                centre[i] = Pos(i);
                width[i] = points[i].width;
            }
            int moved = HallDoorApproach.Apply(centre, width, inHall);
            for (int i = 0; i < points.Length; i++)
            {
                points[i].x = centre[i].x;
                points[i].z = centre[i].z;
                points[i].width = width[i];
            }
            if (moved > 0)
            {
                Debug.Log("WhiteGuardrailBuilder: route lined up with the hall doors (" + moved + " points).");
            }
        }

        // The fence inside the hall: a post where each post of the hall's own railing stands, boards between neighbours.
        // With the hall in KartWallZones it is the Nova Kart wall along the same course instead (no posts).
        private static int AddHall(NovaMesh fence, NovaMesh wall)
        {
            bool kart = IsKartWallZone(HallZone);
            for (int k = 0; k + 1 < hall.links.Length; k += 2)
            {
                Vector3 a = hall.posts[hall.links[k]];
                Vector3 b = hall.posts[hall.links[k + 1]];
                if (kart)
                {
                    kartWall.AddPiece(a, b);
                }
                else
                {
                    AddBoards(fence, a, b);
                }
                AddWall(wall, a, b, Vector3.zero);
            }
            if (kart)
            {
                return 0;
            }
            for (int p = 0; p < hall.posts.Length; p++)
            {
                AddPost(fence, hall.posts[p]);
            }
            return hall.posts.Length;
        }

        // Board from the outside fence's last post p to the nearest end post of the hall course (a post with one neighbour).
        // kart = this part of the fence is the Nova Kart wall: then a piece of wall instead of the boards.
        // hidden = this part has only the invisible collision wall: then nothing to see here either.
        private static void JoinHall(NovaMesh fence, NovaMesh wall, Vector3 p, bool kart, bool hidden)
        {
            int best = NearestHallEnd(p);
            if (best < 0)
            {
                return;
            }
            if (hidden)
            {
                // collision wall only (below)
            }
            else if (kart)
            {
                kartWall.AddPiece(p, hall.posts[best]);
            }
            else
            {
                AddBoards(fence, p, hall.posts[best]);
            }
            AddWall(wall, p, hall.posts[best], Vector3.zero);
        }

        // The end post of the hall course (a post with one neighbour) nearest to p within HallReach, or -1.
        private static int NearestHallEnd(Vector3 p)
        {
            int best = -1;
            float bestDistance = HallReach;
            for (int k = 0; k < hall.posts.Length; k++)
            {
                float d = Vector3.Distance(p, hall.posts[k]);
                if (d < bestDistance && Neighbours(k) == 1)
                {
                    best = k;
                    bestDistance = d;
                }
            }
            return best;
        }

        // How many boards start or end at hall post k.
        private static int Neighbours(int k)
        {
            int count = 0;
            for (int l = 0; l < hall.links.Length; l++)
            {
                if (hall.links[l] == k)
                {
                    count++;
                }
            }
            return count;
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
            bool[] ok = FenceRuns(side);
            int n = points.Length;
            int posts = 0;
            float sinceLastPost = 0f;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if (!ok[i])
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
                bool runStarts = !ok[(i - 1 + n) % n];
                bool runEnds = !ok[j];

                bool kart = IsKartWallZone(points[i].zone);
                bool hidden = IsHiddenFenceZone(points[i].zone);
                if (hidden)
                {
                    AddWall(wall, a, b, Right(i) * (side * WallThickness * 0.5f)); // collision only, nothing to see
                }
                else if (kart)
                {
                    // The Nova Kart wall is thicker than a board: the collision wall starts at its road-side face.
                    kartWall.AddPiece(a, b);
                    AddWall(wall, a, b, Right(i) * (side * (WallThickness - NovaKartWall.Thickness) * 0.5f));
                }
                else
                {
                    AddBoards(fence, a, b);
                    AddWall(wall, a, b, Right(i) * (side * WallThickness * 0.5f)); // wall just outside the fence line
                    posts += AddPosts(fence, a, b, runStarts, runEnds, ref sinceLastPost);
                }
                // Where the fence stops at the hall course, one more board closes the gap to the hall's end post.
                if (runStarts)
                {
                    JoinHall(fence, wall, a, kart, hidden);
                }
                if (runEnds)
                {
                    JoinHall(fence, wall, b, kart, hidden);
                }
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
        // HasFence for every point of one side, without leftover pieces shorter than 3 m (they look like a kink).
        private static bool[] FenceRuns(float side)
        {
            int n = points.Length;
            bool[] ok = new bool[n];
            for (int i = 0; i < n; i++)
            {
                ok[i] = HasFence(i, side);
            }
            int k = 0;
            while (k < n)
            {
                if (!ok[k])
                {
                    k++;
                    continue;
                }
                int start = k;
                float length = 0f;
                while (k < n && ok[k])
                {
                    length += Vector3.Distance(FencePoint(k, side), FencePoint((k + 1) % n, side));
                    k++;
                }
                if (length < 3f)
                {
                    for (int r = start; r < k; r++)
                    {
                        ok[r] = false;
                    }
                }
            }
            return ok;
        }

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
            if (inHall[i] || inHall[j])
            {
                return false; // just behind a hall door: inside the building already
            }
            if (NearHallCourse(FencePoint(i, side)) || NearHallCourse(FencePoint(j, side)))
            {
                return false; // the hall course reaches out through the door: no second fence next to it
            }
            Vector3 piece = FencePoint(j, side) - FencePoint(i, side);
            if (Vector3.Dot(piece, Pos(j) - Pos(i)) <= 0f)
            {
                return false; // on the inside of a very tight turn the fence line folds back: leave that bit out
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
            Vector3 onStraight;
            if ((side > 0f ? straightRight : straightLeft).TryGetValue(i, out onStraight))
            {
                return onStraight; // on the straight line to a hall door (StraightenDoorApproach)
            }
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

        // Overwrites the mesh asset in place (same GUID). The data is set again through the Mesh API, so Unity also
        // redraws it: copying the serialized data only updated the collider, the old fence stayed on screen.
        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.uv = mesh.uv;
            existing.triangles = mesh.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
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
