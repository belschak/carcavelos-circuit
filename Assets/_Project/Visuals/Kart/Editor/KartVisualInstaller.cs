using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Visuals.Kart.Editor
{
    // Puts Role 3's kart model under Kart/Visual in Role 1's Kart prefab (request Q11), so nobody has to drag it in by
    // hand. Kart > Visual stays the empty that KartController tilts; the model goes inside it at 0/0/0 (pivot at
    // ground level, forward +Z, 1 unit = 1 m, docs/interfaces.md). The placeholder box is switched off, not deleted:
    // the ghost copies only active meshes, and Role 1 can switch the box back on. Running it twice changes nothing.
    public static class KartVisualInstaller
    {
        private const string KartPath = "Assets/_Project/Driving/Kart.prefab";
        private const string ModelPath = "Assets/_Project/Visuals/Kart/Prefabs/KartVisual_Classic_Rookie.prefab";

        [MenuItem("Carcavelos Circuit/Visuals/Put Kart Model In Kart Prefab")]
        public static void Install()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null || AssetDatabase.LoadAssetAtPath<GameObject>(KartPath) == null)
            {
                Debug.LogError("KartVisualInstaller: " + KartPath + " or " + ModelPath + " is missing.");
                return;
            }

            GameObject kart = PrefabUtility.LoadPrefabContents(KartPath);
            try
            {
                Transform visual = kart.transform.Find("Visual");
                if (visual == null)
                {
                    Debug.LogError("KartVisualInstaller: the Kart prefab has no 'Visual' child (needs #53).");
                    return;
                }

                // Remove an older copy of the model, so a second run gives the same result.
                for (int i = visual.childCount - 1; i >= 0; i--)
                {
                    Transform child = visual.GetChild(i);
                    if (child.name == model.name)
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }

                Transform box = visual.Find("Box");
                if (box != null)
                {
                    box.gameObject.SetActive(false);
                }

                GameObject copy = (GameObject)PrefabUtility.InstantiatePrefab(model, visual);
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.identity;
                copy.transform.localScale = Vector3.one;

                PrefabUtility.SaveAsPrefabAsset(kart, KartPath);
                Debug.Log("KartVisualInstaller: " + model.name + " is now under Kart/Visual, the box is switched off.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(kart);
            }
        }

        // Batch mode: install, then check the result; exit code 1 on any problem.
        public static void InstallAndCheck()
        {
            Install();
            string problem = Check();
            if (problem != null)
            {
                Debug.LogError("KartVisualInstaller check FAIL: " + problem);
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("KartVisualInstaller check PASS");
            EditorApplication.Exit(0);
        }

        // Null when the Kart prefab looks right, otherwise what is wrong.
        private static string Check()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KartPath);
            GameObject kart = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                kart.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Transform visual = kart.transform.Find("Visual");
                Transform model = visual != null ? visual.Find("KartVisual_Classic_Rookie") : null;
                if (model == null)
                {
                    return "no KartVisual_Classic_Rookie under Kart/Visual";
                }
                Transform box = visual.Find("Box");
                if (box != null && box.gameObject.activeSelf)
                {
                    return "the placeholder box is still on";
                }
                if (visual.GetComponentsInChildren<Collider>(true).Length > 0)
                {
                    return "the model brings colliders; they would fight the kart's ball";
                }

                Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                {
                    return "the model has no renderers";
                }
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
                Debug.Log("KartVisualInstaller: model bounds min " + bounds.min.ToString("F3") + " max " + bounds.max.ToString("F3"));
                if (bounds.min.y < -0.05f || bounds.min.y > 0.08f)
                {
                    return "the tyres do not touch the ground (lowest point y = " + bounds.min.y.ToString("F3") + " m)";
                }
                if (bounds.size.z < 1f || bounds.size.z > 2.5f)
                {
                    return "unexpected kart length " + bounds.size.z.ToString("F2") + " m";
                }
                string[] anchors = { "Wheel_RL", "Wheel_RR", "Exhaust_0", "Exhaust_1", "Steering_Wheel" };
                foreach (string anchor in anchors)
                {
                    if (FindAnywhere(kart.transform, anchor) == null)
                    {
                        return "anchor " + anchor + " not found (KartEffects and the steering need it)";
                    }
                }
                return null;
            }
            finally
            {
                Object.DestroyImmediate(kart);
            }
        }

        // Child with this name at any depth.
        private static Transform FindAnywhere(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }
            return null;
        }
    }
}
