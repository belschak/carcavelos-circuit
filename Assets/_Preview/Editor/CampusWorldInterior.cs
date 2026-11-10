// TEST ONLY, never merge into main. Builds a COPY of Luc's Track_CampusWorld scene with the Nova interior inside the
// Edificio Principal. Luc's scene, his models and main stay as they are.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CarcavelosCircuit.Track.Editor;

public static class CampusWorldInterior
{
    private const string SourceScene = "Assets/_Project/Visuals/Scenes/Track_CampusWorld.unity";
    private const string CopyScene = "Assets/_Preview/Track_CampusWorld_Interior.unity";
    private const string HallModel = "Assets/_Preview/NovaHallWorld.fbx";
    private const string CutMeshDir = "Assets/_Preview/CutMeshes";
    private const string BuildingId = "w665606788";          // Edificio Principal in Luc's CampusWorld model
    private const string TrackData = "Assets/_Project/Visuals/CampusWorld/Data/track.json";
    private const string RailingProto = "Prop_RailingSegment"; // Luc's metal railing; the white fence replaces it
    private const float RailingReach = 8f;                     // railings this close to the route are switched off

    // The route before and after HallDoorApproach lined it up with the hall doors (for moving the road mesh).
    [System.Serializable] private class TrackPoint { public float x, y, z, width; }
    [System.Serializable] private class TrackList { public TrackPoint[] points; }
    private static Vector3[] routeBefore, routeAfter;
    private static float[] widthBefore, widthAfter;
    // In front of each hall door Luc's road is taken out and a new straight piece is laid along the lined-up route:
    // route indices first..last (first/last = the point where it joins Luc's road or the point on the facade).
    private static List<int[]> doorPieces;
    private static List<Dictionary<Material, int>> pieceMaterials;
    private static Material roadMaterial;                      // fallback: the first material of Luc's road

    // Edificio Principal footprint (world_data.json, metres; Unity x = east, z = north)
    private static readonly float[] Footprint =
    {
        87.57f, 119.96f, 91.35f, 121.38f, 86.46f, 133.84f, 78.31f, 154.61f, 76.70f, 158.71f, 66.58f, 184.49f, 8.31f, 161.93f,
        2.75f, 159.79f, 7.01f, 148.93f, 8.77f, 144.46f, 10.15f, 140.94f, 16.96f, 123.58f, 27.53f, 96.68f, 28.48f, 93.83f,
        31.60f, 85.29f, 37.26f, 69.74f, 43.19f, 54.42f, 47.66f, 43.32f, 59.21f, 13.85f, 67.64f, -8.38f, 78.73f, -36.01f,
        90.47f, -31.55f, 98.08f, -28.65f, 106.62f, -25.39f, 102.58f, -15.07f, 94.60f, 5.33f, 73.46f, 59.76f, 72.14f, 63.16f,
        69.73f, 75.01f, 67.64f, 74.68f, 65.40f, 80.55f, 63.60f, 85.13f, 63.18f, 86.18f, 54.88f, 107.27f, 58.86f, 108.82f,
        59.36f, 106.16f, 66.33f, 107.80f, 65.68f, 111.46f, 69.48f, 112.88f,
    };

    [MenuItem("Carcavelos Circuit/Build Campus World with Nova interior (test)")]
    public static void Build()
    {
        // Ask to save open changes (choose Don't Save for Luc's scene), then make a fresh copy of his scene.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CopyScene) != null) AssetDatabase.DeleteAsset(CopyScene);
        if (!AssetDatabase.CopyAsset(SourceScene, CopyScene)) { Debug.LogError("Could not copy " + SourceScene); return; }
        Scene scene = EditorSceneManager.OpenScene(CopyScene, OpenSceneMode.Single);
        if (!AssetDatabase.IsValidFolder(CutMeshDir)) AssetDatabase.CreateFolder("Assets/_Preview", "CutMeshes");

        // Switch off the old building block and the old drive-through dressing; cut the ground and the old road out
        // of the footprint so the hall floor is the only floor inside (all in the copy only).
        // The road runs straight into each hall door at the door's width (same rule as the white fence).
        LoadRoute();
        int hidden = 0, cut = 0, railings = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains(BuildingId) || t.name == "Interior_Passages")
                {
                    t.gameObject.SetActive(false);
                    hidden++;
                }
                else if (t.parent != null && t.parent.name == RailingProto && NearRoute(t.position))
                {
                    t.gameObject.SetActive(false);                // one kind of fence only: the white one
                    railings++;
                }
                else if (t.name.StartsWith("World_Ground") || t.name.StartsWith("Track_"))
                {
                    if (CutOutFootprint(t.gameObject)) cut++;
                }
            }
        }

        // The new road pieces in front of the doors, in the material of the road they replace.
        GameObject doorRoads = new GameObject("Hall door roads (test)");
        SceneManager.MoveGameObjectToScene(doorRoads, scene);
        for (int k = 0; k < doorPieces.Count; k++)
        {
            Material m = MostUsed(pieceMaterials[k]);
            MakeDoorRoad(doorRoads.transform, doorPieces[k][0], doorPieces[k][1], m != null ? m : roadMaterial, k);
        }

        // Put the hall in at 0/0/0 (the model already sits at its real place) with colliders on every mesh.
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(HallModel);
        if (model == null) { Debug.LogError(HallModel + " not found (wait for the import)."); return; }
        GameObject hall = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        hall.name = "NovaHall (interior test)";
        foreach (MeshFilter mf in hall.GetComponentsInChildren<MeshFilter>(true))
        {
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            GameObjectUtility.SetStaticEditorFlags(mf.gameObject, StaticEditorFlags.BatchingStatic);
        }

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = hall;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("Campus world with Nova interior: " + CopyScene + " (" + hidden + " old objects switched off, " + cut + " meshes cut, "
            + railings + " metal railings switched off).");
    }

    private static void LoadRoute()
    {
        TrackPoint[] p = JsonUtility.FromJson<TrackList>(System.IO.File.ReadAllText(TrackData)).points;
        routeBefore = new Vector3[p.Length];
        widthBefore = new float[p.Length];
        for (int i = 0; i < p.Length; i++)
        {
            routeBefore[i] = new Vector3(p[i].x, p[i].y, p[i].z);
            widthBefore[i] = p[i].width;
        }
        routeAfter = (Vector3[])routeBefore.Clone();
        widthAfter = (float[])widthBefore.Clone();
        int moved = HallDoorApproach.Apply(routeAfter, widthAfter, null);

        // Runs of moved points; each piece also takes the unmoved point where it joins Luc's road, and ends on the facade.
        roadMaterial = null;
        doorPieces = new List<int[]>();
        pieceMaterials = new List<Dictionary<Material, int>>();
        int n = routeAfter.Length;
        int i = 0;
        while (i < n)
        {
            if (!Moved(i)) { i++; continue; }
            int a = i;
            while (i + 1 < n && Moved(i + 1)) i++;
            int b = i;
            int first = Inside(routeAfter[a - 1].x, routeAfter[a - 1].z) ? a : a - 1;
            int last = Inside(routeAfter[b + 1].x, routeAfter[b + 1].z) ? b : b + 1;
            doorPieces.Add(new[] { first, last });
            pieceMaterials.Add(new Dictionary<Material, int>());
            i++;
        }
        Debug.Log("Campus world with Nova interior: road lined up with the hall doors (" + moved + " route points, "
            + doorPieces.Count + " new road pieces).");
    }

    private static bool Moved(int i)
    {
        return routeAfter[i] != routeBefore[i] || !Mathf.Approximately(widthAfter[i], widthBefore[i]);
    }

    // Which door piece (index into doorPieces) the old road at p belongs to, or -1.
    private static int DoorPieceAt(Vector3 p)
    {
        int n = routeBefore.Length;
        for (int k = 0; k < doorPieces.Count; k++)
        {
            for (int s = doorPieces[k][0]; s < doorPieces[k][1]; s++)
            {
                Vector3 a = routeBefore[s], b = routeBefore[(s + 1) % n];
                Vector2 ab = new Vector2(b.x - a.x, b.z - a.z), ap = new Vector2(p.x - a.x, p.z - a.z);
                float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                if ((ap - ab * t).magnitude < Mathf.Max(widthBefore[s], widthBefore[(s + 1) % n]) / 2 + 1.5f) return k;
            }
        }
        return -1;
    }

    // Straight road piece along the lined-up route from point first to point last, 6 cm above the ground like Luc's.
    private static void MakeDoorRoad(Transform parent, int first, int last, Material mat, int k)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float along = 0f;
        for (int i = first; i <= last; i++)
        {
            Vector3 c = routeAfter[i];
            Vector3 f = routeAfter[Mathf.Min(i + 1, last)] - routeAfter[Mathf.Max(i - 1, first)];
            f.y = 0f;
            Vector3 r = Vector3.Cross(Vector3.up, f.normalized);
            float half = widthAfter[i] / 2;
            if (i > first) along += Vector3.Distance(routeAfter[i - 1], c);
            verts.Add(c - r * half); uvs.Add(new Vector2(-half / 4f, along / 4f));
            verts.Add(c + r * half); uvs.Add(new Vector2(half / 4f, along / 4f));
            if (i > first)
            {
                int q = verts.Count - 4;                          // left/right of the previous and this point
                tris.Add(q); tris.Add(q + 2); tris.Add(q + 3);    // seen from above: left-bottom, left-top, right-top
                tris.Add(q); tris.Add(q + 3); tris.Add(q + 1);    // and left-bottom, right-top, right-bottom
            }
        }
        var mesh = new Mesh { name = "HallDoorRoad_" + k };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        string path = CutMeshDir + "/HallDoorRoad_" + k + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
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
        foreach (var kv in count) if (best == null || kv.Value > count[best]) best = kv.Key;
        return best;
    }

    private static bool NearRoute(Vector3 p)
    {
        for (int i = 0; i < routeAfter.Length; i++)
        {
            if (new Vector2(routeAfter[i].x - p.x, routeAfter[i].z - p.z).sqrMagnitude < RailingReach * RailingReach) return true;
        }
        return false;
    }

    // Copies the mesh without the triangles whose centre lies inside the footprint, saves it as an asset and uses it
    // for the renderer and the collider of this object. Road meshes (Track_...) also lose the bit in front of each hall door.
    private static bool CutOutFootprint(GameObject go)
    {
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;
        Mesh src = mf.sharedMesh;
        Vector3[] v = src.vertices;
        var mesh = new Mesh { name = src.name + "_cut", indexFormat = src.indexFormat };
        mesh.vertices = v;
        mesh.normals = src.normals;
        mesh.uv = src.uv;
        if (src.uv2.Length == v.Length) mesh.uv2 = src.uv2;
        if (src.tangents.Length == v.Length) mesh.tangents = src.tangents;
        if (src.colors.Length == v.Length) mesh.colors = src.colors;
        mesh.subMeshCount = src.subMeshCount;
        int removed = 0;
        for (int s = 0; s < src.subMeshCount; s++)
        {
            int[] tri = src.GetTriangles(s);
            var keep = new List<int>(tri.Length);
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 c = go.transform.TransformPoint((v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f);
                if (Inside(c.x, c.z)) { removed++; continue; }
                int piece = go.name.StartsWith("Track_") ? DoorPieceAt(c) : -1;
                if (piece >= 0)
                {
                    // In front of a hall door: Luc's road goes, the new straight piece replaces it (remember its material).
                    MeshRenderer mr = go.GetComponent<MeshRenderer>();
                    if (go.name.StartsWith("Track_Road") && mr != null && s < mr.sharedMaterials.Length)
                    {
                        if (roadMaterial == null) roadMaterial = mr.sharedMaterials[0];
                        Material m = mr.sharedMaterials[s];
                        pieceMaterials[piece][m] = (pieceMaterials[piece].TryGetValue(m, out int c0) ? c0 : 0) + 1;
                    }
                    removed++;
                    continue;
                }
                keep.Add(tri[i]); keep.Add(tri[i + 1]); keep.Add(tri[i + 2]);
            }
            mesh.SetTriangles(keep, s);
        }
        if (removed == 0) return false;
        mesh.RecalculateBounds();
        string path = CutMeshDir + "/" + go.name + "_cut.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        mf.sharedMesh = mesh;
        MeshCollider mc = go.GetComponent<MeshCollider>();
        if (mc != null) mc.sharedMesh = mesh;
        return true;
    }

    // Point in polygon (ray casting) on the footprint.
    private static bool Inside(float x, float z)
    {
        bool hit = false;
        int n = Footprint.Length / 2;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = Footprint[2 * i], zi = Footprint[2 * i + 1], xj = Footprint[2 * j], zj = Footprint[2 * j + 1];
            if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) hit = !hit;
        }
        return hit;
    }
}
