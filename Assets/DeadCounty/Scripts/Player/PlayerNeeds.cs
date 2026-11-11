using System;
using DeadCounty.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCounty.Player
{
    // Hunger, thirst and fatigue of the player. Empty needs drain health and cap stamina; empty fatigue also
    // darkens the screen through a global volume. Asleep, fatigue fills back (see PlayerSleep).
    [RequireComponent(typeof(PlayerHealth), typeof(PlayerStamina))]
    public class PlayerNeeds : MonoBehaviour
    {
        const float VignetteFadeSpeed = 0.5f;

        [SerializeField] NeedsSettings settings = NeedsSettings.Default;
        [Tooltip("Global volume blended in while fatigue is empty.")]
        [SerializeField] Volume exhaustionVolume;

        ThirdPersonController controller;
        PlayerHealth health;
        PlayerStamina stamina;

        public NeedsModel Model { get; private set; }
        // Set by PlayerSleep.
        public bool Asleep { get; set; }

        void OnValidate()
        {
            var defaults = NeedsSettings.Default;
            if (!(settings.HungerMinutes > 0f)) settings.HungerMinutes = defaults.HungerMinutes;
            if (!(settings.ThirstMinutes > 0f)) settings.ThirstMinutes = defaults.ThirstMinutes;
            if (!(settings.FatigueMinutes > 0f)) settings.FatigueMinutes = defaults.FatigueMinutes;
            if (!(settings.RestMinutes > 0f)) settings.RestMinutes = defaults.RestMinutes;
        }

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            health = GetComponent<PlayerHealth>();
            stamina = GetComponent<PlayerStamina>();
            if (exhaustionVolume == null) throw new InvalidOperationException($"PlayerNeeds on '{name}' needs its exhaustion volume");
            exhaustionVolume.weight = 0f;
            Model = new NeedsModel(settings);
        }

        void Update()
        {
            if (!health.IsAlive) return;
            var dt = Time.deltaTime;
            health.ApplyDrain(Asleep ? Model.Sleep(dt) : Model.Tick(dt, controller.IsSprinting));
            stamina.Model.SetCapacity(Model.StaminaCapacity);
            exhaustionVolume.weight = Mathf.MoveTowards(exhaustionVolume.weight, Model.Exhausted ? 1f : 0f, VignetteFadeSpeed * dt);
        }

        public void ResetFull()
        {
            Model.Reset();
            exhaustionVolume.weight = 0f;
        }
    }
}
