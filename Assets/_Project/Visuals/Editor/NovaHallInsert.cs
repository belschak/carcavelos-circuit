using System.Collections.Generic;
using System.IO;
using CarcavelosCircuit.Track.Editor;
using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Visuals.Editor
{
    // Puts Noah's Nova hall (the Edificio Principal from the inside, help PR #45) into the campus world (task V12).
    // Ported from Noah's test-only scene builder (branch test/campus-world-interior-noah, CampusWorldInterior.cs),
    // so the race scene itself gets the hall instead of a copy. CampusWorldBuilder calls Apply once the world, the
    // road and the dressing are in the scene. Steps:
    //   1. switch off the old building block and the old drive-through passages,
    //   2. cut the ground and the old road out of the building's footprint (the hall floor is the only floor inside),
    //   3. in front of each hall door: take out the old road and lay a straight piece along the door axis
    //      (Role 2's HallDoorApproach lines the route up with the doors, the same rule the white fence uses),
    //   4. put the hall model in at 0/0/0 (it already sits at its real place) with a collider on every mesh,
    //   5. Noah's white fence (Role 2, #63, request Q19) at 0/0/0; with it, the metal railings along the road and the
    //      hall's lilac rails go, so the map has one kind of fence. Without the fence prefab both stay.
    // The hall model and HallDoors.json come from Noah's generator: blender -b -P tools/world_gen/nova_hall.py -- world
    public static class NovaHallInsert
    {
        private const string Root = "Assets/_Project/Visuals/CampusWorld";
        private const string HallModel = Root + "/Models/NovaHallWorld.fbx";
        private const string CutMeshParent = Root + "/Generated";
        private const string CutMeshDir = CutMeshParent + "/NovaHallCuts";
        private const string TrackData = Root + "/Data/track.json";
        private const string FencePrefab = "Assets/_Project/Track/Prefabs/WhiteGuardrails.prefab";
        private const string BuildingId = "w665606788";            // Edificio Principal in the CampusWorld model
        private const string Passages = "Interior_Passages";        // the old drive-through dressing in the block
        private const string DressingRoot = "Dressing";             // Luc's props (dressing.json), as Dressing/<type>/<prop>
        private const string RailingProto = "Prop_RailingSegment";  // Luc's metal railing; the white fence replaces it
        private const float RailingReach = 8f;                      // railings this close to the route are switched off
        private const string HallRails = "Hall_Track_Rails";        // the hall's lilac railing; the white fence replaces it

        [System.Serializable] private class TrackPoint { public float x, y, z, width; }
        [System.Serializable] private class TrackList { public TrackPoint[] points; }

        // The route before and after HallDoorApproach lined it up with the hall doors.
        private static Vector3[] routeBefore, routeAfter;
        private static float[] widthBefore, widthAfter;
        // In front of each door: route indices first..last of the new straight piece (first = where it joins the old
        // road, last = the point on the facade), and which road materials it replaces (the most used one is kept).
        private static List<int[]> doorPieces;
        private static List<Dictionary<Material, int>> pieceMaterials;
        private static Material roadMaterial;                       // fallback: the first material of the old road

        // Edificio Principal footprint (Noah's world_data.json, metres; x = east, z = north).
        private static readonly float[] Footprint =
        {
            87.57f, 119.96f, 91.35f, 121.38f, 86.46f, 133.84f, 78.31f, 154.61f, 76.70f, 158.71f, 66.58f, 184.49f, 8.31f, 161.93f,
            2.75f, 159.79f, 7.01f, 148.93f, 8.77f, 144.46f, 10.15f, 140.94f, 16.96f, 123.58f, 27.53f, 96.68f, 28.48f, 93.83f,
            31.60f, 85.29f, 37.26f, 69.74f, 43.19f, 54.42f, 47.66f, 43.32f, 59.21f, 13.85f, 67.64f, -8.38f, 78.73f, -36.01f,
            90.47f, -31.55f, 98.08f, -28.65f, 106.62f, -25.39f, 102.58f, -15.07f, 94.60f, 5.33f, 73.46f, 59.76f, 72.14f, 63.16f,
            69.73f, 75.01f, 67.64f, 74.68f, 65.40f, 80.55f, 63.60f, 85.13f, 63.18f, 86.18f, 54.88f, 107.27f, 58.86f, 108.82f,
            59.36f, 106.16f, 66.33f, 107.80f, 65.68f, 111.46f, 69.48f, 112.88f,
        };

        public static bool HasHall()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(HallModel) != null;
        }

        // True for a point inside the building's footprint (x/z only).
        public static bool InsideHall(Vector3 p)
        {
            return Inside(p.x, p.z);
        }

        // Returns a one-line summary for the build log, or null when the hall model is missing (then nothing changes).
        public static string Apply(GameObject worldRoot)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(HallModel);
            if (model == null)
            {
                Debug.LogWarning("NovaHallInsert: " + HallModel + " not found, the old building block stays.");
                return null;
            }
            GameObject fencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FencePrefab);
            bool fence = fencePrefab != null;

            // Fresh cut meshes on every build, so nothing from an old export is left behind.
            if (AssetDatabase.IsValidFolder(CutMeshDir))
            {
                AssetDatabase.DeleteAsset(CutMeshDir);
            }
            if (!AssetDatabase.IsValidFolder(CutMeshParent))
            {
                AssetDatabase.CreateFolder(Root, "Generated");
            }
            AssetDatabase.CreateFolder(CutMeshParent, "NovaHallCuts");

            LoadRoute();
            int hidden = 0, cut = 0, railings = 0, props = 0;
            foreach (Transform t in worldRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains(BuildingId) || t.name == Passages)
                {
                    t.gameObject.SetActive(false);
                    hidden++;
                }
                // Luc's old atrium furniture (V4b: café tables, chairs, planters, piano) stood on the indoor lanes:
                // every prop whose foot is inside the footprint goes, the hall brings its own interior.
                else if (t.parent != null && t.parent.parent != null && t.parent.parent.name == DressingRoot &&
                         Inside(t.position.x, t.position.z))
                {
                    t.gameObject.SetActive(false);
                    props++;
                }
                else if (fence && t.parent != null && t.parent.name == RailingProto && NearRoute(t.position))
                {
                    t.gameObject.SetActive(false);
                    railings++;
                }
                else if (t.name.StartsWith("World_Ground") || t.name.StartsWith("Track_"))
                {
                    if (CutOutFootprint(t.gameObject))
                    {
                        cut++;
                    }
                }
            }

            var doorRoads = new GameObject("NovaHall door roads");
            doorRoads.transform.SetParent(worldRoot.transform, false);
            for (int k = 0; k < doorPieces.Count; k++)
            {
                Material m = MostUsed(pieceMaterials[k]);
                MakeDoorRoad(doorRoads.transform, doorPieces[k][0], doorPieces[k][1], m != null ? m : roadMaterial, k);
            }

            var hall = (GameObject)PrefabUtility.InstantiatePrefab(model, worldRoot.transform);
            hall.name = "NovaHall (Noah, #45)";
            foreach (MeshFilter mf in hall.GetComponentsInChildren<MeshFilter>(true))
            {
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                GameObjectUtility.SetStaticEditorFlags(mf.gameObject, StaticEditorFlags.BatchingStatic);
                if (fence && mf.gameObject.name == HallRails)
                {
                    mf.gameObject.SetActive(false);
                }
            }

            if (fence)
            {
                var fenceObject = (GameObject)PrefabUtility.InstantiatePrefab(fencePrefab, worldRoot.transform);
                fenceObject.transform.position = Vector3.zero;
            }

            return "Nova hall in: " + hidden + " old objects off, " + props + " old props off, " + cut + " meshes cut, " + doorPieces.Count +
                   " door roads, " + (fence ? "white fence in, " + railings + " metal railings off" : "no white fence prefab, railings stay");
        }

        private static void LoadRoute()
        {
            TrackPoint[] p = JsonUtility.FromJson<TrackList>(File.ReadAllText(TrackData)).points;
            routeBefore = new Vector3[p.Length];
            widthBefore = new float[p.Length];
            for (int k = 0; k < p.Length; k++)
            {
                routeBefore[k] = new Vector3(p[k].x, p[k].y, p[k].z);
                widthBefore[k] = p[k].width;
            }
            routeAfter = (Vector3[])routeBefore.Clone();
            widthAfter = (float[])widthBefore.Clone();
            var facade = new List<int>();
            HallDoorApproach.Apply(routeAfter, widthAfter, null, facade);

            // Runs of moved points; each piece also takes the unmoved point where it joins the old road, and ends on the facade.
            roadMaterial = null;
            doorPieces = new List<int[]>();
            pieceMaterials = new List<Dictionary<Material, int>>();
            int n = routeAfter.Length;
            int i = 0;
            while (i < n)
            {
                if (!Moved(i))
                {
                    i++;
                    continue;
                }
                int a = i;
                while (i + 1 < n && Moved(i + 1))
                {
                    i++;
                }
                int b = i;
                int first = facade.Contains(a) ? a : a - 1;
                int last = facade.Contains(b) ? b : b + 1;
                doorPieces.Add(new[] { first, last });
                pieceMaterials.Add(new Dictionary<Material, int>());
                i++;
            }
        }

        private static bool Moved(int i)
        {
            return routeAfter[i] != routeBefore[i] || !Mathf.Approximately(widthAfter[i], widthBefore[i]);
        }

        // Which door piece the old road at p belongs to, or -1.
        private static int DoorPieceAt(Vector3 p)
        {
            int n = routeBefore.Length;
            for (int k = 0; k < doorPieces.Count; k++)
            {
                for (int s = doorPieces[k][0]; s < doorPieces[k][1]; s++)
                {
                    // The old road along the old line, and anything under the new piece (same height: it would flicker).
                    if (NearSegment(p, routeBefore[s], routeBefore[(s + 1) % n], Mathf.Max(widthBefore[s], widthBefore[(s + 1) % n]) / 2 + 1.5f))
                    {
                        return k;
                    }
                    if (NearSegment(p, routeAfter[s], routeAfter[(s + 1) % n], Mathf.Max(widthAfter[s], widthAfter[(s + 1) % n]) / 2 + 1.5f))
                    {
                        return k;
                    }
                }
            }
            return -1;
        }

        private static bool NearSegment(Vector3 p, Vector3 a, Vector3 b, float reach)
        {
            Vector2 ab = new Vector2(b.x - a.x, b.z - a.z), ap = new Vector2(p.x - a.x, p.z - a.z);
            float t = Vector2.Dot(ap, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude);
            if (t < -0.1f || t > 1.1f)
            {
                return false; // before or after this piece: the old road stays (no gap at the join)
            }
            return (ap - ab * Mathf.Clamp01(t)).magnitude < reach;
        }

        // Straight road piece along the lined-up route from point first to point last.
        private static void MakeDoorRoad(Transform parent, int first, int last, Material mat, int k)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float along = 0f;
            Vector3 lastLeft = Vector3.zero, lastRight = Vector3.zero;
            for (int i = first; i <= last; i++)
            {
                Vector3 c = routeAfter[i];
                Vector3 f = routeAfter[Mathf.Min(i + 1, last)] - routeAfter[Mathf.Max(i - 1, first)];
                f.y = 0f;
                Vector3 r = Vector3.Cross(Vector3.up, f.normalized);
                float half = widthAfter[i] / 2;
                if (i > first)
                {
                    along += Vector3.Distance(routeAfter[i - 1], c);
                }
                Vector3 left = c - r * half, right = c + r * half;
                // On the inside of a tight turn an edge would run backwards for a moment: hold it where it was instead.
                if (i > first && Vector3.Dot(left - lastLeft, f) <= 0f)
                {
                    left = lastLeft;
                }
                if (i > first && Vector3.Dot(right - lastRight, f) <= 0f)
                {
                    right = lastRight;
                }
                lastLeft = left;
                lastRight = right;
                verts.Add(left);
                uvs.Add(new Vector2(-half / 4f, along / 4f));
                verts.Add(right);
                uvs.Add(new Vector2(half / 4f, along / 4f));
                if (i > first)
                {
                    int q = verts.Count - 4;
                    tris.Add(q); tris.Add(q + 2); tris.Add(q + 3); // seen from above: left-bottom, left-top, right-top
                    tris.Add(q); tris.Add(q + 3); tris.Add(q + 1); // and left-bottom, right-top, right-bottom
                }
            }
            var mesh = new Mesh { name = "HallDoorRoad_" + k };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, CutMeshDir + "/HallDoorRoad_" + k + ".asset");
            var go = new GameObject("Door road " + k);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        private static Material MostUsed(Dictionary<Material, int> count)
        {
            Material best = null;
            foreach (KeyValuePair<Material, int> kv in count)
            {
                if (best == null || kv.Value > count[best])
                {
                    best = kv.Key;
                }
            }
            return best;
        }

        private static bool NearRoute(Vector3 p)
        {
            for (int i = 0; i < routeAfter.Length; i++)
            {
                if (new Vector2(routeAfter[i].x - p.x, routeAfter[i].z - p.z).sqrMagnitude < RailingReach * RailingReach)
                {
                    return true;
                }
            }
            return false;
        }

        // Copies the mesh without the triangles whose centre lies inside the footprint, saves it as an asset and uses
        // it for the renderer and the collider. Road meshes (Track_...) also lose the bit in front of each hall door.
        private static bool CutOutFootprint(GameObject go)
        {
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                return false;
            }
            Mesh src = mf.sharedMesh;
            Vector3[] v = src.vertices;
            var mesh = new Mesh { name = src.name + "_cut", indexFormat = src.indexFormat };
            mesh.vertices = v;
            mesh.normals = src.normals;
            mesh.uv = src.uv;
            if (src.uv2.Length == v.Length)
            {
                mesh.uv2 = src.uv2;
            }
            if (src.tangents.Length == v.Length)
            {
                mesh.tangents = src.tangents;
            }
            if (src.colors.Length == v.Length)
            {
                mesh.colors = src.colors;
            }
            mesh.subMeshCount = src.subMeshCount;
            bool road = go.name.StartsWith("Track_");
            int removed = 0;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                int[] tri = src.GetTriangles(s);
                var keep = new List<int>(tri.Length);
                for (int i = 0; i < tri.Length; i += 3)
                {
                    Vector3 c = go.transform.TransformPoint((v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f);
                    if (Inside(c.x, c.z))
                    {
                        removed++;
                        continue;
                    }
                    if (road && (InsideWorld(go, v[tri[i]]) || InsideWorld(go, v[tri[i + 1]]) || InsideWorld(go, v[tri[i + 2]])))
                    {
                        removed++; // road reaching over the facade: same height as the hall floor
                        continue;
                    }
                    int piece = road ? DoorPieceAt(c) : -1;
                    if (piece >= 0)
                    {
                        // In front of a hall door: the old road goes, the new straight piece replaces it (remember its material).
                        MeshRenderer mr = go.GetComponent<MeshRenderer>();
                        if (go.name.StartsWith("Track_Road") && mr != null && s < mr.sharedMaterials.Length)
                        {
                            if (roadMaterial == null)
                            {
                                roadMaterial = mr.sharedMaterials[0];
                            }
                            Material m = mr.sharedMaterials[s];
                            pieceMaterials[piece][m] = (pieceMaterials[piece].TryGetValue(m, out int seen) ? seen : 0) + 1;
                        }
                        removed++;
                        continue;
                    }
                    keep.Add(tri[i]);
                    keep.Add(tri[i + 1]);
                    keep.Add(tri[i + 2]);
                }
                mesh.SetTriangles(keep, s);
            }
            if (removed == 0)
            {
                return false;
            }
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, CutMeshDir + "/" + go.name + "_cut.asset");
            mf.sharedMesh = mesh;
            MeshCollider mc = go.GetComponent<MeshCollider>();
            if (mc != null)
            {
                mc.sharedMesh = mesh;
            }
            return true;
        }

        private static bool InsideWorld(GameObject go, Vector3 local)
        {
            Vector3 w = go.transform.TransformPoint(local);
            return Inside(w.x, w.z);
        }

        // Point in polygon (ray casting) on the footprint.
        private static bool Inside(float x, float z)
        {
            bool hit = false;
            int n = Footprint.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = Footprint[2 * i], zi = Footprint[2 * i + 1], xj = Footprint[2 * j], zj = Footprint[2 * j + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi)
                {
                    hit = !hit;
                }
            }
            return hit;
        }
    }
}
