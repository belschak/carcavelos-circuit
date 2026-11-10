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
    // Two things are cleared out of the way first, in the open scene only, never in anyone's files:
    // Role 3's own railings that stand on the road, and our rails where the hall already has its own.
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

            // Role 3's world has railings of its own, and some of them stand on the road. Switch those off, so the
            // track is clear and only one railing runs along each edge.
            int switchedOff = ClearRailingsOnTheRoad(scene, centre, width);
            // Inside the building the hall model already has this railing; leave those stretches to it.
            List<Bounds> hallRails = FindHallRails(scene);

            EnsureFolder(MeshFolder.TrimEnd('/'));
            NovaBatch batch = new NovaBatch(GroupName, false, true);
            int placed = 0;
            int leftToHall = 0;
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
                    Vector3 a = RailPoint(centre, width, i, side);
                    if (Covered(a, hallRails))
                    {
                        leftToHall++;
                        continue;
                    }
                    AddPost(batch, white, a, centre, i);
                    if (gap[j])
                    {
                        continue;
                    }
                    Vector3 b = RailPoint(centre, width, j, side);
                    if (Covered(b, hallRails))
                    {
                        continue;
                    }
                    AddBand(batch, lilac, a, b);
                    placed++;
                }
            }
            batch.Flush(null, MeshFolder); // Flush makes its own root object called "Guardrails"

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("CampusWorldGuardrails: " + placed + " rail pieces in " + scene.name + ", "
                + switchedOff + " of Role 3's railings switched off (they stood on the road), "
                + leftToHall + " spots left to the building's own railing. Save the scene (Cmd+S) to keep this.");
        }

        // The point on the road edge (pushed slightly outwards) at ring i.
        private static Vector3 RailPoint(Vector3[] centre, float[] width, int i, float side)
        {
            Vector3 right = NovaRouteMath.Right(centre, i);
            return centre[i] + right * (width[i] * 0.5f * side + OutsideOffset * side);
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

        // Switches off every railing of Role 3's that stands on the road or right at its edge.
        private static int ClearRailingsOnTheRoad(UnityEngine.SceneManagement.Scene scene, Vector3[] centre, float[] width)
        {
            int off = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == GroupName)
                {
                    continue; // our own rails, not his
                }
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith(OtherRailName))
                    {
                        continue;
                    }
                    int i = NovaRouteMath.ClosestIndex(centre, t.position);
                    Vector3 d = centre[i] - t.position;
                    d.y = 0f;
                    if (d.magnitude < width[i] * 0.5f + ClearMargin)
                    {
                        t.gameObject.SetActive(false);
                        off++;
                    }
                }
            }
            return off;
        }

        // The areas the building's own railing already covers.
        private static List<Bounds> FindHallRails(UnityEngine.SceneManagement.Scene scene)
        {
            List<Bounds> found = new List<Bounds>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.gameObject.activeInHierarchy && r.name.StartsWith(HallRailName))
                    {
                        found.Add(r.bounds);
                    }
                }
            }
            return found;
        }

        // Seen from above, is this spot inside one of those areas?
        private static bool Covered(Vector3 spot, List<Bounds> areas)
        {
            for (int i = 0; i < areas.Count; i++)
            {
                Bounds b = areas[i];
                if (spot.x > b.min.x && spot.x < b.max.x && spot.z > b.min.z && spot.z < b.max.z)
                {
                    return true;
                }
            }
            return false;
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
