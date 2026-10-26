using System;
using UnityEngine;

namespace DeadCounty.Animation
{
    // Procedural locomotion for the segmented prototype characters (no rig, no clips), built on human gait data:
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
    // - the torso leans with speed, acceleration and into turns (centripetal), the head follows the aim.
    // The tuning comes from a GaitProfile shared per kind of body, the motion from an IHumanoidMotion (player
    // controller, zombie agent) whose PoseModifiers add zombie expression; a limp makes the legs asymmetric.
    // Temporary until a rigged character with Mixamo / Synty animations replaces it.
    public class ProceduralHumanoidAnimator : MonoBehaviour
    {
        struct FootPose
        {
            public float Forward, Lift, Roll, Support;
        }

        // A planted foot moves back exactly as far as the body goes forward, so it keeps its place on the ground
        // when the stride under it is resized by a speed change (lunge, braking). Drift is how far it ends up
        // from the steady-speed cycle; the next swing absorbs it. A swinging foot covers, each frame, the share
        // of its remaining way that the swing curve covers, toward a landing point that follows speed changes
        // at a bounded rate, and lands where the swing brought it.
        struct FootTrack
        {
            public bool Planted, Swinging;
            // ToeOff: how far the cycle still had to go back at the last stance frame.
            // Lateral: sideways foothold in the model frame; stance keeps it world-fixed like Forward.
            public float Forward, Drift, ToeOff, Blend, Landing, Lateral;
        }

        // How fast a swinging foot's landing point follows the stride: normal accelerations stay well below it,
        // the stop at the end of a lunge does not swing the foot back.
        const float LandingShiftSpeed = 3f;

        [Tooltip("Motion source implementing IHumanoidMotion.")]
        [SerializeField] MonoBehaviour controller;
        [SerializeField] GaitProfile gait;
        [SerializeField] Transform hips, spine, neck;
        [SerializeField] Transform leftShoulder, rightShoulder, leftElbow, rightElbow;
        [SerializeField] Transform leftHip, rightHip, leftKnee, rightKnee, leftAnkle, rightAnkle;

        [Header("Proportions (m)")]
        [SerializeField] float thighLength = 0.44f;
        [SerializeField] float shinLength = 0.43f;
        [SerializeField] float ankleHeight = 0.08f;
        [SerializeField] float footLength = 0.17f;
        [SerializeField] float crouchHipHeight = 0.5f;
        [Tooltip("Neck pitch at rest (deg).")]
        [SerializeField] float headPitch = 6f;

        [Header("Asymmetry")]
        [SerializeField] LimpSettings limp = LimpSettings.Default;

        [Header("Ground adaptation")]
        [SerializeField] LayerMask groundMask = 1;

        IHumanoidMotion motion;
        PoseModifiers pose;
        Transform model;
        Vector3 lastPosition, smoothVelocity;
        float phase, crouch, air, standingHipHeight, hipJointOffset, leftFootOffset, rightFootOffset;
        float lastYaw, yawRate, forwardAccel, airVerticalSpeed, landingDrop, visualY, visualYVelocity;
        // Signed body yaw since the last Evaluate, in radians; stance slides each foothold by it.
        float frameYawDeltaRad;
        // Bank applied to the model last Evaluate, in degrees; footholds ride its changes.
        float lastBank;
        // Instantaneous ground speed: stance slides back by the body's travel, not its stepping speed.
        float translationSpeed;
        float bob, hipY, hipYVelocity, headYaw, leftArmDrive, rightArmDrive, leftTrackX, rightTrackX, clock;
        float pendingDt;
        FootTrack leftTrack, rightTrack;
        int resetId, framesSinceUpdate, updateEvery = 1;
        bool wasGrounded = true, snapped;

        // LOD: without ground rays the feet assume flat ground.
        public bool GroundRays { get; set; } = true;

        // LOD: animate every N frames, with the time of the skipped frames.
        public int UpdateEvery
        {
            get => updateEvery;
            set => updateEvery = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "UpdateEvery must be at least 1");
        }

        public LimpSettings Limp
        {
            get => limp;
            set => limp = value;
        }

        // Whether each foot is in its stance phase, planted on the ground.
        public bool LeftFootPlanted { get; private set; }
        public bool RightFootPlanted { get; private set; }
        // Boot pitch (deg) of each foot, test observability for the flat-foot slip gate.
        public float LeftFootRoll { get; private set; }
        public float RightFootRoll { get; private set; }

        void Awake() => Initialize(controller as IHumanoidMotion
            ?? throw new InvalidOperationException($"{name}: the animator's controller must implement {nameof(IHumanoidMotion)}"));

        // Called by Awake with the serialized controller; code-built bodies (tests) pass their own source.
        public void Initialize(IHumanoidMotion source)
        {
            motion = source ?? throw new ArgumentNullException(nameof(source));
            if (gait == null) throw new InvalidOperationException($"{name}: the animator needs a {nameof(GaitProfile)}");
            model = hips.parent;
            standingHipHeight = hipY = hips.localPosition.y;
            hipJointOffset = leftHip.localPosition.y;
            leftTrackX = leftHip.localPosition.x;
            rightTrackX = rightHip.localPosition.x;
            pose = motion.Pose;
            ResetTracking();
        }

        // Forgets the tracked motion so a teleport or respawn reads as if the body had always been there, without
        // lean, bank or step spikes. Runs by itself when the source's MotionResetId changes.
        public void Snap()
        {
            ResetTracking();
            snapped = true;
        }

        void ResetTracking()
        {
            resetId = motion.MotionResetId;
            lastPosition = transform.position;
            lastYaw = transform.eulerAngles.y;
            smoothVelocity = transform.forward * motion.CurrentSpeed;
            translationSpeed = motion.CurrentSpeed;
            forwardAccel = yawRate = airVerticalSpeed = landingDrop = frameYawDeltaRad = 0f;
            visualY = transform.position.y;
            visualYVelocity = 0f;
            leftTrack = rightTrack = default;
            leftTrack.Lateral = leftTrackX;
            rightTrack.Lateral = rightTrackX;
            pose = motion.Pose;
            pendingDt = 0f;
            framesSinceUpdate = 0;
        }

        void LateUpdate()
        {
            pendingDt += Time.deltaTime;
            if (++framesSinceUpdate < updateEvery) return;
            framesSinceUpdate = 0;
            Evaluate(pendingDt);
            pendingDt = 0f;
        }

        public void Evaluate(float dt)
        {
            if (dt <= 0f) return;
            if (motion.MotionResetId != resetId) Snap();
            var g = gait;
            clock += dt;
            TrackMotion(dt);
            var grounded = motion.IsGrounded;
            var speed = motion.CurrentSpeed;
            crouch = Mathf.MoveTowards(crouch, motion.IsCrouching ? 1f : 0f, dt * 4f);
            air = Mathf.MoveTowards(air, grounded ? 0f : 1f, dt * 6f);
            SmoothPose(dt);
            UpdateLanding(grounded, dt);
            SmoothSteps(grounded, dt);

            // Turning on the spot still takes small steps.
            var gaitSpeed = Mathf.Max(speed, Mathf.Abs(yawRate) * Mathf.Deg2Rad * g.TurnStepFactor * (1f - air));
            var runness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(g.RunTransitionSpeed - g.RunBlendBelow, g.RunTransitionSpeed + g.RunBlendAbove, gaitSpeed)) * (1f - crouch);
            var sprintness = Mathf.InverseLerp(g.SprintStartSpeed, g.SprintFullSpeed, gaitSpeed) * (1f - crouch);
            var moving = Mathf.Clamp01(gaitSpeed / g.FullStrideSpeed);
            var cadence = (g.CadenceBase + g.CadencePerSpeed * gaitSpeed) * (1f - g.CrouchCadenceDrop * crouch);
            phase = Mathf.Repeat(phase + cadence * dt, 1f);
            var lift = Mathf.Lerp(Mathf.Lerp(g.WalkLift, g.RunLift, runness), g.SprintLift, sprintness) * moving * (1f - g.CrouchLiftDrop * crouch);

            // Banked into turns like a runner. Posed before the feet so the yaw compensation reads
            // this frame's bank, and the stored footholds are carried through the frame's roll.
            var bank = Mathf.Atan(speed * yawRate * Mathf.Deg2Rad / 9.81f) * Mathf.Rad2Deg * g.BankScale;
            bank = Mathf.Clamp(bank, -g.MaxTurnLean, g.MaxTurnLean) * (1f - air);
            model.localRotation = Quaternion.Euler(0f, 0f, -bank);
            var bankDelta = (bank - lastBank) * Mathf.Deg2Rad;
            lastBank = bank;
            if (bankDelta != 0f)
            {
                var bankCos = Mathf.Cos(bankDelta);
                var bankSin = Mathf.Sin(bankDelta);
                leftTrack.Lateral = leftTrack.Lateral * bankCos - ankleHeight * bankSin;
                rightTrack.Lateral = rightTrack.Lateral * bankCos - ankleHeight * bankSin;
            }

            // A limp gives each leg its own duty; the bad leg's cycle lags the good one's by PhaseOffset.
            var limping = limp.Enabled;
            var leftBad = limping && limp.LeftIsBad;
            var rightBad = limping && !limp.LeftIsBad;
            var leftDuty = Duty(limping ? (leftBad ? limp.BadDuty : limp.GoodDuty) : g.WalkDuty, runness, sprintness);
            var rightDuty = Duty(limping ? (rightBad ? limp.BadDuty : limp.GoodDuty) : g.WalkDuty, runness, sprintness);
            var offset = limping ? limp.PhaseOffset : 0.5f;
            var leftPhase = leftBad ? Mathf.Repeat(phase + offset, 1f) : phase;
            var rightPhase = leftBad ? phase : Mathf.Repeat(phase + offset, 1f);

            // Half the ground covered while a foot is down: the planted foot slides back by the body's
            // travel, which is less than the stepping speed when turning (on the spot: none).
            // Full-axis on purpose: every motion source integrates rotate-then-move, so the travel
            // lies exactly along the new forward and the yaw rotation covers the turn.
            var leftHalf = Mathf.Min(gaitSpeed * leftDuty / (2f * cadence), g.MaxHalfStride);
            var rightHalf = Mathf.Min(gaitSpeed * rightDuty / (2f * cadence), g.MaxHalfStride);
            var advance = translationSpeed * dt;
            var left = FootCycle(ref leftTrack, leftPhase, leftDuty, leftHalf, g.StanceBias * leftHalf, lift * (leftBad ? limp.BadLiftScale : 1f), advance, dt, leftTrackX, frameYawDeltaRad);
            var right = FootCycle(ref rightTrack, rightPhase, rightDuty, rightHalf, g.StanceBias * rightHalf, lift * (rightBad ? limp.BadLiftScale : 1f), advance, dt, rightTrackX, frameYawDeltaRad);
            LeftFootPlanted = leftPhase < leftDuty;
            RightFootPlanted = rightPhase < rightDuty;
            // Boot pitch of each foot, for tests measuring slip while the boot is flat.
            // Raw, pre-moving: Leg applies the x moving scale at the call.
            LeftFootRoll = left.Roll;
            RightFootRoll = right.Roll;

            // A lunge pushes the hips forward; the IK keeps the feet where they are.
            var hipForward = g.LungeHipShift * pose.Lunge;
            var blend = 1f - Mathf.Exp(-g.FootBlendSpeed * dt);
            var rays = grounded && GroundRays;
            leftFootOffset = Mathf.Lerp(leftFootOffset, rays ? GroundOffset(leftHip, left.Forward - hipForward) : 0f, blend);
            rightFootOffset = Mathf.Lerp(rightFootOffset, rays ? GroundOffset(rightHip, right.Forward - hipForward) : 0f, blend);

            // Feeding lowers the hips like a crouch.
            var baseHip = Mathf.Lerp(standingHipHeight, crouchHipHeight, Mathf.Max(crouch, pose.Feed));
            var support = Mathf.Max(left.Support, right.Support);
            var runOffset = support > 0f ? -g.RunBob * support : g.RunBob * 0.5f;
            bob = Mathf.Lerp(bob, runOffset * runness * moving, 1f - Mathf.Exp(-25f * dt));
            var badSupport = leftBad ? left.Support : rightBad ? right.Support : 0f;
            var nominal = baseHip + bob - limp.ExtraDip * badSupport * moving;
            var cap = Mathf.Min(HipCap(left, leftFootOffset, hipForward), HipCap(right, rightFootOffset, hipForward));
            var hipTarget = (grounded ? Mathf.Clamp(cap, nominal - g.MaxDip, nominal) : nominal) - landingDrop;
            hipY = Mathf.SmoothDamp(hipY, hipTarget, ref hipYVelocity, g.HipSmoothTime, Mathf.Infinity, dt);

            var stance = 1f - g.RunSwayDrop * runness;
            var sway = g.WalkSway * (right.Support - left.Support) * stance * moving;
            var pelvisRoll = -g.PelvisRoll * (left.Support - right.Support) * stance * moving * (limping ? limp.PelvisRollScale : 1f);
            var half = 0.5f * (leftHalf + rightHalf);
            var pelvisYaw = Mathf.Lerp(g.PelvisYaw, g.SprintPelvisYaw, sprintness) * (half > 0.01f ? (left.Forward - right.Forward) / (2f * half) : 0f) * moving;
            hips.localPosition = new Vector3(sway, hipY, hipForward);
            hips.localRotation = Quaternion.Euler(0f, pelvisYaw, pelvisRoll);

            Leg(leftHip, leftKnee, leftAnkle, leftTrack.Lateral, left.Forward, left.Lift + leftFootOffset + g.AirLegTuck * air, left.Roll * moving, LeftFootPlanted);
            Leg(rightHip, rightKnee, rightAnkle, rightTrack.Lateral, right.Forward, right.Lift + rightFootOffset + g.AirLegTuck * air, right.Roll * moving, RightFootPlanted);

            // Upright when walking, forward when running, plus acceleration; banked into turns like a runner.
            var lean = Mathf.Lerp(g.WalkLean, g.RunLean, runness) + g.SprintLean * sprintness
                + Mathf.Clamp(forwardAccel * g.AccelerationLean, -g.MaxBrakeLean, g.MaxAccelerationLean) * (1f - air)
                + g.CrouchLean * crouch;
            var still = 1f - moving;
            var breathe = Mathf.Sin(clock * 2f * Mathf.PI * g.IdleBreathFrequency) * g.IdleBreathAmplitude * still;
            var idleSway = Mathf.Sin(clock * 2f * Mathf.PI * g.IdleSwayFrequency) * g.IdleSwayAmplitude * still;
            // The head follows the aim within neck limits, less so when sprinting; past the limit it either holds
            // there or, for the player, recenters rather than looking behind.
            var aimDelta = Mathf.DeltaAngle(transform.eulerAngles.y, motion.AimYaw);
            var follow = (1f - g.SprintHeadFollowDrop * sprintness) * pose.LookWeight;
            var recenter = !g.HeadClampsAtLimit && Mathf.Abs(aimDelta) > g.HeadRecenterAngle;
            var yawTarget = recenter ? 0f : Mathf.Clamp(aimDelta, -g.HeadYawLimit, g.HeadYawLimit) * follow;
            headYaw = Mathf.Lerp(headYaw, yawTarget, 1f - Mathf.Exp(-10f * dt));
            var headTilt = Mathf.Clamp(motion.AimPitch * g.HeadPitchFollow, -g.HeadPitchLimit, g.HeadPitchLimit) * follow;
            // The neck makes up for most of the hunch and lunge to keep looking ahead, not for feeding.
            var posePitch = pose.Hunch + g.LungePitch * pose.Lunge;
            spine.localRotation = Quaternion.Euler(lean + breathe + posePitch + g.FeedPitch * pose.Feed, -pelvisYaw * 0.9f + headYaw * 0.3f, idleSway);
            neck.localRotation = Quaternion.Euler(headPitch - (lean + posePitch) * 0.7f + headTilt, headYaw * 0.7f, pose.HeadRoll);

            // Each arm swings with the opposite leg.
            var armBlend = 1f - Mathf.Exp(-15f * dt);
            leftArmDrive = Mathf.Lerp(leftArmDrive, right.Forward * (rightHalf > 0.01f ? 1f / rightHalf : 0f), armBlend);
            rightArmDrive = Mathf.Lerp(rightArmDrive, left.Forward * (leftHalf > 0.01f ? 1f / leftHalf : 0f), armBlend);
            var armsForward = pose.ArmsForward;
            var armSwing = (Mathf.Lerp(g.WalkArmSwing, g.RunArmSwing, runness) + g.SprintArmSwing * sprintness) * moving
                * (1f - g.CrouchArmSwingDrop * crouch) * (1f - g.ArmsForwardSwingDrop * armsForward);
            var bend = Mathf.Lerp(g.WalkElbowBend, g.RunElbowBend, runness) * moving + g.SprintElbowBend * sprintness + g.BaseElbowBend + g.CrouchElbowBend * crouch;
            var raise = -g.AirArmRaise * air - g.CrouchArmRaise * crouch + landingDrop * g.LandingArmRaise;
            // Arms reach out in front, fully held out at 0.8, then raised further up to 1.
            var reach = Mathf.Clamp01(armsForward / 0.8f);
            var reachPitch = Mathf.Lerp(g.ArmsForwardPitch, g.ArmsRaisedPitch, Mathf.InverseLerp(0.8f, 1f, armsForward));
            Arm(leftShoulder, leftElbow, -armSwing * leftArmDrive + raise, -g.ArmSpread, bend, reach, reachPitch, g.ReachElbowBend);
            Arm(rightShoulder, rightElbow, -armSwing * rightArmDrive + raise, g.ArmSpread, bend, reach, reachPitch, g.ReachElbowBend);
        }

        float Duty(float walkDuty, float runness, float sprintness) => Mathf.Lerp(Mathf.Lerp(walkDuty, gait.RunDuty, runness), gait.SprintDuty, sprintness);

        void SmoothPose(float dt)
        {
            var target = motion.Pose;
            var k = 1f - Mathf.Exp(-dt / gait.PoseSmoothTime);
            pose.ArmsForward = Mathf.Lerp(pose.ArmsForward, target.ArmsForward, k);
            pose.Hunch = Mathf.Lerp(pose.Hunch, target.Hunch, k);
            pose.HeadRoll = Mathf.Lerp(pose.HeadRoll, target.HeadRoll, k);
            pose.LookWeight = Mathf.Lerp(pose.LookWeight, target.LookWeight, k);
            pose.Lunge = Mathf.Lerp(pose.Lunge, target.Lunge, k);
            pose.Feed = Mathf.Lerp(pose.Feed, target.Feed, k);
        }

        void TrackMotion(float dt)
        {
            // The displacement since the last frame is the warp itself, not motion.
            if (snapped)
            {
                snapped = false;
                lastPosition = transform.position;
                lastYaw = transform.eulerAngles.y;
                return;
            }
            var position = transform.position;
            var velocity = (position - lastPosition) / dt;
            lastPosition = position;
            translationSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            if (!motion.IsGrounded) airVerticalSpeed = velocity.y;

            var previous = smoothVelocity;
            smoothVelocity = Vector3.Lerp(smoothVelocity, new Vector3(velocity.x, 0f, velocity.z), 1f - Mathf.Exp(-12f * dt));
            forwardAccel = Mathf.Lerp(forwardAccel, Vector3.Dot(smoothVelocity - previous, transform.forward) / dt, 1f - Mathf.Exp(-8f * dt));

            var yaw = transform.eulerAngles.y;
            frameYawDeltaRad = Mathf.DeltaAngle(lastYaw, yaw) * Mathf.Deg2Rad;
            yawRate = Mathf.Lerp(yawRate, Mathf.DeltaAngle(lastYaw, yaw) / dt, 1f - Mathf.Exp(-10f * dt));
            lastYaw = yaw;
        }

        // Knees absorb the impact in proportion to the fall speed, then straighten again.
        void UpdateLanding(bool grounded, float dt)
        {
            if (grounded && !wasGrounded)
                landingDrop = Mathf.Max(landingDrop, Mathf.Clamp01((-airVerticalSpeed - gait.LandingMinSpeed) / gait.LandingSpeedRange) * gait.MaxLandingDrop);
            wasGrounded = grounded;
            landingDrop *= Mathf.Exp(-dt / gait.LandingRecoveryTime);
        }

        // The CharacterController pops up and down steps instantly; the visible body eases over them.
        void SmoothSteps(bool grounded, float dt)
        {
            var y = transform.position.y;
            if (!grounded || Mathf.Abs(visualY - y) > 0.5f)
            {
                visualY = y;
                visualYVelocity = 0f;
            }
            else
            {
                visualY = Mathf.SmoothDamp(visualY, y, ref visualYVelocity, gait.StepSmoothTime, Mathf.Infinity, dt);
            }
            model.localPosition = new Vector3(0f, visualY - y, 0f);
        }

    // Stance: the foot moves from +half to -half with the ground (heel strike, then toe-off), the whole
    // window shifted back by `shift`; it lands on the cycle and then moves back by `advance` per frame, which
    // matches the cycle at a steady speed. The body also turns under a planted foot: the old foothold is
    // rotated by the frame's yaw delta, like the backward slide cancels the translation. Swing: it travels
    // forward again on an arc from where it took off, lifting at least as high as the distance it has to
    // catch up (capped at a walking step), and steps back toward its nominal sideways place. All match the
    // plain cycle exactly going straight.
    FootPose FootCycle(ref FootTrack track, float p, float duty, float half, float shift, float lift, float advance, float dt, float sideX, float yawDelta)
    {
        var heel = gait.HeelStrikeRoll;
        var toe = gait.ToeOffRoll;
        if (p < duty)
        {
            var t = p / duty;
            var roll = t < 0.2f ? Mathf.Lerp(heel, 0f, t / 0.2f) : t > 0.7f ? Mathf.Lerp(0f, toe, (t - 0.7f) / 0.3f) : 0f;
            var cycle = Mathf.Lerp(half, -half, t) - shift;
            // Landing: on the cycle, unless the swing ended away from it, then where the swing ended (the
            // window covers how far a steady swing and the cycle can differ over one frame).
            if (track.Planted)
            {
                // The root turned under the foot: rotate the old foothold by the frame's yaw expressed in
                // the banked model frame (a plain yaw would shear it), then slide it back by the travel.
                // ankleHeight stands in for the true ankle height: exact for yaw, second order via bank.
                var yaw = Quaternion.Inverse(model.localRotation)
                    * Quaternion.Euler(0f, -yawDelta * Mathf.Rad2Deg, 0f)
                    * model.localRotation;
                var rotated = yaw * new Vector3(track.Lateral, ankleHeight, track.Forward);
                track.Lateral = rotated.x;
                track.Forward = rotated.z - advance;
            }
            else track.Forward = track.Swinging
                ? Mathf.Clamp(cycle, track.Forward - 2f * advance, track.Forward + advance)
                : cycle;
                track.Planted = true;
                track.Swinging = false;
                track.Drift = track.Forward - cycle;
                track.ToeOff = cycle + half + shift;
                return new FootPose { Forward = track.Forward, Roll = roll, Support = Mathf.Sin(Mathf.PI * t) };
            }
            var landing = half - shift;
            if (!track.Swinging)
            {
                // Takes off from where the foot stands, after the rest of its stance (at most this frame's step).
                if (track.Planted) track.Forward -= Mathf.Min(track.ToeOff, advance);
                track.Planted = false;
                track.Swinging = true;
                track.Blend = 0f;
                track.Landing = landing;
            }
            track.Landing = Mathf.MoveTowards(track.Landing, landing, LandingShiftSpeed * dt);
            track.Lateral = Mathf.Lerp(track.Lateral, sideX, 1f - Mathf.Exp(-10f * dt));
            var s = (p - duty) / (1f - duty);
            var blend = Mathf.SmoothStep(0f, 1f, s);
            if (blend > track.Blend && track.Blend < 1f)
                track.Forward += (track.Landing - track.Forward) * (blend - track.Blend) / (1f - track.Blend);
            track.Blend = blend;
            return new FootPose
            {
                Forward = track.Forward,
                Lift = Mathf.Max(lift, Mathf.Min(Mathf.Abs(track.Drift), gait.WalkLift)) * Mathf.Sin(Mathf.PI * s),
                Roll = Mathf.Lerp(toe, heel, s),
            };
        }

        // Highest the hips can be while this foot stays on the ground with a stretched leg (no limit for a
        // foot that is lifted in its swing). `hipForward` is how far the hips are pushed ahead of the feet frame.
        float HipCap(FootPose foot, float groundOffset, float hipForward)
        {
            if (foot.Lift > 0.02f) return float.PositiveInfinity;
            var leg = thighLength + shinLength - 0.01f;
            var reach = foot.Forward - hipForward;
            var heelRaise = foot.Roll > 0f ? footLength * Mathf.Sin(foot.Roll * Mathf.Deg2Rad) : 0f;
            return Mathf.Sqrt(Mathf.Max(0f, leg * leg - reach * reach)) + ankleHeight + heelRaise + foot.Lift + groundOffset - hipJointOffset;
        }

        // Ground height under a foot `forward` ahead of its hip joint, relative to the visible feet level,
        // clamped to one step up or down.
        float GroundOffset(Transform hip, float forward)
        {
            var feetY = model.position.y;
            var foot = hip.position + transform.forward * forward;
            var range = gait.MaxFootOffset;
            var origin = new Vector3(foot.x, feetY + range, foot.z);
            return Physics.Raycast(origin, Vector3.down, out var hit, range * 2f, groundMask, QueryTriggerInteraction.Ignore)
                ? Mathf.Clamp(hit.point.y - feetY, -range, range)
                : 0f;
        }

        // Negative X swings a limb forward; Z spreads the arm away from the torso. `reach` blends toward arms
        // held out at `reachPitch` with nearly straight elbows.
        static void Arm(Transform shoulder, Transform elbow, float swing, float outward, float bend, float reach, float reachPitch, float reachBend)
        {
            shoulder.localRotation = Quaternion.Euler(Mathf.Lerp(swing, reachPitch, reach), 0f, outward);
            elbow.localRotation = Quaternion.Euler(-Mathf.Lerp(bend + Mathf.Max(0f, -swing) * 0.4f, reachBend, reach), 0f, 0f);
        }

        // Two-bone IK placing the ankle in the model frame at (`lateral`, `height` above the ground, `forward`),
        // with the knee pointing forward. The pelvis sway, yaw, roll and lunge shift are cancelled so they never
        // drag a `planted` foot. `roll` pitches the boot (heel strike, toe-off) and lifts the ankle as the foot
        // pivots on its toes; the boot otherwise stays level.
        void Leg(Transform hip, Transform knee, Transform ankle, float lateral, float forward, float height, float roll, bool planted)
        {
            var joint = hips.localPosition + hips.localRotation * hip.localPosition;
            var down = joint.y - ankleHeight - height;
            if (roll > 0f) down -= footLength * Mathf.Sin(roll * Mathf.Deg2Rad);
            var lateralOffset = lateral - joint.x;
            forward -= joint.z;
            // A foot out of reach (hips lagging over a far foot) is lifted straight up rather than pulled toward the
            // hip, so it never slides when planted nor jumps forward when it takes off; past heel strike a planted
            // boot pitches further so the toes stay down while the heel rises.
            var maxReach = thighLength + shinLength - 0.001f;
            var reachableDown = Mathf.Sqrt(Mathf.Max(0f, maxReach * maxReach - forward * forward - lateralOffset * lateralOffset));
            if (down > reachableDown)
            {
                if (planted && roll >= 0f) roll = Mathf.Asin(Mathf.Min(1f, Mathf.Sin(roll * Mathf.Deg2Rad) + (down - reachableDown) / footLength)) * Mathf.Rad2Deg;
                down = reachableDown;
            }
            var tilt = Quaternion.AngleAxis(Mathf.Atan2(lateralOffset, down) * Mathf.Rad2Deg, Vector3.forward);
            var planeDown = Mathf.Sqrt(down * down + lateralOffset * lateralOffset);

            var reach = Mathf.Min(Mathf.Sqrt(forward * forward + planeDown * planeDown), maxReach);
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
