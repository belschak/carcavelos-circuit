using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace CarcavelosCircuit.RaceFlow
{
    // Load time P8 (18 Nov 2026): on the Web the campus world (Track_CampusWorld, about 60 % of the download) is not
    // inside the game's .data but in its own file, an AssetBundle that Role4WebBuild makes next to it. Why:
    //   - the game code and menus start while the world still downloads (the page's loading bar simply goes on), and
    //   - the world file is named after its content, so the browser keeps it across deploys: after a deploy that did
    //     not touch the world, a returning player downloads only the code and menus again (about half).
    // RaceBootstrap calls Load() before it looks for the track scene; once the bundle is open the scene loads by its
    // name like any scene in the build. Without a world file (the Editor, tests, a build with the world inside the
    // .data) Load() does nothing and everything works as before.
    public static class WorldStream
    {
        private const int Attempts = 2; // one quiet retry for a dropped connection, then the page's network card

        private static AssetBundle bundle;

        // True when the world file could not be loaded: RaceBootstrap must then not fall back to a test track.
        public static bool Failed { get; private set; }

        // True when this run streams the world (so RaceBootstrap knows the page waits for the track).
        public static bool IsUsed { get; private set; }

        public static IEnumerator Load()
        {
            string url = WebPage.WorldBundleUrl;
            if (string.IsNullOrEmpty(url) || bundle != null)
            {
                yield break;
            }
            IsUsed = true;
            Failed = false;
            for (int attempt = 1; attempt <= Attempts; attempt++)
            {
                // No version/hash argument: on the Web the browser's IndexedDB cache (index.html cacheControl,
                // "immutable" for the hashed world-*.bundle names) keeps the file, Unity's own Caching is not used.
                using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(url))
                {
                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    float sent = -1f;
                    while (!operation.isDone)
                    {
                        float progress = Mathf.Clamp01(request.downloadProgress);
                        if (progress - sent >= 0.01f)
                        {
                            sent = progress;
                            WebPage.SendWorldState(WebPage.WorldLoading, progress);
                        }
                        yield return null;
                    }
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        bundle = DownloadHandlerAssetBundle.GetContent(request);
                        if (bundle != null)
                        {
                            WebPage.SendWorldState(WebPage.WorldLoading, 1f);
                            Debug.Log("WorldStream: campus world downloaded (" + request.downloadedBytes / 1048576 + " MB).");
                            yield break;
                        }
                    }
                    Debug.LogWarning("WorldStream: the world file did not load (attempt " + attempt + "): " + request.error);
                }
            }
            Failed = true;
            Debug.LogError("WorldStream: the campus world could not be downloaded.");
            WebPage.SendWorldState(WebPage.WorldFailed, 0f);
        }

        // After the world scene is loaded its objects no longer need the downloaded file; this frees its memory
        // (tens of MB, which matters on phones). The loaded scene and everything in it stay.
        public static void Release()
        {
            if (bundle != null)
            {
                bundle.Unload(false);
                bundle = null;
            }
        }
    }
}
