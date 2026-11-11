using UnityEngine;

namespace DeadCounty.Player
{
    // Procedural animation for the segmented prototype character (no rig, no clips). The gait phase advances
    // with distance travelled so feet roughly match ground speed; legs use a planar two-bone IK toward foot
    // targets, which also keeps the feet planted when crouching. Arms counter-swing, the torso bobs and leans.
    // Temporary until a rigged character with Mixamo / Synty animations replaces it.
    public class ProceduralHumanoidAnimator : MonoBehaviour
    {
        [SerializeField] ThirdPersonController controller;
        [SerializeField] Transform hips, spine, neck;
        [SerializeField] Transform leftShoulder, rightShoulder, leftElbow, rightElbow;
        [SerializeField] Transform leftHip, rightHip, leftKnee, rightKnee, leftAnkle, rightAnkle;

        [Header("Proportions (m)")]
        [SerializeField] float thighLength = 0.4f;
        [SerializeField] float shinLength = 0.38f;
        [SerializeField] float ankleHeight = 0.08f;
        [SerializeField] float crouchHipHeight = 0.5f;
        [SerializeField] float headPitch = 6f;

        [Header("Gait")]
        [SerializeField] float walkCycle = 1.3f;
        [SerializeField] float sprintCycle = 2.4f;
        [SerializeField] float walkStride = 0.26f;
        [SerializeField] float sprintStride = 0.5f;
        [SerializeField] float walkLift = 0.08f;
        [SerializeField] float sprintLift = 0.2f;

        float phase, crouch, air, standingHipHeight, hipJointOffset;

        void Awake()
        {
            standingHipHeight = hips.localPosition.y;
            hipJointOffset = leftHip.localPosition.y;
        }

        void LateUpdate()
        {
            var dt = Time.deltaTime;
            var speed = controller.CurrentSpeed;
            var run = Mathf.InverseLerp(controller.WalkSpeed, controller.SprintSpeed, speed);
            var move = Mathf.Clamp01(speed / 1.2f);
            phase = Mathf.Repeat(phase + speed * dt / Mathf.Lerp(walkCycle, sprintCycle, run), 1f);
            crouch = Mathf.MoveTowards(crouch, controller.IsCrouching ? 1f : 0f, dt * 4f);
            air = Mathf.MoveTowards(air, controller.IsGrounded ? 0f : 1f, dt * 6f);

            var s = Mathf.Sin(phase * Mathf.PI * 2f);
            var c = Mathf.Cos(phase * Mathf.PI * 2f);

            // Hips are lowest when the legs are spread.
            var bob = -Mathf.Abs(s) * Mathf.Lerp(0.025f, 0.05f, run) * move * (1f - air);
            var hipHeight = Mathf.Lerp(standingHipHeight, crouchHipHeight, crouch) + bob;
            hips.localPosition = new Vector3(0f, hipHeight, 0f);
            hips.localRotation = Quaternion.Euler(0f, s * 6f * move, 0f);

            var lean = Mathf.Lerp(3f, 14f, run) * move + 35f * crouch;
            var breathe = Mathf.Sin(Time.time * 1.7f) * 1.5f * (1f - move);
            spine.localRotation = Quaternion.Euler(lean + breathe, -s * 9f * move, 0f);
            neck.localRotation = Quaternion.Euler(headPitch - lean * 0.7f, 0f, 0f);

            var stride = Mathf.Lerp(walkStride, sprintStride, run) * move;
            var lift = Mathf.Lerp(walkLift, sprintLift, run) * move;
            var legHeight = hipHeight + hipJointOffset - ankleHeight;
            // A foot lifts while it swings forward (sine rising), and both tuck in the air.
            Leg(leftHip, leftKnee, leftAnkle, stride * s, legHeight - lift * Mathf.Max(0f, c) - 0.22f * air);
            Leg(rightHip, rightKnee, rightAnkle, -stride * s, legHeight - lift * Mathf.Max(0f, -c) - 0.22f * air);

            var armSwing = Mathf.Lerp(18f, 50f, run) * move * (1f - 0.5f * crouch);
            var bend = Mathf.Lerp(12f, 75f, run) * move + 8f + 25f * crouch;
            var raise = -35f * air - 15f * crouch;
            Arm(leftShoulder, leftElbow, armSwing * s + raise, -7f, bend);
            Arm(rightShoulder, rightElbow, -armSwing * s + raise, 7f, bend);
        }

        // Negative X swings a limb forward; Z spreads the arm away from the torso.
        static void Arm(Transform shoulder, Transform elbow, float swing, float outward, float bend)
        {
            shoulder.localRotation = Quaternion.Euler(swing, 0f, outward);
            elbow.localRotation = Quaternion.Euler(-bend - Mathf.Max(0f, -swing) * 0.4f, 0f, 0f);
        }

        // Planar two-bone IK in the hip's sagittal plane: the ankle goes `forward` along +Z and `down` below
        // the hip joint, with the knee pointing forward and the boot kept level.
        void Leg(Transform hip, Transform knee, Transform ankle, float forward, float down)
        {
            var reach = Mathf.Min(Mathf.Sqrt(forward * forward + down * down), thighLength + shinLength - 0.001f);
            var hipAngle = Mathf.Atan2(forward, down) * Mathf.Rad2Deg + LawOfCosines(thighLength, reach, shinLength);
            var kneeAngle = 180f - LawOfCosines(thighLength, shinLength, reach);
            hip.localRotation = Quaternion.Euler(-hipAngle, 0f, 0f);
            knee.localRotation = Quaternion.Euler(kneeAngle, 0f, 0f);
            ankle.localRotation = Quaternion.Euler(hipAngle - kneeAngle, 0f, 0f);
        }

        // Angle in degrees between sides a and b of a triangle, given the side opposite to it.
        static float LawOfCosines(float a, float b, float opposite)
            => Mathf.Acos(Mathf.Clamp((a * a + b * b - opposite * opposite) / (2f * a * b), -1f, 1f)) * Mathf.Rad2Deg;
    }
}
