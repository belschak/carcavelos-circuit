using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarcavelosCircuit.Shared.Editor
{
    // Task S1: puts the project settings and the empty module scenes in place, the same way on every machine.
    // Menu: Carcavelos Circuit > Project Setup. Command line (Unity closed):
    //   Unity.exe -batchmode -projectPath . -executeMethod CarcavelosCircuit.Shared.Editor.ProjectSetup.RunFromCommandLine -quit
    // Running it again is safe: it never overwrites a scene that already exists.
    public static class ProjectSetup
    {
        // One empty scene per owner (docs/team.md). Main is first in the build, so the game starts there.
        private static readonly string[] Scenes =
        {
            "Assets/_Project/RaceFlow/Scenes/Main.unity",
            "Assets/_Project/Track/Scenes/Track_Campus.unity",
            "Assets/_Project/Driving/Scenes/Driving_TestBed.unity",
            "Assets/_Project/Visuals/Scenes/Visuals_Showroom.unity",
        };

        // Only these go into the build: the game scene and the track it loads.
        private static readonly string[] BuildScenes =
        {
            "Assets/_Project/RaceFlow/Scenes/Main.unity",
            "Assets/_Project/Track/Scenes/Track_Campus.unity",
            "Assets/_Project/RaceFlow/Scenes/RaceFlow_PlaceholderTrack.unity", // Role 4 test loop, added only if it exists
        };

        [MenuItem("Carcavelos Circuit/Project Setup")]
        public static void Run()
        {
            ApplyEditorSettings();
            ApplyPlayerSettings();
            RemoveTemplateExtras();
            CreateScenes();
            SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("ProjectSetup: done.");
        }

        public static void RunFromCommandLine()
        {
            Run();
        }

        // Text assets + visible .meta files = Git can diff and merge them (docs/setup.md).
        private static void ApplyEditorSettings()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorSettings.serializeInlineMappingsOnOneLine = true; // "Reduce version control noise"
            VersionControlSettings.mode = "Visible Meta Files";
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Carcavelos Circuit Team";
            PlayerSettings.productName = "Carcavelos Circuit";
            // Web build as decided in docs/decisions.md: gzip, no decompression fallback.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = false;
        }

        // The Universal 3D template ships a tutorial readme and a sample scene we do not use.
        private static void RemoveTemplateExtras()
        {
            string[] extras = { "Assets/Readme.asset", "Assets/TutorialInfo", "Assets/Scenes" };
            foreach (string path in extras)
            {
                if (AssetDatabase.AssetPathExists(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static void CreateScenes()
        {
            foreach (string path in Scenes)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    continue; // already there: never overwrite someone's work
                }
                EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
                // Camera + light, so each owner can press Play right away.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, path);
            }
        }

        private static void SetBuildScenes()
        {
            var list = new List<EditorBuildSettingsScene>();
            var ours = new List<string>();
            foreach (string path in BuildScenes)
            {
                if (System.IO.File.Exists(path))
                {
                    list.Add(new EditorBuildSettingsScene(path, true));
                    ours.Add(path);
                }
            }
            // Merged, never replaced: keep every scene someone else added (e.g. Track_CampusWorld from the race builder).
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (!ours.Contains(existing.path))
                {
                    list.Add(existing);
                }
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        // Creates "Assets/a/b/c" one level at a time (AssetDatabase needs the parent to exist).
        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
