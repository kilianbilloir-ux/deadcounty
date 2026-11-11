using DeadCounty.UI;
using UnityEngine;

namespace DeadCounty.Audio
{
    // Fire-and-forget playback of sound cues on a pool of voices kept across scene loads, plus the channel volumes
    // and the pause of world audio. Owners of looping sources apply a cue with Configure and re-run it on
    // GameSettings.Changed.
    public static class Sfx
    {
        const int VoiceCount = 32;

        static GameObject root;
        static AudioSource[] voices;
        static int[] startedAt;
        static int sequence;
        static int cursor;
        static bool subscribed;

        // Pooled 3D one-shot; null when the cue has no clip.
        public static AudioSource Play(SoundCue cue, Vector3 position, float volumeScale = 1f, float pitchScale = 1f)
        {
            var source = Start(cue, position, volumeScale, pitchScale);
            if (source != null) source.spatialBlend = 1f;
            return source;
        }

        // Pooled non-spatial one-shot: interface, music stingers, the player's own body.
        public static AudioSource Play2D(SoundCue cue, float volumeScale = 1f)
        {
            var source = Start(cue, Vector3.zero, volumeScale, 1f);
            if (source != null) source.spatialBlend = 0f;
            return source;
        }

        // Effects are positional (linear rolloff from minDistance to silence at maxDistance, no doppler); the other
        // channels are 2D. Interface and Music keep playing while the game is paused.
        public static void Configure(AudioSource source, SoundCue cue)
        {
            Install();
            source.spatialBlend = cue.channel == SoundChannel.Effects ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = cue.minDistance;
            source.maxDistance = cue.maxDistance;
            source.dopplerLevel = 0f;
            source.volume = cue.volume * ChannelVolume(cue.channel);
            source.ignoreListenerPause = cue.channel is SoundChannel.Interface or SoundChannel.Music;
        }

        public static float ChannelVolume(SoundChannel channel) => channel switch
        {
            SoundChannel.Effects => GameSettings.EffectsVolume,
            SoundChannel.Ambience => GameSettings.AmbienceVolume,
            SoundChannel.Music => GameSettings.MusicVolume,
            SoundChannel.Interface => GameSettings.InterfaceVolume,
            _ => 1f,
        };

        static AudioSource Start(SoundCue cue, Vector3 position, float volumeScale, float pitchScale)
        {
            if (cue == null) return null;
            var clip = cue.Pick();
            if (clip == null) return null;
            var source = NextVoice();
            source.transform.position = position;
            Configure(source, cue);
            source.volume *= volumeScale;
            source.pitch = cue.RandomPitch() * pitchScale;
            source.clip = clip;
            source.loop = false;
            source.Play();
            return source;
        }

        // The first idle voice after the last one used, otherwise the one started longest ago.
        static AudioSource NextVoice()
        {
            Install();
            var oldest = 0;
            for (var i = 0; i < VoiceCount; i++)
            {
                var index = (cursor + i) % VoiceCount;
                if (!voices[index].isPlaying)
                {
                    oldest = index;
                    break;
                }
                if (startedAt[index] < startedAt[oldest]) oldest = index;
            }
            cursor = (oldest + 1) % VoiceCount;
            startedAt[oldest] = ++sequence;
            voices[oldest].Stop();
            return voices[oldest];
        }

        static void Install()
        {
            if (!subscribed)
            {
                subscribed = true;
                GameSettings.Changed += ApplyMasterVolume;
                ApplyMasterVolume();
            }
            if (root != null) return;

            root = new GameObject("Sfx");
            if (Application.isPlaying)
            {
                root.hideFlags = HideFlags.HideInHierarchy;
                Object.DontDestroyOnLoad(root);
                root.AddComponent<SfxPauseWatcher>();
            }
            else root.hideFlags = HideFlags.HideAndDontSave;

            voices = new AudioSource[VoiceCount];
            startedAt = new int[VoiceCount];
            for (var i = 0; i < VoiceCount; i++)
            {
                var voice = new GameObject("Voice").AddComponent<AudioSource>();
                voice.transform.SetParent(root.transform, false);
                voice.gameObject.hideFlags = root.hideFlags;
                voice.playOnAwake = false;
                voices[i] = voice;
            }
        }

        static void ApplyMasterVolume() => AudioListener.volume = GameSettings.MasterVolume;

        // Domain reload is disabled on entering Play mode: the pool of the previous session is destroyed with it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            root = null;
            voices = null;
            startedAt = null;
            sequence = 0;
            cursor = 0;
            subscribed = false;
            AudioListener.pause = false;
        }
    }
}
