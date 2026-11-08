using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow.Editor
{
    // Role 4 smoke test: a Web build of the scenes in the build list into Build-Web/ (ignored by Git).
    // The real release build and upload stay with Role 3 (docs/builds.md). Command line (Unity closed):
    //   Unity.exe -batchmode -projectPath . -buildTarget WebGL -executeMethod CarcavelosCircuit.RaceFlow.Editor.Role4WebBuild.Build -quit
    // Then: python tools/serve_web_build.py  and open http://localhost:8000
    public static class Role4WebBuild
    {
        [MenuItem("Carcavelos Circuit/Web Smoke Build")]
        public static void Build()
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
                locationPathName = "Build-Web",
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("Role4WebBuild: " + report.summary.result + ", " + report.summary.totalSize / (1024 * 1024) + " MB, " + report.summary.totalErrors + " errors.");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
