using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    // Third-person movement on a CharacterController: walk, sprint, crouch toggle, jump, gravity.
    // Look input rotates CameraTarget, which the Cinemachine ThirdPersonFollow camera tracks; moving turns
    // the body toward the camera-relative input direction.
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Speeds (m/s)")]
        [SerializeField] float walkSpeed = 2.5f;
        [SerializeField] float sprintSpeed = 5.5f;
        [SerializeField] float crouchSpeed = 1.4f;
        [SerializeField] float acceleration = 14f;
        [SerializeField] float turnSmoothTime = 0.1f;

        [Header("Jump and gravity")]
        [SerializeField] float jumpHeight = 1f;
        [SerializeField] float gravity = -18f;

        [Header("Crouch")]
        [SerializeField] float standingHeight = 1.8f;
        [SerializeField] float crouchHeight = 1.1f;
        [SerializeField] float heightChangeSpeed = 6f;

        [Header("Camera")]
        [SerializeField] Transform cameraTarget;
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float stickSpeed = 180f;
        [SerializeField] float minPitch = -40f;
        [SerializeField] float maxPitch = 70f;

        CharacterController controller;
        InputAction move, look, jump, sprint, crouch;
        Vector3 moveDirection;
        float yaw, pitch, verticalVelocity, turnVelocity, cameraTargetStandingY;

        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        public float CurrentSpeed { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsGrounded => controller.isGrounded;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            var actions = InputSystem.actions;
            move = actions.FindAction("Player/Move", true);
            look = actions.FindAction("Player/Look", true);
            jump = actions.FindAction("Player/Jump", true);
            sprint = actions.FindAction("Player/Sprint", true);
            crouch = actions.FindAction("Player/Crouch", true);
            yaw = transform.eulerAngles.y;
            cameraTargetStandingY = cameraTarget.localPosition.y;
        }

        void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            ReadLook();
            UpdateCrouch();
            Move();
            // World rotation set last so the body turning this frame does not drag the camera.
            cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
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
            var input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            var hasInput = input.sqrMagnitude > 0.01f;
            IsSprinting = hasInput && !IsCrouching && sprint.IsPressed();

            var targetSpeed = !hasInput ? 0f : IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed * input.magnitude, acceleration * Time.deltaTime);

            if (hasInput)
            {
                var targetAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg + yaw;
                var angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnVelocity, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
                moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            }

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (jump.WasPressedThisFrame() && controller.isGrounded && !IsCrouching)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            verticalVelocity += gravity * Time.deltaTime;

            controller.Move((moveDirection * CurrentSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);
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
            return !Physics.SphereCast(origin, radius * 0.95f, Vector3.up, out _, standingHeight - controller.height, ~0, QueryTriggerInteraction.Ignore);
        }
    }
}
