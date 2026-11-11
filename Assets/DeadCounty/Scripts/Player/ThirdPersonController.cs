using System;
using DeadCounty.Animation;
using DeadCounty.Core;
using DeadCounty.Locomotion;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    public enum Gait { Idle, Walk, Run, Sprint }

    // Third-person movement on a CharacterController with human-scale values: walk by default (1.5 m/s),
    // Shift runs (3.5 m/s), Alt sprints (6 m/s), C crouch-walks (1 m/s). Planar physics (acceleration, speed
    // dependent turn rate, pivot on sharp reversals, airborne momentum) come from PlanarLocomotion, shared with
    // the zombies; climbing a slope and hard landings slow the player down on top of it.
    // PlayerStamina gates the gaits (no run or sprint while exhausted) and pays for jumps.
    // Look input rotates CameraTarget, which the Cinemachine ThirdPersonFollow camera tracks.
    // Grounding uses its own probe plus snapping, so walking down steps and ramps never flickers into the air
    // state; coyote time and a jump buffer make jumps forgiving; slopes steeper than the controller's slope
    // limit slide the player off. NoiseRadius exposes the GDD noise values for the zombie AI.
    // Hits go through Knockback (collides like any move) and Stagger; Teleport warps the player and camera.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CharacterController), typeof(PlayerStamina))]
    public class ThirdPersonController : MonoBehaviour, IHumanoidMotion
    {
        [Header("Speeds (m/s)")]
        [SerializeField] float crouchSpeed = 1f;
        [SerializeField] float walkSpeed = 1.5f;
        [SerializeField] float runSpeed = 3.5f;
        [SerializeField] float sprintSpeed = 6f;
        [Tooltip("Gamepad stick deflection above which the player runs instead of walking.")]
        [SerializeField, Range(0f, 1f)] float stickRunThreshold = 0.7f;
        [Tooltip("Speed factor while carrying more than the inventory capacity; running and sprinting are locked.")]
        [SerializeField, Range(0f, 1f)] float encumberedSpeedFactor = 0.75f;

        [Header("Acceleration and turning")]
        [SerializeField] LocomotionSettings locomotion = LocomotionSettings.Player;
        [Tooltip("Fraction of speed lost on a 45 degree climb.")]
        [SerializeField, Range(0f, 1f)] float uphillSlowdown = 0.5f;

        [Header("Jump and gravity")]
        [SerializeField] float jumpHeight = 0.5f;
        [SerializeField] float gravity = -9.81f;
        [SerializeField] float coyoteTime = 0.12f;
        [SerializeField] float jumpBufferTime = 0.12f;
        [Tooltip("Landing faster than this (m/s) slows the player down while the legs absorb the impact.")]
        [SerializeField] float hardLandingSpeed = 5f;
        [SerializeField] float landingRecovery = 0.35f;

        [Header("Ground")]
        [SerializeField] LayerMask groundMask = 1;
        [Tooltip("Highest step down the player stays glued to instead of falling.")]
        [SerializeField] float snapDistance = 0.45f;
        [SerializeField] float slideSpeed = 6f;

        [Header("Crouch")]
        [SerializeField] float standingHeight = 1.8f;
        [SerializeField] float crouchHeight = 1.1f;
        [SerializeField] float heightChangeSpeed = 6f;

        [Header("Noise radius (m), GDD 8.7")]
        [SerializeField] float crouchNoise = 1f;
        [SerializeField] float walkNoise = 3f;
        [SerializeField] float runNoise = 8f;
        [SerializeField] float sprintNoise = 12f;
        [SerializeField] float landingNoise = 6f;

        [Header("Camera")]
        [SerializeField] Transform cameraTarget;
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float stickSpeed = 180f;
        [SerializeField] float minPitch = -40f;
        [SerializeField] float maxPitch = 70f;

        CharacterController controller;
        PlayerStamina stamina;
        InputAction move, look, jump, run, sprint, crouch, cancel, click;
        LocomotionState motion;
        Vector3 groundNormal = Vector3.up, knockbackVelocity;
        float yaw, pitch, verticalVelocity, cameraTargetStandingY, landingNoiseTimer, recoveryTimer;
        float knockbackTimer, staggerTimer, staggerSpeedFactor = 1f;
        float lastGroundedTime = float.NegativeInfinity, lastJumpPressTime = float.NegativeInfinity;
        bool menuOpen;

        // When false (death screen) movement, jump and crouch input are ignored and the player brakes; look still works.
        public bool InputEnabled { get; set; } = true;
        // While a menu is open (inventory) the cursor is free, the camera holds and the player brakes.
        public bool MenuOpen
        {
            get => menuOpen;
            set
            {
                menuOpen = value;
                SetCursorLocked(!value);
            }
        }
        // Movement, jump, crouch and interaction input are read.
        public bool Controllable => InputEnabled && !menuOpen;
        // Carrying more than the inventory capacity (GDD 8.5).
        public bool Encumbered { get; set; }
        // Actual horizontal velocity of the last move, after collisions.
        public Vector3 Velocity { get; private set; }
        // Increases on every Teleport so animators can snap instead of blending across the warp.
        public int MotionResetId { get; private set; }
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float SprintSpeed => sprintSpeed;
        public Gait Gait { get; private set; }
        // Actual horizontal speed after collisions, so walking into a wall reads as standing still.
        public float CurrentSpeed { get; private set; }
        public bool IsSprinting => Gait == Gait.Sprint;
        public bool IsCrouching { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsSliding { get; private set; }
        public float GroundAngle => Vector3.Angle(groundNormal, Vector3.up);
        // Camera aim in world space, used to turn the character's head toward where the player looks.
        public float AimYaw => yaw;
        public float AimPitch => pitch;
        public PoseModifiers Pose => PoseModifiers.Neutral;
        // Radius within which zombies hear the player this frame, 0 when silent.
        public float NoiseRadius { get; private set; }

        // Landing contribution to this frame's noise, 0 outside the hard-landing window; lets the emitter
        // type the footstep event as Landing when the landing peak dominates.
        public float LandingRadius => landingNoiseTimer > 0f ? landingNoise : 0f;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            stamina = GetComponent<PlayerStamina>();
            var actions = InputSystem.actions;
            move = actions.FindAction("Player/Move", true);
            look = actions.FindAction("Player/Look", true);
            jump = actions.FindAction("Player/Jump", true);
            run = actions.FindAction("Player/Run", true);
            sprint = actions.FindAction("Player/Sprint", true);
            crouch = actions.FindAction("Player/Crouch", true);
            cancel = actions.FindAction("UI/Cancel", true);
            click = actions.FindAction("UI/Click", true);
            yaw = transform.eulerAngles.y;
            motion = LocomotionState.Facing(transform.forward);
            cameraTargetStandingY = cameraTarget.localPosition.y;
        }

        void OnEnable() => SetCursorLocked(true);

        void OnDisable() => SetCursorLocked(false);

        void Update()
        {
            if (!menuOpen) UpdateCursor();
            if (!menuOpen && Cursor.lockState == CursorLockMode.Locked) ReadLook();
            UpdateCrouch();
            Move();
            UpdateStamina();
            UpdateNoise();
            // World rotation set last so the body turning this frame does not drag the camera.
            cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // Escape frees the cursor (editor, alt-tab); clicking back into the game locks it again.
        void UpdateCursor()
        {
            if (cancel.WasPressedThisFrame()) SetCursorLocked(false);
            else if (click.WasPressedThisFrame() && Cursor.lockState != CursorLockMode.Locked) SetCursorLocked(true);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void ReadLook()
        {
            var delta = look.ReadValue<Vector2>();
            // Mouse delta is already per frame; stick values are a rate and need deltaTime.
            delta *= look.activeControl?.device is Pointer ? mouseSensitivity : stickSpeed * Time.deltaTime;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
        }

        void Move()
        {
            var dt = Time.deltaTime;
            var input = Controllable ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            var hasInput = input.sqrMagnitude > 0.01f;
            var onStick = move.activeControl?.device is Gamepad;
            var staggered = staggerTimer > 0f;
            Gait = !hasInput ? Gait.Idle
                : IsCrouching || staggered || Encumbered ? Gait.Walk
                : sprint.IsPressed() && stamina.Model.CanSprint ? Gait.Sprint
                : (run.IsPressed() || (onStick && input.magnitude > stickRunThreshold)) && stamina.Model.CanRun ? Gait.Run
                : Gait.Walk;

            var wish = hasInput ? Quaternion.Euler(0f, Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg + yaw, 0f) * Vector3.forward : motion.Heading;
            var targetSpeed = !hasInput ? 0f : GaitSpeed() * (onStick && Gait == Gait.Walk ? Mathf.Clamp01(input.magnitude / stickRunThreshold) : 1f);
            if (IsGrounded)
            {
                targetSpeed *= SlopeFactor();
                if (recoveryTimer > 0f) targetSpeed *= 0.4f;
            }
            if (staggered) targetSpeed *= staggerSpeedFactor;
            if (Encumbered) targetSpeed *= encumberedSpeedFactor;
            PlanarLocomotion.Step(ref motion, locomotion, wish, targetSpeed, hasInput, IsGrounded, dt);
            transform.rotation = Quaternion.LookRotation(motion.Heading);
            recoveryTimer -= dt;
            staggerTimer -= dt;

            if (Controllable && jump.WasPressedThisFrame()) lastJumpPressTime = Time.time;
            var jumped = false;
            // Stamina is checked last so it is only spent on a jump that actually fires.
            if (Controllable && !IsCrouching && !IsSliding && recoveryTimer <= 0f && Time.time - lastGroundedTime <= coyoteTime
                && Time.time - lastJumpPressTime <= jumpBufferTime && stamina.Model.TryJump())
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                lastJumpPressTime = lastGroundedTime = float.NegativeInfinity;
                jumped = true;
            }
            else if (IsGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            verticalVelocity += gravity * dt;

            var slide = IsSliding ? Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized * slideSpeed : Vector3.zero;
            var knock = knockbackVelocity * Mathf.Clamp(knockbackTimer, 0f, dt);
            knockbackTimer -= dt;
            var start = transform.position;
            controller.Move((motion.PlanarVelocity + slide + Vector3.up * verticalVelocity) * dt + knock);
            var moved = transform.position - start;
            Velocity = new Vector3(moved.x, 0f, moved.z) / Mathf.Max(dt, 1e-5f);
            CurrentSpeed = Velocity.magnitude;
            // Walking into a wall bleeds the stored speed instead of keeping it for when the wall ends.
            if (IsGrounded && CurrentSpeed < motion.Speed) motion.Speed = Mathf.Max(CurrentSpeed, motion.Speed - locomotion.Deceleration * dt);

            var wasGrounded = IsGrounded;
            if (wasGrounded && !jumped && !controller.isGrounded && Probe(snapDistance, out var below))
                controller.Move(Vector3.down * below.distance);
            UpdateGround(wasGrounded);
        }

        float GaitSpeed() => IsCrouching ? crouchSpeed : Gait switch
        {
            Gait.Sprint => sprintSpeed,
            Gait.Run => runSpeed,
            _ => walkSpeed,
        };

        // Uphill along the heading slows the player down; flat ground and descents keep full speed.
        float SlopeFactor()
        {
            var climb = Mathf.Atan(-Vector3.Dot(groundNormal, motion.Heading) / Mathf.Max(groundNormal.y, 0.1f)) * Mathf.Rad2Deg;
            return 1f - uphillSlowdown * Mathf.Clamp01(climb / 45f);
        }

        void UpdateStamina()
        {
            var activity = !IsGrounded ? StaminaActivity.Airborne
                : Gait == Gait.Sprint ? StaminaActivity.Sprint
                : Gait == Gait.Run ? StaminaActivity.Run
                : CurrentSpeed > 0.1f ? StaminaActivity.Walk
                : StaminaActivity.Idle;
            stamina.Model.Tick(activity, Time.deltaTime);
        }

        // Moves the player along `displacement` over `duration` through the CharacterController, so walls stop it.
        public void Knockback(Vector3 displacement, float duration)
        {
            if (duration <= 0f) throw new ArgumentOutOfRangeException(nameof(duration), duration, "Knockback needs a positive duration");
            knockbackVelocity = displacement / duration;
            knockbackTimer = duration;
        }

        // Scales the target speed by `speedFactor` and forbids running and sprinting for `duration` seconds.
        public void Stagger(float duration, float speedFactor)
        {
            staggerTimer = duration;
            staggerSpeedFactor = speedFactor;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            var delta = position - transform.position;
            motion = LocomotionState.Facing(rotation * Vector3.forward);
            // A CharacterController overwrites a moved transform with its own position unless it is disabled.
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(motion.Heading));
            controller.enabled = true;

            verticalVelocity = 0f;
            knockbackTimer = staggerTimer = recoveryTimer = landingNoiseTimer = 0f;
            lastJumpPressTime = float.NegativeInfinity;
            Velocity = Vector3.zero;
            CurrentSpeed = 0f;
            yaw = transform.eulerAngles.y;
            cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
            CinemachineCore.OnTargetObjectWarped(cameraTarget, delta);
            MotionResetId++;
        }

        // Back on both feet at full height, e.g. when respawning after dying while crouched.
        public void StandUp()
        {
            IsCrouching = false;
            controller.height = standingHeight;
            controller.center = Vector3.up * standingHeight / 2f;
            var target = cameraTarget.localPosition;
            target.y = cameraTargetStandingY;
            cameraTarget.localPosition = target;
        }

        void UpdateGround(bool wasGrounded)
        {
            IsGrounded = controller.isGrounded || (verticalVelocity <= 0f && Probe(0.08f, out _));
            // Face normal from a straight ray: sphere casts return slanted normals on step edges.
            var center = transform.position + Vector3.up * 0.5f;
            groundNormal = Physics.Raycast(center, Vector3.down, out var hit, 0.8f, groundMask, QueryTriggerInteraction.Ignore) ? hit.normal : Vector3.up;
            IsSliding = IsGrounded && GroundAngle > controller.slopeLimit + 1f;
            if (!IsGrounded) return;
            if (!wasGrounded && verticalVelocity < -hardLandingSpeed)
            {
                landingNoiseTimer = 0.3f;
                recoveryTimer = landingRecovery;
            }
            lastGroundedTime = Time.time;
        }

        // Sphere cast from just above the feet; returns the gap between the feet and the ground.
        bool Probe(float distance, out RaycastHit hit)
        {
            const float lift = 0.1f;
            var radius = controller.radius * 0.9f;
            var origin = transform.position + Vector3.up * (radius + lift);
            if (!Physics.SphereCast(origin, radius, Vector3.down, out hit, distance + lift, groundMask, QueryTriggerInteraction.Ignore))
                return false;
            hit.distance -= lift;
            return true;
        }

        void UpdateNoise()
        {
            landingNoiseTimer -= Time.deltaTime;
            var gaitNoise = IsCrouching ? crouchNoise : Gait switch
            {
                Gait.Sprint => sprintNoise,
                Gait.Run => runNoise,
                _ => walkNoise,
            };
            var steps = !IsGrounded ? 0f : gaitNoise * Mathf.Clamp01(CurrentSpeed / GaitSpeed());
            NoiseRadius = landingNoiseTimer > 0f ? Mathf.Max(steps, landingNoise) : steps;
        }

        void UpdateCrouch()
        {
            if (Controllable && crouch.WasPressedThisFrame() && (!IsCrouching || CanStand())) IsCrouching = !IsCrouching;

            var height = Mathf.MoveTowards(controller.height, IsCrouching ? crouchHeight : standingHeight, heightChangeSpeed * Time.deltaTime);
            controller.height = height;
            controller.center = Vector3.up * height / 2f;
            var target = cameraTarget.localPosition;
            target.y = cameraTargetStandingY * height / standingHeight;
            cameraTarget.localPosition = target;
        }

        // Prevents standing up under a low ceiling such as the 1.2 m crouch tunnel.
        bool CanStand()
        {
            var radius = controller.radius;
            var origin = transform.position + Vector3.up * (controller.height - radius);
            return !Physics.SphereCast(origin, radius * 0.95f, Vector3.up, out _, standingHeight - controller.height, groundMask, QueryTriggerInteraction.Ignore);
        }
    }
}
