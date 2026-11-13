using System.Collections;
using CarcavelosCircuit.Track;
using Unity.Cinemachine;
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

        [SerializeField, Tooltip("Role 3's kart model (KartVisual). Put into the kart's empty Visual slot until Role 3 does it in the prefab (Q14 bridge). May be empty")]
        private GameObject kartModel;

        [SerializeField]
        private RaceManager raceManager;

        [SerializeField, Tooltip("Role 1's Cinemachine chase camera, connected to the race (Q13). May be empty: the race still runs")]
        private ChaseCameraRig chaseCamera;

        // Start can be a coroutine: Unity waits for each "yield return" without freezing the game.
        private IEnumerator Start()
        {
            // -1 = no fixed frame rate. On the Web the browser then draws in step with the screen (vsync), which is
            // the smoothest; a fixed number would switch to a timer and judder.
            Application.targetFrameRate = -1;

            // Try the track scenes in RaceSettings order (Role 2's campus scene first, then the team's other campus
            // scenes, our small test loop last). The first one that is in the build list and has a track wins.
            string[] candidates = settings.TrackScenesInOrder();
            TrackInfo track = null;
            string trackScene = "";
            for (int i = 0; i < candidates.Length && track == null; i++)
            {
                yield return LoadSceneIfNeeded(candidates[i]);
                track = FindTrackIn(candidates[i]);
                if (track != null)
                {
                    trackScene = candidates[i];
                }
                else if (SceneManager.GetSceneByName(candidates[i]).isLoaded)
                {
                    // A scene without a track (still being built): close it so its test camera or lights do not show
                    // up in the race. A plain log line: an empty track scene is normal while its owner builds it.
                    Debug.Log("RaceBootstrap: no TrackInfo found in " + candidates[i] + ", trying the next track scene.");
                    yield return SceneManager.UnloadSceneAsync(candidates[i]);
                }
            }

            if (track == null)
            {
                Debug.LogError("RaceBootstrap: no track to race on. Check the scene names in RaceSettings and File > Build Profiles.");
                yield break;
            }
            Debug.Log("RaceBootstrap: racing on " + trackScene + ".");

            // Use the track scene's light and sky.
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(trackScene));
            TurnOffTestCameras(trackScene);

            Transform spawn = track.SpawnPoint;
            GameObject kartObject = Instantiate(kartPrefab, spawn.position, spawn.rotation);
            kartObject.name = "Kart";
            KartModelSwap.AddModelIfMissing(kartObject, kartModel);
            for (int i = 0; i < kartAttachments.Length; i++)
            {
                if (kartAttachments[i] != null)
                {
                    Instantiate(kartAttachments[i], kartObject.transform);
                }
            }

            // The race first, the camera after: a missing camera must never stop the race from being set up.
            raceManager.SetUp(kartObject, track, trackScene);
            if (chaseCamera != null)
            {
                chaseCamera.SetTarget(kartObject.transform);
                raceManager.Restarted += chaseCamera.SnapBehindTarget;
                raceManager.Recovered += chaseCamera.SnapBehindTarget; // R or a fall teleports the kart: cut, do not swoop
            }
        }

        // Symmetric to the += above: never leave a listener on the RaceManager.
        private void OnDestroy()
        {
            if (chaseCamera != null && raceManager != null)
            {
                raceManager.Restarted -= chaseCamera.SnapBehindTarget;
                raceManager.Recovered -= chaseCamera.SnapBehindTarget;
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
                // Normal for a track scene that is not merged yet; a plain log so the Console stays free of warnings.
                Debug.Log("RaceBootstrap: scene " + sceneName + " is not in the build scene list, skipped.");
                yield break;
            }
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }

        // A track scene may have its own camera for testing it alone. In the race only Main's camera may render
        // and listen (two AudioListeners = Unity warning, two cameras = wrong picture, two Cinemachine cameras = a fight).
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
                // A test Cinemachine camera in a track scene would compete with Main's chase camera for the brain.
                foreach (CinemachineCamera testCamera in roots[i].GetComponentsInChildren<CinemachineCamera>())
                {
                    testCamera.enabled = false;
                }
            }
        }

        // Looks through the scene's top-level objects for Role 2's TrackInfo. The old RaceFlow stand-in
        // PlaceholderTrack is a TrackInfo too (a subclass), so scenes built before the swap are still found.
        private static TrackInfo FindTrackIn(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded)
            {
                return null;
            }
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                TrackInfo track = roots[i].GetComponentInChildren<TrackInfo>();
                if (track != null)
                {
                    return track;
                }
            }
            return null;
        }
    }
}
