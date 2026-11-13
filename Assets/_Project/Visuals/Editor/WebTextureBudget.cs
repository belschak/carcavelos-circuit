using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Visuals.Editor
{
    // V24: smaller textures for the Web build only (Role 3).
    // The Web download was 175 MB, 109 MB of it textures (CI build report, 13 Nov). Most of that were 1024 px
    // maps on small props that fill ~100 px on screen at kart distance. This sets a WebGL override per texture;
    // the editor, Mac and PC builds keep the full size because only the WebGL platform entry changes.
    // CampusWorldBuilder.ConfigureImporters calls Apply for every world texture, so a world rebuild keeps it.
    // Batch run without rebuilding the world: -executeMethod CarcavelosCircuit.Visuals.Editor.WebTextureBudget.ApplyAllBatch
    public static class WebTextureBudget
    {
        public const string WebTarget = "WebGL";
        private const string TexDir = "Assets/_Project/Visuals/CampusWorld/Textures";

        // Props from the asset kit: B = beach, C = cars, P = bikes, S = street furniture, V = scooter.
        private static readonly Regex PropKit = new Regex(@"^([BCPSV]\d)_", RegexOptions.Compiled);

        // Bigger props (cars, food truck, kiosk, lifeguard tower) are seen up close at the roadside: 512.
        // Small ones (bar stool, bistro set, bikes, scooters, parasol, lounger, surf rack, volley net): 256.
        private static readonly string[] BigProps = { "B3", "C1", "C2", "C3", "C4", "C5", "C6", "S3", "S4" };

        // Ground and wall normal maps with soft relief, seen at a slant or from afar. Road normals stay at 1024.
        private static readonly string[] SoftNormals =
        {
            "coast_concrete_beige_normal.png", "coast_concrete_ochre_normal.png", "coast_concrete_pale_normal.png",
            "concrete_panel_normal.png", "north_buff_normal.png", "facade_white_normal.png", "entrance_planks_normal.png",
        };

        // Gloss masks whose RGB is black and whose alpha only varies a little (measured 13 Nov).
        private static readonly string[] FlatMasks =
        {
            "concrete_polished_metallic_smoothness.png", "asphalt_metallic_smoothness.png", "window_band_metallic_smoothness.png",
        };

        // Largest size the Web build may use for this texture file name, or 0 for "no Web override".
        public static int MaxWebSize(string file)
        {
            if (string.IsNullOrEmpty(file))
            {
                return 0;
            }
            Match prop = PropKit.Match(file);
            if (prop.Success)
            {
                return Array.IndexOf(BigProps, prop.Groups[1].Value) >= 0 ? 512 : 256;
            }
            if (file == "sky_panorama.png")
            {
                return 512;                    // per cube face; the 2048 px source only has ~512 px per 90 degrees
            }
            if (Array.IndexOf(SoftNormals, file) >= 0)
            {
                return 512;
            }
            if (Array.IndexOf(FlatMasks, file) >= 0)
            {
                return 256;
            }
            if (file == "veg_leaves_cutout_albedo.png")
            {
                return 512;
            }
            return 0;
        }

        // Sets (or clears) the WebGL override. Returns true when the importer changed.
        public static bool Apply(TextureImporter importer, string file)
        {
            if (importer == null)
            {
                return false;
            }
            int size = MaxWebSize(file);
            TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings(WebTarget);
            if (size <= 0)
            {
                if (!web.overridden)
                {
                    return false;
                }
                web.overridden = false;
                importer.SetPlatformTextureSettings(web);
                return true;
            }
            if (web.overridden && web.maxTextureSize == size && web.format == TextureImporterFormat.Automatic
                && web.textureCompression == TextureImporterCompression.Compressed && !web.crunchedCompression)
            {
                return false;
            }
            web.overridden = true;
            web.maxTextureSize = size;
            web.format = TextureImporterFormat.Automatic;
            web.textureCompression = TextureImporterCompression.Compressed;
            web.crunchedCompression = false;
            importer.SetPlatformTextureSettings(web);
            return true;
        }

        [MenuItem("Carcavelos Circuit/Visuals/Apply Web texture sizes")]
        public static void ApplyAll()
        {
            var log = new StringBuilder("WebTextureBudget:\n");
            int changed = 0;
            int overridden = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture", new[] { TexDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    continue;
                }
                string file = Path.GetFileName(path);
                int size = MaxWebSize(file);
                if (size > 0)
                {
                    overridden++;
                    log.AppendLine("  " + file + " -> " + size);
                }
                if (Apply(importer, file))
                {
                    changed++;
                    importer.SaveAndReimport();
                }
            }
            log.AppendLine("  " + overridden + " textures with a Web size, " + changed + " importers changed");
            Debug.Log(log.ToString());
        }

        public static void ApplyAllBatch()
        {
            try
            {
                ApplyAll();
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
