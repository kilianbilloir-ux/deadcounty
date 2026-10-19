using System;
using UnityEngine;

namespace DeadCounty.Animation
{
    // Expressive overlays on top of the gait, set each frame by the motion source (the zombie brain); the
    // animator smooths them.
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

        public static PoseModifiers Neutral => new() { LookWeight = 1f };
    }
}
