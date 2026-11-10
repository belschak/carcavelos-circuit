// TEST ONLY, never merge into main. Rebuilds the campus-world copy with the Nova interior by itself whenever Unity
// imports a new NovaHallWorld.fbx, so nobody has to remember the menu (Carcavelos Circuit > Build Campus World with
// Nova interior (test)).
using UnityEditor;

public class NovaHallAutoRebuild : AssetPostprocessor
{
    private const string HallModel = "Assets/_Preview/NovaHallWorld.fbx";
    private const string CutMeshDir = "Assets/_Preview/CutMeshes";

    // Once after loading: if the copy was never built with the ground cut out, build it now.
    [InitializeOnLoadMethod]
    private static void BuildOnceIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (!AssetDatabase.IsValidFolder(CutMeshDir) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                CampusWorldInterior.Build();
            }
        };
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (path == HallModel)
            {
                // Run after the import has finished, and never while playing.
                EditorApplication.delayCall += () =>
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode) CampusWorldInterior.Build();
                };
                return;
            }
        }
    }
}
