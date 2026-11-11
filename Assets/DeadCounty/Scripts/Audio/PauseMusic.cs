using DeadCounty.Player;
using DeadCounty.UI;
using UnityEngine;

namespace DeadCounty.Audio
{
    // Quiet music behind the pause menu and its options. Music channel, so it plays while the clock is stopped.
    [RequireComponent(typeof(GameMenu))]
    public class PauseMusic : MonoBehaviour
    {
        [SerializeField] SoundCue theme;
        [Tooltip("Seconds to rise once the game is paused.")]
        [SerializeField] float fadeIn = 2.5f;
        [Tooltip("Seconds to die away on resume.")]
        [SerializeField] float fadeOut = 0.8f;

        GameMenu menu;
        AudioSource source;
        float level, volume;
        bool started;

        // 0 silent, 1 fully in.
        public float Level => level;
        public bool Playing => source.isPlaying;

        void Awake()
        {
            menu = GetComponent<GameMenu>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
        }

        void OnEnable() => GameSettings.Changed += Configure;

        void OnDisable() => GameSettings.Changed -= Configure;

        void Start()
        {
            if (theme == null) return;
            Configure();
            source.clip = theme.Pick();
        }

        void Configure()
        {
            if (theme == null) return;
            Sfx.Configure(source, theme);
            volume = source.volume;
            source.volume = volume * level * level;
        }

        // Real time: the clock is stopped while the menu is up.
        void Update()
        {
            if (theme == null) return;
            var paused = menu.Paused;
            level = Mathf.MoveTowards(level, paused ? 1f : 0f, Time.unscaledDeltaTime / (paused ? fadeIn : fadeOut));
            source.volume = volume * level * level;
            if (level > 0f && !source.isPlaying)
            {
                // Held rather than stopped, so each pause picks the music up where the last one left it.
                if (started) source.UnPause();
                else source.Play();
                started = true;
            }
            else if (level <= 0f && source.isPlaying)
            {
                source.Pause();
            }
        }
    }
}
