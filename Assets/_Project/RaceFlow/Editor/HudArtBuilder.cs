using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace CarcavelosCircuit.RaceFlow.Editor
{
    // Makes the HUD's art files from code (task R15, arcade UI restyle), so nobody has to click them together by hand:
    // 1. Two SDF font assets for UI Toolkit from the OFL fonts in UI/Fonts (Unbounded Black = display,
    //    Barlow Condensed ExtraBold = numbers and UI). SDF = the letters are stored as "distance to the edge", which
    //    is what lets USS draw thick outlines and hard shadows (-unity-text-outline-*, text-shadow). The padding
    //    round each letter decides how thick an outline can get, so it is generous (9 px at 56 px).
    //    Static atlas: every letter a player name may use (PlayerNameRules: Latin letters with accents) is baked in
    //    once, so the font file never changes while playing (a "dynamic" font rewrites its .asset in Play Mode).
    // 2. The plate pictures in UI/Art (PNG, drawn by PlateArt at twice the size they are shown, so they stay sharp
    //    on a phone). UI/HudArt.uss shows them 9-sliced.
    // Run: menu Carcavelos > Build HUD Art, or batchmode -executeMethod CarcavelosCircuit.RaceFlow.Editor.HudArtBuilder.BuildAll
    // Running it again overwrites the files with the same result.
    public static class HudArtBuilder
    {
        private const string UiFolder = "Assets/_Project/RaceFlow/UI";
        private const string ArtFolder = UiFolder + "/Art";

        // Printable ASCII and Latin-1 (José, Müller), plus the few symbols the HUD writes. The UI face also gets Latin
        // Extended-A (Łukasz, Şahin), because player names in the leaderboard rows use it (PlayerNameRules allows them).
        private static string Characters(bool withExtendedA)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int c = 32; c <= 126; c++) builder.Append((char)c);
            for (int c = 160; c <= 255; c++) builder.Append((char)c);
            if (withExtendedA)
            {
                for (int c = 256; c <= 383; c++) builder.Append((char)c);
            }
            builder.Append("·–—‘’“”…•×");
            return builder.ToString();
        }

        [MenuItem("Carcavelos/Build HUD Art")]
        public static void BuildAll()
        {
            BuildFont(UiFolder + "/Fonts/Unbounded/Unbounded-Black.ttf", UiFolder + "/Fonts/Unbounded/Unbounded-Black-SDF.asset", 56, 9, false);
            BuildFont(UiFolder + "/Fonts/BarlowCondensed/BarlowCondensed-ExtraBold.ttf", UiFolder + "/Fonts/BarlowCondensed/BarlowCondensed-ExtraBold-SDF.asset", 56, 8, true);
            BuildPlates();
            // A style sheet only finds a picture or font that existed when it was imported: import them again.
            string[] sheets = { "RaceHud.uss", "StartScreen.uss", "Leaderboard.uss", "HudArt.uss", "RaceHudTheme.tss" };
            for (int i = 0; i < sheets.Length; i++)
            {
                AssetDatabase.ImportAsset(UiFolder + "/" + sheets[i], ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("HudArtBuilder: fonts and plates built");
        }

        private static void BuildFont(string ttfPath, string assetPath, int pointSize, int padding, bool withExtendedA)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogError("HudArtBuilder: font file missing: " + ttfPath);
                return;
            }
            AssetDatabase.DeleteAsset(assetPath); // a clean rebuild, never half old, half new
            FontAsset asset = FontAsset.CreateFontAsset(font, pointSize, padding, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
            {
                Debug.LogError("HudArtBuilder: could not make a font asset from " + ttfPath);
                return;
            }
            string missing;
            asset.TryAddCharacters(Characters(withExtendedA), out missing);
            if (!string.IsNullOrEmpty(missing))
            {
                Debug.Log("HudArtBuilder: " + Path.GetFileName(ttfPath) + " has no glyph for " + missing.Length + " characters (they fall back to the default font)");
            }
            Debug.Log("HudArtBuilder: " + Path.GetFileName(ttfPath) + " uses " + asset.atlasTextures.Length + " atlas page(s) of 1024 x 1024");
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);
            // The atlas pictures and the material live inside the .asset file.
            for (int i = 0; i < asset.atlasTextures.Length; i++)
            {
                asset.atlasTextures[i].name = asset.name + " Atlas " + i;
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[i], asset);
            }
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);
        }

        private static void BuildPlates()
        {
            if (!AssetDatabase.IsValidFolder(ArtFolder))
            {
                AssetDatabase.CreateFolder(UiFolder, "Art");
            }
            Color ink55 = HudColors.WithAlpha(HudColors.Ink, 0.55f);
            Color cream = HudColors.Cream;
            Color cream90 = HudColors.WithAlpha(HudColors.Cream, 0.9f);

            // Panels (time, checkpoints, drift, cards, menus): navy gradient, cream stroke, hard ink shadow.
            WritePlate("plate-navy", 96, 104, new PlateLook(HudColors.WithAlpha(HudColors.PlateTop, 0.95f), HudColors.WithAlpha(HudColors.Plate, 0.95f), cream90, ink55, 28f, 6f, 4f, 12f));
            // Headers, the start prompt's partner, the "you" row on the start board: blue gradient.
            WritePlate("plate-blue", 96, 104, new PlateLook(HudColors.PlateBlueHi, HudColors.PlateBlueLo, cream, ink55, 28f, 6f, 4f, 12f));
            // Buttons and the start prompt: orange gradient on an orange lip (a physical button).
            WritePlate("plate-orange", 96, 104, new PlateLook(HudColors.PlateOrangeHi, HudColors.PlateOrangeLo, cream, HudColors.LipOrange, 28f, 6f, 4f, 12f));
            // The player's own leaderboard row: orange, thinner stroke, small shadow.
            WritePlate("plate-orange-row", 64, 68, new PlateLook(HudColors.PlateOrangeHi, HudColors.PlateOrangeLo, cream, ink55, 18f, 4f, 3f, 6f));
            // Coach praise: a sand plate.
            WritePlate("plate-sand", 96, 104, new PlateLook(new Color(0.96f, 0.86f, 0.66f, 1f), HudColors.Sand, cream, ink55, 28f, 6f, 4f, 12f));
            // Chips: navy (first-lap split), cream (best time), coral (slower), mint (faster).
            WritePlate("plate-navy-chip", 64, 72, new PlateLook(HudColors.WithAlpha(HudColors.PlateTop, 0.95f), HudColors.WithAlpha(HudColors.Plate, 0.95f), cream90, ink55, 22f, 4f, 3f, 8f));
            WritePlate("plate-cream", 64, 72, new PlateLook(cream, new Color(0.96f, 0.90f, 0.77f, 1f), HudColors.Ink, ink55, 22f, 4f, 0f, 8f));
            WritePlate("plate-coral", 64, 72, new PlateLook(new Color(0.96f, 0.38f, 0.32f, 1f), HudColors.Coral, cream, ink55, 22f, 4f, 3f, 8f));
            WritePlate("plate-mint", 64, 72, new PlateLook(new Color(0.42f, 0.89f, 0.62f, 1f), new Color(0.16f, 0.70f, 0.43f, 1f), cream, ink55, 22f, 4f, 3f, 8f));
            // The dark minimap tile: see-through so the world shows, still a cream stroke.
            WritePlate("plate-map", 96, 104, new PlateLook(HudColors.WithAlpha(HudColors.PlateTop, 0.72f), HudColors.WithAlpha(HudColors.Plate, 0.72f), cream90, ink55, 28f, 6f, 0f, 12f));

            // Podium shields for places 1-3 (shown 40 x 46).
            WriteShield("shield-sun", new PlateLook(new Color(1f, 0.87f, 0.45f, 1f), new Color(0.96f, 0.66f, 0.10f, 1f), cream, ink55, 0f, 6f, 0f, 8f));
            WriteShield("shield-silver", new PlateLook(new Color(0.94f, 0.96f, 0.99f, 1f), new Color(0.70f, 0.76f, 0.84f, 1f), cream, ink55, 0f, 6f, 0f, 8f));
            WriteShield("shield-bronze", new PlateLook(new Color(0.93f, 0.66f, 0.43f, 1f), new Color(0.74f, 0.42f, 0.20f, 1f), cream, ink55, 0f, 6f, 0f, 8f));

            // The item slot: an azulejo tile (shown 112 x 112).
            WritePixels("item-frame", 224, 224, PlateArt.AzulejoFrame(224, HudColors.WithAlpha(HudColors.Plate, 0.95f), HudColors.PlateBlueLo, cream, HudColors.Ink));
        }

        private static void WritePlate(string name, int width, int height, PlateLook look)
        {
            WritePixels(name, width, height, PlateArt.Plate(width, height, look));
        }

        private static void WriteShield(string name, PlateLook look)
        {
            WritePixels(name, 80, 92, PlateArt.Shield(80, 92, look));
        }

        // PlateArt rows go top-down, Texture2D rows bottom-up: flip while copying.
        private static void WritePixels(string name, int width, int height, Color[] topDown)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] bottomUp = new Color[topDown.Length];
            for (int y = 0; y < height; y++)
            {
                System.Array.Copy(topDown, y * width, bottomUp, (height - 1 - y) * width, width);
            }
            texture.SetPixels(bottomUp);
            texture.Apply();
            string path = ArtFolder + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            // UI pictures: no mipmaps, no tiling, no compression (they are tiny), see-through edges kept clean.
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }
    }
}
