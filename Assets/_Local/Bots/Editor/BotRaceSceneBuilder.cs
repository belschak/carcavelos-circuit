using System;
using System.IO;
using CarcavelosCircuit.Driving;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarcavelosCircuit.Bots.Editor
{
    // LOCAL PROTOTYPE (bots). Builds Assets/_Local/Scenes/BotRace.unity by code, nothing is edited by hand:
    //   Unity -batchmode -nographics -projectPath . -executeMethod CarcavelosCircuit.Bots.Editor.BotRaceSceneBuilder.Build -quit
    // It opens the team's Main.unity (camera, RaceManager, HUD, sounds, music), swaps RaceBootstrap for BotRace,
    // drops in the CampusWorld prefab and the race parts from track.json, and saves the result as a COPY.
    // Main.unity itself is never saved.
    public static class BotRaceSceneBuilder
    {
        public const string BotScene = "Assets/_Local/Scenes/BotRace.unity";
        private const string MainScene = "Assets/_Project/RaceFlow/Scenes/Main.unity";
        private const string WorldPrefab = "Assets/_Project/Visuals/CampusWorld/CampusWorld.prefab";
        private const string TrackFile = "Assets/_Project/Visuals/CampusWorld/Data/track.json";
        private const string KartPrefab = "Assets/_Project/Driving/Kart.prefab";
        private const string TuningAsset = "Assets/_Project/Driving/Settings/KartTuning.asset";
        private const string EffectsPrefab = "Assets/_Project/Visuals/KartEffects/Prefabs/KartEffects.prefab";
        private const string EngineClip = "Assets/_Project/Audio/Clips/kart_engine_low.wav";
        private const int PlayerSlot = 5; // last place on the grid: the player gets to overtake

        [Serializable] private class TrackPoint { public float x, y, z, width; }
        [Serializable] private class TrackData { public int jump_index; public int[] checkpoints; public TrackPoint[] points; }

        [MenuItem("Carcavelos Circuit/Local/Build Bot Race Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);

            // What RaceBootstrap was wired to in Main: we reuse it and then remove the bootstrap (BotRace does its job).
            RaceBootstrap bootstrap = UnityEngine.Object.FindAnyObjectByType<RaceBootstrap>();
            RaceManager manager = UnityEngine.Object.FindAnyObjectByType<RaceManager>();
            PlaceholderChaseCamera chase = UnityEngine.Object.FindAnyObjectByType<PlaceholderChaseCamera>();
            SerializedProperty oldAttachments = new SerializedObject(bootstrap).FindProperty("kartAttachments");
            UnityEngine.Object[] attachments = new UnityEngine.Object[oldAttachments.arraySize];
            for (int i = 0; i < attachments.Length; i++)
            {
                attachments[i] = oldAttachments.GetArrayElementAtIndex(i).objectReferenceValue;
            }
            UnityEngine.Object.DestroyImmediate(bootstrap);

            // The ghost replays a one-lap time trial; it makes no sense in a three-lap race against bots.
            GhostPlayer ghost = UnityEngine.Object.FindAnyObjectByType<GhostPlayer>();
            if (ghost != null)
            {
                UnityEngine.Object.DestroyImmediate(ghost.gameObject);
            }

            // The world: Role 3's prefab, as Role 2 puts it into Track_Campus.
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldPrefab));

            TrackData data = JsonUtility.FromJson<TrackData>(File.ReadAllText(TrackFile));
            GameObject root = new GameObject("BotRace");

            // Racing line for the bots, baked from track.json.
            BotRoute route = root.AddComponent<BotRoute>();
            route.points = new Vector3[data.points.Length];
            route.widths = new float[data.points.Length];
            for (int i = 0; i < data.points.Length; i++)
            {
                route.points[i] = new Vector3(data.points[i].x, data.points[i].y, data.points[i].z);
                route.widths[i] = data.points[i].width;
            }
            route.checkpointAt = data.checkpoints;
            route.jumpIndex = data.jump_index;
            // Racing line at the two corners where the bots lost the most time (sector times): the tight bend on the
            // coastal road and the last corner before the start/finish line.
            route.lineApex = new[] { 246, 660 };
            route.lineHalfLength = new[] { 9, 8 };
            route.lineMetres = new[] { 1.4f, 1.4f };
            // The one corner a drifting bot may drift: the long left on the upper plaza, with a straight after it.
            route.driftFrom = 161;
            route.driftTo = 176;
            float spacing = 0f;
            for (int i = 0; i < route.points.Length; i++)
            {
                spacing += Vector3.Distance(route.points[i], route.points[(i + 1) % route.points.Length]);
            }
            spacing /= route.points.Length;

            // Six grid places in two columns behind the start line (route index 0), front row first.
            Transform[] grid = new Transform[6];
            GameObject gridRoot = new GameObject("Grid");
            gridRoot.transform.SetParent(root.transform, false);
            for (int i = 0; i < grid.Length; i++)
            {
                int row = i / 2;
                bool rightColumn = i % 2 == 1;
                float metresBehindLine = 3f + row * 4.5f + (rightColumn ? 2.25f : 0f); // right column half a row back
                float routePos = route.points.Length - metresBehindLine / spacing;
                int index = route.Wrap(Mathf.RoundToInt(routePos));
                GameObject place = new GameObject("Grid " + (i + 1));
                place.transform.SetParent(gridRoot.transform, false);
                place.transform.SetPositionAndRotation(
                    route.PointAt(routePos) + route.Right(index) * (rightColumn ? 1.5f : -1.5f) + Vector3.up * 0.1f,
                    Quaternion.LookRotation(route.Forward(index)));
                grid[i] = place.transform;
            }

            // Role 2's track parts (TrackInfo + Checkpoint with a respawn point each), as CampusWorldBuilder makes them.
            GameObject trackObject = new GameObject("Track (Role 2 contract)");
            trackObject.transform.SetParent(root.transform, false);
            TrackInfo trackInfo = trackObject.AddComponent<TrackInfo>();
            int count = data.checkpoints.Length;
            Checkpoint[] checkpoints = new Checkpoint[count];
            for (int c = 0; c < count; c++)
            {
                int i = data.checkpoints[c];
                GameObject cp = new GameObject(c == 0 ? "Checkpoint 0 (start-finish)" : "Checkpoint " + c);
                cp.transform.SetParent(trackObject.transform, false);
                cp.transform.SetPositionAndRotation(route.points[i] + Vector3.up * 2f, Quaternion.LookRotation(route.Forward(i)));
                BoxCollider trigger = cp.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(route.widths[i] + 4f, 6f, 1f);
                GameObject respawn = new GameObject("RespawnPoint");
                respawn.transform.SetParent(cp.transform, false);
                respawn.transform.SetPositionAndRotation(route.points[route.Wrap(i + 2)] + Vector3.up * 0.1f,
                    Quaternion.LookRotation(route.Forward(i + 2)));
                Checkpoint checkpoint = cp.AddComponent<Checkpoint>();
                SerializedObject cso = new SerializedObject(checkpoint);
                cso.FindProperty("index").intValue = c;
                cso.FindProperty("respawnPoint").objectReferenceValue = respawn.transform;
                cso.ApplyModifiedPropertiesWithoutUndo();
                checkpoints[c] = checkpoint;
            }
            SerializedObject tso = new SerializedObject(trackInfo);
            SetArray(tso.FindProperty("checkpoints"), checkpoints);
            tso.FindProperty("spawnPoint").objectReferenceValue = grid[PlayerSlot];
            tso.ApplyModifiedPropertiesWithoutUndo();

            // What the RaceManager on this branch understands (PlaceholderTrack). Its triggers are switched OFF:
            // BotRace reports the player's checkpoints itself, so the race can have three laps.
            GameObject managerTrackObject = new GameObject("Track for RaceManager (triggers off)");
            managerTrackObject.transform.SetParent(root.transform, false);
            PlaceholderTrack managerTrack = managerTrackObject.AddComponent<PlaceholderTrack>();
            GameObject playerRespawn = new GameObject("Player RespawnPoint (moved by BotRace)");
            playerRespawn.transform.SetParent(managerTrackObject.transform, false);
            playerRespawn.transform.SetPositionAndRotation(grid[PlayerSlot].position, grid[PlayerSlot].rotation);
            PlaceholderCheckpoint[] managerCheckpoints = new PlaceholderCheckpoint[count];
            for (int c = 0; c < count; c++)
            {
                GameObject cp = new GameObject("RaceManager checkpoint " + c);
                cp.transform.SetParent(managerTrackObject.transform, false);
                cp.transform.SetPositionAndRotation(checkpoints[c].transform.position, checkpoints[c].transform.rotation);
                BoxCollider trigger = cp.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.enabled = false;
                PlaceholderCheckpoint checkpoint = cp.AddComponent<PlaceholderCheckpoint>();
                SerializedObject cso = new SerializedObject(checkpoint);
                cso.FindProperty("index").intValue = c;
                cso.FindProperty("respawnPoint").objectReferenceValue = playerRespawn.transform;
                cso.ApplyModifiedPropertiesWithoutUndo();
                managerCheckpoints[c] = checkpoint;
            }
            SerializedObject mso = new SerializedObject(managerTrack);
            SetArray(mso.FindProperty("checkpoints"), managerCheckpoints);
            mso.FindProperty("spawnPoint").objectReferenceValue = grid[PlayerSlot];
            mso.ApplyModifiedPropertiesWithoutUndo();

            // The bot race itself.
            BotRace race = root.AddComponent<BotRace>();
            SerializedObject rso = new SerializedObject(race);
            rso.FindProperty("raceManager").objectReferenceValue = manager;
            rso.FindProperty("chaseCamera").objectReferenceValue = chase;
            rso.FindProperty("kartPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(KartPrefab);
            SetArray(rso.FindProperty("playerAttachments"), attachments);
            rso.FindProperty("raceManagerTrack").objectReferenceValue = managerTrack;
            rso.FindProperty("playerRespawn").objectReferenceValue = playerRespawn.transform;
            rso.FindProperty("trackInfo").objectReferenceValue = trackInfo;
            rso.FindProperty("route").objectReferenceValue = route;
            rso.FindProperty("tuning").objectReferenceValue = AssetDatabase.LoadAssetAtPath<KartTuning>(TuningAsset);
            SetArray(rso.FindProperty("gridSlots"), grid);
            rso.FindProperty("playerSlot").intValue = PlayerSlot;
            rso.FindProperty("kartEffectsPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(EffectsPrefab);
            rso.FindProperty("botEngineClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(EngineClip);
            SerializedProperty bots = rso.FindProperty("bots");
            bots.arraySize = 5;
            //            name        colour                              top    corner noise react lane   closing extraClear mistake
            // Different strengths: Gaivota is the best all-rounder, Bica is quick on the straights but loses in corners,
            // Onda is the other way round, Pastel and Sardinha are a little slower everywhere.
            SetBot(bots, 0, "Gaivota",  new Color(0.86f, 0.12f, 0.12f), 0.995f, 0.935f, 0.20f, 0.10f, -0.3f, 0.3f, 0.0f, 0.25f, true);  // aggressive, drifts the marked corner
            SetBot(bots, 1, "Onda",     new Color(0.10f, 0.35f, 0.90f), 0.995f, 0.96f, 0.30f, 0.14f, 0.4f, 0.5f, 0.0f, 0.18f);   // consistent, corner specialist
            SetBot(bots, 2, "Bica",     new Color(0.50f, 0.20f, 0.80f), 1.000f, 0.93f, 0.35f, 0.18f, 0.0f, 0.3f, 0.0f, 0.25f, true);  // aggressive, straight-line speed
            SetBot(bots, 3, "Pastel",   new Color(0.98f, 0.75f, 0.08f), 0.985f, 0.93f, 0.45f, 0.22f, -0.5f, 0.7f, 0.4f, 0.20f);  // careful
            SetBot(bots, 4, "Sardinha", new Color(0.10f, 0.65f, 0.30f), 0.985f, 0.94f, 0.40f, 0.28f, 0.4f, 0.5f, 0.0f, 0.18f);   // consistent
            rso.ApplyModifiedPropertiesWithoutUndo();

            BotRaceOverlay overlay = root.AddComponent<BotRaceOverlay>();
            SerializedObject oso = new SerializedObject(overlay);
            oso.FindProperty("race").objectReferenceValue = race;
            oso.ApplyModifiedPropertiesWithoutUndo();

            BotRaceTest test = root.AddComponent<BotRaceTest>();
            SerializedObject xso = new SerializedObject(test);
            xso.FindProperty("race").objectReferenceValue = race;
            xso.FindProperty("raceCamera").objectReferenceValue = chase != null ? chase.GetComponent<Camera>() : null;
            xso.ApplyModifiedPropertiesWithoutUndo();

            if (manager == null || chase == null || rso.FindProperty("kartPrefab").objectReferenceValue == null
                || rso.FindProperty("tuning").objectReferenceValue == null)
            {
                throw new Exception("BotRaceSceneBuilder: something from Main.unity or the Driving folder was not found.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(BotScene));
            EditorSceneManager.SaveScene(scene, BotScene, true); // true = save a copy, Main.unity stays as it is
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(BotScene, OpenSceneMode.Single); // drop the changed (unsaved) Main from memory
            Debug.Log("BotRaceSceneBuilder: saved " + BotScene + " (" + route.points.Length + " route points, "
                + count + " checkpoints, 6 grid places).");
        }

        private static void SetArray(SerializedProperty list, UnityEngine.Object[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void SetBot(SerializedProperty bots, int i, string name, Color colour, float topSpeed, float cornering,
            float lineNoise, float reaction, float lane, float passClosingSpeed, float extraClearSeconds, float mistakeChance, bool drifts = false)
        {
            SerializedProperty bot = bots.GetArrayElementAtIndex(i);
            bot.FindPropertyRelative("name").stringValue = name;
            bot.FindPropertyRelative("colour").colorValue = colour;
            bot.FindPropertyRelative("topSpeed").floatValue = topSpeed;
            bot.FindPropertyRelative("cornering").floatValue = cornering;
            bot.FindPropertyRelative("lineNoise").floatValue = lineNoise;
            bot.FindPropertyRelative("reaction").floatValue = reaction;
            bot.FindPropertyRelative("lane").floatValue = lane;
            bot.FindPropertyRelative("passClosingSpeed").floatValue = passClosingSpeed;
            bot.FindPropertyRelative("extraClearSeconds").floatValue = extraClearSeconds;
            bot.FindPropertyRelative("mistakeChance").floatValue = mistakeChance;
            bot.FindPropertyRelative("drifts").boolValue = drifts;
        }
    }
}
