// LOCAL ONLY (untracked). See ../PlaceholderChaseCamera.cs. When Unity was started to build the bot race scene or the
// bot web build, every time Main.unity is opened the Main Camera gets the adapter component, in memory only (the bot
// scene builder saves a COPY of the scene; Main.unity itself is never saved).
using System;
using CarcavelosCircuit.RaceFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BotRaceCompat
{
    static BotRaceCompat()
    {
        string line = string.Join(" ", Environment.GetCommandLineArgs());
        if (line.Contains("BotRaceSceneBuilder") || line.Contains("BotRaceWebBuild"))
        {
            EditorSceneManager.sceneOpened += OnOpened;
            EditorSceneManager.sceneSaving += OnSaving;
        }
    }

    [Serializable] private class Pt { public float x, y, z; }
    [Serializable] private class Track { public Pt[] points; }

    // V14c: the bot scene builder makes its own track root without Role 2's TrackProgress (T8, #76). Right before the
    // bot scene is saved the root gets one with the road points of track.json (what CampusWorldBuilder does for Q20),
    // so positions, the minimap and the wrong-way check have the real racing line instead of the checkpoint polygon.
    private static void OnSaving(Scene scene, string path)
    {
        if (!path.EndsWith("_Local/Scenes/BotRace.unity")) return;
        CarcavelosCircuit.Track.TrackInfo info = UnityEngine.Object.FindAnyObjectByType<CarcavelosCircuit.Track.TrackInfo>();
        if (info == null || info.GetComponent<CarcavelosCircuit.Track.TrackProgress>() != null) return;
        Track t = JsonUtility.FromJson<Track>(System.IO.File.ReadAllText("Assets/_Project/Visuals/CampusWorld/Data/track.json"));
        var road = new System.Collections.Generic.List<Vector3>();
        foreach (Pt p in t.points) road.Add(new Vector3(p.x, p.y, p.z));
        info.gameObject.AddComponent<CarcavelosCircuit.Track.TrackProgress>().SetRoadPoints(road);
        Debug.Log("BotRaceCompat: TrackProgress with " + road.Count + " road points added to '" + info.name + "'.");
    }

    private static void OnOpened(Scene scene, OpenSceneMode mode)
    {
        if (!scene.path.EndsWith("RaceFlow/Scenes/Main.unity")) return;
        ChaseCameraRig rig = UnityEngine.Object.FindAnyObjectByType<ChaseCameraRig>();
        if (rig != null && rig.GetComponent<PlaceholderChaseCamera>() == null)
        {
            rig.gameObject.AddComponent<PlaceholderChaseCamera>();
            Debug.Log("BotRaceCompat: adapter for the old chase camera added to '" + rig.name + "' (in memory).");
        }
    }
}
