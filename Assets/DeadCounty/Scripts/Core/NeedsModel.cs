using System;
using UnityEngine;

namespace DeadCounty.Core
{
    [Serializable]
    public struct NeedsSettings
    {
        [Tooltip("Minutes for a full gauge to empty.")]
        public float HungerMinutes;
        public float ThirstMinutes;
        public float FatigueMinutes;
        [Tooltip("Fatigue drains this many times faster while sprinting.")]
        public float SprintFatigueFactor;
        [Tooltip("Health lost per minute while hunger is empty.")]
        public float StarvingDamagePerMinute;
        [Tooltip("Health lost per minute while thirst is empty.")]
        public float DehydratedDamagePerMinute;
        [Tooltip("Share of the stamina capacity left while fatigue is empty.")]
        public float ExhaustedStaminaCapacity;

        // GDD 8.4: a 100 health player starves in 3 min and dies of thirst in 2.
        public static NeedsSettings Default => new()
        {
            HungerMinutes = 20f,
            ThirstMinutes = 12f,
            FatigueMinutes = 25f,
            SprintFatigueFactor = 2f,
            StarvingDamagePerMinute = 100f / 3f,
            DehydratedDamagePerMinute = 50f,
            ExhaustedStaminaCapacity = 0.5f,
        };
    }

    // Hunger, thirst and fatigue from 1 (fine) down to 0, draining over time (GDD 8.4). Empty hunger and thirst
    // cost health; empty fatigue costs no health but halves the stamina capacity.
    public class NeedsModel
    {
        readonly NeedsSettings settings;

        public NeedsModel(NeedsSettings settings)
        {
            if (!(settings.HungerMinutes > 0f) || !(settings.ThirstMinutes > 0f) || !(settings.FatigueMinutes > 0f))
                throw new ArgumentOutOfRangeException(nameof(settings), "NeedsModel needs positive drain times");
            this.settings = settings;
            Reset();
        }

        public float Hunger { get; private set; }
        public float Thirst { get; private set; }
        public float Fatigue { get; private set; }
        public bool Exhausted => Fatigue <= 0f;
        public float StaminaCapacity => Exhausted ? settings.ExhaustedStaminaCapacity : 1f;

        // Returns the health lost over dt, counting only the part of the frame spent with an empty need so the
        // result does not depend on the frame rate.
        public float Tick(float dt, bool sprinting)
        {
            Hunger = Drain(Hunger, settings.HungerMinutes, dt, out var starving);
            Thirst = Drain(Thirst, settings.ThirstMinutes, dt, out var dehydrated);
            var fatigueMinutes = sprinting ? settings.FatigueMinutes / settings.SprintFatigueFactor : settings.FatigueMinutes;
            Fatigue = Drain(Fatigue, fatigueMinutes, dt, out _);
            return (starving * settings.StarvingDamagePerMinute + dehydrated * settings.DehydratedDamagePerMinute) / 60f;
        }

        // Eating and drinking: shares of a full gauge, capped at full.
        public void Restore(float hunger, float thirst)
        {
            Hunger = Mathf.Clamp01(Hunger + hunger);
            Thirst = Mathf.Clamp01(Thirst + thirst);
        }

        public void Reset()
        {
            Hunger = 1f;
            Thirst = 1f;
            Fatigue = 1f;
        }

        static float Drain(float value, float minutes, float dt, out float emptyTime)
        {
            var perSecond = 1f / (minutes * 60f);
            emptyTime = Mathf.Max(dt - value / perSecond, 0f);
            return Mathf.Max(value - perSecond * dt, 0f);
        }
    }
}
