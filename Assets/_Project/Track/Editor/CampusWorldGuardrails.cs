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
    // Railings of his that stand on the road are switched off, so the track is clear and no railing is doubled.
    // That happens in the open scene only: his prefab, his models and his scene file are never edited.
    // Run it again after the world was rebuilt: a rebuild makes a new scene without the rails.
    public static class CampusWorldGuardrails
    {
        private const string DataPath = "Assets/_Project/Visuals/CampusWorld/Data/track.json";
        private const string RailPrefabPath = "Assets/_Project/Visuals/CampusWorld/Prefabs/Prop_RailingSegment.prefab";
        private const string GroupName = "Guardrails";
        private const float OutsideOffset = 0.15f; // the rail stands this far outside the road edge
        private const float ClearMargin = 2f;      // Role 3's own railings this near the road are switched off

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
                gap[i] = p.tunnel; // only the tunnel keeps no rail: it has walls of its own
            }

            // Role 3's world has railings of its own, and some of them stand on the road. Switch those off, so the
            // track is clear and only one railing runs along each edge. Only in this scene, never in his files.
            int switchedOff = ClearRailingsOnTheRoad(scene, centre, width);

            GameObject group = new GameObject(GroupName);
            int placed = 0;
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
            Debug.Log("CampusWorldGuardrails: " + placed + " railings placed in " + scene.name + ", "
                + switchedOff + " of Role 3's own railings switched off because they stood on the road."
                + " Save the scene (Cmd+S) to keep this.");
        }

        // Switches off every railing of Role 3's that stands on the road or right at its edge, so nothing is
        // in the way and no second railing shows up next to ours. His railings elsewhere stay as they are.
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
                    if (!t.name.StartsWith("Prop_RailingSegment"))
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
