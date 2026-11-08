using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R1: Main.unity starts here. It loads the track scene on top of Main ("additively"),
    // spawns the kart at the track's SpawnPoint, and hands everything to the RaceManager.
    // One scene per owner: the track lives in its own scene, Main only adds the race around it.
    public class RaceBootstrap : MonoBehaviour
    {
        [SerializeField, Tooltip("Which track scene to load (RaceFlow/Settings/RaceSettings.asset)")]
        private RaceSettings settings;

        [SerializeField, Tooltip("The kart to spawn. Must have a component that implements IKartState")]
        private GameObject kartPrefab;

        [SerializeField, Tooltip("Spawned as children of the kart, e.g. the KartAudio prefab (engine, drift, boost sounds)")]
        private GameObject[] kartAttachments;

        [SerializeField]
        private RaceManager raceManager;

        [SerializeField, Tooltip("Follows the kart. Replaced by the Role 1 chase camera later; may be left empty then")]
        private PlaceholderChaseCamera chaseCamera;

        // Start can be a coroutine: Unity waits for each "yield return" without freezing the game.
        private IEnumerator Start()
        {
            string trackScene = settings.TrackSceneName;
            yield return LoadSceneIfNeeded(trackScene);
            PlaceholderTrack track = FindTrackIn(trackScene);

            // While the campus scene is still empty (Role 2 is building it), use our small test loop.
            if (track == null && settings.FallbackTrackSceneName != "")
            {
                // A warning, not a log line: on swap day Role 2's TrackInfo may already be in the campus scene while
                // this code still looks for PlaceholderTrack. Then we would race the test loop without noticing.
                Debug.LogWarning("RaceBootstrap: no PlaceholderTrack found in " + trackScene + ", racing the fallback "
                    + settings.FallbackTrackSceneName + " instead. If Role 2's TrackInfo is already in " + trackScene
                    + ", do the swap (docs/explain/role4-walkthrough.md, section 5).");
                // Close the unfinished campus scene so its test camera or lights do not show up in the race.
                if (SceneManager.GetSceneByName(trackScene).isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(trackScene);
                }
                trackScene = settings.FallbackTrackSceneName;
                yield return LoadSceneIfNeeded(trackScene);
                track = FindTrackIn(trackScene);
            }

            if (track == null)
            {
                Debug.LogError("RaceBootstrap: no track to race on. Check the scene names in RaceSettings and File > Build Profiles.");
                yield break;
            }

            // Use the track scene's light and sky.
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(trackScene));
            TurnOffTestCameras(trackScene);

            Transform spawn = track.SpawnPoint;
            GameObject kartObject = Instantiate(kartPrefab, spawn.position, spawn.rotation);
            kartObject.name = "Kart";
            for (int i = 0; i < kartAttachments.Length; i++)
            {
                if (kartAttachments[i] != null)
                {
                    Instantiate(kartAttachments[i], kartObject.transform);
                }
            }

            // The race first, the camera after: the placeholder camera is removed once Role 1's camera exists,
            // and a missing camera must never stop the race from being set up.
            raceManager.SetUp(kartObject, track, trackScene);
            if (chaseCamera != null)
            {
                chaseCamera.SetTarget(kartObject.transform);
                raceManager.Restarted += chaseCamera.SnapBehindTarget;
            }
        }

        // Symmetric to the += above: never leave a listener on the RaceManager.
        private void OnDestroy()
        {
            if (chaseCamera != null && raceManager != null)
            {
                raceManager.Restarted -= chaseCamera.SnapBehindTarget;
            }
        }

        private IEnumerator LoadSceneIfNeeded(string sceneName)
        {
            if (SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                yield break; // already open, e.g. both scenes open in the Editor
            }
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning("RaceBootstrap: scene " + sceneName + " is not in the build scene list.");
                yield break;
            }
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }

        // A track scene may have its own camera for testing it alone. In the race only Main's camera may render
        // and listen (two AudioListeners = Unity warning, two cameras = wrong picture).
        private static void TurnOffTestCameras(string sceneName)
        {
            GameObject[] roots = SceneManager.GetSceneByName(sceneName).GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                foreach (Camera cam in roots[i].GetComponentsInChildren<Camera>())
                {
                    cam.gameObject.SetActive(false);
                }
                foreach (AudioListener listener in roots[i].GetComponentsInChildren<AudioListener>())
                {
                    listener.enabled = false;
                }
            }
        }

        // Looks through the scene's top-level objects for the track component.
        private static PlaceholderTrack FindTrackIn(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded)
            {
                return null;
            }
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                PlaceholderTrack track = roots[i].GetComponentInChildren<PlaceholderTrack>();
                if (track != null)
                {
                    return track;
                }
            }
            return null;
        }
    }
}
