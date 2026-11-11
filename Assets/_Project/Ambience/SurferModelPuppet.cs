using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // Put this on an imported, rigged human model (e.g. a Microsoft Rocketbox avatar, see
    // Ambience/ThirdParty/Rocketbox) to let it ride instead of the code-built surfer (see ISurferVisual).
    // Every frame it stands on the board and bends its bones into the same pose as the code-built surfer
    // (SurferBody.PoseJoints): each bone is turned so it points from its joint to the next one (hip -> knee,
    // knee -> foot, shoulder -> elbow ...). So one pose list drives both bodies: paddling, sitting, the surf stance,
    // the lean into a carve and the fall. Bones that are not set are left alone (the model is then only moved).
    // Optionally an Animator gets the SurferState number (int) and the lean (float).
    public class SurferModelPuppet : MonoBehaviour, ISurferVisual
    {
        // Leg length (hip -> knee -> foot) of the code-built surfer's skeleton, metres: the model is scaled to it so
        // its feet land on the board.
        public const float CodeLegLength = 0.86f;

        [SerializeField, Tooltip("Hips (Rocketbox: Bip01 Pelvis)")] private Transform pelvis;
        [SerializeField, Tooltip("Lowest spine bone; turning it turns the whole upper body (Bip01 Spine)")] private Transform spine;
        [SerializeField, Tooltip("Bip01 Neck")] private Transform neck;
        [SerializeField, Tooltip("Bip01 Head")] private Transform head;
        [SerializeField, Tooltip("Bip01 L UpperArm")] private Transform upperArmL;
        [SerializeField, Tooltip("Bip01 L Forearm")] private Transform forearmL;
        [SerializeField, Tooltip("Bip01 L Hand")] private Transform handL;
        [SerializeField, Tooltip("Bip01 R UpperArm")] private Transform upperArmR;
        [SerializeField, Tooltip("Bip01 R Forearm")] private Transform forearmR;
        [SerializeField, Tooltip("Bip01 R Hand")] private Transform handR;
        [SerializeField, Tooltip("Bip01 L Thigh")] private Transform thighL;
        [SerializeField, Tooltip("Bip01 L Calf")] private Transform calfL;
        [SerializeField, Tooltip("Bip01 L Foot")] private Transform footL;
        [SerializeField, Tooltip("Bip01 L Toe0")] private Transform toeL;
        [SerializeField, Tooltip("Bip01 R Thigh")] private Transform thighR;
        [SerializeField, Tooltip("Bip01 R Calf")] private Transform calfR;
        [SerializeField, Tooltip("Bip01 R Foot")] private Transform footR;
        [SerializeField, Tooltip("Bip01 R Toe0")] private Transform toeR;

        [SerializeField, Tooltip("The model's Animator (optional)")]
        private Animator animator;

        [SerializeField, Tooltip("Animator int parameter that gets the surfer's state number")]
        private string stateParameter = "SurferState";

        [SerializeField, Tooltip("Animator float parameter that gets the lean into the turn (-1..1)")]
        private string leanParameter = "Lean";

        private bool ready;
        private Transform[] all;
        private Quaternion[] bindRotations;
        private float bodyScale = 1f;
        private Vector3 headForward;           // which way the face looks, in the head bone's own space
        private bool checkedParameters;
        private bool hasState;
        private bool hasLean;

        // True when every bone is set, so the model can be posed (false = it is only moved onto the board).
        public bool CanPose
        {
            get
            {
                return pelvis != null && spine != null && neck != null && head != null && upperArmL != null && forearmL != null
                       && handL != null && upperArmR != null && forearmR != null && handR != null && thighL != null && calfL != null
                       && footL != null && toeL != null && thighR != null && calfR != null && footR != null && toeR != null;
            }
        }

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
            Prepare();
            Vector3 scale = rider.lossyScale;
            transform.position = rider.MultiplyPoint3x4(Vector3.zero);
            transform.rotation = rider.rotation;
            transform.localScale = scale * bodyScale;
            if (CanPose && joints != null && joints.Length >= SurferBody.JointCount)
            {
                Pose(rider, joints);
            }
            Animate(brain);
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        // Once: remember the model's rest pose (to start every frame from it, so small errors never pile up), its
        // leg length (for the scale) and which way its face looks.
        private void Prepare()
        {
            if (ready)
            {
                return;
            }
            ready = true;
            if (!CanPose)
            {
                return;
            }
            all = new[] { pelvis, spine, neck, head, upperArmL, forearmL, handL, upperArmR, forearmR, handR,
                          thighL, calfL, footL, toeL, thighR, calfR, footR, toeR };
            bindRotations = new Quaternion[all.Length];
            for (int i = 0; i < all.Length; i++)
            {
                bindRotations[i] = all[i].localRotation;
            }
            float leg = Vector3.Distance(thighL.position, calfL.position) + Vector3.Distance(calfL.position, footL.position);
            float rootScale = transform.lossyScale.x;
            if (leg > 0.0001f && rootScale > 0.0001f)
            {
                bodyScale = CodeLegLength / (leg / rootScale);
            }
            // In the rest pose the toes point the way the model faces.
            Vector3 facing = (toeL.position + toeR.position) * 0.5f - (footL.position + footR.position) * 0.5f;
            facing.y = 0f;
            facing = facing.sqrMagnitude < 0.000001f ? transform.forward : facing.normalized;
            headForward = head.InverseTransformDirection(facing);
        }

        private void Pose(Matrix4x4 rider, Vector3[] j)
        {
            for (int i = 0; i < all.Length; i++)
            {
                all[i].localRotation = bindRotations[i];
            }
            // Hips: the line from the left to the right hip, and up the back.
            Match(pelvis, thighR.position - thighL.position, neck.position - pelvis.position,
                  rider.MultiplyVector(j[SurferBody.HipR] - j[SurferBody.HipL]), rider.MultiplyVector(j[SurferBody.Neck] - j[SurferBody.Pelvis]));
            // Upper body: the shoulder line and the spine.
            Match(spine, upperArmR.position - upperArmL.position, neck.position - spine.position,
                  rider.MultiplyVector(j[SurferBody.ShoulderR] - j[SurferBody.ShoulderL]), rider.MultiplyVector(j[SurferBody.Neck] - j[SurferBody.Pelvis]));
            Aim(neck, neck, head, rider.MultiplyVector(j[SurferBody.Head] - j[SurferBody.Neck]));
            // Head: up through the neck, the face towards the point the surfer looks at.
            Match(head, head.TransformDirection(headForward), head.position - neck.position,
                  rider.MultiplyVector(j[SurferBody.LookAt] - j[SurferBody.Head]), rider.MultiplyVector(j[SurferBody.Head] - j[SurferBody.Neck]));
            Aim(upperArmL, upperArmL, forearmL, rider.MultiplyVector(j[SurferBody.ElbowL] - j[SurferBody.ShoulderL]));
            Aim(forearmL, forearmL, handL, rider.MultiplyVector(j[SurferBody.HandL] - j[SurferBody.ElbowL]));
            Aim(upperArmR, upperArmR, forearmR, rider.MultiplyVector(j[SurferBody.ElbowR] - j[SurferBody.ShoulderR]));
            Aim(forearmR, forearmR, handR, rider.MultiplyVector(j[SurferBody.HandR] - j[SurferBody.ElbowR]));
            Aim(thighL, thighL, calfL, rider.MultiplyVector(j[SurferBody.KneeL] - j[SurferBody.HipL]));
            Aim(calfL, calfL, footL, rider.MultiplyVector(j[SurferBody.FootL] - j[SurferBody.KneeL]));
            Aim(footL, footL, toeL, rider.MultiplyVector(j[SurferBody.ToeL] - j[SurferBody.FootL]));
            Aim(thighR, thighR, calfR, rider.MultiplyVector(j[SurferBody.KneeR] - j[SurferBody.HipR]));
            Aim(calfR, calfR, footR, rider.MultiplyVector(j[SurferBody.FootR] - j[SurferBody.KneeR]));
            Aim(footR, footR, toeR, rider.MultiplyVector(j[SurferBody.ToeR] - j[SurferBody.FootR]));
            // Finally move the whole model so its hips are where the pose wants them.
            transform.position += rider.MultiplyPoint3x4(j[SurferBody.Pelvis]) - pelvis.position;
        }

        // Turns 'bone' (and everything under it) so the line from 'from' to 'to' points along 'target'.
        public static void Aim(Transform bone, Transform from, Transform to, Vector3 target)
        {
            Vector3 now = to.position - from.position;
            if (now.sqrMagnitude < 0.0000001f || target.sqrMagnitude < 0.0000001f)
            {
                return;
            }
            bone.rotation = Quaternion.FromToRotation(now, target) * bone.rotation;
        }

        // Turns 'bone' so two of its directions (a, roughly square to b) match two wanted ones.
        public static void Match(Transform bone, Vector3 a, Vector3 b, Vector3 wantA, Vector3 wantB)
        {
            if (a.sqrMagnitude < 0.0000001f || b.sqrMagnitude < 0.0000001f || wantA.sqrMagnitude < 0.0000001f || wantB.sqrMagnitude < 0.0000001f)
            {
                return;
            }
            Quaternion now = Quaternion.LookRotation(a, b);
            Quaternion want = Quaternion.LookRotation(wantA, wantB);
            bone.rotation = want * Quaternion.Inverse(now) * bone.rotation;
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
