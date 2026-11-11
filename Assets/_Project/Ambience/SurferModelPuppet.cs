using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // Put this on an imported, rigged human model to let it ride instead of the code-built surfer (see ISurferVisual).
    // It follows the board every frame and tells the model's Animator what the surfer does: an int parameter with
    // the SurferState number (0 Hidden, 1 Paddling, 2 Waiting, 3 Catching, 4 Riding, 5 Falling, 6 Finished) and a
    // float parameter with the lean into the turn (-1..1). Parameters the Animator does not have are skipped.
    public class SurferModelPuppet : MonoBehaviour, ISurferVisual
    {
        [SerializeField, Tooltip("The model's Animator (optional: without one the model is only moved, not animated)")]
        private Animator animator;

        [SerializeField, Tooltip("Animator int parameter that gets the surfer's state number")]
        private string stateParameter = "SurferState";

        [SerializeField, Tooltip("Animator float parameter that gets the lean into the turn (-1..1)")]
        private string leanParameter = "Lean";

        [SerializeField, Tooltip("Where the model's feet stand on the board, metres in rider space (+z = nose, +y = up)")]
        private Vector3 offset = Vector3.zero;

        [SerializeField, Tooltip("Turn of the model on the board, degrees. Most models face +z; a surfer stands sideways = 90")]
        private float yaw = 90f;

        private bool checkedParameters;
        private bool hasState;
        private bool hasLean;

        public void Show(SurferBrain brain, Matrix4x4 rider, Vector3[] joints)
        {
            if (brain == null || !brain.Visible)
            {
                Hide();
                return;
            }
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            transform.position = rider.MultiplyPoint3x4(offset);
            transform.rotation = rider.rotation * Quaternion.Euler(0f, yaw, 0f);
            transform.localScale = rider.lossyScale;
            Animate(brain);
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void Animate(SurferBrain brain)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }
            if (!checkedParameters)
            {
                // Look once which parameters exist, so a model without them never fills the log with warnings.
                checkedParameters = true;
                AnimatorControllerParameter[] parameters = animator.parameters;
                for (int i = 0; i < parameters.Length; i++)
                {
                    hasState |= parameters[i].name == stateParameter && parameters[i].type == AnimatorControllerParameterType.Int;
                    hasLean |= parameters[i].name == leanParameter && parameters[i].type == AnimatorControllerParameterType.Float;
                }
            }
            if (hasState)
            {
                animator.SetInteger(stateParameter, (int)brain.State);
            }
            if (hasLean)
            {
                animator.SetFloat(leanParameter, brain.Lean);
            }
        }
    }
}
