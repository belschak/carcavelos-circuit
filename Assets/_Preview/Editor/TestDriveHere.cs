// TEST ONLY, never merge into main. One click to drive the open scene (e.g. Track_CampusWorld_Interior): puts Role 1's
// Kart at the scene's SpawnPoint, adds Role 1's ChaseCamera following it, lets the scene camera show that view and
// presses Play. Nothing is saved; the next "Build Campus World with Nova interior (test)" makes a fresh scene anyway.
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TestDriveHere
{
    private const string KartPrefab = "Assets/_Project/Driving/Kart.prefab";
    private const string ChaseCameraPrefab = "Assets/_Project/Driving/ChaseCamera.prefab";
    private const string SpawnName = "SpawnPoint";       // the start position in Luc's campus world (and its copy)
    private const string KartName = "Test Kart";
    private const string CameraName = "Test Chase Camera";
    private const float StartHeight = 0.5f;              // start a bit above the road so the kart does not start in it

    [MenuItem("Carcavelos Circuit/Test drive here (test)")]
    public static void Drive()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();

        // Where to start: the SpawnPoint of the open scene.
        Transform spawn = FindInScene(scene, SpawnName);
        if (spawn == null)
        {
            Debug.LogError("Test drive: no object called " + SpawnName + " in " + scene.name + ".");
            return;
        }

        // A kart and a chase camera from an earlier test drive go first, so there is only one of each.
        RemoveIfThere(scene, KartName);
        RemoveIfThere(scene, CameraName);

        GameObject kartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KartPrefab);
        GameObject cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChaseCameraPrefab);
        if (kartPrefab == null || cameraPrefab == null)
        {
            Debug.LogError("Test drive: " + KartPrefab + " or " + ChaseCameraPrefab + " not found.");
            return;
        }

        // The kart on the start line, facing along the track.
        var kart = (GameObject)PrefabUtility.InstantiatePrefab(kartPrefab, scene);
        kart.name = KartName;
        kart.transform.SetPositionAndRotation(spawn.position + Vector3.up * StartHeight, spawn.rotation);

        // Without RaceFlow nobody switches the keys on: use Role 1's test-bed switch, on this scene copy only
        // (the Kart prefab keeps it off, as Role 1 asks).
        Component controller = kart.GetComponent("KartController");
        if (controller != null)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("driveWithoutRaceFlow").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The chase camera follows the kart (same set-up as Role 1's Driving_TestBed).
        var chase = (GameObject)PrefabUtility.InstantiatePrefab(cameraPrefab, scene);
        chase.name = CameraName;
        CinemachineCamera cm = chase.GetComponent<CinemachineCamera>();
        if (cm != null) cm.Target.TrackingTarget = kart.transform;

        // A real camera with a CinemachineBrain shows what the chase camera sees.
        Camera cam = FindCamera(scene);
        if (cam == null)
        {
            var go = new GameObject("Test Camera");
            SceneManager.MoveGameObjectToScene(go, scene);
            cam = go.AddComponent<Camera>();
        }
        cam.gameObject.SetActive(true);
        cam.enabled = true;
        if (cam.GetComponent<CinemachineBrain>() == null) cam.gameObject.AddComponent<CinemachineBrain>();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Test drive: kart at " + SpawnName + ". W/S = gas/brake, A/D = steer, Space = drift.");
        EditorApplication.EnterPlaymode();
    }

    // First object with this name anywhere in the scene (also switched-off ones).
    private static Transform FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
        }
        return null;
    }

    private static void RemoveIfThere(Scene scene, string name)
    {
        Transform old = FindInScene(scene, name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
    }

    // The scene's own camera (Luc's "Preview Camera" in the campus world).
    private static Camera FindCamera(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera cam = root.GetComponentInChildren<Camera>(true);
            if (cam != null) return cam;
        }
        return null;
    }
}
