using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    // Third-person movement on a CharacterController: walk, sprint, crouch toggle, jump, gravity.
    // Look input rotates CameraTarget, which the Cinemachine ThirdPersonFollow camera tracks; moving turns
    // the body toward the camera-relative input direction.
    // Grounding uses its own probe plus snapping, so walking down steps and ramps never flickers into the air
    // state; coyote time and a jump buffer make jumps forgiving; slopes steeper than the controller's slope
    // limit slide the player off. NoiseRadius exposes the GDD noise values for the zombie AI.
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Speeds (m/s)")]
        [SerializeField] float walkSpeed = 2.5f;
        [SerializeField] float sprintSpeed = 5.5f;
        [SerializeField] float crouchSpeed = 1.4f;
        [SerializeField] float groundAcceleration = 20f;
        [SerializeField] float airAcceleration = 4f;
        [SerializeField] float turnSmoothTime = 0.1f;

        [Header("Jump and gravity")]
        [SerializeField] float jumpHeight = 1f;
        [SerializeField] float gravity = -18f;
        [SerializeField] float coyoteTime = 0.12f;
        [SerializeField] float jumpBufferTime = 0.12f;

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
        [SerializeField] float sprintNoise = 12f;
        [SerializeField] float landingNoise = 6f;

        [Header("Camera")]
        [SerializeField] Transform cameraTarget;
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float stickSpeed = 180f;
        [SerializeField] float minPitch = -40f;
        [SerializeField] float maxPitch = 70f;

        CharacterController controller;
        InputAction move, look, jump, sprint, crouch, cancel, click;
        Vector3 planarVelocity, groundNormal = Vector3.up;
        float yaw, pitch, verticalVelocity, turnVelocity, cameraTargetStandingY, landingNoiseTimer;
        float lastGroundedTime = float.NegativeInfinity, lastJumpPressTime = float.NegativeInfinity;

        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        // Actual horizontal speed after collisions, so walking into a wall reads as standing still.
        public float CurrentSpeed { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsSliding { get; private set; }
        public float GroundAngle => Vector3.Angle(groundNormal, Vector3.up);
        // Radius within which zombies hear the player this frame, 0 when silent.
        public float NoiseRadius { get; private set; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            var actions = InputSystem.actions;
            move = actions.FindAction("Player/Move", true);
            look = actions.FindAction("Player/Look", true);
            jump = actions.FindAction("Player/Jump", true);
            sprint = actions.FindAction("Player/Sprint", true);
            crouch = actions.FindAction("Player/Crouch", true);
            cancel = actions.FindAction("UI/Cancel", true);
            click = actions.FindAction("UI/Click", true);
            yaw = transform.eulerAngles.y;
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
            IsSprinting = hasInput && !IsCrouching && sprint.IsPressed();
            var targetSpeed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;

            var wish = Vector3.zero;
            if (hasInput)
            {
                var targetAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg + yaw;
                var angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnVelocity, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
                wish = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward * (targetSpeed * input.magnitude);
            }
            planarVelocity = Vector3.MoveTowards(planarVelocity, wish, (IsGrounded ? groundAcceleration : airAcceleration) * dt);

            if (jump.WasPressedThisFrame()) lastJumpPressTime = Time.time;
            var jumped = false;
            if (!IsCrouching && !IsSliding && Time.time - lastGroundedTime <= coyoteTime && Time.time - lastJumpPressTime <= jumpBufferTime)
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

            var wasGrounded = IsGrounded;
            if (wasGrounded && !jumped && !controller.isGrounded && Probe(snapDistance, out var below))
                controller.Move(Vector3.down * below.distance);
            UpdateGround(wasGrounded);
        }

        void UpdateGround(bool wasGrounded)
        {
            IsGrounded = controller.isGrounded || (verticalVelocity <= 0f && Probe(0.08f, out _));
            // Face normal from a straight ray: sphere casts return slanted normals on step edges.
            var center = transform.position + Vector3.up * 0.5f;
            groundNormal = Physics.Raycast(center, Vector3.down, out var hit, 0.8f, groundMask, QueryTriggerInteraction.Ignore) ? hit.normal : Vector3.up;
            IsSliding = IsGrounded && GroundAngle > controller.slopeLimit + 1f;
            if (!IsGrounded) return;
            if (!wasGrounded && verticalVelocity < -5f) landingNoiseTimer = 0.3f;
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
            var fullSpeed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            var steps = !IsGrounded ? 0f : (IsCrouching ? crouchNoise : IsSprinting ? sprintNoise : walkNoise) * Mathf.Clamp01(CurrentSpeed / fullSpeed);
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
