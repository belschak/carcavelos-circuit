using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Task V32: the far kart (LODGroup) uses materials the rest of the game never draws, and while two versions blend
    // Unity draws both with the LOD_FADE_CROSSFADE shader keyword. The first time the browser meets such a shader
    // combination it has to compile it, which can freeze one frame (a 218 ms hitch was measured once right after the
    // start). So when the first kart appears, this draws every material of both levels once, with and without the
    // cross-fade keyword, into a tiny picture nobody sees. After that the browser has them ready. Once per session.
    public class KartLodWarmup : MonoBehaviour
    {
        private const string CrossFade = "LOD_FADE_CROSSFADE";
        private const int PictureSize = 16;          // pixels; nothing is shown, the size only has to be valid
        private static bool warmedUp;                // all karts share the materials, so the first kart warms for all

        private void Start()
        {
            if (warmedUp)
            {
                return;
            }
            LODGroup group = GetComponent<LODGroup>();
            if (group == null)
            {
                return;   // a kart model without levels of detail has nothing new to warm up
            }
            warmedUp = true;
            DrawOnceOffscreen(group);
        }

        // Every material of every mesh in the group, each listed once (the same material on many parts counts once).
        public static List<Material> MaterialsToWarm(LODGroup group)
        {
            var list = new List<Material>();
            if (group == null)
            {
                return list;
            }
            LOD[] lods = group.GetLODs();
            for (int l = 0; l < lods.Length; l++)
            {
                Renderer[] renderers = lods[l].renderers;
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (renderers[r] == null)
                    {
                        continue;
                    }
                    Material[] materials = renderers[r].sharedMaterials;
                    for (int m = 0; m < materials.Length; m++)
                    {
                        if (materials[m] != null && !list.Contains(materials[m]))
                        {
                            list.Add(materials[m]);
                        }
                    }
                }
            }
            return list;
        }

        // A throw-away camera far below the world renders the kart's meshes with every material, plain and with the
        // cross-fade keyword, into a 16 x 16 picture, then everything is deleted again. The race camera never sees it.
        private void DrawOnceOffscreen(LODGroup group)
        {
            var target = new RenderTexture(PictureSize, PictureSize, 24);
            var cameraObject = new GameObject("KartLodWarmupCamera");
            cameraObject.transform.position = new Vector3(0f, -5000f, 0f);
            Camera warmCamera = cameraObject.AddComponent<Camera>();
            warmCamera.targetTexture = target;
            warmCamera.enabled = false;              // renders only when asked below
            warmCamera.nearClipPlane = 0.1f;
            warmCamera.farClipPlane = 50f;
            Matrix4x4 inFront = Matrix4x4.Translate(cameraObject.transform.position + Vector3.forward * 5f);

            var copies = new List<Material>();
            foreach (Material material in MaterialsToWarm(group))
            {
                Material fading = new Material(material);
                fading.EnableKeyword(CrossFade);
                copies.Add(fading);
                QueueMeshesUsing(group, material, warmCamera, inFront);
                QueueMeshesUsing(group, material, warmCamera, inFront, fading);
            }
            warmCamera.Render();
            Debug.Log("KartLodWarmup: " + copies.Count + " kart materials drawn once offscreen (plain + cross-fade).");

            warmCamera.targetTexture = null;
            target.Release();
            Destroy(target);
            Destroy(cameraObject);
            for (int i = 0; i < copies.Count; i++)
            {
                Destroy(copies[i]);
            }
        }

        // Queues each mesh part that uses "material" for the warm-up camera, drawn with "drawWith" (or the material).
        private static void QueueMeshesUsing(LODGroup group, Material material, Camera warmCamera, Matrix4x4 place,
                                             Material drawWith = null)
        {
            foreach (Renderer renderer in group.GetComponentsInChildren<Renderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;   // the driver is a skinned mesh in both levels: nothing new to warm up there
                }
                Material[] materials = renderer.sharedMaterials;
                for (int sub = 0; sub < materials.Length && sub < filter.sharedMesh.subMeshCount; sub++)
                {
                    if (materials[sub] == material)
                    {
                        var settings = new RenderParams(drawWith != null ? drawWith : material) { camera = warmCamera };
                        Graphics.RenderMesh(settings, filter.sharedMesh, sub, place);
                        return;   // one part per material is enough to compile its shader
                    }
                }
            }
        }
    }
}
