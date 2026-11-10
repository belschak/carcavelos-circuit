using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // Puts guardrails along the road of the campus world scene that is open right now (Role 3's Track_CampusWorld
    // or the interior test copy). They use Role 3's own railing prefab, so they look like every other railing in
    // his world; only where they stand is decided here, from the route in his track.json.
    // Nothing of his is edited: the prefab is only placed, and his own railings stay where they are.
    // Run it again after the world was rebuilt: a rebuild makes a new scene without the rails.
    public static class CampusWorldGuardrails
    {
        private const string DataPath = "Assets/_Project/Visuals/CampusWorld/Data/track.json";
        private const string RailPrefabPath = "Assets/_Project/Visuals/CampusWorld/Prefabs/Prop_RailingSegment.prefab";
        private const string GroupName = "Guardrails";
        private const int JumpGap = 6;         // rings left free before and after the jump, so the ramp stays clear
        private const float TooClose = 2.5f;   // a new railing this near one of Role 3's would look like a double rail
        private const float OutsideOffset = 0.15f; // the rail stands this far outside the road edge

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
            public int jump_index;
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
            GameObject railPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RailPrefabPath);
            if (railPrefab == null)
            {
                Debug.LogError("CampusWorldGuardrails: " + RailPrefabPath + " not found (Role 3's railing prefab).");
                return;
            }
            TrackData data = JsonUtility.FromJson<TrackData>(File.ReadAllText(DataPath));
            if (data == null || data.points == null || data.points.Length < 3)
            {
                Debug.LogError("CampusWorldGuardrails: no route points in " + DataPath + ".");
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
                // No rail in the tunnel (it has walls) and around the jump (the kart flies over the edge there).
                gap[i] = p.tunnel || Near(i, data.jump_index, count, JumpGap);
            }

            // Role 3's world already has railings in places. Leave those spots free instead of standing a
            // second railing next to his.
            List<Vector3> existing = FindExistingRailings(scene);

            GameObject group = new GameObject(GroupName);
            int placed = 0;
            int skipped = 0;
            for (int i = 0; i < count; i++)
            {
                if (gap[i])
                {
                    continue;
                }
                Vector3 right = NovaRouteMath.Right(centre, i);
                for (int k = 0; k < 2; k++)
                {
                    float side = k == 0 ? -1f : 1f;
                    Vector3 spot = centre[i] + right * (width[i] * 0.5f * side + OutsideOffset * side);
                    if (!IsFree(spot, existing))
                    {
                        skipped++;
                        continue;
                    }
                    // The prefab's panel runs along its local x and is thin along its local z, so its forward
                    // has to point away from the road (same as Role 3's own segments).
                    GameObject rail = (GameObject)PrefabUtility.InstantiatePrefab(railPrefab, scene);
                    rail.transform.SetParent(group.transform, true);
                    rail.transform.position = spot;
                    rail.transform.rotation = Quaternion.LookRotation(right * side, Vector3.up);
                    placed++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("CampusWorldGuardrails: " + placed + " railings placed in " + scene.name + " ("
                + existing.Count + " of Role 3's own railings found, " + skipped + " spots left to them)."
                + " Save the scene (Cmd+S) to keep them.");
        }

        // Every railing Role 3's world already placed, as world positions.
        private static List<Vector3> FindExistingRailings(UnityEngine.SceneManagement.Scene scene)
        {
            List<Vector3> found = new List<Vector3>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == GroupName)
                {
                    continue; // our own rails, not his
                }
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("Prop_RailingSegment"))
                    {
                        found.Add(t.position);
                    }
                }
            }
            return found;
        }

        // No railing of Role 3's standing within "TooClose" metres of this spot (height ignored: same storey is enough).
        private static bool IsFree(Vector3 spot, List<Vector3> existing)
        {
            for (int i = 0; i < existing.Count; i++)
            {
                Vector3 d = existing[i] - spot;
                d.y = 0f;
                if (d.sqrMagnitude < TooClose * TooClose)
                {
                    return false;
                }
            }
            return true;
        }

        // True when ring "i" is at most "range" rings away from "target" on the closed loop.
        private static bool Near(int i, int target, int count, int range)
        {
            int d = Mathf.Abs(i - target);
            return Mathf.Min(d, count - d) <= range;
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
    }
}
