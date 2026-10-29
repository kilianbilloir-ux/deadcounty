using System;
using DeadCounty.Player;
using DeadCounty.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DeadCounty.Audio
{
    // Dark drone under the title (Music channel, heard while the clock is stopped), with a far bell tolling now and
    // then; it rises when the title shows and dies away once Jouer is chosen. A Menu principal reloads the scene,
    // which brings it back.
    public class TitleMusic : MonoBehaviour
    {
        // The bell is pitched to notes of the drone's A minor: A, G and E below.
        static readonly float[] TollPitches = { 1f, 0.891f, 0.749f };

        [SerializeField] SoundCue drone;
        [SerializeField] SoundCue toll;
        [Tooltip("Seconds to rise when the title shows.")]
        [SerializeField] float fadeIn = 4f;
        [Tooltip("Seconds to die away after Jouer.")]
        [SerializeField] float fadeOut = 3f;
        [Tooltip("Seconds between two tolls, min and max.")]
        [SerializeField] Vector2 tollInterval = new(14f, 26f);

        GameMenu menu;
        AudioSource source;
        float level, volume, tollAt;

        void Awake()
        {
            menu = FindAnyObjectByType<GameMenu>();
            if (menu == null) throw new InvalidOperationException($"TitleMusic '{name}' found no GameMenu in the scene");
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            tollAt = Random.Range(tollInterval.x * 0.5f, tollInterval.x);
        }

        void OnEnable() => GameSettings.Changed += Configure;

        void OnDisable() => GameSettings.Changed -= Configure;

        void Start()
        {
            if (drone == null) return;
            Configure();
            source.clip = drone.Pick();
            source.volume = 0f;
            source.Play();
        }

        void Configure()
        {
            if (drone == null) return;
            Sfx.Configure(source, drone);
            volume = source.volume;
            source.volume = volume * level;
        }

        // Real time: the clock is stopped behind the title.
        void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var onTitle = menu.OnTitle;
            level = Mathf.MoveTowards(level, onTitle ? 1f : 0f, dt / (onTitle ? fadeIn : fadeOut));
            source.volume = volume * level * level;
            if (onTitle && (tollAt -= dt) <= 0f)
            {
                tollAt = Random.Range(tollInterval.x, tollInterval.y);
                var bell = Sfx.Play2D(toll, level);
                if (bell != null) bell.pitch *= TollPitches[Random.Range(0, TollPitches.Length)];
            }
            if (onTitle || level > 0f) return;
            source.Stop();
            enabled = false;
        }
    }
}
