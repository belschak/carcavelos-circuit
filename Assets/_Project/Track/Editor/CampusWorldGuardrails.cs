using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // Puts the same white guardrails (NovaGuardrail) along the road of the campus world scene that is open right now
    // (Luc's Track_CampusWorld or the interior test copy). Reads only the route numbers from Role 3's track.json,
    // never touches Luc's scene file, his models or CampusWorldBuilder.
    // Run it again after the world was rebuilt: a rebuild makes a new scene without the rails.
    public static class CampusWorldGuardrails
    {
        private const string DataPath = "Assets/_Project/Visuals/CampusWorld/Data/track.json";
        private const string MeshFolder = "Assets/_Project/Track/Meshes/CampusWorld/";
        private const string GroupName = "Guardrails";
        private const int JumpGap = 6; // rings left free before and after the jump, so the ramp stays clear

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
            TrackData data = JsonUtility.FromJson<TrackData>(File.ReadAllText(DataPath));
            if (data == null || data.points == null || data.points.Length < 3)
            {
                Debug.LogError("CampusWorldGuardrails: no route points in " + DataPath + ".");
                return;
            }

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
            RemoveOldGroup(scene);

            Vector3[] centre = new Vector3[data.points.Length];
            float[] width = new float[data.points.Length];
            bool[] gaps = new bool[data.points.Length];
            for (int i = 0; i < data.points.Length; i++)
            {
                TrackPoint p = data.points[i];
                centre[i] = new Vector3(p.x, p.y, p.z);
                width[i] = p.width;
                // No rail in the tunnel (it has walls) and around the jump (the kart flies over the edge there).
                gaps[i] = p.tunnel || Near(i, data.jump_index, data.points.Length, JumpGap);
            }

            EnsureFolder(MeshFolder.TrimEnd('/'));
            NovaLook look = NovaLook.Create();
            NovaBatch batch = new NovaBatch(GroupName, false, true);
            NovaGuardrail.Add(batch, look, centre, width, gaps);

            batch.Flush(null, MeshFolder); // Flush makes its own root object called "Guardrails"
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("CampusWorldGuardrails: guardrails added to " + scene.name + " (" + centre.Length + " route points). Save the scene to keep them.");
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
