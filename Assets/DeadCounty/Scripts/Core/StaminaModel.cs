using System;
using UnityEngine;

namespace DeadCounty.Core
{
    // Crouch-idle maps to Idle and crouch-walk to Walk.
    public enum StaminaActivity { Idle, Walk, Run, Sprint, Airborne }

    [Serializable]
    public struct StaminaSettings
    {
        public float Max;
        public float SprintDrainPerSecond;
        public float JumpCost;
        public float MinToJump;
        [Tooltip("Seconds after the last cost before stamina starts coming back.")]
        public float RegenDelay;
        public float IdleRegenPerSecond;
        public float WalkRegenPerSecond;
        [Tooltip("Running and sprinting stay locked after hitting 0 until stamina is back to this value.")]
        public float ExhaustionRecoverThreshold;

        public static StaminaSettings Default => new()
        {
            Max = 100f,
            SprintDrainPerSecond = 20f,
            JumpCost = 10f,
            MinToJump = 10f,
            RegenDelay = 1f,
            IdleRegenPerSecond = 25f,
            WalkRegenPerSecond = 18f,
            ExhaustionRecoverThreshold = 40f,
        };
    }

    // Three-gait stamina: sprinting drains it, running is free (its cost is noise), standing and walking refill
    // it after a short delay. Emptying it exhausts the player, who can then only walk until it recovers.
    public class StaminaModel
    {
        readonly StaminaSettings settings;
        float sinceCost;

        public StaminaModel(StaminaSettings settings)
        {
            this.settings = settings;
            Reset();
        }

        public float Current { get; private set; }
        public float Normalized => Current / settings.Max;
        public bool Exhausted { get; private set; }
        public bool CanRun => !Exhausted;
        public bool CanSprint => !Exhausted && Current > 0f;

        public void Tick(StaminaActivity activity, float dt)
        {
            if (activity == StaminaActivity.Sprint)
            {
                Spend(settings.SprintDrainPerSecond * dt);
                return;
            }
            var regenRate = activity switch
            {
                StaminaActivity.Idle => settings.IdleRegenPerSecond,
                StaminaActivity.Walk => settings.WalkRegenPerSecond,
                _ => 0f,
            };
            sinceCost += dt;
            // Only the part of the frame past the delay regenerates, so the result does not depend on the frame rate.
            var regenTime = Mathf.Min(dt, sinceCost - settings.RegenDelay);
            if (regenTime > 0f) Current = Mathf.Min(Current + regenRate * regenTime, settings.Max);
            if (Exhausted && Current >= settings.ExhaustionRecoverThreshold) Exhausted = false;
        }

        public bool TryJump()
        {
            if (Exhausted || Current < settings.MinToJump) return false;
            Spend(settings.JumpCost);
            return true;
        }

        public void Reset()
        {
            Current = settings.Max;
            Exhausted = false;
            sinceCost = settings.RegenDelay;
        }

        void Spend(float amount)
        {
            Current = Mathf.Max(Current - amount, 0f);
            sinceCost = 0f;
            if (Current <= 0f) Exhausted = true;
        }
    }
}
