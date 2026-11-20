using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow.Editor
{
    // Smaller Web download (Role 4, release). Since V21 the campus world carries about 160 textures (109 MB in the build,
    // 3x more than before), so the Web page loaded about 2 minutes. GitHub Pages already gzips the files, but DXT
    // textures barely shrink with gzip.
    //
    // This hook changes only the Web (WebGL) import of the world textures, never the .meta files and never the editor or
    // desktop look:
    //   * crunch compression (DXT + crunch, quality CrunchQuality): about 3-5x smaller download, same resolution
    //   * normal maps at most NormalMaxSize px: bumps are hard to see at kart speed, they were 1024 px each
    // Skipped: textures someone already set up for WebGL by hand (Platform override in the inspector), uncompressed
    // ones (UI art), data/mask maps (crunch blocks would show as blotches in the ground blend), and every folder that
    // is not listed in WorldFolders.
    // Bump Version when a rule changes: Unity then re-imports the textures once.
    public class WebTextureCrunch : AssetPostprocessor
    {
        private const uint Version = 1;
        private const int CrunchQuality = 50;
        private const int NormalMaxSize = 512;

        private static readonly string[] WorldFolders =
        {
            "Assets/_Project/Visuals/CampusWorld/Textures/",
            "Assets/_Project/Track/Textures/",
        };

        public override uint GetVersion()
        {
            return Version;
        }

        private void OnPreprocessTexture()
        {
            if (context.selectedBuildTarget != BuildTarget.WebGL || !IsWorldTexture(assetPath))
            {
                return;
            }
            var importer = (TextureImporter)assetImporter;
            TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings("WebGL");
            if (web.overridden)
            {
                return; // set by hand for WebGL: keep it
            }
            TextureImporterPlatformSettings shared = importer.GetDefaultPlatformTextureSettings();
            if (shared.textureCompression == TextureImporterCompression.Uncompressed || IsDataMap(assetPath))
            {
                return;
            }

            bool normal = importer.textureType == TextureImporterType.NormalMap || FileName(assetPath).Contains("_normal");
            web.overridden = true;
            web.format = TextureImporterFormat.Automatic;
            web.textureCompression = shared.textureCompression;
            web.maxTextureSize = normal ? Mathf.Min(shared.maxTextureSize, NormalMaxSize) : shared.maxTextureSize;
            web.crunchedCompression = true;
            web.compressionQuality = CrunchQuality;
            web.resizeAlgorithm = shared.resizeAlgorithm;
            importer.SetPlatformTextureSettings(web);
        }

        public static bool IsWorldTexture(string path)
        {
            foreach (string folder in WorldFolders)
            {
                if (path.StartsWith(folder))
                {
                    return true;
                }
            }
            return false;
        }

        // Linear data maps (ground blend masks, detail data): kept as they are.
        public static bool IsDataMap(string path)
        {
            string name = FileName(path);
            return name.Contains("_data") || name.Contains("mask");
        }

        private static string FileName(string path)
        {
            return System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        }
    }
}
