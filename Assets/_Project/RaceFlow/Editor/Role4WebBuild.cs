using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.WebGL;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarcavelosCircuit.RaceFlow.Editor
{
    // The Web build of the scenes in the build list into Build-Web/ (ignored by Git).
    // Every Web setting is set here in code before the build, so the result is the same on every laptop
    // (some of them, like Code Optimization, are saved per machine in Library/ and would otherwise differ).
    // Command line (Unity closed):
    //   Unity.exe -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod CarcavelosCircuit.RaceFlow.Editor.Role4WebBuild.Build -quit
    // Add -webHost=itch for a gzip build for itch.io (default: uncompressed, works on GitHub Pages and itch.io).
    // Then: python tools/serve_web_build.py  and open http://localhost:8000
    public static class Role4WebBuild
    {
        private const string OutputFolder = "Build-Web";
        private const string Template = "PROJECT:CarcavelosCircuit"; // Assets/WebGLTemplates/CarcavelosCircuit
        private const string EditorPipelinePath = "Assets/Settings/PC_RPAsset.asset"; // the project's default URP asset in the editor
        private const int InitialMemoryMB = 256; // the game needs about this much; starting small means slow heap growth while loading

        [MenuItem("Carcavelos Circuit/Web Build")]
        public static void Build()
        {
            bool gzipForItch = HasArgument("-webHost=itch");
            ApplyWebSettings(gzipForItch);

            // CI runs only this method, so first rebuild Main.unity and the placeholder kart from what is on main now
            // (Role 1's kart, Role 3's kart model, every merged track scene). Then the build is always the integrated game.
            Role4SceneBuilder.BuildAll();
            Role4SceneBuilder.MergeBuildScenes(AssetDatabase.LoadAssetAtPath<RaceSettings>(Role4SceneBuilder.SettingsPath));

            // The online leaderboard's address and public key from the CI secrets, only for this build (task R10).
            // After BuildAll (which always saves the config empty), before anything else changes the project.
            LeaderboardBuildConfig.FillFromEnvironment();

            // Build with the Web URP asset as the project default, so the build does not also carry the shader variants
            // of the PC asset (Forward+, SSAO). The editor's default is put back afterwards, even if the build fails.
            RenderPipelineAsset webPipeline = Role4WebQuality.SetUp();
            // Remember the editor's asset by its path: the build unloads assets, so a plain reference would be empty afterwards.
            string editorPipelinePath = AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);
            if (string.IsNullOrEmpty(editorPipelinePath) || (webPipeline != null && editorPipelinePath == AssetDatabase.GetAssetPath(webPipeline)))
            {
                editorPipelinePath = EditorPipelinePath; // a build that was killed half-way left the Web asset as the default
            }
            if (webPipeline != null)
            {
                GraphicsSettings.defaultRenderPipeline = webPipeline;
            }

            BuildReport report;
            try
            {
                // Hashed file names change every build, so old files would pile up next to the new ones.
                FileUtil.DeleteFileOrDirectory(OutputFolder);
                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
                    locationPathName = OutputFolder,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None, // release build: no development player, no profiler
                };
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                LeaderboardBuildConfig.Clear(); // the asset in the repo stays empty
                GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(editorPipelinePath);
                AssetDatabase.SaveAssets();
            }

            LogSettings(webPipeline);
            Debug.Log("Role4WebBuild: " + report.summary.result + ", " + report.summary.totalErrors + " errors, "
                + report.summary.totalTime.TotalMinutes.ToString("0.0") + " min.");
            if (report.summary.result == BuildResult.Succeeded)
            {
                LogFileSizes();
            }
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }

        // One line per setting with the reason. These are the values decided in docs/decisions.md (P2).
        private static void ApplyWebSettings(bool gzipForItch)
        {
            NamedBuildTarget web = NamedBuildTarget.WebGL;

            // Size: small WebAssembly. Not "Disk Size with LTO": its link step ran for over an hour on our laptops and then
            // failed without an error message (docs/decisions.md, P2), for only a few percent less download.
            UserBuildSettings.codeOptimization = WasmCodeOptimization.DiskSize;
            PlayerSettings.SetIl2CppCompilerConfiguration(web, Il2CppCompilerConfiguration.Master);
            PlayerSettings.SetIl2CppCodeGeneration(web, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.High); // RaceFlow/link.xml keeps what UI Toolkit needs
            PlayerSettings.stripEngineCode = true;
            // Mesh colliders are cooked at build time, not while the scene loads in the browser (single-threaded there;
            // the campus world has a mesh collider on every building).
            PlayerSettings.bakeCollisionMeshes = true;
            PlayerSettings.WebGL.wasm2023 = true; // native Wasm exceptions + SIMD; Chrome 95+, Firefox 100+, Safari 15.2+
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.showDiagnostics = false;

            // Hosting: uncompressed files work on any static host. GitHub Pages gzips them on the fly (and cannot send the
            // header Unity's own .gz files need); itch.io serves them too. -webHost=itch builds pre-gzipped files instead.
            // The textures themselves are crunched for the Web by WebTextureCrunch (gzip barely shrinks DXT data).
            PlayerSettings.WebGL.compressionFormat = gzipForItch ? WebGLCompressionFormat.Gzip : WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.nameFilesAsHashes = true; // a new build never mixes with old cached files
            PlayerSettings.WebGL.dataCaching = true;       // second visit loads from the browser's IndexedDB cache

            // Loading and running.
            PlayerSettings.WebGL.initialMemorySize = InitialMemoryMB;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
            PlayerSettings.SplashScreen.show = false; // allowed on Unity 6 Personal, saves about 2 s per load
            PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.WebGL, true); // fewer draw calls once track meshes are Static
            PlayerSettings.defaultWebScreenWidth = 1280; // only the starting size; the template makes the canvas fill the window
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.WebGL.template = Template;
            AssetDatabase.SaveAssets();
        }

        private static void LogSettings(RenderPipelineAsset webPipeline)
        {
            NamedBuildTarget web = NamedBuildTarget.WebGL;
            Debug.Log("Role4WebBuild settings: codeOptimization=" + UserBuildSettings.codeOptimization
                + ", il2cppConfig=" + PlayerSettings.GetIl2CppCompilerConfiguration(web)
                + ", il2cppCodeGeneration=" + PlayerSettings.GetIl2CppCodeGeneration(web)
                + ", stripping=" + PlayerSettings.GetManagedStrippingLevel(web)
                + ", stripEngineCode=" + PlayerSettings.stripEngineCode
                + ", wasm2023=" + PlayerSettings.WebGL.wasm2023
                + ", exceptions=" + PlayerSettings.WebGL.exceptionSupport
                + ", compression=" + PlayerSettings.WebGL.compressionFormat
                + ", decompressionFallback=" + PlayerSettings.WebGL.decompressionFallback
                + ", nameFilesAsHashes=" + PlayerSettings.WebGL.nameFilesAsHashes
                + ", dataCaching=" + PlayerSettings.WebGL.dataCaching
                + ", initialMemoryMB=" + PlayerSettings.WebGL.initialMemorySize
                + ", splash=" + PlayerSettings.SplashScreen.show
                + ", staticBatching=" + PlayerSettings.GetStaticBatchingForPlatform(BuildTarget.WebGL)
                + ", template=" + PlayerSettings.WebGL.template
                + ", worldTextures=crunch q50, normals max 512 (WebTextureCrunch v" + new WebTextureCrunch().GetVersion() + ")"
                + ", webPipeline=" + (webPipeline != null ? webPipeline.name : "none (see the error above)"));
        }

        // Prints every file in Build-Web/Build with its size and its gzip size (= roughly what GitHub Pages sends).
        private static void LogFileSizes()
        {
            string[] files = Directory.GetFiles(Path.Combine(OutputFolder, "Build"));
            long totalRaw = 0;
            long totalGzip = 0;
            foreach (string file in files)
            {
                long raw = new FileInfo(file).Length;
                long gzip = file.EndsWith(".gz") ? raw : GzipSize(file);
                totalRaw += raw;
                totalGzip += gzip;
                Debug.Log("Role4WebBuild file: " + Path.GetFileName(file) + "  " + ToMB(raw) + " MB (gzip " + ToMB(gzip) + " MB)");
            }
            Debug.Log("Role4WebBuild size: " + ToMB(totalRaw) + " MB on disk, about " + ToMB(totalGzip) + " MB over the wire (gzip).");
        }

        private static long GzipSize(string file)
        {
            using (FileStream input = File.OpenRead(file))
            using (MemoryStream output = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                {
                    input.CopyTo(gzip);
                }
                return output.Length;
            }
        }

        private static string ToMB(long bytes)
        {
            return (bytes / (1024.0 * 1024.0)).ToString("0.00");
        }

        private static bool HasArgument(string argument)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == argument)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
