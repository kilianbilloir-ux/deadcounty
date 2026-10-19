using System;
using UnityEngine;

namespace DeadCounty.AI
{
    // Zombie grab attack: a slow telegraphed wind-up the player can read and dodge, then a short lunge along a
    // locked direction. Distances in metres, times in seconds, angles in degrees.
    [Serializable]
    public struct AttackSettings
    {
        [Tooltip("Trigger distance against a target standing still or coming closer.")]
        public float TriggerBase;
        [Tooltip("Trigger distance lost per m/s the target moves away, so a fleeing target is not caught from afar.")]
        public float TriggerPerAwaySpeed;
        public float TriggerMin;
        public float TriggerHalfAngle;
        public float Windup;
        [Tooltip("Time into the wind-up at which the arms come up, the visible tell.")]
        public float ArmsUpAt;
        [Tooltip("The zombie keeps tracking its target during the wind-up, no faster than this (deg/s).")]
        public float WindupTurnRate;
        public float LungeDistance;
        public float LungeDuration;
        [Tooltip("The lunge never brings the zombie closer than this to its target.")]
        public float ContactDistance;
        public float HitReach;
        public float HitHalfWidth;
        public float Damage;
        public float RecoverAfterHit;
        public float RecoverAfterMiss;
        public float Cooldown;

        public static AttackSettings Default => new()
        {
            TriggerBase = 1.6f,
            TriggerPerAwaySpeed = 0.3f,
            TriggerMin = 1f,
            TriggerHalfAngle = 45f,
            Windup = 1f,
            ArmsUpAt = 0.6f,
            WindupTurnRate = 90f,
            LungeDistance = 0.6f,
            LungeDuration = 0.2f,
            ContactDistance = 0.65f,
            HitReach = 1.3f,
            HitHalfWidth = 0.5f,
            Damage = 25f,
            RecoverAfterHit = 0.6f,
            RecoverAfterMiss = 0.9f,
            Cooldown = 0.4f,
        };
    }
}
