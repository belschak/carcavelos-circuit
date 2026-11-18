using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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

            // The menus' display font (Archivo Expanded) as an SDF asset, made here if the project does not have it yet.
            HudArtBuilder.EnsureDisplayFont();

            // CI runs only this method, so first rebuild Main.unity and the placeholder kart from what is on main now
            // (Role 1's kart, Role 3's kart model, every merged track scene). Then the build is always the integrated game.
            Role4SceneBuilder.BuildAll();
            Role4SceneBuilder.MergeBuildScenes(AssetDatabase.LoadAssetAtPath<RaceSettings>(Role4SceneBuilder.SettingsPath));

            // The online leaderboard's address and public key from the CI secrets, only for this build (task R10).
            // After BuildAll (which always saves the config empty), before anything else changes the project.
            LeaderboardBuildConfig.FillFromEnvironment();

            // The online race (M2): only when CI copied the Photon Fusion SDK in and the PHOTON_APP_ID secret is set.
            OnlineBuildConfig.Apply();

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

            BuildSummary report; // a copy: the phone build below destroys the first BuildReport object
            FogStripping fogBefore = FogStripping.Read();
            try
            {
                // Hashed file names change every build, so old files would pile up next to the new ones.
                FileUtil.DeleteFileOrDirectory(OutputFolder);
                string[] scenes = WebScenes();
                phoneDataFile = null;
                worldFiles = null;
                string worldManifest = null;
                if (WorldAsOwnFile(scenes, gzipForItch))
                {
                    // Fog variants: with "Automatic" Unity keeps only the fog modes of the scenes in the player build. The
                    // world (exponential fog) is no longer one of them, so the karts and drivers would lose their fog
                    // variants. Custom = exactly the set the old build had (exponential), for the player and the world.
                    FogStripping.KeepExponentialOnly();
                    worldManifest = BuildWorldBundles();
                    if (worldManifest != null)
                    {
                        scenes = PlayerScenes(scenes);
                    }
                    else
                    {
                        fogBefore.Write(); // no world file: the classic build (world inside the .data), settings as before
                    }
                }
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = OutputFolder,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None, // release build: no development player, no profiler
                    subtarget = (int)WebGLTextureSubtarget.DXT, // desktop GPUs; phones get their own .data below
                    // Code and engine stripping must also keep what only the world file uses (its components).
                    assetBundleManifestPath = worldManifest,
                };
                report = BuildPipeline.BuildPlayer(options).summary;
                if (report.result == BuildResult.Succeeded && !gzipForItch)
                {
                    phoneDataFile = AddPhoneData(options);
                }
                if (report.result == BuildResult.Succeeded && worldManifest != null)
                {
                    worldFiles = CopyWorldBundles(phoneDataFile != null);
                }
            }
            finally
            {
                fogBefore.Write();
                LeaderboardBuildConfig.Clear(); // the asset in the repo stays empty
                OnlineBuildConfig.Clear(); // no App ID file and no CARCAVELOS_FUSION define left behind
                GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(editorPipelinePath);
                AssetDatabase.SaveAssets();
            }

            LogSettings(webPipeline);
            Debug.Log("Role4WebBuild: " + report.result + ", " + report.totalErrors + " errors, "
                + report.totalTime.TotalMinutes.ToString("0.0") + " min.");
            if (report.result == BuildResult.Succeeded)
            {
                StampWorld(OutputFolder, worldFiles);
                StampBuildVersion(OutputFolder, phoneDataFile);
                LogFileSizes();
            }
            if (Application.isBatchMode && report.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }

        // ---------- Phones: the same game with ETC2 textures (load time, 17 Nov) ----------
        // Phone GPUs (iPhone, Android) have no DXT, so Unity unpacked every DXT texture on the CPU while loading: measured
        // +9 s to "Tap to play" and +20 s until the track is there on a 4x slower CPU, and 4-8x the texture memory.
        // Unity's documented answer (Manual: Web > Texture compression) is a second build with another texture format
        // whose .data sits next to the first; index.html picks it when the GPU has ETC2 but no DXT. ETC2 can be crunched
        // like DXT, so the phone download stays the same size. Code files must be byte-identical (same hashed names),
        // otherwise the phone .data is left out and phones keep the DXT one (a warning in the build log).
        private const string PhoneFolder = "Build-Web-ETC2";
        // Unity's build backend (Bee) can delete the first build's files as "no longer in use" while it makes the second
        // one (CI 18 Nov 2026, Library from the cache: "Delete 5 artifact files ... (like Build-Web/Build)", then the
        // build failed in LogFileSizes and the phone data was wrongly called different). So a plain copy of the desktop
        // build waits here during the phone build and is put back afterwards; Bee does not know this folder.
        public const string DesktopKeepFolder = "Build-Web-DXT-keep";
        private static string phoneDataFile;

        private static string AddPhoneData(BuildPlayerOptions desktop)
        {
            FileUtil.DeleteFileOrDirectory(PhoneFolder);
            FileUtil.DeleteFileOrDirectory(DesktopKeepFolder);
            FileUtil.CopyFileOrDirectory(OutputFolder, DesktopKeepFolder);
            BuildPlayerOptions phone = desktop;
            phone.locationPathName = PhoneFolder;
            phone.subtarget = (int)WebGLTextureSubtarget.ETC2;
            BuildSummary report;
            try
            {
                report = BuildPipeline.BuildPlayer(phone).summary;
            }
            finally
            {
                // the desktop build exactly as it was, whatever the phone build did to Build-Web
                FileUtil.DeleteFileOrDirectory(OutputFolder);
                FileUtil.MoveFileOrDirectory(DesktopKeepFolder, OutputFolder);
            }
            try
            {
                if (report.result != BuildResult.Succeeded)
                {
                    Debug.LogWarning("Role4WebBuild: the ETC2 build for phones failed (" + report.totalErrors + " errors); phones use the DXT data.");
                    return null;
                }
                string desktopBuild = Path.Combine(OutputFolder, "Build");
                string phoneBuild = Path.Combine(PhoneFolder, "Build");
                string phoneData = null;
                foreach (string file in Directory.GetFiles(phoneBuild))
                {
                    string name = Path.GetFileName(file);
                    if (name.EndsWith(".data"))
                    {
                        phoneData = name;
                    }
                    else if (!File.Exists(Path.Combine(desktopBuild, name)))
                    {
                        Debug.LogWarning("Role4WebBuild: the ETC2 build's " + name + " differs from the desktop build; phones use the DXT data.");
                        return null;
                    }
                }
                if (phoneData == null || File.Exists(Path.Combine(desktopBuild, phoneData)))
                {
                    Debug.LogWarning("Role4WebBuild: no separate ETC2 .data was made; phones use the DXT data.");
                    return null;
                }
                File.Copy(Path.Combine(phoneBuild, phoneData), Path.Combine(desktopBuild, phoneData));
                Debug.Log("Role4WebBuild: ETC2 data for phones added: " + phoneData);
                return phoneData;
            }
            finally
            {
                FileUtil.DeleteFileOrDirectory(PhoneFolder);
            }
        }

        // ---------- The campus world as its own file (load time P8, 18 Nov) ----------
        // The world scene was ~2/3 of the .data (31 of 46 MB over the wire). As an AssetBundle next to the game files
        // (RaceFlow/WorldStream.cs loads it) the code and menus start while it downloads, and because its name is its
        // content hash the browser keeps it across deploys that did not change the world. One file per texture format,
        // like the .data: DXT for desktops, ETC2 for phones; index.html picks the one that fits (window.ccWorldBundleUrl).
        // -worldInData on the command line builds the old way (world inside the .data), e.g. to compare.
        public const string WorldDxtPlaceholder = "__CC_WORLD_DXT__";
        public const string WorldEtc2Placeholder = "__CC_WORLD_ETC2__";
        public const string WorldSharePlaceholder = "0/*__CC_WORLD_SHARE__*/";
        private const string WorldBundleFolder = "Build-WorldBundles";
        private const string WorldBundleName = "world";
        private static WorldFiles worldFiles;

        public class WorldFiles
        {
            public string Dxt;   // file name in Build/, world-<md5>.bundle
            public string Etc2;  // null when phones use the DXT files (no ETC2 .data either)
            public float Share;  // the world's part of a desktop download (gzip), for the page's loading bar
        }

        private static bool WorldAsOwnFile(string[] scenes, bool gzipForItch)
        {
            return !gzipForItch && !HasArgument("-worldInData") && Array.IndexOf(scenes, WorldScene) >= 0;
        }

        // The player build without the world scene (it is in the world file).
        public static string[] PlayerScenes(string[] webScenes)
        {
            var kept = new System.Collections.Generic.List<string>();
            foreach (string scene in webScenes)
            {
                if (scene != WorldScene)
                {
                    kept.Add(scene);
                }
            }
            return kept.ToArray();
        }

        // Both world files; returns the DXT one's manifest (for stripping), or null when either failed (classic build).
        private static string BuildWorldBundles()
        {
            FileUtil.DeleteFileOrDirectory(WorldBundleFolder);
            WebGLTextureSubtarget before = EditorUserBuildSettings.webGLBuildSubtarget;
            try
            {
                string manifest = BuildWorldBundle(WebGLTextureSubtarget.DXT, "dxt");
                if (manifest == null || BuildWorldBundle(WebGLTextureSubtarget.ETC2, "etc2") == null)
                {
                    Debug.LogWarning("Role4WebBuild: the world file could not be built; the world stays inside the .data.");
                    return null;
                }
                return manifest;
            }
            finally
            {
                EditorUserBuildSettings.webGLBuildSubtarget = before;
            }
        }

        private static string BuildWorldBundle(WebGLTextureSubtarget textures, string folderName)
        {
            string folder = Path.Combine(WorldBundleFolder, folderName);
            Directory.CreateDirectory(folder);
            EditorUserBuildSettings.webGLBuildSubtarget = textures; // the bundle's texture format follows this setting
            var bundles = new[] { new AssetBundleBuild { assetBundleName = WorldBundleName, assetNames = new[] { WorldScene } } };
            // LZ4 chunks: small in memory while open; GitHub Pages gzips it on the way like the .data.
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(folder, bundles,
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.WebGL);
            string file = Path.Combine(folder, WorldBundleName);
            if (manifest == null || !File.Exists(file))
            {
                return null;
            }
            Debug.Log("Role4WebBuild: world file (" + folderName + ") " + ToMB(new FileInfo(file).Length) + " MB.");
            return Path.Combine(folder, folderName + ".manifest"); // the folder's own manifest is named after the folder
        }

        // Copies the world files into Build/ under their content hash and measures the world's share of the download.
        private static WorldFiles CopyWorldBundles(bool phonesHaveEtc2Data)
        {
            string build = Path.Combine(OutputFolder, "Build");
            var files = new WorldFiles();
            files.Dxt = CopyHashed(Path.Combine(WorldBundleFolder, "dxt", WorldBundleName), build);
            if (phonesHaveEtc2Data)
            {
                files.Etc2 = CopyHashed(Path.Combine(WorldBundleFolder, "etc2", WorldBundleName), build);
            }
            long world = GzipSize(Path.Combine(build, files.Dxt));
            long game = 0;
            foreach (string file in Directory.GetFiles(build))
            {
                string name = Path.GetFileName(file);
                if (!name.StartsWith("world-") && name != phoneDataFile)
                {
                    game += GzipSize(file);
                }
            }
            files.Share = world + game > 0 ? (float)world / (world + game) : 0f;
            return files;
        }

        private static string CopyHashed(string source, string folder)
        {
            string name = WorldFileName(File.ReadAllBytes(source));
            File.Copy(source, Path.Combine(folder, name), true);
            return name;
        }

        public static string WorldFileName(byte[] content)
        {
            using (MD5 md5 = MD5.Create())
            {
                var name = new StringBuilder("world-");
                foreach (byte b in md5.ComputeHash(content))
                {
                    name.Append(b.ToString("x2"));
                }
                return name.Append(".bundle").ToString();
            }
        }

        private static void StampWorld(string folder, WorldFiles files)
        {
            string indexPath = Path.Combine(folder, "index.html");
            if (files != null && File.Exists(indexPath))
            {
                File.WriteAllText(indexPath, StampWorldHtml(File.ReadAllText(indexPath), files));
                Debug.Log("Role4WebBuild: world files " + files.Dxt + (files.Etc2 != null ? " + " + files.Etc2 + " (ETC2)" : "")
                    + ", " + Mathf.RoundToInt(files.Share * 100f) + " % of the download.");
            }
        }

        // The world file names and the world's share of the download into the page ("Campus world" in index.html).
        public static string StampWorldHtml(string html, WorldFiles files)
        {
            html = html.Replace(WorldDxtPlaceholder, files.Dxt);
            if (!string.IsNullOrEmpty(files.Etc2))
            {
                html = html.Replace(WorldEtc2Placeholder, files.Etc2);
            }
            return html.Replace(WorldSharePlaceholder, files.Share.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
        }

        // GraphicsSettings fog stripping, read and written through the settings asset (Unity has no API for it).
        private struct FogStripping
        {
            private int mode;
            private bool linear;
            private bool exp;
            private bool exp2;

            private static SerializedObject Settings()
            {
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
                return assets.Length > 0 ? new SerializedObject(assets[0]) : null;
            }

            public static FogStripping Read()
            {
                var fog = new FogStripping { mode = -1 };
                SerializedObject settings = Settings();
                if (settings != null)
                {
                    fog.mode = settings.FindProperty("m_FogStripping").intValue;
                    fog.linear = settings.FindProperty("m_FogKeepLinear").boolValue;
                    fog.exp = settings.FindProperty("m_FogKeepExp").boolValue;
                    fog.exp2 = settings.FindProperty("m_FogKeepExp2").boolValue;
                }
                return fog;
            }

            public void Write()
            {
                SerializedObject settings = Settings();
                if (settings == null || mode < 0)
                {
                    return;
                }
                settings.FindProperty("m_FogStripping").intValue = mode;
                settings.FindProperty("m_FogKeepLinear").boolValue = linear;
                settings.FindProperty("m_FogKeepExp").boolValue = exp;
                settings.FindProperty("m_FogKeepExp2").boolValue = exp2;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }

            public static void KeepExponentialOnly()
            {
                new FogStripping { mode = 1, linear = false, exp = true, exp2 = false }.Write(); // 1 = Custom
            }
        }

        // V37: the scenes in the build list, minus the ones the race never loads on the Web. RaceBootstrap takes the first
        // track scene that has a track (RaceSettings order: Track_Campus, Track_CampusWorld, Track_NovaCampus, ...), so
        // with the campus world in the build Track_NovaCampus is never reached, yet all its meshes and textures were in
        // the download. The editor and the build list keep it; only the Web player leaves it out.
        // Load time (17 Nov): also Role 2's empty Track_Campus (no TrackInfo). RaceBootstrap loaded and unloaded it on
        // every start before the campus world, about 0.8 s; now that scene is simply not in the Web build and skipped.
        public static readonly string[] WebSkipScenes =
        {
            "Assets/_Project/Track/Scenes/Track_NovaCampus.unity",
            "Assets/_Project/Track/Scenes/Track_Campus.unity",
        };

        public const string WorldScene = "Assets/_Project/Visuals/Scenes/Track_CampusWorld.unity";

        public static string[] WebScenes()
        {
            return FilterWebScenes(EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes));
        }

        // The build list minus WebSkipScenes, but only while the campus world is in it (else nothing is skipped).
        public static string[] FilterWebScenes(string[] all)
        {
            var kept = new System.Collections.Generic.List<string>();
            bool hasWorld = System.Array.IndexOf(all, WorldScene) >= 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (hasWorld && System.Array.IndexOf(WebSkipScenes, all[i]) >= 0)
                {
                    continue; // never loaded while the campus world is in the build
                }
                kept.Add(all[i]);
            }
            return kept.ToArray();
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
            // V37: drop the vertex data no shader in the build reads (e.g. tangents on materials without a normal map,
            // a second UV set nobody samples). Unity decides per mesh from the materials it is drawn with.
            PlayerSettings.stripUnusedMeshComponents = true;
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
            PlayerSettings.SplashScreen.showUnityLogo = false; // load time (17 Nov): its 2.7 MB logo texture was still in the build
            // V37: no static batching on the Web. It stored a second, merged copy of every static mesh in the scene data,
            // which doubled the download (Web .data 126 -> 63 MB without it, 16 Nov). The SRP Batcher already keeps
            // the draw calls cheap; set it back to true here if a Web fps check ever shows the CPU as the limit.
            PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.WebGL, false);
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
                + ", unityLogo=" + PlayerSettings.SplashScreen.showUnityLogo
                + ", worldTextures=crunch q50, normals max 512 (WebTextureCrunch v" + new WebTextureCrunch().GetVersion() + ")"
                + ", webPipeline=" + (webPipeline != null ? webPipeline.name : "none (see the error above)"));
        }

        // ---------- Build id: the page never mixes a new build with cached old page files (load time, 17 Nov) ----------
        // The game files already have their content hash in the name. The page files (index.html, TemplateData/) do not,
        // and GitHub Pages lets browsers keep them 10 minutes. So after every build: one id from all the built files,
        // written into index.html (placeholder __CC_BUILD_ID__) and version.json, and added as ?v=<id> to every
        // TemplateData address in index.html and style.css. index.html checks version.json and reloads once when a newer
        // build is out (see "Always the newest build" in the template).
        public const string BuildIdPlaceholder = "__CC_BUILD_ID__";
        public const string PhoneDataPlaceholder = "__CC_ETC2_DATA__";

        public static void StampBuildVersion(string folder, string phoneData)
        {
            string indexPath = Path.Combine(folder, "index.html");
            if (!File.Exists(indexPath))
            {
                Debug.LogWarning("Role4WebBuild: no index.html to stamp in " + folder);
                return;
            }
            string id = BuildId(folder);
            File.WriteAllText(indexPath, StampHtml(File.ReadAllText(indexPath), id, phoneData));
            string cssPath = Path.Combine(folder, "TemplateData", "style.css");
            if (File.Exists(cssPath))
            {
                File.WriteAllText(cssPath, StampCss(File.ReadAllText(cssPath), id));
            }
            File.WriteAllText(Path.Combine(folder, "version.json"), VersionJson(id));
            Debug.Log("Role4WebBuild: build id " + id + " (index.html, version.json, TemplateData ?v=" + id + ")");
        }

        public static string VersionJson(string id)
        {
            return "{\"build\":\"" + id + "\"}\n";
        }

        // SHA-1 over every file of the build (path + content), first 12 hex digits. Same files = same id.
        public static string BuildId(string folder)
        {
            string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            using (SHA1 sha = SHA1.Create())
            {
                foreach (string file in files)
                {
                    string relative = file.Substring(folder.Length).Replace('\\', '/');
                    if (relative.EndsWith("/version.json"))
                    {
                        continue;
                    }
                    byte[] name = Encoding.UTF8.GetBytes(relative + "\n");
                    sha.TransformBlock(name, 0, name.Length, null, 0);
                    byte[] content = File.ReadAllBytes(file);
                    sha.TransformBlock(content, 0, content.Length, null, 0);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                var hex = new StringBuilder();
                for (int i = 0; i < 6; i++)
                {
                    hex.Append(sha.Hash[i].ToString("x2"));
                }
                return hex.ToString();
            }
        }

        // src="TemplateData/x" and href="TemplateData/x" (no query yet) in index.html.
        private static readonly Regex TemplateDataLink = new Regex("((?:src|href)=\")(TemplateData/[^\"?#]+)(\")");
        // url(x), url("x"), url('x') in style.css with a relative address (not data:, http(s):, / or #).
        private static readonly Regex CssUrl = new Regex("(url\\(\\s*[\"']?)(?![\"']|data:|https?:|/|#)([^\"')?#\\s]+)([\"']?\\s*\\))");

        // phoneData: the ETC2 .data file name, or null (the placeholder then stays and the page uses the DXT data).
        public static string StampHtml(string html, string id, string phoneData)
        {
            html = html.Replace(BuildIdPlaceholder, id);
            if (!string.IsNullOrEmpty(phoneData))
            {
                html = html.Replace(PhoneDataPlaceholder, phoneData);
            }
            return TemplateDataLink.Replace(html, m => m.Groups[1].Value + m.Groups[2].Value + "?v=" + id + m.Groups[3].Value);
        }

        public static string StampCss(string css, string id)
        {
            return CssUrl.Replace(css, m => m.Groups[1].Value + m.Groups[2].Value + "?v=" + id + m.Groups[3].Value);
        }

        // Prints every file in Build-Web/Build with its size and its gzip size (= roughly what GitHub Pages sends).
        private static void LogFileSizes()
        {
            string[] files = Directory.GetFiles(Path.Combine(OutputFolder, "Build"));
            long totalRaw = 0;
            long totalGzip = 0;
            long worldGzip = 0;
            foreach (string file in files)
            {
                long raw = new FileInfo(file).Length;
                long gzip = file.EndsWith(".gz") ? raw : GzipSize(file);
                string name = Path.GetFileName(file);
                bool phoneOnly = name == phoneDataFile || (worldFiles != null && name == worldFiles.Etc2);
                bool world = worldFiles != null && name == worldFiles.Dxt;
                if (world)
                {
                    worldGzip = gzip;
                }
                if (!phoneOnly)
                {
                    totalRaw += raw; // a visitor downloads either the DXT or the ETC2 data, never both
                    totalGzip += gzip;
                }
                Debug.Log("Role4WebBuild file: " + Path.GetFileName(file) + "  " + ToMB(raw) + " MB (gzip " + ToMB(gzip) + " MB)"
                    + (phoneOnly ? "  [ETC2, phones only]" : "") + (world ? "  [campus world, loads after the game starts]" : ""));
            }
            Debug.Log("Role4WebBuild size: " + ToMB(totalRaw) + " MB on disk, about " + ToMB(totalGzip) + " MB over the wire (gzip)"
                + (worldFiles != null ? ": the game " + ToMB(totalGzip - worldGzip) + " MB, then the campus world " + ToMB(worldGzip) + " MB." : "."));
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
