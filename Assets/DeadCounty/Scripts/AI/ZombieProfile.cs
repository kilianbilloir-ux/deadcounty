using DeadCounty.Animation;
using DeadCounty.Locomotion;
using UnityEngine;

namespace DeadCounty.AI
{
    // Everything that defines a zombie kind: behaviour, body physics, gait and the share of limpers.
    // Read-only at runtime: Play mode edits would persist into the asset.
    [CreateAssetMenu(menuName = "DeadCounty/Zombie Profile", fileName = "ZP_")]
    public class ZombieProfile : ScriptableObject
    {
        public ZombieTuning Tuning = ZombieTuning.Standard;
        public LocomotionSettings Locomotion = ShamblerLocomotion;
        public GaitProfile Gait;
        [Range(0f, 1f)] public float LimpChance = 0.3f;

        // Heavy to get going and slow to turn once moving, so a running player can circle it and a walking one
        // cannot. PivotAngle above 180 never triggers: a zombie does not plant and pivot.
        public static LocomotionSettings ShamblerLocomotion => new()
        {
            Acceleration = 2.5f,
            Deceleration = 4f,
            AirAcceleration = 1f,
            StandingTurnRate = 160f,
            MovingTurnRate = 90f,
            TurnRateReferenceSpeed = 1.2f,
            PivotAngle = 181f,
            PivotMinSpeed = 100f,
            PivotTurnScale = 1f,
            TightTurnSpeedFactor = 0.4f,
        };
    }
}
