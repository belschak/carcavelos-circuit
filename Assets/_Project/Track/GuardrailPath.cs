using UnityEngine;

namespace CarcavelosCircuit.Track
{
    // T7: one hand-drawn guardrail line inside the Nova hall, where the route data does not match the hall.
    // Its children are the points of the line, in order. Draw them with Shift + click in the Scene view
    // (see GuardrailPathEditor); WhiteGuardrailBuilder turns every line into the white fence.
    public class GuardrailPath : MonoBehaviour
    {
        // Shows the line in the Scene view, so you can see what you drew.
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.8f);
            for (int i = 0; i < transform.childCount; i++)
            {
                Vector3 p = transform.GetChild(i).position;
                Gizmos.DrawSphere(p, 0.25f);
                if (i > 0)
                {
                    Gizmos.DrawLine(transform.GetChild(i - 1).position, p);
                }
            }
        }
    }
}
