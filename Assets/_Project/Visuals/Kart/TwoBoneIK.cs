using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Two-bone inverse kinematics as plain maths (no GameObjects, unit tested): where must the elbow be so that an
    // upper arm of length upperLength and a forearm of length lowerLength reach from the shoulder to the target?
    // The elbow lies in the plane through shoulder, target and pole (a point the elbow should point towards).
    public static class TwoBoneIK
    {
        public static Vector3 ElbowPosition(Vector3 shoulder, float upperLength, float lowerLength, Vector3 target, Vector3 pole)
        {
            Vector3 toTarget = target - shoulder;
            // Out of reach: stretch the arm straight towards the target. Too close: keep a tiny distance.
            float reach = Mathf.Clamp(toTarget.magnitude, 0.0001f, upperLength + lowerLength - 0.0001f);
            Vector3 along = toTarget.sqrMagnitude > 0f ? toTarget.normalized : Vector3.forward;

            // Law of cosines: how far along the shoulder-target line the elbow sits, and how far it sticks out.
            float alongDistance = (upperLength * upperLength - lowerLength * lowerLength + reach * reach) / (2f * reach);
            float sideDistance = Mathf.Sqrt(Mathf.Max(upperLength * upperLength - alongDistance * alongDistance, 0f));

            // The side the elbow bends to: towards the pole, at right angles to the shoulder-target line.
            Vector3 side = Vector3.ProjectOnPlane(pole - shoulder, along);
            if (side.sqrMagnitude < 1e-8f)
            {
                side = Vector3.ProjectOnPlane(Vector3.down, along);   // pole on the line: bend downwards
            }
            return shoulder + along * alongDistance + side.normalized * sideDistance;
        }
    }
}
