using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CarcavelosCircuit.Playtest.Editor
{
    // LOCAL PLAYTEST (Luc, 13 Nov 2026). Builds, by code:
    //   Assets/_Local/Scenes/Launcher.unity  start menu (1 free roam, 2 bot race, quit)
    //   Assets/_Local/Scenes/FreeRoam.unity  Main.unity's camera, look volume and lights, the CampusWorld prefab, the
    //                                        racing line and the boost pads, one kart that drives at once; no race
    // Unity -batchmode -projectPath . -executeMethod CarcavelosCircuit.Playtest.Editor.PlaytestSceneBuilder.Build -quit
    // Main.unity itself is never saved. AddBoostPads() is also used by the bot race scene builder.
    public static class PlaytestSceneBuilder
    {
        public const string LauncherScene = "Assets/_Local/Scenes/Launcher.unity";
        public const string FreeRoamScene = "Assets/_Local/Scenes/FreeRoam.unity";
        private const string MainScene = "Assets/_Project/RaceFlow/Scenes/Main.unity";
        private const string WorldPrefab = "Assets/_Project/Visuals/CampusWorld/CampusWorld.prefab";
        private const string TrackFile = "Assets/_Project/Visuals/CampusWorld/Data/track.json";
        private const string KartPrefab = "Assets/_Project/Driving/Kart.prefab";
        private const string EffectsPrefab = "Assets/_Project/Visuals/KartEffects/Prefabs/KartEffects.prefab";

        [Serializable] private class P { public float x, y, z; }
        [Serializable] private class T { public int spawn_index; public P[] points; }

        [MenuItem("Carcavelos Circuit/Local/Build Playtest Scenes")]
        public static void Build()
        {
            BuildLauncher();
            BuildFreeRoam();
            Debug.Log("PlaytestSceneBuilder: saved " + LauncherScene + " and " + FreeRoamScene);
        }

        private static void BuildLauncher()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.45f, 0.78f);
            new GameObject("Launcher").AddComponent<Launcher>();
            EditorSceneManager.SaveScene(scene, LauncherScene);
        }

        private static void BuildFreeRoam()
        {
            Scene scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            RaceBootstrap bootstrap = UnityEngine.Object.FindAnyObjectByType<RaceBootstrap>();
            var attachments = new List<UnityEngine.Object>();
            if (bootstrap != null)
            {
                SerializedProperty arr = new SerializedObject(bootstrap).FindProperty("kartAttachments");
                for (int i = 0; arr != null && i < arr.arraySize; i++)
                {
                    attachments.Add(arr.GetArrayElementAtIndex(i).objectReferenceValue);
                }
            }
            // keep what you see with (cameras, the look volumes, lights); drop the race (manager, HUD, bootstrap,
            // ghost, items, sounds and music)
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bool seeing = root.GetComponentsInChildren<Camera>(true).Length > 0 || root.GetComponentsInChildren<Volume>(true).Length > 0
                              || root.GetComponentsInChildren<Light>(true).Length > 0
                              || root.GetComponentsInChildren<MonoBehaviour>(true).Any(b => b != null && b.GetType().Name == "CinemachineCamera");
                bool racing = root.GetComponentsInChildren<RaceManager>(true).Length > 0 || root.GetComponentsInChildren<RaceBootstrap>(true).Length > 0
                              || root.GetComponentsInChildren<MonoBehaviour>(true).Any(b => b != null && (b.GetType().Name == "RaceHud" || b.GetType().Name == "GhostPlayer"
                                  || b.GetType().Name == "ItemSystem" || b.GetType().Name == "RaceSounds" || b.GetType().Name == "RaceMusic"));
                if (!seeing || racing)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldPrefab));
            var root2 = new GameObject("FreeRoam");
            GameObject race = RaceParts(root2.transform, out Transform spawn);
            AddBoostPads(race);
            FreeRoam free = root2.AddComponent<FreeRoam>();
            var so = new SerializedObject(free);
            so.FindProperty("kartPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(KartPrefab);
            so.FindProperty("kartEffectsPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(EffectsPrefab);
            SerializedProperty att = so.FindProperty("kartAttachments");
            att.arraySize = attachments.Count;
            for (int i = 0; i < attachments.Count; i++)
            {
                att.GetArrayElementAtIndex(i).objectReferenceValue = attachments[i];
            }
            so.FindProperty("spawn").objectReferenceValue = spawn;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, FreeRoamScene);
        }

        // The racing line (TrackProgress with track.json's road points) and the spawn on it; no checkpoints: no race.
        private static GameObject RaceParts(Transform parent, out Transform spawn)
        {
            T t = JsonUtility.FromJson<T>(File.ReadAllText(TrackFile));
            var race = new GameObject("Race (free roam)");
            race.transform.SetParent(parent, false);
            race.AddComponent<TrackInfo>();
            var pts = t.points.Select(p => new Vector3(p.x, p.y, p.z)).ToList();
            race.AddComponent<TrackProgress>().SetRoadPoints(pts);
            int i = Mathf.Clamp(t.spawn_index, 0, pts.Count - 1);
            Vector3 d = pts[(i + 1) % pts.Count] - pts[i];
            d.y = 0f;
            var s = new GameObject("Spawn");
            s.transform.SetParent(race.transform, false);
            s.transform.SetPositionAndRotation(pts[i] + Vector3.up * 0.4f, Quaternion.LookRotation(d.normalized));
            spawn = s.transform;
            return race;
        }

        // Role 3's boost pads (CampusWorldBuilder.AddBoostPads, task V11) under `race`: the team's builder puts them
        // only into its own preview scene, so the scenes the playtest app drives had none (Luc, 13 Nov).
        public static void AddBoostPads(GameObject race)
        {
            Type builder = Type.GetType("CarcavelosCircuit.Visuals.Editor.CampusWorldBuilder, CarcavelosCircuit.Visuals.Editor");
            Type data = builder?.GetNestedType("TrackData", BindingFlags.NonPublic);
            MethodInfo add = builder?.GetMethod("AddBoostPads", BindingFlags.NonPublic | BindingFlags.Static);
            if (data == null || add == null)
            {
                Debug.LogWarning("PlaytestSceneBuilder: CampusWorldBuilder.AddBoostPads not found, no boost pads.");
                return;
            }
            object track = JsonUtility.FromJson(File.ReadAllText(TrackFile), data);
            Physics.SyncTransforms();
            add.Invoke(null, new[] { race, track });
            Debug.Log("PlaytestSceneBuilder: boost pads added to " + race.name);
        }
    }
}
