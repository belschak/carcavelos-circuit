using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Keeps the driver's hands on the steering wheel (Role 3, V3): each hand stays on the spot of the rim where it
    // holds the wheel in the seated pose, and the arm bends to reach it (TwoBoneIK). No Animation Rigging package
    // needed. Runs in LateUpdate, after KartVisualSteering has turned the wheel in Update.
    // Bones are the ones in Char_Rookie: LeftShoulder > LeftArm > LeftForeArm > LeftHand (same on the right).
    public class KartDriverArms : MonoBehaviour
    {
        private class Arm
        {
            public Transform clavicle, upper, lower, hand; // clavicle = LeftShoulder / RightShoulder
            public Quaternion clavicleRest, upperRest, lowerRest;   // local rotations in the seated pose
            public Vector3 gripPosition;                // where the hand holds the wheel, in wheel space
            public Quaternion gripRotation;             // the hand's rotation, in wheel space
            public Vector3 elbowPole;                   // the seated elbow, in the space of the arm's parent
            public float upperLength, lowerLength;
        }

        private const float MaxReachDegrees = 30f;   // how far a clavicle may turn to bring the hand to the rim

        private Transform wheel;
        private Arm left, right;

        private void Start()
        {
            wheel = Find("Steering_Wheel");
            // The seated pose is the reference: the wheel is still straight before the first frame.
            left = MakeArm("LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand");
            right = MakeArm("RightShoulder", "RightArm", "RightForeArm", "RightHand");
            if (wheel == null || left == null || right == null)
            {
                Debug.LogWarning("KartDriverArms: steering wheel or arm bones not found, the hands stay in the seated pose.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            Solve(left);
            Solve(right);
        }

        // Public so editor checks can run it without Play Mode.
        public void SolveNow()
        {
            if (wheel == null)
            {
                Start();
            }
            if (enabled)
            {
                LateUpdate();
            }
        }

        // How far (m) the hand that is furthest from its grip is from the rim right now. Used by the editor check.
        public float WorstGripError()
        {
            float worst = 0f;
            foreach (Arm arm in new[] { left, right })
            {
                if (arm != null)
                {
                    worst = Mathf.Max(worst, Vector3.Distance(arm.hand.position, wheel.TransformPoint(arm.gripPosition)));
                }
            }
            return worst;
        }

        private void Solve(Arm arm)
        {
            // Start from the seated pose every frame, so small errors never add up.
            arm.clavicle.localRotation = arm.clavicleRest;
            arm.upper.localRotation = arm.upperRest;
            arm.lower.localRotation = arm.lowerRest;

            Vector3 target = wheel.TransformPoint(arm.gripPosition);

            // Out of reach (far hand at full lock): the clavicle turns a little, so the shoulder comes forward to
            // the wheel, like a real driver reaching. At most MaxReachDegrees.
            Vector3 shoulder = arm.upper.position;
            float missing = Vector3.Distance(shoulder, target) - (arm.upperLength + arm.lowerLength) * 0.995f;
            if (missing > 0f)
            {
                Vector3 pivot = arm.clavicle.position;
                Vector3 wanted = shoulder + (target - shoulder).normalized * missing;
                Quaternion reach = Quaternion.FromToRotation(shoulder - pivot, wanted - pivot);
                arm.clavicle.rotation = Quaternion.RotateTowards(Quaternion.identity, reach, MaxReachDegrees) * arm.clavicle.rotation;
                shoulder = arm.upper.position;
            }

            Vector3 pole = arm.upper.parent.TransformPoint(arm.elbowPole);
            Vector3 elbow = TwoBoneIK.ElbowPosition(shoulder, arm.upperLength, arm.lowerLength, target, pole);

            // Point the upper arm at the new elbow, then the forearm at the target, then turn the hand like the grip.
            arm.upper.rotation = Quaternion.FromToRotation(arm.lower.position - shoulder, elbow - shoulder) * arm.upper.rotation;
            arm.lower.rotation = Quaternion.FromToRotation(arm.hand.position - arm.lower.position, target - arm.lower.position) * arm.lower.rotation;
            arm.hand.rotation = wheel.rotation * arm.gripRotation;
        }

        private Arm MakeArm(string clavicleName, string upperName, string lowerName, string handName)
        {
            Transform clavicle = Find(clavicleName), upper = Find(upperName), lower = Find(lowerName), hand = Find(handName);
            if (clavicle == null || upper == null || lower == null || hand == null || wheel == null)
            {
                return null;
            }
            return new Arm
            {
                clavicle = clavicle,
                upper = upper,
                lower = lower,
                clavicleRest = clavicle.localRotation,
                hand = hand,
                upperRest = upper.localRotation,
                lowerRest = lower.localRotation,
                gripPosition = wheel.InverseTransformPoint(hand.position),
                gripRotation = Quaternion.Inverse(wheel.rotation) * hand.rotation,
                elbowPole = upper.parent.InverseTransformPoint(lower.position),
                upperLength = Vector3.Distance(upper.position, lower.position),
                lowerLength = Vector3.Distance(lower.position, hand.position),
            };
        }

        private Transform Find(string partName)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == partName)
                {
                    return t;
                }
            }
            return null;
        }
    }
}
