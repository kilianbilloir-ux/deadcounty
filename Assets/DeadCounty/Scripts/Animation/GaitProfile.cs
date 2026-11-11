using UnityEngine;

namespace DeadCounty.Animation
{
    // Every gait and upper body tuning value of ProceduralHumanoidAnimator, shared by all bodies of a kind.
    // Defaults are the survivor's human gait (GP_Survivor); CreateShambler gives the zombie walk (GP_Shambler).
    // Angles in degrees, distances in metres, speeds in m/s. Read-only at runtime.
    [CreateAssetMenu(menuName = "DeadCounty/Gait Profile", fileName = "GP_")]
    public class GaitProfile : ScriptableObject
    {
        [Header("Cadence")]
        [Tooltip("Stride cycles per second = base + perSpeed x speed.")]
        public float CadenceBase = 0.85f;
        public float CadencePerSpeed = 0.12f;
        [Tooltip("Fraction of the cadence lost when fully crouched.")]
        public float CrouchCadenceDrop = 0.15f;

        [Header("Gait bands")]
        [Tooltip("Speed around which walking turns into running and a flight phase appears.")]
        public float RunTransitionSpeed = 2.2f;
        [Tooltip("The walk to run blend starts this far below RunTransitionSpeed...")]
        public float RunBlendBelow = 0.4f;
        [Tooltip("...and ends this far above it.")]
        public float RunBlendAbove = 0.6f;
        public float SprintStartSpeed = 4.5f;
        public float SprintFullSpeed = 6f;
        [Tooltip("Speed at which steps reach their full lift, roll and arm swing.")]
        public float FullStrideSpeed = 0.5f;
        [Tooltip("Turning on the spot steps like moving at this many m/s per rad/s of turn rate.")]
        public float TurnStepFactor = 0.25f;

        [Header("Stride")]
        [Tooltip("Fraction of a cycle each foot spends on the ground.")]
        public float WalkDuty = 0.62f;
        public float RunDuty = 0.38f;
        public float SprintDuty = 0.3f;
        public float WalkLift = 0.09f;
        public float RunLift = 0.22f;
        public float SprintLift = 0.32f;
        [Tooltip("Fraction of the foot lift lost when fully crouched.")]
        public float CrouchLiftDrop = 0.5f;
        public float MaxHalfStride = 0.75f;
        [Tooltip("How far the stance window sits behind the hip, as a fraction of half the stance length.")]
        public float StanceBias = 0.22f;
        [Tooltip("Boot pitch at heel strike (negative: toes up).")]
        public float HeelStrikeRoll = -12f;
        [Tooltip("Boot pitch at toe-off (positive: heel up).")]
        public float ToeOffRoll = 25f;
        [Tooltip("Feet tuck up by this much in the air.")]
        public float AirLegTuck = 0.25f;

        [Header("Pelvis")]
        [Tooltip("Lowest the hips may drop below their nominal height to keep the stance foot planted.")]
        public float MaxDip = 0.12f;
        public float HipSmoothTime = 0.04f;
        public float RunBob = 0.05f;
        public float WalkSway = 0.025f;
        [Tooltip("Pelvis roll toward the swing leg.")]
        public float PelvisRoll = 5f;
        [Tooltip("Pelvis yaw following the forward leg, walking and sprinting.")]
        public float PelvisYaw = 5f;
        public float SprintPelvisYaw = 10f;
        [Tooltip("Fraction of the walking sway and roll lost when running.")]
        public float RunSwayDrop = 0.6f;

        [Header("Torso")]
        public float WalkLean = 3f;
        public float RunLean = 8f;
        [Tooltip("Added on top of RunLean when sprinting.")]
        public float SprintLean = 10f;
        public float CrouchLean = 35f;
        [Tooltip("Torso pitch per m/s² of forward acceleration.")]
        public float AccelerationLean = 2f;
        public float MaxBrakeLean = 8f;
        public float MaxAccelerationLean = 12f;
        [Tooltip("Scale of the physical (centripetal) bank into turns.")]
        public float BankScale = 1f;
        public float MaxTurnLean = 12f;
        [Tooltip("Spine pitch while standing still.")]
        public float IdleBreathAmplitude = 1.5f;
        [Tooltip("Hz.")]
        public float IdleBreathFrequency = 1.7f / (2f * Mathf.PI);
        [Tooltip("Spine roll while standing still.")]
        public float IdleSwayAmplitude;
        [Tooltip("Hz.")]
        public float IdleSwayFrequency = 0.3f;

        [Header("Head")]
        public float HeadYawLimit = 70f;
        public float HeadPitchLimit = 30f;
        [Tooltip("Aim further than the yaw limit: true holds the head at the limit, false recenters it past HeadRecenterAngle.")]
        public bool HeadClampsAtLimit;
        public float HeadRecenterAngle = 110f;
        [Tooltip("Fraction of the aim pitch the head follows.")]
        public float HeadPitchFollow = 0.6f;
        [Tooltip("Fraction of the head aim lost when sprinting.")]
        public float SprintHeadFollowDrop = 0.7f;

        [Header("Arms")]
        public float WalkArmSwing = 18f;
        public float RunArmSwing = 40f;
        [Tooltip("Added on top of RunArmSwing when sprinting.")]
        public float SprintArmSwing = 25f;
        [Tooltip("Fraction of the arm swing lost when fully crouched.")]
        public float CrouchArmSwingDrop = 0.5f;
        public float WalkElbowBend = 12f;
        public float RunElbowBend = 80f;
        [Tooltip("Added on top of RunElbowBend when sprinting.")]
        public float SprintElbowBend = 15f;
        [Tooltip("Elbow bend standing still.")]
        public float BaseElbowBend = 8f;
        public float CrouchElbowBend = 25f;
        [Tooltip("Arms spread away from the torso.")]
        public float ArmSpread = 7f;
        public float AirArmRaise = 35f;
        public float CrouchArmRaise = 15f;
        [Tooltip("Arm raise per metre of landing compression.")]
        public float LandingArmRaise = 60f;

        [Header("Landing")]
        [Tooltip("Falls slower than this do not compress the legs.")]
        public float LandingMinSpeed = 1.5f;
        [Tooltip("Fall speed above LandingMinSpeed giving the full compression.")]
        public float LandingSpeedRange = 6f;
        public float MaxLandingDrop = 0.22f;
        public float LandingRecoveryTime = 0.15f;

        [Header("Ground adaptation")]
        public float MaxFootOffset = 0.35f;
        public float FootBlendSpeed = 15f;
        public float StepSmoothTime = 0.07f;

        [Header("Pose modifiers")]
        [Tooltip("Time constant smoothing the motion source's PoseModifiers (s).")]
        public float PoseSmoothTime = 0.25f;
        [Tooltip("Shoulder pitch with ArmsForward at 0.8 (arms held out in front).")]
        public float ArmsForwardPitch = -70f;
        [Tooltip("Shoulder pitch with ArmsForward at 1 (arms raised to grab).")]
        public float ArmsRaisedPitch = -100f;
        [Tooltip("Fraction of the arm swing lost per unit of ArmsForward.")]
        public float ArmsForwardSwingDrop = 0.8f;
        [Tooltip("Elbow bend of arms held out in front.")]
        public float ReachElbowBend = 15f;
        public float LungePitch = 25f;
        public float LungeHipShift = 0.1f;
        public float FeedPitch = 60f;

        // Zombie shuffle: slow cadence, long stance, feet skimming the ground, flat heel and toe, no flight
        // phase or sprint at zombie speeds, little banking and arm swing, a head that sticks at its limit and a
        // slow sway instead of breathing.
        public static GaitProfile CreateShambler()
        {
            var profile = CreateInstance<GaitProfile>();
            profile.CadenceBase = 0.7f;
            profile.CadencePerSpeed = 0.3f;
            profile.WalkDuty = 0.72f;
            profile.WalkLift = 0.05f;
            profile.HeelStrikeRoll = -4f;
            profile.ToeOffRoll = 12f;
            profile.RunTransitionSpeed = 3.5f;
            profile.SprintStartSpeed = 99f;
            profile.SprintFullSpeed = 100f;
            profile.MaxTurnLean = 5f;
            profile.BankScale = 0.35f;
            profile.WalkArmSwing = 6f;
            profile.HeadYawLimit = 85f;
            profile.HeadClampsAtLimit = true;
            profile.IdleBreathAmplitude = 0f;
            profile.IdleSwayAmplitude = 2f;
            profile.IdleSwayFrequency = 0.3f;
            return profile;
        }
    }
}
