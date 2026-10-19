using System;
using UnityEngine;

namespace DeadCounty.AI
{
    // Behaviour of one zombie kind. Speeds in m/s, times in seconds, distances in metres, angles in degrees;
    // jitters and chances are fractions, drawn per zombie from its seed.
    [Serializable]
    public struct ZombieTuning
    {
        [Header("Speeds")]
        public float WanderSpeed;
        public float InvestigateSpeed;
        public float SearchSpeed;
        public float ChaseSpeed;
        public float WanderSpeedJitter;
        [Tooltip("Applies to the investigate, search and chase speeds.")]
        public float SpeedJitter;

        [Header("Wander")]
        [Tooltip("Chance of standing still rather than walking to a new point around the anchor.")]
        public float IdleChance;
        public float IdleMin;
        public float IdleMax;
        public float WanderRadius;
        [Tooltip("Wander points farther than this from the anchor are rejected.")]
        public float LeashRadius;

        [Header("Senses")]
        public float FieldOfView;
        public float DayRange;
        public float NightRange;
        [Tooltip("Sight range at night on a target with its flashlight on.")]
        public float FlashlightRange;
        public float CrouchRangeFactor;
        public float EyeHeight;
        [Tooltip("A target this close is felt all around, line of sight permitting.")]
        public float ProximityRadius;
        [Tooltip("Snarl before chasing a target felt in proximity.")]
        public float ProximitySnarl;
        public float HearingMultiplier;
        [Tooltip("Hearing multiplier while idle, wandering or feeding.")]
        public float HearingUnaware;
        [Tooltip("Hearing multiplier while alert or investigating.")]
        public float HearingAlert;
        [Tooltip("Hearing multiplier while searching, chasing or attacking.")]
        public float HearingHunting;

        [Header("Suspicion")]
        public float AlertThreshold;
        public float ChaseThreshold;
        [Tooltip("Shortest alert before a chase: the telegraph the player can read.")]
        public float MinAlertTime;
        [Tooltip("Shortest alert after hearing another zombie's call.")]
        public float CalledAlertTime;
        [Tooltip("Seconds without any stimulus before suspicion starts decaying.")]
        public float SuspicionHold;
        [Tooltip("Suspicion lost per second.")]
        public float SuspicionDecay;
        [Tooltip("Suspicion added by a heard noise. Noise alone never reaches ChaseThreshold.")]
        public float NoiseSuspicion;
        [Tooltip("Sight fills suspicion this much faster once alert, investigating or searching.")]
        public float AlertedFillMultiplier;
        [Tooltip("Fill rate factor when only one of the sight rays reaches the target.")]
        public float OneRayFactor;

        [Header("Chase and search")]
        [Tooltip("After losing sight the chase still follows the target's true position this long: momentum around corners.")]
        public float TrackTime;
        [Tooltip("Out of sight this long and at the predicted point, the chase turns into a search.")]
        public float GiveUpTime;
        [Tooltip("Longest extrapolation of the last known velocity.")]
        public float PredictionHorizon;
        public float SearchDuration;
        public int SearchPoints;
        public float SearchRadiusStart;
        public float SearchRadiusEnd;
        public float SearchPauseMin;
        public float SearchPauseMax;
        [Tooltip("Chance of a search point on the opposite side of the target's last heading.")]
        public float WrongSideChance;
        [Tooltip("Look around at the noise before the short search that ends an investigation.")]
        public float InvestigateLookTime;
        public float InvestigateSearchDuration;
        public int InvestigateSearchPoints;
        public float InvestigateSearchRadius;

        [Header("Social")]
        [Tooltip("Radius of the growl let out on starting a chase.")]
        public float CallRadius;
        public float CallCooldown;

        [Header("Feeding")]
        [Tooltip("Aware zombies this close to the dead target feed on it; farther ones go back to wandering.")]
        public float FeedRadius;

        [Header("Pose")]
        [Tooltip("Per zombie spine hunch range (deg).")]
        public float HunchMin;
        public float HunchMax;
        [Tooltip("Per zombie head tilt, either side (deg).")]
        public float HeadRollMax;

        public AttackSettings Attack;

        public static ZombieTuning Standard => new()
        {
            WanderSpeed = 0.55f,
            InvestigateSpeed = 0.9f,
            SearchSpeed = 0.8f,
            ChaseSpeed = 1.2f,
            WanderSpeedJitter = 0.12f,
            SpeedJitter = 0.1f,

            IdleChance = 0.6f,
            IdleMin = 4f,
            IdleMax = 12f,
            WanderRadius = 8f,
            LeashRadius = 12f,

            FieldOfView = 120f,
            DayRange = 20f,
            NightRange = 8f,
            FlashlightRange = 25f,
            CrouchRangeFactor = 0.6f,
            EyeHeight = 1.6f,
            ProximityRadius = 1.1f,
            ProximitySnarl = 0.4f,
            HearingMultiplier = 1f,
            HearingUnaware = 1f,
            HearingAlert = 1.25f,
            HearingHunting = 1.5f,

            AlertThreshold = 0.3f,
            ChaseThreshold = 1f,
            MinAlertTime = 1.5f,
            CalledAlertTime = 0.6f,
            SuspicionHold = 0.5f,
            SuspicionDecay = 0.4f,
            NoiseSuspicion = 0.5f,
            AlertedFillMultiplier = 1.5f,
            OneRayFactor = 0.6f,

            TrackTime = 1f,
            GiveUpTime = 4f,
            PredictionHorizon = 1.5f,
            SearchDuration = 15f,
            SearchPoints = 4,
            SearchRadiusStart = 4f,
            SearchRadiusEnd = 8f,
            SearchPauseMin = 1.5f,
            SearchPauseMax = 2.5f,
            WrongSideChance = 0.25f,
            InvestigateLookTime = 3f,
            InvestigateSearchDuration = 6f,
            InvestigateSearchPoints = 2,
            InvestigateSearchRadius = 3f,

            CallRadius = 10f,
            CallCooldown = 5f,

            FeedRadius = 3f,

            HunchMin = 8f,
            HunchMax = 18f,
            HeadRollMax = 10f,

            Attack = AttackSettings.Default,
        };
    }
}
