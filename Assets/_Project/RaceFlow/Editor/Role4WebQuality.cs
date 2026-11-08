using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarcavelosCircuit.RaceFlow.Editor
{
    // Task P2: the Web build gets its own render settings, so we can make it sharper or faster without
    // touching the editor/PC settings or Role 3's visual assets. Two pieces, both made by code:
    //   1. RaceFlow/Settings/Web_RPAsset.asset: a copy of the template's Mobile URP asset (render scale 1.0, MSAA 2x).
    //      Made only once; after that the script never changes it, so Luc can tune it in the Inspector.
    //   2. A quality level "Web" (Project Settings > Quality) that uses Web_RPAsset and is the only level used by
    //      the Web platform. The editor keeps using "PC", phones keep "Mobile".
    // Called by Role4WebBuild before every Web build.
    public static class Role4WebQuality
    {
        public const string WebLevelName = "Web";
        private const string WebPipelinePath = "Assets/_Project/RaceFlow/Settings/Web_RPAsset.asset";
        private const string MobilePipelinePath = "Assets/Settings/Mobile_RPAsset.asset";
        private const string QualitySettingsPath = "ProjectSettings/QualitySettings.asset";
        private const string WebPlatform = "WebGL"; // Unity's internal name for the Web platform in QualitySettings

        // Makes sure the Web URP asset and the "Web" quality level exist and returns the asset (null if it failed).
        public static RenderPipelineAsset SetUp()
        {
            UniversalRenderPipelineAsset webPipeline = FindOrCreateWebPipeline();
            if (webPipeline == null)
            {
                return null;
            }
            UseWebLevelForWebPlatform(webPipeline);
            return webPipeline;
        }

        private static UniversalRenderPipelineAsset FindOrCreateWebPipeline()
        {
            UniversalRenderPipelineAsset existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(WebPipelinePath);
            if (existing != null)
            {
                return existing; // keep whatever Luc tuned
            }

            // Copy (not edit) the Mobile asset: it already has the cheap settings (Forward, 1 shadow cascade, no SSAO).
            if (!AssetDatabase.CopyAsset(MobilePipelinePath, WebPipelinePath))
            {
                Debug.LogError("Role4WebQuality: could not copy " + MobilePipelinePath + " to " + WebPipelinePath + ".");
                return null;
            }
            UniversalRenderPipelineAsset created = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(WebPipelinePath);
            created.renderScale = 1.0f;   // Mobile uses 0.8, which looks soft on a laptop screen
            created.msaaSampleCount = 2;  // smooths the edges of the low-poly shapes; 4 costs too much on laptop GPUs
            EditorUtility.SetDirty(created);
            AssetDatabase.SaveAssets();
            Debug.Log("Role4WebQuality: created " + WebPipelinePath + " (render scale 1.0, MSAA 2x).");
            return created;
        }

        // Quality levels have no public "add level" method, so we use SerializedObject on the QualitySettings asset,
        // the same way the Project Settings window edits it (no text edits of the file).
        private static void UseWebLevelForWebPlatform(RenderPipelineAsset webPipeline)
        {
            Object qualityAsset = AssetDatabase.LoadAllAssetsAtPath(QualitySettingsPath)[0];
            SerializedObject quality = new SerializedObject(qualityAsset);
            SerializedProperty levels = quality.FindProperty("m_QualitySettings");

            int webIndex = FindLevel(levels, WebLevelName);
            if (webIndex < 0)
            {
                // Start from a copy of "Mobile" (or the first level): duplicate it, then move the copy to the end,
                // so the indices of the existing levels (and the editor's current level) do not change.
                int source = FindLevel(levels, "Mobile");
                if (source < 0)
                {
                    source = 0;
                }
                levels.InsertArrayElementAtIndex(source);
                levels.MoveArrayElement(source, levels.arraySize - 1);
                webIndex = levels.arraySize - 1;
                SerializedProperty newLevel = levels.GetArrayElementAtIndex(webIndex);
                newLevel.FindPropertyRelative("name").stringValue = WebLevelName;
                SerializedProperty excluded = newLevel.FindPropertyRelative("excludedTargetPlatforms");
                excluded.ClearArray();
                AddPlatform(excluded, "Standalone");
                AddPlatform(excluded, "Android");
                AddPlatform(excluded, "iPhone");
                Debug.Log("Role4WebQuality: added quality level \"" + WebLevelName + "\".");
            }

            SerializedProperty webLevel = levels.GetArrayElementAtIndex(webIndex);
            webLevel.FindPropertyRelative("customRenderPipeline").objectReferenceValue = webPipeline;

            // Every other level is hidden on Web, so the Web build ships only one URP asset (fewer shader variants).
            for (int i = 0; i < levels.arraySize; i++)
            {
                if (i != webIndex)
                {
                    SerializedProperty otherExcluded = levels.GetArrayElementAtIndex(i).FindPropertyRelative("excludedTargetPlatforms");
                    if (!HasPlatform(otherExcluded, WebPlatform))
                    {
                        AddPlatform(otherExcluded, WebPlatform);
                    }
                }
            }

            SetDefaultLevelForWeb(quality.FindProperty("m_PerPlatformDefaultQuality"), webIndex);
            quality.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        // m_PerPlatformDefaultQuality is a list of (platform name, level index) pairs.
        private static void SetDefaultLevelForWeb(SerializedProperty perPlatform, int webIndex)
        {
            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                SerializedProperty pair = perPlatform.GetArrayElementAtIndex(i);
                if (pair.FindPropertyRelative("first").stringValue == WebPlatform)
                {
                    pair.FindPropertyRelative("second").intValue = webIndex;
                    return;
                }
            }
            Debug.LogWarning("Role4WebQuality: no Web entry in the default quality list. Set it by hand: Project Settings > Quality > Web column.");
        }

        private static int FindLevel(SerializedProperty levels, string name)
        {
            for (int i = 0; i < levels.arraySize; i++)
            {
                if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool HasPlatform(SerializedProperty platforms, string platform)
        {
            for (int i = 0; i < platforms.arraySize; i++)
            {
                if (platforms.GetArrayElementAtIndex(i).stringValue == platform)
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddPlatform(SerializedProperty platforms, string platform)
        {
            platforms.arraySize = platforms.arraySize + 1;
            platforms.GetArrayElementAtIndex(platforms.arraySize - 1).stringValue = platform;
        }
    }
}
