using System;
using DeadCounty.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCounty.Player
{
    // Full-screen effects through global volumes: a dark red vignette that deepens as health drops under 40 % and
    // beats slowly, steady when flashes are reduced, and a blur behind the pause menu and the options (GDD 8.11,
    // after Risk of Rain 2), which stop the game. The world stays sharp behind the inventory, which does not, and
    // behind the title page, which shows it off.
    public class PlayerScreenEffects : MonoBehaviour
    {
        const float InjuredBelow = 0.4f;
        const float FullInjuryAt = 0.15f;
        // About one beat a second, far under the three flashes a second that photosensitivity guidelines allow.
        const float BeatRate = 0.9f;
        const float BeatDepth = 0.25f;
        const float InjuryFadeSpeed = 1.5f;
        const float BlurFadeSpeed = 6f;

        [SerializeField] Volume injuryVolume;
        [SerializeField] Volume menuBlurVolume;

        PlayerHealth health;
        GameMenu menu;

        void Awake()
        {
            health = GetComponentInChildren<PlayerHealth>(true);
            menu = GetComponent<GameMenu>();
            if (health == null || menu == null)
                throw new InvalidOperationException($"PlayerScreenEffects on '{name}' needs a PlayerHealth in its children and a GameMenu next to it");
            if (injuryVolume == null || menuBlurVolume == null)
                throw new InvalidOperationException($"PlayerScreenEffects on '{name}' needs its injury and menu blur volumes");
            injuryVolume.weight = 0f;
            menuBlurVolume.weight = 0f;
        }

        // Real time: the pause stops the game clock and sleep runs it 20 times faster.
        void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var injury = health.IsAlive ? Mathf.InverseLerp(InjuredBelow, FullInjuryAt, health.Pool.Normalized) : 0f;
            if (!GameSettings.ReduceFlashes) injury *= 1f - BeatDepth * 0.5f * (1f + Mathf.Sin(Time.unscaledTime * BeatRate * 2f * Mathf.PI));
            injuryVolume.weight = Mathf.MoveTowards(injuryVolume.weight, injury, InjuryFadeSpeed * dt);
            menuBlurVolume.weight = Mathf.MoveTowards(menuBlurVolume.weight, menu.Shown && !menu.TitleShown ? 1f : 0f, BlurFadeSpeed * dt);
        }
    }
}
