using UnityEngine;

namespace DeadCounty.Player
{
    // Procedural locomotion for the segmented prototype character (no rig, no clips), built on human gait data:
    // - cadence rises with speed (about 1 stride per second walking, 1.6 sprinting);
    // - each foot stays on the ground for a duty factor of the cycle (62 % walking, 30 % sprinting) and moves
    //   back at exactly ground speed meanwhile, so it stays planted, then swings forward on an arc with heel
    //   strike and toe-off;
    // - the stance window sits behind the hip (heel strike just ahead, toe-off far behind with the heel up);
    // - the hips can only be as high as the stretched stance leg allows, so they vault over it like an
    //   inverted pendulum (highest mid-stance) and sway toward it; running hips also compress on contact and
    //   rise during the flight phase;
    // - legs use planar two-bone IK, a ray under each foot adapts them to steps and slopes, step-ups of the
    //   CharacterController are smoothed and landings compress the legs;
    // - the torso leans with speed, acceleration and into turns (centripetal), the head follows the camera aim.
    // Temporary until a rigged character with Mixamo / Synty animations replaces it.
    public class ProceduralHumanoidAnimator : MonoBehaviour
    {
        struct FootPose
        {
            public float Forward, Lift, Roll, Support;
        }

        [SerializeField] ThirdPersonController controller;
        [SerializeField] Transform hips, spine, neck;
        [SerializeField] Transform leftShoulder, rightShoulder, leftElbow, rightElbow;
        [SerializeField] Transform leftHip, rightHip, leftKnee, rightKnee, leftAnkle, rightAnkle;

        [Header("Proportions (m)")]
        [SerializeField] float thighLength = 0.44f;
        [SerializeField] float shinLength = 0.43f;
        [SerializeField] float ankleHeight = 0.08f;
        [SerializeField] float footLength = 0.17f;
        [SerializeField] float crouchHipHeight = 0.5f;
        [SerializeField] float headPitch = 6f;

        [Header("Gait (human averages)")]
        [Tooltip("Stride cycles per second = base + perSpeed x speed.")]
        [SerializeField] float cadenceBase = 0.85f;
        [SerializeField] float cadencePerSpeed = 0.12f;
        [Tooltip("Speed (m/s) around which walking turns into running and a flight phase appears.")]
        [SerializeField] float runTransitionSpeed = 2.2f;
        [Tooltip("Fraction of a cycle each foot spends on the ground.")]
        [SerializeField] float walkDuty = 0.62f;
        [SerializeField] float runDuty = 0.38f;
        [SerializeField] float sprintDuty = 0.3f;
        [SerializeField] float walkLift = 0.09f;
        [SerializeField] float runLift = 0.22f;
        [SerializeField] float sprintLift = 0.32f;
        [Tooltip("How far the stance window sits behind the hip, as a fraction of half the stance length.")]
        [SerializeField] float stanceBias = 0.22f;
        [Tooltip("Lowest the hips may drop below their nominal height to keep the stance foot planted.")]
        [SerializeField] float maxDip = 0.12f;
        [SerializeField] float hipSmoothTime = 0.04f;
        [SerializeField] float runBob = 0.05f;
        [SerializeField] float walkSway = 0.025f;

        [Header("Upper body")]
        [Tooltip("Torso pitch per m/s² of forward acceleration.")]
        [SerializeField] float accelerationLean = 2f;
        [SerializeField] float maxTurnLean = 12f;
        [SerializeField] float headYawLimit = 70f;
        [SerializeField] float headPitchLimit = 30f;

        [Header("Ground adaptation")]
        [SerializeField] LayerMask groundMask = 1;
        [SerializeField] float maxFootOffset = 0.35f;
        [SerializeField] float footBlendSpeed = 15f;
        [SerializeField] float stepSmoothTime = 0.07f;

        Transform model;
        Vector3 lastPosition, smoothVelocity;
        float phase, crouch, air, standingHipHeight, hipJointOffset, leftFootOffset, rightFootOffset;
        float lastYaw, yawRate, forwardAccel, airVerticalSpeed, landingDrop, visualY, visualYVelocity;
        float bob, hipY, hipYVelocity, headYaw, leftArmDrive, rightArmDrive, leftTrackX, rightTrackX;
        bool wasGrounded = true;

        void Awake()
        {
            model = hips.parent;
            standingHipHeight = hipY = hips.localPosition.y;
            hipJointOffset = leftHip.localPosition.y;
            leftTrackX = leftHip.localPosition.x;
            rightTrackX = rightHip.localPosition.x;
            lastPosition = transform.position;
            lastYaw = transform.eulerAngles.y;
            visualY = transform.position.y;
        }

        void LateUpdate()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            TrackMotion(dt);
            var grounded = controller.IsGrounded;
            var speed = controller.CurrentSpeed;
            crouch = Mathf.MoveTowards(crouch, controller.IsCrouching ? 1f : 0f, dt * 4f);
            air = Mathf.MoveTowards(air, grounded ? 0f : 1f, dt * 6f);
            UpdateLanding(grounded, dt);
            SmoothSteps(grounded);

            // Turning on the spot still takes small steps.
            var gaitSpeed = Mathf.Max(speed, Mathf.Abs(yawRate) * Mathf.Deg2Rad * 0.25f * (1f - air));
            var runness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(runTransitionSpeed - 0.4f, runTransitionSpeed + 0.6f, gaitSpeed)) * (1f - crouch);
            var sprintness = Mathf.InverseLerp(4.5f, 6f, gaitSpeed) * (1f - crouch);
            var moving = Mathf.Clamp01(gaitSpeed / 0.5f);
            var cadence = (cadenceBase + cadencePerSpeed * gaitSpeed) * (1f - 0.15f * crouch);
            phase = Mathf.Repeat(phase + cadence * dt, 1f);
            var duty = Mathf.Lerp(Mathf.Lerp(walkDuty, runDuty, runness), sprintDuty, sprintness);
            var lift = Mathf.Lerp(Mathf.Lerp(walkLift, runLift, runness), sprintLift, sprintness) * moving * (1f - 0.5f * crouch);

            // Half the ground covered while a foot is down: the planted foot slides back at ground speed.
            var baseHip = Mathf.Lerp(standingHipHeight, crouchHipHeight, crouch);
            var half = Mathf.Min(gaitSpeed * duty / (2f * cadence), 0.75f);
            var shift = stanceBias * half;
            var left = FootCycle(phase, duty, half, shift, lift);
            var right = FootCycle(Mathf.Repeat(phase + 0.5f, 1f), duty, half, shift, lift);

            var blend = 1f - Mathf.Exp(-footBlendSpeed * dt);
            leftFootOffset = Mathf.Lerp(leftFootOffset, grounded ? GroundOffset(leftHip, left.Forward) : 0f, blend);
            rightFootOffset = Mathf.Lerp(rightFootOffset, grounded ? GroundOffset(rightHip, right.Forward) : 0f, blend);

            var support = Mathf.Max(left.Support, right.Support);
            var runOffset = support > 0f ? -runBob * support : runBob * 0.5f;
            bob = Mathf.Lerp(bob, runOffset * runness * moving, 1f - Mathf.Exp(-25f * dt));
            var nominal = baseHip + bob;
            var cap = Mathf.Min(HipCap(left, leftFootOffset), HipCap(right, rightFootOffset));
            var hipTarget = (grounded ? Mathf.Clamp(cap, nominal - maxDip, nominal) : nominal) - landingDrop;
            hipY = Mathf.SmoothDamp(hipY, hipTarget, ref hipYVelocity, hipSmoothTime);

            var stance = 1f - 0.6f * runness;
            var sway = walkSway * (right.Support - left.Support) * stance * moving;
            var pelvisRoll = -5f * (left.Support - right.Support) * stance * moving;
            var pelvisYaw = Mathf.Lerp(5f, 10f, sprintness) * (half > 0.01f ? (left.Forward - right.Forward) / (2f * half) : 0f) * moving;
            var hipHeight = hipY;
            hips.localPosition = new Vector3(sway, hipHeight, 0f);
            hips.localRotation = Quaternion.Euler(0f, pelvisYaw, pelvisRoll);

            Leg(leftHip, leftKnee, leftAnkle, leftTrackX, left.Forward, left.Lift + leftFootOffset + 0.25f * air, left.Roll * moving);
            Leg(rightHip, rightKnee, rightAnkle, rightTrackX, right.Forward, right.Lift + rightFootOffset + 0.25f * air, right.Roll * moving);

            // Upright when walking, forward when running, plus acceleration; banked into turns like a runner.
            var lean = Mathf.Lerp(3f, 8f, runness) + 10f * sprintness
                + Mathf.Clamp(forwardAccel * accelerationLean, -8f, 12f) * (1f - air)
                + 35f * crouch;
            var breathe = Mathf.Sin(Time.time * 1.7f) * 1.5f * (1f - moving);
            var bank = Mathf.Atan(speed * yawRate * Mathf.Deg2Rad / 9.81f) * Mathf.Rad2Deg;
            model.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Clamp(bank, -maxTurnLean, maxTurnLean) * (1f - air));

            // The head follows the camera aim within neck limits, less so when sprinting; never looks behind.
            var aimDelta = Mathf.DeltaAngle(transform.eulerAngles.y, controller.AimYaw);
            var follow = 1f - 0.7f * sprintness;
            var yawTarget = Mathf.Abs(aimDelta) > 110f ? 0f : Mathf.Clamp(aimDelta, -headYawLimit, headYawLimit) * follow;
            headYaw = Mathf.Lerp(headYaw, yawTarget, 1f - Mathf.Exp(-10f * dt));
            var headTilt = Mathf.Clamp(controller.AimPitch * 0.6f, -headPitchLimit, headPitchLimit) * follow;
            spine.localRotation = Quaternion.Euler(lean + breathe, -pelvisYaw * 0.9f + headYaw * 0.3f, 0f);
            neck.localRotation = Quaternion.Euler(headPitch - lean * 0.7f + headTilt, headYaw * 0.7f, 0f);

            // Each arm swings with the opposite leg.
            var armBlend = 1f - Mathf.Exp(-15f * dt);
            var norm = half > 0.01f ? 1f / half : 0f;
            leftArmDrive = Mathf.Lerp(leftArmDrive, right.Forward * norm, armBlend);
            rightArmDrive = Mathf.Lerp(rightArmDrive, left.Forward * norm, armBlend);
            var armSwing = (Mathf.Lerp(18f, 40f, runness) + 25f * sprintness) * moving * (1f - 0.5f * crouch);
            var bend = Mathf.Lerp(12f, 80f, runness) * moving + 15f * sprintness + 8f + 25f * crouch;
            var raise = -35f * air - 15f * crouch + landingDrop * 60f;
            Arm(leftShoulder, leftElbow, -armSwing * leftArmDrive + raise, -7f, bend);
            Arm(rightShoulder, rightElbow, -armSwing * rightArmDrive + raise, 7f, bend);
        }

        void TrackMotion(float dt)
        {
            var position = transform.position;
            var velocity = (position - lastPosition) / dt;
            lastPosition = position;
            if (!controller.IsGrounded) airVerticalSpeed = velocity.y;

            var previous = smoothVelocity;
            smoothVelocity = Vector3.Lerp(smoothVelocity, new Vector3(velocity.x, 0f, velocity.z), 1f - Mathf.Exp(-12f * dt));
            forwardAccel = Mathf.Lerp(forwardAccel, Vector3.Dot(smoothVelocity - previous, transform.forward) / dt, 1f - Mathf.Exp(-8f * dt));

            var yaw = transform.eulerAngles.y;
            yawRate = Mathf.Lerp(yawRate, Mathf.DeltaAngle(lastYaw, yaw) / dt, 1f - Mathf.Exp(-10f * dt));
            lastYaw = yaw;
        }

        // Knees absorb the impact in proportion to the fall speed, then straighten again.
        void UpdateLanding(bool grounded, float dt)
        {
            if (grounded && !wasGrounded)
                landingDrop = Mathf.Max(landingDrop, Mathf.Clamp01((-airVerticalSpeed - 1.5f) / 6f) * 0.22f);
            wasGrounded = grounded;
            landingDrop *= Mathf.Exp(-dt / 0.15f);
        }

        // The CharacterController pops up and down steps instantly; the visible body eases over them.
        void SmoothSteps(bool grounded)
        {
            var y = transform.position.y;
            if (!grounded || Mathf.Abs(visualY - y) > 0.5f)
            {
                visualY = y;
                visualYVelocity = 0f;
            }
            else
            {
                visualY = Mathf.SmoothDamp(visualY, y, ref visualYVelocity, stepSmoothTime);
            }
            model.localPosition = new Vector3(0f, visualY - y, 0f);
        }

        // Stance: the foot moves from +half to -half with the ground (heel strike, then toe-off), the whole
        // window shifted back by `shift`. Swing: it travels forward again on an arc.
        static FootPose FootCycle(float p, float duty, float half, float shift, float lift)
        {
            if (p < duty)
            {
                var t = p / duty;
                var roll = t < 0.2f ? Mathf.Lerp(-12f, 0f, t / 0.2f) : t > 0.7f ? Mathf.Lerp(0f, 25f, (t - 0.7f) / 0.3f) : 0f;
                return new FootPose { Forward = Mathf.Lerp(half, -half, t) - shift, Roll = roll, Support = Mathf.Sin(Mathf.PI * t) };
            }
            var s = (p - duty) / (1f - duty);
            return new FootPose
            {
                Forward = Mathf.Lerp(-half, half, Mathf.SmoothStep(0f, 1f, s)) - shift,
                Lift = lift * Mathf.Sin(Mathf.PI * s),
                Roll = Mathf.Lerp(25f, -12f, s),
            };
        }

        // Highest the hips can be while this foot stays on the ground with a stretched leg (no limit for a
        // foot that is lifted in its swing).
        float HipCap(FootPose foot, float groundOffset)
        {
            if (foot.Lift > 0.02f) return float.PositiveInfinity;
            var leg = thighLength + shinLength - 0.01f;
            var heelRaise = foot.Roll > 0f ? footLength * Mathf.Sin(foot.Roll * Mathf.Deg2Rad) : 0f;
            return Mathf.Sqrt(Mathf.Max(0f, leg * leg - foot.Forward * foot.Forward)) + ankleHeight + heelRaise + foot.Lift + groundOffset - hipJointOffset;
        }

        // Ground height under a foot relative to the visible feet level, clamped to one step up or down.
        float GroundOffset(Transform hip, float forward)
        {
            var feetY = model.position.y;
            var foot = hip.position + transform.forward * forward;
            var origin = new Vector3(foot.x, feetY + maxFootOffset, foot.z);
            return Physics.Raycast(origin, Vector3.down, out var hit, maxFootOffset * 2f, groundMask, QueryTriggerInteraction.Ignore)
                ? Mathf.Clamp(hit.point.y - feetY, -maxFootOffset, maxFootOffset)
                : 0f;
        }

        // Negative X swings a limb forward; Z spreads the arm away from the torso.
        static void Arm(Transform shoulder, Transform elbow, float swing, float outward, float bend)
        {
            shoulder.localRotation = Quaternion.Euler(swing, 0f, outward);
            elbow.localRotation = Quaternion.Euler(-bend - Mathf.Max(0f, -swing) * 0.4f, 0f, 0f);
        }

        // Two-bone IK placing the ankle in the model frame at (`trackX`, `height` above the ground, `forward`),
        // with the knee pointing forward. The pelvis sway, yaw and roll are cancelled so they never drag a
        // planted foot. `roll` pitches the boot (heel strike, toe-off) and lifts the ankle as the foot pivots
        // on its toes; the boot otherwise stays level.
        void Leg(Transform hip, Transform knee, Transform ankle, float trackX, float forward, float height, float roll)
        {
            var joint = hips.localPosition + hips.localRotation * hip.localPosition;
            var down = joint.y - ankleHeight - height;
            if (roll > 0f) down -= footLength * Mathf.Sin(roll * Mathf.Deg2Rad);
            var lateral = trackX - joint.x;
            forward -= joint.z;
            var tilt = Quaternion.AngleAxis(Mathf.Atan2(lateral, down) * Mathf.Rad2Deg, Vector3.forward);
            var planeDown = Mathf.Sqrt(down * down + lateral * lateral);

            var reach = Mathf.Min(Mathf.Sqrt(forward * forward + planeDown * planeDown), thighLength + shinLength - 0.001f);
            var hipAngle = Mathf.Atan2(forward, planeDown) * Mathf.Rad2Deg + LawOfCosines(thighLength, reach, shinLength);
            var kneeAngle = 180f - LawOfCosines(thighLength, shinLength, reach);
            var thigh = tilt * Quaternion.Euler(-hipAngle, 0f, 0f);
            hip.localRotation = Quaternion.Inverse(hips.localRotation) * thigh;
            knee.localRotation = Quaternion.Euler(kneeAngle, 0f, 0f);
            ankle.localRotation = Quaternion.Inverse(thigh * knee.localRotation) * Quaternion.Euler(roll, 0f, 0f);
        }

        // Angle in degrees between sides a and b of a triangle, given the side opposite to it.
        static float LawOfCosines(float a, float b, float opposite)
            => Mathf.Acos(Mathf.Clamp((a * a + b * b - opposite * opposite) / (2f * a * b), -1f, 1f)) * Mathf.Rad2Deg;
    }
}
