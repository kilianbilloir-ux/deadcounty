using System;
using DeadCounty.Core;
using DeadCounty.Player;
using DeadCounty.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DeadCounty.Audio
{
    // The county's soundscape around the listener: wind day and night, the insects taking over from the birds at
    // dusk, owls and dogs after dark, and now and then a far-off sign of the outbreak. Under a roof between walls the
    // outside is muffled and a room tone comes up. Presentation only: nothing here reaches NoiseSystem, the zombies
    // never hear it. A cue left empty is simply not heard. Paused with the rest of the world audio (Sfx).
    public class AmbienceDirector : MonoBehaviour
    {
        public enum Activity { Birds, Day, Night, MostlyNight, Always }

        [Serializable]
        public struct Call
        {
            public SoundCue cue;
            public Activity activity;
            [Tooltip("Mean seconds between two calls at full activity.")]
            public float interval;
            [Tooltip("Distance from the listener (m): the clips carry their own distance, keep it under the cue's minDistance.")]
            public Vector2 distance;
            [Tooltip("Height above the listener (m).")]
            public Vector2 height;
            [Tooltip("Seconds of play before the first one.")]
            public float quietStart;
        }

        sealed class Voice
        {
            public AudioSource Source;
            public AudioLowPassFilter Filter;
            public SoundCue Cue;
            // Cue and channel volume from Sfx.Configure, and the soundscape's share of it.
            public float Volume, Level;
        }

        const float ProbeInterval = 0.25f;
        const float HeadHeight = 1.6f;
        const float RoofReach = 12f;
        const float WallReach = 20f;
        const int WallsForRoom = 4;
        const float IndoorSeconds = 0.5f;
        // The world fades in as the title's drone dies away.
        const float FadeInSeconds = 1.5f;
        const float CallSpatialBlend = 0.8f;
        const int EmitterCount = 3;
        // Slow drift of the wind loop's level and pitch, so its 10 s never repeat the same way.
        const float GustRate = 0.05f;
        const float GustDepth = 0.3f;
        const float WindPitchDrift = 0.04f;

        static readonly Vector3[] WallDirections =
        {
            new(0f, 0f, 1f), new(0.866f, 0f, 0.5f), new(0.866f, 0f, -0.5f),
            new(0f, 0f, -1f), new(-0.866f, 0f, -0.5f), new(-0.866f, 0f, 0.5f),
        };

        [SerializeField] SoundCue wind;
        [SerializeField] SoundCue night;
        [SerializeField] SoundCue roomTone;
        [SerializeField] Call[] calls = Array.Empty<Call>();

        Voice windVoice, nightVoice, roomVoice;
        Voice[] beds, emitters;
        float[] callAt;
        Transform listener, ear;
        DayNightDirector clock;
        // indoor: 0 outside, 1 under a roof between walls, smoothed.
        float elapsed, probeAt, indoor, presence, windSeed;
        bool sheltered;

        void OnValidate()
        {
            for (var i = 0; i < calls.Length; i++) calls[i].interval = Mathf.Max(1f, calls[i].interval);
        }

        void Awake()
        {
            windVoice = CreateVoice("Vent", wind, true);
            nightVoice = CreateVoice("Nuit", night, true);
            roomVoice = CreateVoice("Piece", roomTone, false);
            beds = new[] { windVoice, nightVoice, roomVoice };
            emitters = new Voice[EmitterCount];
            for (var i = 0; i < EmitterCount; i++) emitters[i] = CreateVoice("Appel", null, true);
            callAt = new float[calls.Length];
            for (var i = 0; i < calls.Length; i++) callAt[i] = calls[i].quietStart + AmbienceMix.NextDelay(calls[i].interval, Random.value);
            windSeed = Random.value * 100f;
        }

        void Start()
        {
            var audioListener = FindAnyObjectByType<AudioListener>();
            if (audioListener == null) throw new InvalidOperationException($"AmbienceDirector '{name}' found no AudioListener in the scene");
            listener = audioListener.transform;
            var player = FindAnyObjectByType<ThirdPersonController>();
            ear = player != null ? player.transform : listener;
            clock = FindAnyObjectByType<DayNightDirector>();
            foreach (var bed in beds)
            {
                if (bed.Cue == null) continue;
                Configure(bed);
                bed.Source.clip = bed.Cue.Pick();
                bed.Source.loop = true;
                if (bed.Source.clip == null) continue;
                // Out of step with each other from the first second.
                bed.Source.time = Random.Range(0f, bed.Source.clip.length);
                bed.Source.Play();
            }
        }

        void OnEnable() => GameSettings.Changed += Reconfigure;

        void OnDisable() => GameSettings.Changed -= Reconfigure;

        Voice CreateVoice(string voiceName, SoundCue cue, bool muffled)
        {
            var holder = new GameObject(voiceName);
            holder.transform.SetParent(transform, false);
            var voice = new Voice { Source = holder.AddComponent<AudioSource>(), Cue = cue };
            voice.Source.playOnAwake = false;
            if (muffled) voice.Filter = holder.AddComponent<AudioLowPassFilter>();
            return voice;
        }

        static void Configure(Voice voice)
        {
            Sfx.Configure(voice.Source, voice.Cue);
            voice.Volume = voice.Source.volume;
            voice.Source.volume = voice.Volume * voice.Level;
        }

        void Reconfigure()
        {
            foreach (var bed in beds)
                if (bed.Cue != null) Configure(bed);
            foreach (var emitter in emitters)
            {
                if (emitter.Cue == null) continue;
                Configure(emitter);
                emitter.Source.spatialBlend = CallSpatialBlend;
            }
        }

        // Scaled time: the pause and the title stop the world, its calls included.
        void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            elapsed += dt;
            if (elapsed >= probeAt)
            {
                probeAt = elapsed + ProbeInterval;
                sheltered = Sheltered(ear.position + Vector3.up * HeadHeight);
            }
            indoor = AmbienceMix.Approach(indoor, sheltered ? 1f : 0f, dt, IndoorSeconds);
            presence = AmbienceMix.Approach(presence, 1f, dt, FadeInSeconds);

            var darkness = WorldLighting.NightFactor;
            var outdoor = AmbienceMix.OutdoorGain(indoor) * presence;
            var cutoff = AmbienceMix.Cutoff(indoor);
            var gust = Mathf.Clamp01(Mathf.PerlinNoise(elapsed * GustRate, windSeed));
            SetLevel(windVoice, AmbienceMix.WindLevel(darkness) * (1f - GustDepth * gust) * outdoor, cutoff);
            windVoice.Source.pitch = 1f + WindPitchDrift * (2f * Mathf.Clamp01(Mathf.PerlinNoise(windSeed, elapsed * GustRate)) - 1f);
            SetLevel(nightVoice, AmbienceMix.NightWeight(darkness) * outdoor, cutoff);
            SetLevel(roomVoice, indoor * presence, cutoff);
            foreach (var emitter in emitters)
                if (emitter.Source.isPlaying) SetLevel(emitter, outdoor, cutoff);
            ScheduleCalls(darkness);
        }

        static void SetLevel(Voice voice, float level, float cutoff)
        {
            voice.Level = level;
            voice.Source.volume = voice.Volume * level;
            if (voice.Filter != null) voice.Filter.cutoffFrequency = cutoff;
        }

        // A roof overhead and walls on most sides: a porch or the gas station canopy stays outside.
        static bool Sheltered(Vector3 head)
        {
            if (!Physics.Raycast(head, Vector3.up, RoofReach, GameLayers.EnvironmentMask, QueryTriggerInteraction.Ignore)) return false;
            var walls = 0;
            foreach (var direction in WallDirections)
                if (Physics.Raycast(head, direction, WallReach, GameLayers.EnvironmentMask, QueryTriggerInteraction.Ignore)) walls++;
            return walls >= WallsForRoom;
        }

        // Each call is due at random (Poisson) times; when due it sounds with a chance equal to its activity, and
        // above full activity (the morning chorus) it comes due more often. Out of its hours it waits, no backlog.
        void ScheduleCalls(float darkness)
        {
            var hour = clock != null ? clock.Hour : 12f;
            for (var i = 0; i < calls.Length; i++)
            {
                if (elapsed < callAt[i]) continue;
                var rate = Rate(calls[i].activity, darkness, hour);
                if (Random.value < rate) Play(calls[i]);
                callAt[i] = elapsed + AmbienceMix.NextDelay(calls[i].interval / Mathf.Max(rate, 1f), Random.value);
            }
        }

        static float Rate(Activity activity, float darkness, float hour) => activity switch
        {
            Activity.Birds => AmbienceMix.BirdRate(darkness, hour),
            Activity.Day => AmbienceMix.DayWeight(darkness),
            Activity.Night => AmbienceMix.NightWeight(darkness),
            Activity.MostlyNight => 0.3f + 0.7f * AmbienceMix.NightWeight(darkness),
            _ => 1f,
        };

        // On a free emitter around the listener; with all three busy the call is skipped.
        void Play(Call call)
        {
            if (call.cue == null) return;
            Voice emitter = null;
            foreach (var e in emitters)
                if (!e.Source.isPlaying)
                {
                    emitter = e;
                    break;
                }
            if (emitter == null) return;
            var angle = Random.value * 2f * Mathf.PI;
            var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(call.distance.x, call.distance.y);
            emitter.Source.transform.position = listener.position + offset + Vector3.up * Random.Range(call.height.x, call.height.y);
            emitter.Cue = call.cue;
            Configure(emitter);
            emitter.Source.spatialBlend = CallSpatialBlend;
            emitter.Source.clip = call.cue.Pick();
            emitter.Source.pitch = call.cue.RandomPitch();
            emitter.Source.loop = false;
            SetLevel(emitter, AmbienceMix.OutdoorGain(indoor) * presence, AmbienceMix.Cutoff(indoor));
            emitter.Source.Play();
        }
    }
}
