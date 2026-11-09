using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow.Editor
{
    // Import settings for every sound in Audio/Clips, set by code so they are the same on every laptop (task P4).
    // Role4SceneBuilder calls it on every build; it can also run alone (Unity closed):
    //   Unity.exe -batchmode -projectPath . -executeMethod CarcavelosCircuit.RaceFlow.Editor.AudioImportSetup.ApplyAll -quit
    // Rules (audio-polish-plan.md section 3.2, Unity manual "Audio in Web"):
    // - Normalize OFF: Unity would otherwise push every clip to full peak level and undo our loudness levels
    //   from tools/audio-synth/process_wavs.py (the high engine loop would get 4 dB louder than the low one).
    // - Decompress On Load for ALL clips, music too: lowest latency, and only then does the Web build use the
    //   loop points in the WAV files (gapless loops). Costs some browser memory for the music, fine on a laptop.
    // - Preload Audio Data: every clip is loaded with the scene, so the first countdown beep is not late.
    // - Web: AAC (the only format Unity's Web build has), quality 1.0 for sound effects, 0.7 for music.
    // Only clips whose settings differ are re-imported, so running it again is quick.
    public static class AudioImportSetup
    {
        public const string ClipsFolder = "Assets/_Project/Audio/Clips";
        private const string WebPlatform = "WebGL"; // Unity still calls the Web platform "WebGL" in import settings

        [MenuItem("Carcavelos Circuit/Apply Audio Import Settings")]
        public static void ApplyAll()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ClipsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer != null && Apply(importer, IsMusic(path)))
                {
                    EditorUtility.SetDirty(importer); // so the new settings are written to the .meta file
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            Debug.Log("AudioImportSetup: " + changed + " clip(s) re-imported with the P4 audio settings.");
        }

        // Music = long files (music_*), lower Web quality is fine for them.
        private static bool IsMusic(string path)
        {
            return System.IO.Path.GetFileName(path).StartsWith("music_");
        }

        // Writes the wanted settings into the importer. Returns true if something had to change.
        private static bool Apply(AudioImporter importer, bool isMusic)
        {
            bool changed = false;

            AudioImporterSampleSettings sample = importer.defaultSampleSettings;
            if (sample.loadType != AudioClipLoadType.DecompressOnLoad || !sample.preloadAudioData)
            {
                sample.loadType = AudioClipLoadType.DecompressOnLoad;
                sample.preloadAudioData = true;
                importer.defaultSampleSettings = sample;
                changed = true;
            }

            AudioImporterSampleSettings web = importer.GetOverrideSampleSettings(WebPlatform);
            float webQuality = isMusic ? 0.7f : 1f;
            bool hasWebOverride = importer.ContainsSampleSettingsOverride(WebPlatform);
            if (!hasWebOverride || web.loadType != AudioClipLoadType.DecompressOnLoad || !web.preloadAudioData
                || web.compressionFormat != AudioCompressionFormat.AAC || Mathf.Abs(web.quality - webQuality) > 0.001f)
            {
                web = sample; // same load type and preload as the default settings
                web.compressionFormat = AudioCompressionFormat.AAC;
                web.quality = webQuality;
                importer.SetOverrideSampleSettings(WebPlatform, web);
                changed = true;
            }

            // Normalize has no public property; the importer's own Inspector uses the serialized field m_Normalize.
            SerializedObject serialized = new SerializedObject(importer);
            SerializedProperty normalize = serialized.FindProperty("m_Normalize");
            if (normalize == null)
            {
                normalize = serialized.FindProperty("normalize"); // the name used in the .meta file
            }
            if (normalize == null)
            {
                Debug.LogWarning("AudioImportSetup: could not find the Normalize setting on " + importer.assetPath
                    + ". Untick Normalize by hand in the Inspector.");
            }
            else if (normalize.boolValue)
            {
                normalize.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            return changed;
        }
    }
}
