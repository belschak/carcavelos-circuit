using CarcavelosCircuit.Shared;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarcavelosCircuit.RaceFlow
{
    // STAND-IN kart so the race flow, HUD and sound can be tested before Alessandro's KartController (Role 1) exists.
    // It is deliberately simple (a box that slides) and only implements the IKartState contract,
    // plus IKartBoost so the item boxes (task R7) can be tested with it too, and IKartDrivable so RaceFlow's dev test
    // bots (task R8) can drive it before Role 1's KartController has D8.
    // Main.unity swaps it for the real Kart prefab: RaceBootstrap > Kart Prefab.
    [RequireComponent(typeof(Rigidbody))]
    public class PlaceholderKart : MonoBehaviour, IKartState, IKartBoost, IKartDrivable
    {
        [SerializeField, Tooltip("Top speed in metres per second (20 m/s = 72 km/h)")]
        private float maxSpeed = 20f;

        [SerializeField, Tooltip("How fast the kart gains speed, in m/s per second")]
        private float acceleration = 14f;

        [SerializeField, Tooltip("Turning speed at full steering, in degrees per second")]
        private float turnDegreesPerSecond = 120f;

        [SerializeField, Tooltip("How quickly sideways sliding is removed. Lower = more slidey")]
        private float sideGrip = 8f;

        [SerializeField, Tooltip("Seconds of drifting until blue sparks (stage 1) and orange sparks (stage 2)")]
        private float stage1Seconds = 0.8f;

        [SerializeField]
        private float stage2Seconds = 1.6f;

        [SerializeField, Tooltip("Top speed multiplier while boosting")]
        private float boostMultiplier = 1.35f;

        [SerializeField, Tooltip("Boost length after a stage 1 / stage 2 drift")]
        private float stage1BoostSeconds = 0.4f;

        [SerializeField]
        private float stage2BoostSeconds = 1.2f;

        private Rigidbody body;
        private float throttleInput;
        private float steerInput;
        private bool driftHeld;
        private float driftSeconds;
        private float boostSecondsLeft;
        private float itemBoostMultiplier = 1f; // from an item (IKartBoost), 1 = no item boost running

        public float Speed
        {
            get { return body == null ? 0f : Vector3.Dot(body.linearVelocity, transform.forward); }
        }

        public float MaxSpeed
        {
            get { return maxSpeed; }
        }

        public bool IsDrifting { get; private set; }

        public float DriftCharge01
        {
            get { return IsDrifting ? Mathf.Clamp01(driftSeconds / stage2Seconds) : 0f; }
        }

        public int DriftStage
        {
            get
            {
                if (!IsDrifting) return 0;
                if (driftSeconds >= stage2Seconds) return 2;
                if (driftSeconds >= stage1Seconds) return 1;
                return 0;
            }
        }

        public bool IsBoosting
        {
            get { return boostSecondsLeft > 0f; }
        }

        public bool IsGrounded { get; private set; }

        // Steer was added to IKartState for Role 3's steering wheel (2026-11-10); the stand-in kart never reports it.
        public float Steer => 0f;

        public bool InputEnabled { get; set; }

        // IKartDrivable: null = the keyboard (as before). Set = a bot drives; InputEnabled still wins.
        public IKartInputSource InputSource { get; set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        // Read the keyboard every frame (input is read in Update, physics happens in FixedUpdate).
        private void Update()
        {
            throttleInput = 0f;
            steerInput = 0f;
            driftHeld = false;
            if (InputEnabled && InputSource != null)
            {
                KartInput input = InputSource.ReadInput().Clamped(); // a bot's maths error cannot make it faster
                throttleInput = input.Throttle;
                steerInput = input.Steer;
                driftHeld = input.Drift;
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (!InputEnabled || keyboard == null || InputSource != null)
            {
                return;
            }
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) throttleInput += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) throttleInput -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steerInput += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steerInput -= 1f;
            driftHeld = keyboard.spaceKey.isPressed;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            IsGrounded = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, 0.8f);

            if (boostSecondsLeft > 0f)
            {
                boostSecondsLeft -= dt;
                if (boostSecondsLeft <= 0f)
                {
                    itemBoostMultiplier = 1f; // the item boost is used up
                }
            }
            UpdateDrift(dt);

            if (!IsGrounded)
            {
                return; // no grip in the air
            }

            Vector3 velocity = body.linearVelocity;
            float forwardSpeed = Vector3.Dot(velocity, transform.forward);
            float topSpeed = IsBoosting ? maxSpeed * Mathf.Max(boostMultiplier, itemBoostMultiplier) : maxSpeed;

            // Gas, brake/reverse, or roll out slowly when no key is pressed.
            if (IsBoosting)
            {
                velocity += transform.forward * acceleration * 2f * dt;
            }
            else if (throttleInput > 0f && forwardSpeed < topSpeed)
            {
                velocity += transform.forward * acceleration * dt;
            }
            else if (throttleInput < 0f && forwardSpeed > -maxSpeed * 0.4f)
            {
                velocity -= transform.forward * acceleration * 1.5f * dt;
            }
            else
            {
                velocity -= transform.forward * forwardSpeed * 0.8f * dt;
            }

            // Remove part of the sideways speed so the kart goes where it points. Drifting keeps more slide.
            float speedBeforeGrip = FlatSpeed(velocity);
            float sidewaysSpeed = Vector3.Dot(velocity, transform.right);
            float grip = IsDrifting ? sideGrip * 0.3f : sideGrip;
            velocity -= transform.right * sidewaysSpeed * Mathf.Clamp01(grip * dt);

            // While drifting the slide turns into forward speed instead of disappearing,
            // so a long drift does not stop the kart (only a little drag).
            if (IsDrifting)
            {
                float speedAfterGrip = FlatSpeed(velocity);
                if (speedAfterGrip > 0.1f)
                {
                    float keep = speedBeforeGrip * (1f - 0.1f * dt) / speedAfterGrip;
                    velocity = new Vector3(velocity.x * keep, velocity.y, velocity.z * keep);
                }
            }

            // Never faster than the top speed.
            float flatSpeed = FlatSpeed(velocity);
            if (flatSpeed > topSpeed)
            {
                float scale = topSpeed / flatSpeed;
                velocity = new Vector3(velocity.x * scale, velocity.y, velocity.z * scale);
            }
            body.linearVelocity = velocity;

            // Steering only works while moving; backwards it turns the other way, like a real car.
            float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 5f) * Mathf.Sign(forwardSpeed);
            float turn = steerInput * turnDegreesPerSecond * speedFactor * dt;
            if (IsDrifting)
            {
                turn = turn * 0.8f; // wider arc, so the drift can last long enough for orange sparks
            }
            body.MoveRotation(body.rotation * Quaternion.Euler(0f, turn, 0f));
        }

        // Drift: hold Space while steering at speed. Let go when charged = boost.
        private void UpdateDrift(float dt)
        {
            // Use the real speed over the ground: in a drift the kart points into the corner,
            // so its forward speed (Speed) drops even though it is still fast.
            float groundSpeed = body == null ? 0f : FlatSpeed(body.linearVelocity);
            bool fastEnough = groundSpeed > maxSpeed * 0.4f;
            if (!IsDrifting)
            {
                if (driftHeld && Mathf.Abs(steerInput) > 0.5f && fastEnough && IsGrounded)
                {
                    IsDrifting = true;
                    driftSeconds = 0f;
                }
                return;
            }

            if (!IsGrounded || groundSpeed < maxSpeed * 0.3f)
            {
                IsDrifting = false; // too slow or airborne: cancel, no boost
                return;
            }

            if (driftHeld)
            {
                driftSeconds += dt;
                return;
            }

            // Input switched off (finish line, results): end the drift without a boost.
            if (!InputEnabled)
            {
                IsDrifting = false;
                return;
            }

            // Released: the boost length depends on the stage we reached.
            int stage = DriftStage;
            IsDrifting = false;
            if (stage == 2) boostSecondsLeft = stage2BoostSeconds;
            else if (stage == 1) boostSecondsLeft = stage1BoostSeconds;
        }

        // Speed over the ground, ignoring up/down movement.
        private static float FlatSpeed(Vector3 velocity)
        {
            return new Vector3(velocity.x, 0f, velocity.z).magnitude;
        }

        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            IsDrifting = false;
            driftSeconds = 0f;
            boostSecondsLeft = 0f;
            itemBoostMultiplier = 1f;
        }

        // IKartBoost: an item adds boost time; the faster multiplier wins while the boost runs.
        public void AddBoost(float seconds, float speedMultiplier)
        {
            boostSecondsLeft += seconds;
            itemBoostMultiplier = Mathf.Max(itemBoostMultiplier, speedMultiplier);
        }
    }
}
