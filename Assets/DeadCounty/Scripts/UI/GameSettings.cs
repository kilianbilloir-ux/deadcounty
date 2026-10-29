using System;
using UnityEngine;

namespace DeadCounty.UI
{
    // Display, accessibility and sound options kept between sessions in PlayerPrefs: interface size, one-time tips,
    // reduced flashes and the volumes (0..1). Mouse sensitivity and look inversion live on the controller, which the
    // pause menu loads.
    public static class GameSettings
    {
        public const float MinUiScale = 0.8f;
        public const float MaxUiScale = 1.3f;
        const string UiScaleKey = "dc.uiScale";
        const string TipsKey = "dc.tips";
        const string ReduceFlashesKey = "dc.reduceFlashes";
        const string MasterVolumeKey = "dc.volume.master";
        const string EffectsVolumeKey = "dc.volume.effects";
        const string AmbienceVolumeKey = "dc.volume.ambience";
        const string MusicVolumeKey = "dc.volume.music";
        const string InterfaceVolumeKey = "dc.volume.interface";

        static bool loaded;
        static float uiScale;
        static bool tips, reduceFlashes;
        static float masterVolume, effectsVolume, ambienceVolume, musicVolume, interfaceVolume;

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

        public static float MasterVolume
        {
            get
            {
                Load();
                return masterVolume;
            }
            set => SetVolume(ref masterVolume, MasterVolumeKey, value);
        }

        public static float EffectsVolume
        {
            get
            {
                Load();
                return effectsVolume;
            }
            set => SetVolume(ref effectsVolume, EffectsVolumeKey, value);
        }

        public static float AmbienceVolume
        {
            get
            {
                Load();
                return ambienceVolume;
            }
            set => SetVolume(ref ambienceVolume, AmbienceVolumeKey, value);
        }

        public static float MusicVolume
        {
            get
            {
                Load();
                return musicVolume;
            }
            set => SetVolume(ref musicVolume, MusicVolumeKey, value);
        }

        public static float InterfaceVolume
        {
            get
            {
                Load();
                return interfaceVolume;
            }
            set => SetVolume(ref interfaceVolume, InterfaceVolumeKey, value);
        }

        static void SetVolume(ref float volume, string key, float value)
        {
            Load();
            volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(key, volume);
            Changed?.Invoke();
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            uiScale = Mathf.Clamp(PlayerPrefs.GetFloat(UiScaleKey, 1f), MinUiScale, MaxUiScale);
            tips = PlayerPrefs.GetInt(TipsKey, 1) == 1;
            reduceFlashes = PlayerPrefs.GetInt(ReduceFlashesKey, 0) == 1;
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 0.8f));
            effectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsVolumeKey, 1f));
            ambienceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(AmbienceVolumeKey, 1f));
            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 1f));
            interfaceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(InterfaceVolumeKey, 1f));
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
