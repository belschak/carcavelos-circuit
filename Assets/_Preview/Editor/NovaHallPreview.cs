// TEST ONLY, never merge into main: opens a new empty scene with the Nova hall (Menu: Carcavelos Circuit > Preview Nova Hall ...).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NovaHallPreview
{
    [MenuItem("Carcavelos Circuit/Preview Nova Hall (temporary)")]
    public static void Open() { OpenFbx("Assets/_Preview/NovaHall.fbx"); }

    [MenuItem("Carcavelos Circuit/Preview Nova Hall in context (temporary)")]
    public static void OpenInContext() { OpenFbx("Assets/_Preview/NovaHallContext.fbx"); }

    private static void OpenFbx(string path)
    {
        // Ask to save the current scene (choose Don't Save), then start a fresh scene with a light and a camera.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { Debug.LogError(path + " not found (wait for the import to finish)."); return; }
        GameObject hall = (GameObject)PrefabUtility.InstantiatePrefab(asset);

        // Measure the hall, then look at it from inside the entrance hall.
        Bounds b = new Bounds(hall.transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer r in hall.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        Debug.Log("NovaHall bounds: centre " + b.center + ", size " + b.size);

        Selection.activeGameObject = hall;
        SceneView view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            view.LookAt(b.center, view.rotation, b.size.magnitude * 0.6f, false, true);
            view.Focus();
        }
    }
}
