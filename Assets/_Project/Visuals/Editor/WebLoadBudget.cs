using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Visuals.Editor
{
    // Load-time pass for the Web build (Role 4 help, 17 Nov 2026). The live game took 17-19 s to "Click to play" on a fast
    // laptop; almost all of it is the download (60 MB over the wire, measured: textures 26 MB, meshes 18 MB).
    // Like WebTextureCrunch, this hook changes only the Web (WebGL) import, never the .meta files and never the editor,
    // Mac or PC look, and it keeps every setting someone made by hand. Three rules:
    //   1. Textures with a WebTextureBudget size (drivers, surfers) get that size on the Web even when nobody ran
    //      "Apply Web texture sizes" (V40's three new drivers shipped 2048 px maps: 4.9 MB over the wire).
    //   2. World textures that WebTextureBudget made smaller (props, soft normals) are also crunched like every other
    //      world texture: WebTextureCrunch skips any texture with a Web override, so these 53 stayed plain DXT
    //      (about 6 MB over the wire). Skies, gloss masks and the leaf cut-out keep plain DXT (crunch shows there).
    //   3. Kart, driver, tyre and surfer models get Medium mesh compression on the Web (16-bit vertex data; the
    //      Classic kart and the Rookie driver already use it everywhere). Unity unpacks it to full floats on load.
    // Bump Version when a rule changes: Unity then re-imports the affected assets once.
    public class WebLoadBudget : AssetPostprocessor
    {
        private const uint Version = 1;
        private const int CrunchQuality = 50; // the same as WebTextureCrunch
        private const string WorldTexDir = "Assets/_Project/Visuals/CampusWorld/Textures/";

        private static readonly string[] SmallModelFolders =
        {
            "Assets/_Project/Visuals/Kart/Models/",
            "Assets/_Project/Ambience/ThirdParty/Rocketbox/",
        };

        // Plain DXT on purpose: smooth gradients (sky), near-flat gloss masks and the alpha cut-out edge of the leaves.
        private static readonly string[] KeepUncrunched =
        {
            "sky_panorama.png", "sky_sunset.png", "veg_leaves_cutout_albedo.png",
            "concrete_polished_metallic_smoothness.png", "asphalt_metallic_smoothness.png", "window_band_metallic_smoothness.png",
        };

        public override uint GetVersion()
        {
            return Version;
        }

        public override int GetPostprocessOrder()
        {
            return 10; // after WebTextureCrunch (order 0), so its rules run first
        }

        private void OnPreprocessTexture()
        {
            if (context.selectedBuildTarget != BuildTarget.WebGL)
            {
                return;
            }
            var importer = (TextureImporter)assetImporter;
            TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings(WebTextureBudget.WebTarget);
            WebTextureDecision decision = DecideTexture(assetPath, web.overridden, web.maxTextureSize, web.crunchedCompression);
            if (decision.Size > 0)
            {
                web.overridden = true;
                web.maxTextureSize = decision.Size;
                web.format = TextureImporterFormat.Automatic;
                web.textureCompression = TextureImporterCompression.Compressed;
                web.crunchedCompression = false;
                web.resizeAlgorithm = importer.GetDefaultPlatformTextureSettings().resizeAlgorithm;
            }
            if (decision.Crunch)
            {
                web.crunchedCompression = true;
                web.compressionQuality = CrunchQuality;
            }
            if (decision.Size > 0 || decision.Crunch)
            {
                importer.SetPlatformTextureSettings(web);
            }
        }

        private void OnPreprocessModel()
        {
            if (context.selectedBuildTarget != BuildTarget.WebGL)
            {
                return;
            }
            var importer = (ModelImporter)assetImporter;
            if (UsesMediumCompression(assetPath) && importer.meshCompression == ModelImporterMeshCompression.Off)
            {
                importer.meshCompression = ModelImporterMeshCompression.Medium;
            }
        }

        public struct WebTextureDecision
        {
            public int Size;    // > 0: set this Web size (no override existed)
            public bool Crunch; // true: crunch on the Web
        }

        // The rules as a plain function of the path and the current WebGL override (tests call it directly).
        public static WebTextureDecision DecideTexture(string path, bool overridden, int overrideSize, bool crunched)
        {
            var decision = new WebTextureDecision();
            string file = System.IO.Path.GetFileName(path);
            int budget = WebTextureBudget.MaxWebSize(file);
            if (budget <= 0)
            {
                return decision; // not ours: WebTextureCrunch or the texture's own settings decide
            }
            if (!overridden)
            {
                decision.Size = budget; // rule 1
            }
            else if (overrideSize != budget)
            {
                return decision; // set by hand to another size: keep it as it is
            }
            decision.Crunch = !crunched && path.StartsWith(WorldTexDir) && System.Array.IndexOf(KeepUncrunched, file) < 0; // rule 2
            return decision;
        }

        public static bool UsesMediumCompression(string path)
        {
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            foreach (string folder in SmallModelFolders)
            {
                if (path.StartsWith(folder))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
