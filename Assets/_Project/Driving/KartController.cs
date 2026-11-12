using CarcavelosCircuit.Shared;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarcavelosCircuit.Driving
{
    // The kart (tasks D1 and D3). One invisible ball (Rigidbody + SphereCollider) carries all the physics.
    // We never let it roll over: only its heading (yaw) can change, so the kart cannot flip.
    // Each physics step we split the ball's velocity into "forward" and "sideways", change the forward part
    // with the pedals, and remove most of the sideways part (that is the grip).
    // Drift (hold Space while steering): less grip, so the kart slides; the longer it lasts the more it charges.
    // Letting go of a charged drift gives a short boost. The charge rules live in DriftCharge (unit tested).
    // Ramp jump (D4): when the ground ends the kart is airborne. Gravity and its speed carry it through the air,
    // steering is weaker, and after the top of the jump it falls faster. It always lands upright, because only its
    // heading can change. The model (Visual) tilts to the slope and levels out in the air, only for looks.
    // Item boosts (Role 4's item boxes, IKartBoost): RaceFlow adds boost time from outside; the faster multiplier
    // of the drift boost and the item boost wins while the boost runs.
    // Bots (IKartDrivable, D8): a bot sets InputSource and the kart reads its input instead of the keyboard.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class KartController : MonoBehaviour, IKartState, IKartBoost, IKartDrivable
    {
        [SerializeField, Tooltip("All the driving numbers (Driving/Settings/KartTuning.asset)")]
        private KartTuning tuning;

        [SerializeField, Tooltip("ONLY for Driving_TestBed: read the keys even when RaceFlow has not switched input on. Must be OFF in the Kart prefab")]
        private bool driveWithoutRaceFlow;

        [SerializeField, Tooltip("The Visual child of the Kart. It tilts to the slope (looks only). Empty = no tilting")]
        private Transform visual;

        private Rigidbody body;
        private SphereCollider ball;
        private DriftCharge drift;
        private float throttleInput;   // +1 gas, -1 brake/reverse
        private float steerInput;      // +1 right, -1 left
        private bool driftHeld;        // drift key (Space) is down
        private float boostSecondsLeft;
        private float itemBoostMultiplier = 1f; // from an item (IKartBoost), 1 = no item boost running
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

        public bool IsDrifting { get { return drift != null && drift.IsActive; } }
        public float DriftCharge01 { get { return drift == null ? 0f : drift.Charge01; } }
        public int DriftStage { get { return drift == null ? 0 : drift.Stage; } }
        public bool IsBoosting { get { return boostSecondsLeft > 0f; } }

        // Simple "is the ground under the ball" check. The ramp jump (D4) builds on it.
        public bool IsGrounded { get { return grounded; } }

        // What the player steers right now (-1 left .. +1 right), so the model's steering wheel can follow it.
        // It is the key input itself, so it is 0 whenever input is off (countdown, results).
        public float Steer { get { return steerInput; } }

        // RaceFlow writes this every frame. The kart never switches it on by itself.
        public bool InputEnabled { get; set; }

        // IKartDrivable: null (the default) = the keyboard and the phone buttons drive. Set = this source drives,
        // for example a bot. InputEnabled still wins: when it is off, nobody drives.
        public IKartInputSource InputSource { get; set; }

        // True when the keys may be read (the test-bed switch counts as "on").
        private bool InputActive { get { return InputEnabled || driveWithoutRaceFlow; } }

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

            if (tuning != null)
            {
                drift = new DriftCharge(tuning.Stage1Seconds, tuning.Stage2Seconds,
                    tuning.Stage1BoostSeconds, tuning.Stage2BoostSeconds);
            }
        }

        private void Start()
        {
            if (tuning == null)
            {
                Debug.LogWarning("KartController has no KartTuning assigned, the kart cannot drive.", this);
            }
        }

        // The input is read every frame; physics runs in FixedUpdate.
        // InputPicker decides who drives: nobody (input off), the bot (InputSource) or the keyboard.
        private void Update()
        {
            KartInput input = InputPicker.Pick(InputActive, InputSource, ReadKeyboard());
            throttleInput = input.Throttle;
            steerInput = input.Steer;
            driftHeld = input.Drift;
        }

        // What the keyboard says this frame (nothing when there is no keyboard).
        private static KartInput ReadKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return KartInput.None;
            }
            float throttle = 0f;
            float steer = 0f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) throttle += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) throttle -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steer += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steer -= 1f;
            return new KartInput(throttle, steer, keyboard.spaceKey.isPressed);
        }

        private void FixedUpdate()
        {
            if (tuning == null)
            {
                return; // nothing to drive with (Console shows a warning from Start)
            }

            float dt = Time.fixedDeltaTime;
            CheckGround();
            if (boostSecondsLeft > 0f)
            {
                boostSecondsLeft -= dt;
                if (boostSecondsLeft <= 0f)
                {
                    itemBoostMultiplier = 1f; // the item boost is used up
                }
            }
            // Input switched off (finish line, results, pause): a running boost ends too, so the kart rolls out
            // by itself instead of being pushed on for the rest of the boost.
            if (!InputActive)
            {
                boostSecondsLeft = 0f;
                itemBoostMultiplier = 1f; // forget the item multiplier too, or it would carry over to the next item
            }
            UpdateDrift(dt);
            body.angularVelocity = Vector3.zero; // a wall hit must not spin the kart, only steering turns it

            if (!grounded)
            {
                FlyThroughAir(dt);
                return;
            }

            Vector3 velocity = body.linearVelocity;

            // Three directions on the ground: where the kart points, to its right, and straight out of the road.
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            Vector3 right = Vector3.Cross(groundNormal, forward);
            float forwardSpeed = Vector3.Dot(velocity, forward);
            float sidewaysSpeed = Vector3.Dot(velocity, right);
            float upSpeed = Vector3.Dot(velocity, groundNormal);

            forwardSpeed = ApplyPedals(forwardSpeed, dt);

            // Grip: remove part of the sideways speed every step, so the kart goes where it points.
            // While drifting the grip is low, so the kart slides through the corner.
            float speedBeforeGrip = Mathf.Sqrt(forwardSpeed * forwardSpeed + sidewaysSpeed * sidewaysSpeed);
            float grip = IsDrifting ? tuning.SideGrip * tuning.DriftGripMultiplier : tuning.SideGrip;
            sidewaysSpeed *= Mathf.Clamp01(1f - grip * dt);

            // In a drift the slide turns into forward speed instead of disappearing (only a little drag),
            // so a long drift does not stop the kart.
            if (IsDrifting)
            {
                float speedAfterGrip = Mathf.Sqrt(forwardSpeed * forwardSpeed + sidewaysSpeed * sidewaysSpeed);
                if (speedAfterGrip > 0.1f)
                {
                    float keep = speedBeforeGrip * (1f - tuning.DriftSpeedLoss * dt) / speedAfterGrip;
                    forwardSpeed *= keep;
                    sidewaysSpeed *= keep;
                }
            }

            body.linearVelocity = forward * forwardSpeed + right * sidewaysSpeed + groundNormal * upSpeed;

            TurnKart(forwardSpeed, dt, 1f);
        }

        // In the air the pedals and the grip do nothing: the speed from the ramp carries the kart.
        // Falling (after the top of the jump) gets extra gravity so the kart lands sooner and feels less floaty.
        private void FlyThroughAir(float dt)
        {
            Vector3 velocity = body.linearVelocity;
            if (velocity.y < 0f)
            {
                body.AddForce(Physics.gravity * (tuning.FallGravityMultiplier - 1f), ForceMode.Acceleration);
            }
            TurnKart(Vector3.Dot(velocity, transform.forward), dt, tuning.AirSteerFraction);
        }

        // Gas, brake/reverse, or roll out slowly when no key is pressed. Returns the new forward speed.
        // While boosting the kart accelerates by itself towards the higher boost top speed.
        private float ApplyPedals(float forwardSpeed, float dt)
        {
            if (IsBoosting)
            {
                float boostTop = tuning.MaxSpeed * Mathf.Max(tuning.BoostSpeedMultiplier, itemBoostMultiplier);
                return Mathf.MoveTowards(forwardSpeed, boostTop, tuning.BoostAcceleration * dt);
            }
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

        // Start, charge, release or cancel the drift. Called every physics step.
        private void UpdateDrift(float dt)
        {
            // Speed over the ground: in a drift the kart points into the corner, so its forward speed
            // drops even though it is still fast. That is why we measure the real movement here.
            Vector3 velocity = body.linearVelocity;
            float groundSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;

            if (!drift.IsActive)
            {
                bool fastEnough = groundSpeed > tuning.MaxSpeed * tuning.DriftStartSpeedFraction;
                if (driftHeld && Mathf.Abs(steerInput) > tuning.DriftMinSteer && fastEnough && grounded)
                {
                    drift.Begin(steerInput > 0f ? 1 : -1);
                }
                return;
            }

            // Too slow, in the air, or input switched off (finish line, results, pause): drift ends, no boost.
            bool tooSlow = groundSpeed < tuning.MaxSpeed * tuning.DriftMinSpeedFraction;
            if (!grounded || tooSlow || !InputActive)
            {
                drift.Cancel();
                return;
            }

            if (driftHeld)
            {
                // Steering into the corner charges faster than steering against it.
                float intoTurn = steerInput * drift.Direction; // +1 into the corner, -1 against it
                float chargeRate = Mathf.Lerp(tuning.ChargeRateAgainstTurn, tuning.ChargeRateIntoTurn, (intoTurn + 1f) / 2f);
                drift.Tick(dt, chargeRate);
                return;
            }

            // Key released: a charged drift gives a boost, an early release gives nothing.
            float earned = drift.Release();
            if (earned > boostSecondsLeft)
            {
                boostSecondsLeft = earned;
            }
        }

        // Turn the heading. Steering needs speed, and in reverse it turns the other way, like a real car.
        // In a drift the kart keeps turning into the corner; steering only makes the arc tighter or wider.
        // strength 1 = normal steering, less in the air.
        private void TurnKart(float forwardSpeed, float dt, float strength)
        {
            float steerAmount = steerInput;
            if (IsDrifting)
            {
                float intoTurn = steerInput * drift.Direction;
                steerAmount = drift.Direction * Mathf.Lerp(tuning.DriftMinTurn, tuning.DriftMaxTurn, (intoTurn + 1f) / 2f);
            }

            float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / tuning.FullSteerSpeed) * Mathf.Sign(forwardSpeed);
            float turnDegrees = steerAmount * tuning.TurnDegreesPerSecond * strength * speedFactor * dt;
            body.MoveRotation(Quaternion.Euler(0f, turnDegrees, 0f) * body.rotation);
        }

        // Looks only: the model leans to the slope under the kart and levels out again in the air.
        // The physics body never tilts, so this cannot affect driving or flipping.
        private void LateUpdate()
        {
            if (visual == null || tuning == null)
            {
                return;
            }
            Quaternion level = Quaternion.FromToRotation(Vector3.up, groundNormal) * transform.rotation;
            visual.rotation = Quaternion.Slerp(visual.rotation, level, tuning.VisualTiltSpeed * Time.deltaTime);
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

        // Hitting a wall (a surface that is steep, not a road or a ramp) cancels the drift, no boost.
        private void OnCollisionEnter(Collision collision)
        {
            if (IsDrifting && collision.GetContact(0).normal.y < 0.5f)
            {
                drift.Cancel();
            }
        }

        // Teleport + stop everything (used by Retry and Recovery): no velocity, no drift, no boost.
        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            throttleInput = 0f;
            steerInput = 0f;
            driftHeld = false;
            boostSecondsLeft = 0f;
            itemBoostMultiplier = 1f;
            if (drift != null)
            {
                drift.Cancel();
            }
        }

        // IKartBoost (Role 4's item boxes): adds boost time, so boosts chain; the faster multiplier wins.
        public void AddBoost(float seconds, float speedMultiplier)
        {
            boostSecondsLeft += seconds;
            itemBoostMultiplier = Mathf.Max(itemBoostMultiplier, speedMultiplier);
        }
    }
}
