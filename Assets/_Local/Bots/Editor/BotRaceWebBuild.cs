using System.IO;
using System.Reflection;
using CarcavelosCircuit.RaceFlow.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarcavelosCircuit.Bots.Editor
{
    // LOCAL PROTOTYPE (bots). Web build of BotRace.unity alone into Build-Web-Bots/ (same pattern as WebPerfBuild):
    //   Unity -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod CarcavelosCircuit.Bots.Editor.BotRaceWebBuild.Build -quit
    // Uses the team's Web settings (Role4WebBuild.ApplyWebSettings, uncompressed) and Web URP asset
    // (Role4WebQuality.SetUp). The build list (EditorBuildSettings) is not touched, and the editor's render pipeline
    // is put back afterwards, as the team build does.
    public static class BotRaceWebBuild
    {
        private const string OutputFolder = "Build-Web-Bots";
        private const string EditorPipelinePath = "Assets/Settings/PC_RPAsset.asset"; // as in Role4WebBuild

        public static void Build()
        {
            BotRaceSceneBuilder.Build();

            // ApplyWebSettings is private in Role 4's file; calling it by name keeps their file untouched.
            MethodInfo apply = typeof(Role4WebBuild).GetMethod("ApplyWebSettings", BindingFlags.NonPublic | BindingFlags.Static);
            if (apply == null)
            {
                Debug.LogError("BotRaceWebBuild: Role4WebBuild.ApplyWebSettings not found.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            apply.Invoke(null, new object[] { false }); // false = uncompressed

            RenderPipelineAsset webPipeline = Role4WebQuality.SetUp();
            string editorPipelinePath = AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);
            if (string.IsNullOrEmpty(editorPipelinePath) || (webPipeline != null && editorPipelinePath == AssetDatabase.GetAssetPath(webPipeline)))
            {
                editorPipelinePath = EditorPipelinePath;
            }
            if (webPipeline != null)
            {
                GraphicsSettings.defaultRenderPipeline = webPipeline;
            }

            BuildReport report;
            try
            {
                FileUtil.DeleteFileOrDirectory(OutputFolder);
                BuildPlayerOptions options = new BuildPlayerOptions();
                options.scenes = new[] { BotRaceSceneBuilder.BotScene };
                options.locationPathName = OutputFolder;
                options.target = BuildTarget.WebGL;
                options.options = BuildOptions.None; // release, no development player
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(editorPipelinePath);
                AssetDatabase.SaveAssets();
            }

            long bytes = 0;
            if (report.summary.result == BuildResult.Succeeded)
            {
                foreach (string file in Directory.GetFiles(OutputFolder, "*", SearchOption.AllDirectories))
                {
                    bytes += new FileInfo(file).Length;
                }
            }
            Debug.Log("BotRaceWebBuild: " + report.summary.result + ", " + report.summary.totalErrors + " errors, "
                + report.summary.totalWarnings + " warnings, " + report.summary.totalTime.TotalMinutes.ToString("0.0") + " min, "
                + (bytes / 1e6).ToString("0.0") + " MB in " + OutputFolder + ", pipeline " + (webPipeline != null ? webPipeline.name : "none"));
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
