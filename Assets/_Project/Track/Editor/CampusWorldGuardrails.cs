using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // Puts guardrails along the road of the campus world scene that is open right now (Role 3's Track_CampusWorld
    // or the interior test copy). They copy the look of the railing inside the Nova building ("Hall_Track_Rails"):
    // chunky white posts with a wide lilac band, in the hall model's own two materials.
    // Where they stand comes from the route in Role 3's track.json.
    // There is only ever one kind of guardrail: every other railing along the track is switched off first, in the
    // open scene only, never in anyone's files.
    // Run it again after the world was rebuilt: a rebuild makes a new scene without the rails.
    public static class CampusWorldGuardrails
    {
        private const string DataPath = "Assets/_Project/Visuals/CampusWorld/Data/track.json";
        private const string HallModelPath = "Assets/_Preview/NovaHallWorld.fbx";
        private const string MeshFolder = "Assets/_Project/Track/Meshes/CampusWorld/";
        private const string GroupName = "Guardrails";
        private const string HallRailName = "Hall_Track_Rails";  // the building's own railing along the track
        private const string OtherRailName = "Prop_RailingSegment"; // Role 3's railing pieces

        private const float OutsideOffset = 0.15f; // the rail stands this far outside the road edge
        private const float ClearMargin = 2f;      // railings of Role 3's this near the road are switched off
        private const float PostSize = 0.14f;      // the posts are square and chunky, as in the building
        private const float PostHeight = 1f;
        private const float BandBottom = 0.55f;    // the wide lilac band sits about two thirds up
        private const float BandTop = 0.85f;
        private const float BandThickness = 0.06f;

        // Checking that the lane between the rails is really drivable: step outwards from the middle of the road
        // and stop at the first thing standing in the way (a column, a wall, a planter).
        private const float ScanStep = 0.25f;
        private const float ScanLow = 0.25f;   // ignore the road surface itself
        private const float ScanHigh = 1.4f;   // roughly kart height: what would be hit when driving
        private const float MinHalfWidth = 2f; // under 4 m total the lane is reported as too narrow

        // Same field names as Role 3's track.json, so JsonUtility can read it.
        [System.Serializable]
        private class TrackPoint
        {
            public float x, y, z, width;
            public bool tunnel;
        }

        [System.Serializable]
        private class TrackData
        {
            public TrackPoint[] points;
        }

        [MenuItem("Carcavelos Circuit/Add White Guardrails To Open Campus World (Role 2)")]
        public static void AddToOpenScene()
        {
            if (!File.Exists(DataPath))
            {
                Debug.LogError("CampusWorldGuardrails: " + DataPath + " not found. Open the campus world project first.");
                return;
            }
            TrackData data = JsonUtility.FromJson<TrackData>(File.ReadAllText(DataPath));
            if (data == null || data.points == null || data.points.Length < 3)
            {
                Debug.LogError("CampusWorldGuardrails: no route points in " + DataPath + ".");
                return;
            }
            Material white = HallMaterial("W_N_rail");
            Material lilac = HallMaterial("W_N_rail_lilac");
            if (white == null || lilac == null)
            {
                Debug.LogError("CampusWorldGuardrails: the railing materials of " + HallModelPath + " were not found.");
                return;
            }

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
            RemoveOldGroup(scene);

            int count = data.points.Length;
            Vector3[] centre = new Vector3[count];
            float[] width = new float[count];
            bool[] gap = new bool[count];
            for (int i = 0; i < count; i++)
            {
                TrackPoint p = data.points[i];
                centre[i] = new Vector3(p.x, p.y, p.z);
                width[i] = p.width;
                gap[i] = p.tunnel; // only the tunnel keeps no rail: it has walls of its own
            }

            // Exactly one kind of guardrail on the whole map: every other railing along the track goes off first.
            // Role 3's pieces only where they are on or beside the road, the building's track railing completely
            // (ours replaces it and looks the same).
            int switchedOff = ClearOtherRailings(scene, centre, width);

            // Only now, with the other railings gone, measure how far the road is really free on each side.
            Physics.SyncTransforms();
            float[,] half = new float[count, 2];
            int narrowed = 0;
            float worst = float.MaxValue;
            Vector3 worstAt = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                for (int k = 0; k < 2; k++)
                {
                    float wanted = width[i] * 0.5f + OutsideOffset;
                    float free = FreeHalfWidth(centre, i, k == 0 ? -1f : 1f, wanted);
                    half[i, k] = free;
                    if (free < wanted - 0.01f)
                    {
                        narrowed++;
                    }
                }
                float lane = half[i, 0] + half[i, 1];
                if (!gap[i] && lane < worst)
                {
                    worst = lane;
                    worstAt = centre[i];
                }
            }

            EnsureFolder(MeshFolder.TrimEnd('/'));
            NovaBatch batch = new NovaBatch(GroupName, false, true);
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                for (int k = 0; k < 2; k++)
                {
                    float side = k == 0 ? -1f : 1f;
                    if (gap[i])
                    {
                        continue;
                    }
                    Vector3 a = centre[i] + NovaRouteMath.Right(centre, i) * (half[i, k] * side);
                    AddPost(batch, white, a, centre, i);
                    placed++;
                    if (gap[j])
                    {
                        continue;
                    }
                    Vector3 b = centre[j] + NovaRouteMath.Right(centre, j) * (half[j, k] * side);
                    AddBand(batch, lilac, a, b);
                }
            }
            batch.Flush(null, MeshFolder); // Flush makes its own root object called "Guardrails"

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("CampusWorldGuardrails: " + placed + " posts with band in " + scene.name + ", "
                + switchedOff + " other railings switched off, " + narrowed + " rail sides pulled in so the lane stays free."
                + " Narrowest lane " + worst.ToString("F1") + " m at " + worstAt.ToString("F0")
                + ". Save the scene (Cmd+S) to keep this.");
            if (worst < MinHalfWidth * 2f)
            {
                Debug.LogWarning("CampusWorldGuardrails: the lane gets as narrow as " + worst.ToString("F1")
                    + " m at " + worstAt.ToString("F0") + ". That is too tight to drive: the route has to go around"
                    + " what is standing there (route data: " + DataPath + ").");
            }
        }

        // How far the road is free on one side of ring i, at most "wanted" metres. Steps outwards from the middle
        // and stops in front of the first thing a kart would hit, so the lane between the rails is really drivable.
        private static float FreeHalfWidth(Vector3[] centre, int i, float side, float wanted)
        {
            Vector3 right = NovaRouteMath.Right(centre, i) * side;
            Quaternion along = Quaternion.LookRotation(NovaRouteMath.Forward(centre, i), Vector3.up);
            Vector3 size = new Vector3(ScanStep, ScanHigh - ScanLow, ScanStep) * 0.5f;
            float free = 0f;
            while (free + ScanStep <= wanted)
            {
                Vector3 p = centre[i] + right * (free + ScanStep * 0.5f) + Vector3.up * ((ScanLow + ScanHigh) * 0.5f);
                if (Physics.CheckBox(p, size, along, ~0, QueryTriggerInteraction.Ignore))
                {
                    return free; // something is standing here: the rail goes in front of it
                }
                free += ScanStep;
            }
            return wanted;
        }

        // One square white post, turned so its flat sides follow the road.
        private static void AddPost(NovaBatch batch, Material white, Vector3 a, Vector3[] centre, int i)
        {
            Quaternion along = Quaternion.LookRotation(NovaRouteMath.Forward(centre, i), Vector3.up);
            batch.For(white).Box(a + Vector3.up * (PostHeight * 0.5f), new Vector3(PostSize, PostHeight, PostSize), along, 1f);
        }

        // The wide lilac band between two posts.
        private static void AddBand(NovaBatch batch, Material lilac, Vector3 a, Vector3 b)
        {
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 0.01f)
            {
                return;
            }
            Quaternion along = Quaternion.LookRotation(dir, Vector3.up);
            float height = BandTop - BandBottom;
            Vector3 mid = (a + b) * 0.5f + Vector3.up * (BandBottom + height * 0.5f);
            batch.For(lilac).Box(mid, new Vector3(BandThickness, height, dir.magnitude + 0.05f), along, 1f);
        }

        // A material that lives inside the hall model (the model keeps its materials, they are not separate files).
        private static Material HallMaterial(string name)
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(HallModelPath);
            for (int i = 0; i < all.Length; i++)
            {
                Material m = all[i] as Material;
                if (m != null && m.name == name)
                {
                    return m;
                }
            }
            return null;
        }

        // Switches off the railings that are not ours: the building's own track railing everywhere (ours takes its
        // place and looks the same), and Role 3's pieces where they stand on the road or right at its edge.
        private static int ClearOtherRailings(UnityEngine.SceneManagement.Scene scene, Vector3[] centre, float[] width)
        {
            int off = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == GroupName)
                {
                    continue; // our own rails
                }
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    bool isHallRail = t.name.StartsWith(HallRailName);
                    bool isOtherRail = t.name.StartsWith(OtherRailName);
                    if (!isHallRail && !isOtherRail)
                    {
                        continue;
                    }
                    if (isOtherRail)
                    {
                        // Role 3 also has railings far from the track (terraces, stairs); those stay.
                        int i = NovaRouteMath.ClosestIndex(centre, t.position);
                        Vector3 d = centre[i] - t.position;
                        d.y = 0f;
                        if (d.magnitude >= width[i] * 0.5f + ClearMargin)
                        {
                            continue;
                        }
                    }
                    t.gameObject.SetActive(false);
                    off++;
                }
            }
            return off;
        }

        // A second run should replace the rails, not stack a new set on top of the old one.
        private static void RemoveOldGroup(UnityEngine.SceneManagement.Scene scene)
        {
            List<GameObject> old = new List<GameObject>();
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == GroupName)
                {
                    old.Add(go);
                }
            }
            for (int i = 0; i < old.Count; i++)
            {
                Object.DestroyImmediate(old[i]);
            }
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
