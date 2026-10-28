using DeadCounty.Animation;
using DeadCounty.Player;
using UnityEngine;

namespace DeadCounty.Audio
{
    // Footsteps on each foot plant of the procedural animator, picked by the ground underfoot, plus jump and landing.
    // Loudness follows the gait like the noise the zombies hear (PlayerNoiseEmitter): crouched steps are barely
    // audible, sprinting ones hard and sharp. Presentation only: no NoiseSystem event is emitted here.
    // Runs after the animator's LateUpdate, which sets the plant flags.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(ThirdPersonController), typeof(ProceduralHumanoidAnimator))]
    public class PlayerFootstepAudio : MonoBehaviour
    {
        const float FootSpacing = 0.12f;
        // The animator keeps cycling while idle: plants below this speed only sound when turning on the spot.
        const float MinStepSpeed = 0.3f;
        const float TurnStepYawRate = 120f;
        const float JumpSpeed = 1.5f;
        const float MinLandSpeed = 2f;
        const float HardLandSpeed = 9f;
        const float QuietAfterLanding = 0.15f;

        [Tooltip("Walking steps per Surface, in the order of the enum.")]
        [SerializeField] SoundCue[] steps = new SoundCue[7];
        [Tooltip("Running and sprinting steps per Surface: harder heel strike and scuff.")]
        [SerializeField] SoundCue[] runSteps = new SoundCue[7];
        [SerializeField] SoundCue jump;
        [SerializeField] SoundCue land;

        [Header("Volume by gait")]
        [SerializeField, Range(0f, 1f)] float crouchVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] float walkVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] float runVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] float sprintVolume = 1f;

        ThirdPersonController controller;
        ProceduralHumanoidAnimator animator;
        bool leftWasPlanted, rightWasPlanted, wasGrounded;
        float lastY, lastVerticalSpeed, lastYaw, quietUntil;
        int resetId;

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            animator = GetComponent<ProceduralHumanoidAnimator>();
        }

        void OnEnable() => Resync();

        void LateUpdate()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (controller.MotionResetId != resetId)
            {
                Resync();
                return;
            }

            var y = transform.position.y;
            var verticalSpeed = (y - lastY) / dt;
            var yaw = transform.eulerAngles.y;
            var yawRate = Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) / dt;
            var grounded = controller.IsGrounded;
            if (wasGrounded && !grounded && verticalSpeed > JumpSpeed) Jumped();
            // The landing frame only covers part of the fall: the previous frame has the full speed.
            if (!wasGrounded && grounded) Landed(Mathf.Max(-verticalSpeed, -lastVerticalSpeed));

            var left = animator.LeftFootPlanted;
            var right = animator.RightFootPlanted;
            if (grounded && Time.time >= quietUntil)
            {
                if (left && !leftWasPlanted) Step(-1f, yawRate);
                if (right && !rightWasPlanted) Step(1f, yawRate);
            }

            leftWasPlanted = left;
            rightWasPlanted = right;
            wasGrounded = grounded;
            lastY = y;
            lastVerticalSpeed = verticalSpeed;
            lastYaw = yaw;
        }

        void Resync()
        {
            resetId = controller.MotionResetId;
            leftWasPlanted = animator.LeftFootPlanted;
            rightWasPlanted = animator.RightFootPlanted;
            wasGrounded = controller.IsGrounded;
            lastY = transform.position.y;
            lastVerticalSpeed = 0f;
            lastYaw = transform.eulerAngles.y;
        }

        void Step(float side, float yawRate)
        {
            var speed = controller.CurrentSpeed;
            var shuffling = speed < MinStepSpeed;
            if (shuffling && yawRate < TurnStepYawRate) return;
            var surface = (int)GroundSurface.At(transform.position);
            var running = !controller.IsCrouching && !shuffling && controller.Gait is Gait.Run or Gait.Sprint;
            var volume = controller.IsCrouching ? crouchVolume : controller.Gait switch
            {
                Gait.Sprint => sprintVolume,
                Gait.Run => runVolume,
                _ => walkVolume,
            };
            if (shuffling) volume *= 0.5f;
            var pitch = controller.IsSprinting ? 1.04f : controller.IsCrouching ? 0.96f : 1f;
            Sfx.Play((running ? runSteps : steps)[surface], transform.position + transform.right * (side * FootSpacing), volume, pitch);
        }

        void Jumped()
        {
            var position = transform.position;
            Sfx.Play(jump, position + Vector3.up * 1.2f);
            Sfx.Play(runSteps[(int)GroundSurface.At(position)], position, 0.5f);
        }

        void Landed(float fallSpeed)
        {
            if (fallSpeed < MinLandSpeed) return;
            var hard = Mathf.InverseLerp(MinLandSpeed, HardLandSpeed, fallSpeed);
            var position = transform.position;
            Sfx.Play(runSteps[(int)GroundSurface.At(position)], position, Mathf.Lerp(0.6f, 1f, hard));
            Sfx.Play(land, position + Vector3.up * 0.8f, Mathf.Lerp(0.3f, 1f, hard), Mathf.Lerp(1.05f, 0.9f, hard));
            quietUntil = Time.time + QuietAfterLanding;
        }
    }
}
