using System;
using UnityEngine;

namespace DeadCounty.UI
{
    // Display and accessibility options kept between sessions in PlayerPrefs: interface size, one-time tips and
    // reduced flashes. Mouse sensitivity and look inversion live on the controller, which the pause menu loads.
    public static class GameSettings
    {
        public const float MinUiScale = 0.8f;
        public const float MaxUiScale = 1.3f;
        const string UiScaleKey = "dc.uiScale";
        const string TipsKey = "dc.tips";
        const string ReduceFlashesKey = "dc.reduceFlashes";

        static bool loaded;
        static float uiScale;
        static bool tips, reduceFlashes;

        public static event Action Changed;

        public static float UiScale
        {
            get
            {
                Load();
                return uiScale;
            }
            set
            {
                Load();
                uiScale = Mathf.Clamp(value, MinUiScale, MaxUiScale);
                PlayerPrefs.SetFloat(UiScaleKey, uiScale);
                Changed?.Invoke();
            }
        }

        public static bool Tips
        {
            get
            {
                Load();
                return tips;
            }
            set
            {
                Load();
                tips = value;
                PlayerPrefs.SetInt(TipsKey, value ? 1 : 0);
                Changed?.Invoke();
            }
        }

        // Drops the hit flash and the pulse of the injury vignette (Game Accessibility Guidelines).
        public static bool ReduceFlashes
        {
            get
            {
                Load();
                return reduceFlashes;
            }
            set
            {
                Load();
                reduceFlashes = value;
                PlayerPrefs.SetInt(ReduceFlashesKey, value ? 1 : 0);
                Changed?.Invoke();
            }
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            uiScale = Mathf.Clamp(PlayerPrefs.GetFloat(UiScaleKey, 1f), MinUiScale, MaxUiScale);
            tips = PlayerPrefs.GetInt(TipsKey, 1) == 1;
            reduceFlashes = PlayerPrefs.GetInt(ReduceFlashesKey, 0) == 1;
        }

        // Domain reload is disabled on entering Play mode: reload the stored values and drop the old listeners.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false;
            Changed = null;
        }
    }
}
