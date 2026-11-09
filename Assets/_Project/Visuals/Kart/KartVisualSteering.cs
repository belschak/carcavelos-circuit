using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
// Visual steering for the NovaKart (Role 3, from Luc's NovaKart roster package v12). It moves no physics.
// KartVisualFromState (or Role 1's driving script) writes "steer" and "speed" every frame;
// this script turns the steering wheel, steers the front wheels and rolls all four wheels.
// With the Animation Rigging package the hands follow the wheel through an IK rig; without it the driver keeps
// the seated pose from the FBX (quick version, see TASKS V3).
public class KartVisualSteering : MonoBehaviour
{
    [Range(-1f, 1f)] public float steer;   // -1 = full left, 0 = straight, +1 = full right
    public float speed;                    // forward speed in metres per second (negative = reverse)

    [Header("Kart parts (found by name if left empty)")]
    public Transform steeringWheel;
    public Transform wheelFL, wheelFR, wheelRL, wheelRR;

    [Header("Tuning")]
    public float steeringWheelMaxAngle = 45f;   // steering-wheel turn at full lock (arms reach out; both hands stay on the rim up to here)
    public float frontWheelMaxAngle = 25f;      // front-wheel turn at full lock, in degrees
    public float wheelRadius = 0.21f;           // tyre radius in metres
    public float steerSmoothing = 12f;          // higher = the wheel follows the input faster
    public Vector3 steeringAxis = new Vector3(0f, 0.56f, -0.83f);   // steering column, pointing at the driver
    public float maxLean = 10f;                 // driver's upper body lean at full lock, in degrees
    public float maxTwist = 14f;                // shoulders turn with the wheel at full lock (keeps the outer hand on the rim)
    public Transform driverLean;                // "Lean_Driver" on the driver (found by name)

    Quaternion steeringWheelRest;   // rotation of the steering wheel when driving straight
    Quaternion leanRest;            // rotation of Lean_Driver relative to the kart when driving straight
    float shownSteer;               // smoothed steering value that is actually shown
    float rollAngle;                // how far the wheels have rolled, in degrees

    void Awake()
    {
        if (steeringWheel == null) steeringWheel = FindPart("Steering_Wheel");
        if (wheelFL == null) wheelFL = FindPart("Wheel_FL");
        if (wheelFR == null) wheelFR = FindPart("Wheel_FR");
        if (wheelRL == null) wheelRL = FindPart("Wheel_RL");
        if (wheelRR == null) wheelRR = FindPart("Wheel_RR");
        steeringWheelRest = steeringWheel.localRotation;
        if (driverLean == null) driverLean = FindPart("Lean_Driver", optional: true);
        if (driverLean != null) leanRest = Quaternion.Inverse(transform.rotation) * driverLean.rotation;
    }

    // Update runs before the Animator, so the hand IK already sees this frame's steering-wheel angle.
    void Update()
    {
        // 1) ease towards the input, so the wheel does not snap from left to right
        shownSteer = Mathf.Lerp(shownSteer, steer, 1f - Mathf.Exp(-steerSmoothing * Time.deltaTime));
        // 2) rolling: distance travelled / radius = angle in radians
        rollAngle += speed * Time.deltaTime / wheelRadius * Mathf.Rad2Deg;
        Apply();
    }

    // Show the current "steer" value immediately (used by editor previews and tests).
    public void ShowNow()
    {
        if (steeringWheel == null) Awake();
        shownSteer = steer;
        Apply();
    }

    void Apply()
    {
        // steering wheel: turn around the steering column
        steeringWheel.localRotation = Quaternion.AngleAxis(shownSteer * steeringWheelMaxAngle, steeringAxis.normalized) * steeringWheelRest;

        // wheels: roll around the axle (local X), front wheels also turn around the kart's up axis (Y)
        Quaternion roll = Quaternion.Euler(rollAngle, 0f, 0f);
        Quaternion turn = Quaternion.Euler(0f, shownSteer * frontWheelMaxAngle, 0f);
        wheelFL.localRotation = turn * roll;
        wheelFR.localRotation = turn * roll;
        wheelRL.localRotation = roll;
        wheelRR.localRotation = roll;

        // driver: the upper body leans to the OUTSIDE of the turn, as the cornering force pushes it
        // (this also brings the shoulder of the rising hand closer to the steering wheel)
        if (driverLean != null)
            driverLean.rotation = transform.rotation * Quaternion.AngleAxis(shownSteer * maxTwist, Vector3.up)
                                  * Quaternion.AngleAxis(-shownSteer * maxLean, Vector3.forward) * leanRest;
    }

    Transform FindPart(string partName, bool optional = false)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == partName) return t;
        if (!optional) Debug.LogError($"KartVisualSteering: part '{partName}' not found", this);
        return null;
    }
}
}
