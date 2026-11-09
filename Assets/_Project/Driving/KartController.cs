using CarcavelosCircuit.Shared;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarcavelosCircuit.Driving
{
    // The kart (task D1). One invisible ball (Rigidbody + SphereCollider) carries all the physics.
    // We never let it roll over: only its heading (yaw) can change, so the kart cannot flip.
    // Each physics step we split the ball's velocity into "forward" and "sideways", change the forward part
    // with the pedals, and remove most of the sideways part (that is the grip).
    // Drift, boost and the ramp jump come in later tasks (D3, D4); their IKartState values are fixed placeholders for now.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class KartController : MonoBehaviour, IKartState
    {
        [SerializeField, Tooltip("All the driving numbers (Driving/Settings/KartTuning.asset)")]
        private KartTuning tuning;

        [SerializeField, Tooltip("ONLY for Driving_TestBed: read the keys even when RaceFlow has not switched input on. Must be OFF in the Kart prefab")]
        private bool driveWithoutRaceFlow;

        private Rigidbody body;
        private SphereCollider ball;
        private float throttleInput;   // +1 gas, -1 brake/reverse
        private float steerInput;      // +1 right, -1 left
        private bool grounded;
        private Vector3 groundNormal = Vector3.up;

        // Speed along the way the kart points (negative = reversing).
        public float Speed
        {
            get { return body == null ? 0f : Vector3.Dot(body.linearVelocity, transform.forward); }
        }

        public float MaxSpeed
        {
            get { return tuning == null ? 0f : tuning.MaxSpeed; }
        }

        // Drift and boost arrive in D3.
        public bool IsDrifting { get { return false; } }
        public float DriftCharge01 { get { return 0f; } }
        public int DriftStage { get { return 0; } }
        public bool IsBoosting { get { return false; } }

        // Simple "is the ground under the ball" check. The ramp jump (D4) builds on it.
        public bool IsGrounded { get { return grounded; } }

        // RaceFlow writes this every frame. The kart never switches it on by itself.
        public bool InputEnabled { get; set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ball = GetComponent<SphereCollider>();

            // Set in code so the prefab needs no Inspector clicks and nobody can change them by accident.
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ; // cannot tip over
            body.interpolation = RigidbodyInterpolation.Interpolate;  // smooth movement for the camera
            body.collisionDetectionMode = CollisionDetectionMode.Continuous; // fast kart cannot tunnel through walls

            // A ball with no friction: the ground and the walls must not grab it, grip is done in FixedUpdate.
            PhysicsMaterial slippery = new PhysicsMaterial("KartBall");
            slippery.staticFriction = 0f;
            slippery.dynamicFriction = 0f;
            slippery.frictionCombine = PhysicsMaterialCombine.Minimum;
            ball.sharedMaterial = slippery;
        }

        // Keys are read every frame; physics runs in FixedUpdate.
        private void Update()
        {
            throttleInput = 0f;
            steerInput = 0f;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !(InputEnabled || driveWithoutRaceFlow))
            {
                return;
            }
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) throttleInput += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) throttleInput -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steerInput += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steerInput -= 1f;
        }

        private void FixedUpdate()
        {
            if (tuning == null)
            {
                return; // nothing to drive with (Console shows a warning from Start)
            }

            float dt = Time.fixedDeltaTime;
            CheckGround();
            if (!grounded)
            {
                return; // in the air gravity does the work (steering in the air comes with D4)
            }

            body.angularVelocity = Vector3.zero; // a wall hit must not spin the kart, only steering turns it

            Vector3 velocity = body.linearVelocity;

            // Three directions on the ground: where the kart points, to its right, and straight out of the road.
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            Vector3 right = Vector3.Cross(groundNormal, forward);
            float forwardSpeed = Vector3.Dot(velocity, forward);
            float sidewaysSpeed = Vector3.Dot(velocity, right);
            float upSpeed = Vector3.Dot(velocity, groundNormal);

            forwardSpeed = ApplyPedals(forwardSpeed, dt);

            // Grip: remove part of the sideways speed every step, so the kart goes where it points.
            sidewaysSpeed *= Mathf.Clamp01(1f - tuning.SideGrip * dt);

            body.linearVelocity = forward * forwardSpeed + right * sidewaysSpeed + groundNormal * upSpeed;

            Steer(forwardSpeed, dt);
        }

        // Gas, brake/reverse, or roll out slowly when no key is pressed. Returns the new forward speed.
        private float ApplyPedals(float forwardSpeed, float dt)
        {
            if (throttleInput > 0f)
            {
                // Pressing gas while rolling backwards is braking, so use the strong brake first.
                float rate = forwardSpeed < 0f ? tuning.BrakeDeceleration : tuning.Acceleration;
                return Mathf.MoveTowards(forwardSpeed, tuning.MaxSpeed, rate * dt);
            }
            if (throttleInput < 0f)
            {
                float rate = forwardSpeed > 0f ? tuning.BrakeDeceleration : tuning.ReverseAcceleration;
                return Mathf.MoveTowards(forwardSpeed, -tuning.MaxReverseSpeed, rate * dt);
            }
            return Mathf.MoveTowards(forwardSpeed, 0f, tuning.CoastDeceleration * dt);
        }

        // Turn the heading. Steering needs speed, and in reverse it turns the other way, like a real car.
        private void Steer(float forwardSpeed, float dt)
        {
            float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / tuning.FullSteerSpeed) * Mathf.Sign(forwardSpeed);
            float turnDegrees = steerInput * tuning.TurnDegreesPerSecond * speedFactor * dt;
            body.MoveRotation(Quaternion.Euler(0f, turnDegrees, 0f) * body.rotation);
        }

        // One ray from the centre of the ball straight down. Checkpoint triggers are ignored.
        private void CheckGround()
        {
            float radius = ball.radius * transform.lossyScale.x;
            RaycastHit hit;
            grounded = Physics.Raycast(transform.TransformPoint(ball.center), Vector3.down, out hit,
                radius + tuning.GroundCheckExtra, ~0, QueryTriggerInteraction.Ignore);
            groundNormal = grounded ? hit.normal : Vector3.up;
        }

        private void Start()
        {
            if (tuning == null)
            {
                Debug.LogWarning("KartController has no KartTuning assigned, the kart cannot drive.", this);
            }
        }

        // Teleport + stop everything (used by Retry and Recovery). Drift and boost are cleared here once D3 exists.
        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            throttleInput = 0f;
            steerInput = 0f;
        }
    }
}
