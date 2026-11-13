using UnityEditor;

namespace CarcavelosCircuit.Visuals.Editor
{
    // Import settings for Luc's four item icons (request Q16 from Role 4, task R7): every PNG in Visuals/Items/Icons
    // comes in as a UI sprite, so RaceFlow can show it in the HUD item slot without anyone setting it by hand.
    // Unity runs this before it imports such a file, also on a fresh clone and after every change to a PNG.
    public class ItemIconImport : AssetPostprocessor
    {
        private const string IconFolder = "Assets/_Project/Visuals/Items/Icons/";

        private void OnPreprocessTexture()
        {
            // Only the item icons; every other texture keeps its own settings.
            if (!assetPath.StartsWith(IconFolder))
            {
                return;
            }
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;     // usable as Sprite or as Texture2D in UI Toolkit
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;                    // no dark fringe round the transparent edge
            importer.mipmapEnabled = false;                         // shown flat at one size in the HUD
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;  // no bleeding from the opposite edge
            importer.maxTextureSize = 256;                          // the PNGs are 256 x 256
            importer.textureCompression = TextureImporterCompression.Uncompressed; // crisp edges; 4 x 256 KB
        }
    }
}
