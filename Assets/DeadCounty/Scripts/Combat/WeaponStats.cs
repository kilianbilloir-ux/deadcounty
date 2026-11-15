using System;
using UnityEngine;

namespace DeadCounty.Combat
{
    // One melee swing. Distances in metres, times in seconds, angles in degrees. The wind-up is the commitment,
    // the recovery the opening the swing leaves.
    [Serializable]
    public struct WeaponStats
    {
        public float Damage;
        [Tooltip("From the attacker's axis to the surface of the target.")]
        public float Reach;
        public float HalfAngle;
        [Min(1)] public int MaxTargets;
        public float Windup;
        public float Recovery;
        public float StaminaCost;
        [Tooltip("Radius within which a blow that lands is heard.")]
        public float NoiseRadius;
        [Tooltip("Seconds a target reels after the blow.")]
        public float Stagger;
        [Tooltip("Distance the blow pushes a target back.")]
        public float Knockback;

        // Bare hands: a shove buys room and barely hurts.
        public static WeaponStats Shove => new()
        {
            Damage = 4f,
            Reach = 1f,
            HalfAngle = 55f,
            MaxTargets = 2,
            Windup = 0.14f,
            Recovery = 0.4f,
            StaminaCost = 6f,
            NoiseRadius = 2f,
            Stagger = 1.1f,
            Knockback = 0.9f,
        };

        // The same swing from an exhausted arm.
        public readonly WeaponStats Tired()
        {
            var tired = this;
            tired.Damage *= 0.5f;
            tired.Stagger *= 0.5f;
            tired.Knockback = 0f;
            return tired;
        }
    }
}
