// TEST ONLY, never merge into main. Builds a COPY of Luc's Track_CampusWorld scene with the Nova interior inside the
// Edificio Principal. Luc's scene, his models and main stay as they are.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CampusWorldInterior
{
    private const string SourceScene = "Assets/_Project/Visuals/Scenes/Track_CampusWorld.unity";
    private const string CopyScene = "Assets/_Preview/Track_CampusWorld_Interior.unity";
    private const string HallModel = "Assets/_Preview/NovaHallWorld.fbx";
    private const string BuildingId = "w665606788";          // Edificio Principal in Luc's CampusWorld model

    [MenuItem("Carcavelos Circuit/Build Campus World with Nova interior (test)")]
    public static void Build()
    {
        // Ask to save open changes (choose Don't Save for Luc's scene), then make a fresh copy of his scene.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CopyScene) != null) AssetDatabase.DeleteAsset(CopyScene);
        if (!AssetDatabase.CopyAsset(SourceScene, CopyScene)) { Debug.LogError("Could not copy " + SourceScene); return; }
        Scene scene = EditorSceneManager.OpenScene(CopyScene, OpenSceneMode.Single);

        // Switch off the old building block and the old drive-through dressing (in the copy only).
        int hidden = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains(BuildingId) || t.name == "Interior_Passages")
                {
                    t.gameObject.SetActive(false);
                    hidden++;
                }
            }
        }

        // Put the hall in at 0/0/0 (the model already sits at its real place) with colliders on every mesh.
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(HallModel);
        if (model == null) { Debug.LogError(HallModel + " not found (wait for the import)."); return; }
        GameObject hall = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        hall.name = "NovaHall (interior test)";
        foreach (MeshFilter mf in hall.GetComponentsInChildren<MeshFilter>(true))
        {
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            GameObjectUtility.SetStaticEditorFlags(mf.gameObject, StaticEditorFlags.BatchingStatic);
        }

        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = hall;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("Campus world with Nova interior: " + CopyScene + " (" + hidden + " old objects switched off).");
    }
}
