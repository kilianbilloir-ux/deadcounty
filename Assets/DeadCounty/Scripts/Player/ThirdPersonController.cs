using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    public enum Gait { Idle, Walk, Run, Sprint }

    // Third-person movement on a CharacterController with human-scale values: walk by default (1.5 m/s),
    // Shift runs (3.5 m/s), Alt sprints (6 m/s), C crouch-walks (1 m/s). Speed builds up and drops with real
    // acceleration, the body faces its velocity and turns at a rate that shrinks with speed, sharp reversals at
    // speed brake before pivoting, and climbing a slope slows the player down.
    // Look input rotates CameraTarget, which the Cinemachine ThirdPersonFollow camera tracks.
    // Grounding uses its own probe plus snapping, so walking down steps and ramps never flickers into the air
    // state; coyote time and a jump buffer make jumps forgiving; slopes steeper than the controller's slope
    // limit slide the player off. NoiseRadius exposes the GDD noise values for the zombie AI.
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Speeds (m/s)")]
        [SerializeField] float crouchSpeed = 1f;
        [SerializeField] float walkSpeed = 1.5f;
        [SerializeField] float runSpeed = 3.5f;
        [SerializeField] float sprintSpeed = 6f;
        [Tooltip("Gamepad stick deflection above which the player runs instead of walking.")]
        [SerializeField, Range(0f, 1f)] float stickRunThreshold = 0.7f;

        [Header("Acceleration and turning")]
        [SerializeField] float acceleration = 6f;
        [SerializeField] float deceleration = 9f;
        [SerializeField] float airAcceleration = 1.5f;
        [Tooltip("Body turn rate when standing (deg/s); it shrinks linearly down to the sprinting rate.")]
        [SerializeField] float standingTurnRate = 600f;
        [SerializeField] float sprintingTurnRate = 200f;
        [Tooltip("Heading changes wider than this while running brake to a stop before turning.")]
        [SerializeField] float pivotAngle = 120f;
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
        InputAction move, look, jump, run, sprint, crouch, cancel, click;
        Vector3 planarVelocity, heading, groundNormal = Vector3.up;
        float speed, yaw, pitch, verticalVelocity, cameraTargetStandingY, landingNoiseTimer, recoveryTimer;
        float lastGroundedTime = float.NegativeInfinity, lastJumpPressTime = float.NegativeInfinity;

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
        // Radius within which zombies hear the player this frame, 0 when silent.
        public float NoiseRadius { get; private set; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
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
            heading = transform.forward;
            cameraTargetStandingY = cameraTarget.localPosition.y;
        }

        void OnEnable() => SetCursorLocked(true);

        void OnDisable() => SetCursorLocked(false);

        void Update()
        {
            UpdateCursor();
            if (Cursor.lockState == CursorLockMode.Locked) ReadLook();
            UpdateCrouch();
            Move();
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
            var input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            var hasInput = input.sqrMagnitude > 0.01f;
            var onStick = move.activeControl?.device is Gamepad;
            Gait = !hasInput ? Gait.Idle
                : IsCrouching ? Gait.Walk
                : sprint.IsPressed() ? Gait.Sprint
                : run.IsPressed() || (onStick && input.magnitude > stickRunThreshold) ? Gait.Run
                : Gait.Walk;

            var wish = hasInput ? Quaternion.Euler(0f, Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg + yaw, 0f) * Vector3.forward : heading;
            var targetSpeed = !hasInput ? 0f : GaitSpeed() * (onStick && Gait == Gait.Walk ? Mathf.Clamp01(input.magnitude / stickRunThreshold) : 1f);

            if (IsGrounded)
            {
                if (hasInput)
                {
                    var turn = Vector3.Angle(heading, wish);
                    var turnRate = Mathf.Lerp(standingTurnRate, sprintingTurnRate, speed / sprintSpeed);
                    // A runner cannot reverse on the spot: plant, brake, then turn. Tight turns also cost speed.
                    var pivoting = turn > pivotAngle && speed > runSpeed * 0.7f;
                    if (pivoting)
                    {
                        targetSpeed = 0f;
                        turnRate *= 0.3f;
                    }
                    else
                    {
                        targetSpeed *= Mathf.Lerp(1f, 0.55f, turn / 180f) * SlopeFactor();
                    }
                    heading = Vector3.RotateTowards(heading, wish, turnRate * Mathf.Deg2Rad * dt, 0f);
                }
                if (recoveryTimer > 0f) targetSpeed *= 0.4f;
                speed = Mathf.MoveTowards(speed, targetSpeed, (targetSpeed > speed ? acceleration : deceleration) * dt);
                planarVelocity = heading * speed;
            }
            else
            {
                // Airborne: momentum carries, the player can only nudge it.
                planarVelocity = Vector3.MoveTowards(planarVelocity, wish * targetSpeed, airAcceleration * dt);
                speed = planarVelocity.magnitude;
                if (speed > 0.1f) heading = planarVelocity / speed;
            }
            transform.rotation = Quaternion.LookRotation(heading);
            recoveryTimer -= dt;

            if (jump.WasPressedThisFrame()) lastJumpPressTime = Time.time;
            var jumped = false;
            if (!IsCrouching && !IsSliding && recoveryTimer <= 0f && Time.time - lastGroundedTime <= coyoteTime && Time.time - lastJumpPressTime <= jumpBufferTime)
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
            var start = transform.position;
            controller.Move((planarVelocity + slide + Vector3.up * verticalVelocity) * dt);
            var moved = transform.position - start;
            CurrentSpeed = new Vector2(moved.x, moved.z).magnitude / Mathf.Max(dt, 1e-5f);
            // Walking into a wall bleeds the stored speed instead of keeping it for when the wall ends.
            if (IsGrounded && CurrentSpeed < speed) speed = Mathf.Max(CurrentSpeed, speed - deceleration * dt);

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
            var climb = Mathf.Atan(-Vector3.Dot(groundNormal, heading) / Mathf.Max(groundNormal.y, 0.1f)) * Mathf.Rad2Deg;
            return 1f - uphillSlowdown * Mathf.Clamp01(climb / 45f);
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
            if (crouch.WasPressedThisFrame() && (!IsCrouching || CanStand())) IsCrouching = !IsCrouching;

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
