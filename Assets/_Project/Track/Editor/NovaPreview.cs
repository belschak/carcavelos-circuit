using System.IO;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // Renders PNG previews of the built track from the editor (no Web build needed): one overview and
    // chase-camera views along the route. Used for QA screenshots in PRs. Files go OUTSIDE the repo.
    public static class NovaPreview
    {
        public static void RenderAll(string folder, Vector3[] route, int spawnIndex)
        {
            Directory.CreateDirectory(folder);
            Bounds area = new Bounds(route[0], Vector3.zero);
            for (int i = 0; i < route.Length; i++)
            {
                area.Encapsulate(route[i]);
            }
            Render(Path.Combine(folder, "overview.png"), area.center + new Vector3(-area.extents.x * 0.6f, Mathf.Max(area.extents.x, area.extents.z) * 1.4f, -area.extents.z * 1.6f), area.center, 50f);

            // Second overview from the campus side, looking south over the tunnel to the sea.
            float span = Mathf.Max(area.extents.x, area.extents.z);
            Render(Path.Combine(folder, "overview-sea.png"), area.center + new Vector3(area.extents.x * 0.4f, span * 0.9f, area.extents.z * 1.3f), area.center - new Vector3(0f, 0f, area.extents.z * 0.4f), 50f);

            int shots = 10;
            for (int k = 0; k < shots; k++)
            {
                int i = (spawnIndex + k * route.Length / shots) % route.Length;
                Vector3 forward = NovaRouteMath.Forward(route, i);
                Vector3 eye = route[i] - forward * 7f + Vector3.up * 2.8f; // roughly where the chase camera sits
                Render(Path.Combine(folder, "chase-" + k.ToString("00") + ".png"), eye, route[i] + forward * 12f + Vector3.up * 1f, 60f);
            }
            Debug.Log("NovaPreview: wrote " + (shots + 1) + " images to " + folder);
        }

        private static void Render(string file, Vector3 eye, Vector3 target, float fov)
        {
            GameObject go = new GameObject("PreviewCamera");
            Camera cam = go.AddComponent<Camera>();
            cam.transform.position = eye;
            cam.transform.LookAt(target);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 4000f;
            cam.clearFlags = CameraClearFlags.Skybox;
            RenderTexture rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(file, image.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
        }
    }
}
