using System;
using UnityEngine;

namespace DeadCounty.Animation
{
    // Expressive overlays on top of the gait, set each frame by the motion source (zombie brain, player pose
    // driver); the animator smooths them, except the strikes and the flinch, which must snap.
    [Serializable]
    public struct PoseModifiers
    {
        [Tooltip("0 swinging arms, 0.8 arms held out in front, 1 arms raised to grab.")]
        [Range(0f, 1f)] public float ArmsForward;
        [Tooltip("Extra spine pitch (deg).")]
        public float Hunch;
        [Tooltip("Neck roll (deg).")]
        public float HeadRoll;
        [Tooltip("How much the head follows AimYaw and AimPitch.")]
        [Range(0f, 1f)] public float LookWeight;
        [Range(0f, 1f)] public float Lunge;
        [Tooltip("Bent over a body on the ground.")]
        [Range(0f, 1f)] public float Feed;
        [Tooltip("Right hand out to push a door or pick something up.")]
        [Range(0f, 1f)] public float Reach;
        [Tooltip("Right hand brought up to the mouth: eating, drinking.")]
        [Range(0f, 1f)] public float HandToMouth;
        [Tooltip("Hands digging through furniture, in turn.")]
        [Range(0f, 1f)] public float Rummage;
        [Tooltip("Out of breath: deeper, faster breathing, even on the move.")]
        [Range(0f, 1f)] public float Pant;
        [Tooltip("Right arm through a swing, applied without smoothing: 0 cocked up beside the head, 0.5 out in front, 1 followed through across the body.")]
        [Range(0f, 1f)] public float Swing;
        [Tooltip("How much the swing takes over the right arm and the chest, applied without smoothing.")]
        [Range(0f, 1f)] public float SwingWeight;
        [Tooltip("How much the swing comes down overhead onto the ground instead of across the body, applied without smoothing.")]
        [Range(0f, 1f)] public float Chop;
        [Tooltip("Both hands thrust out to shove, applied without smoothing.")]
        [Range(0f, 1f)] public float Shove;
        [Tooltip("Right leg through a stomp, applied without smoothing: the foot stamps down in front at 0.5 and is back home at 1.")]
        [Range(0f, 1f)] public float Stomp;
        [Tooltip("Recoil of the torso and head (deg), applied without smoothing: a hit, a zombie's spasm.")]
        public float FlinchPitch;
        public float FlinchRoll;

        public static PoseModifiers Neutral => new() { LookWeight = 1f };
    }
}
