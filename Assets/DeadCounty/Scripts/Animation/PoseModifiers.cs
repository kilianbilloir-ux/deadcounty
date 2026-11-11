using System;
using UnityEngine;

namespace DeadCounty.Animation
{
    // Expressive overlays on top of the gait, set each frame by the motion source (zombie brain, player pose
    // driver); the animator smooths them, except the flinch, which must snap.
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
        [Tooltip("Hands digging through furniture, in turn.")]
        [Range(0f, 1f)] public float Rummage;
        [Tooltip("Out of breath: deeper, faster breathing, even on the move.")]
        [Range(0f, 1f)] public float Pant;
        [Tooltip("Recoil of the torso and head (deg), applied without smoothing: a hit, a zombie's spasm.")]
        public float FlinchPitch;
        public float FlinchRoll;

        public static PoseModifiers Neutral => new() { LookWeight = 1f };
    }
}
