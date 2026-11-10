using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // Drawing tool for GuardrailPath: select the path, then Shift + click on the floor in the Scene view adds a point.
    // The point lands on a flat plane at the height of the GuardrailPath object (so roofs and walls never catch the click).
    [CustomEditor(typeof(GuardrailPath))]
    public class GuardrailPathEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            GuardrailPath path = (GuardrailPath)target;
            EditorGUILayout.HelpBox("Shift + click in the Scene view adds a point at the height of this object (set its Y to the floor, 14.56 in the hall). "
                + "Move points with the normal move tool. Then run Carcavelos Circuit > Build White Guardrails (Role 2).", MessageType.Info);
            EditorGUILayout.LabelField("Points", path.transform.childCount.ToString());
            if (path.transform.childCount > 0 && GUILayout.Button("Remove last point"))
            {
                Undo.DestroyObjectImmediate(path.transform.GetChild(path.transform.childCount - 1).gameObject);
            }
        }

        private void OnSceneGUI()
        {
            GuardrailPath path = (GuardrailPath)target;
            Event e = Event.current;
            if (!e.shift)
            {
                return;
            }
            // While Shift is held, clicks belong to this tool instead of selecting other objects.
            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                Plane floor = new Plane(Vector3.up, path.transform.position);
                if (floor.Raycast(ray, out float distance))
                {
                    GameObject point = new GameObject("Point " + path.transform.childCount);
                    Undo.RegisterCreatedObjectUndo(point, "Add guardrail point");
                    point.transform.SetParent(path.transform, true);
                    point.transform.position = ray.GetPoint(distance);
                }
                e.Use();
            }
        }
    }
}
