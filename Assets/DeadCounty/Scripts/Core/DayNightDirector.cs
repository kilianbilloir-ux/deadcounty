using System;
using UnityEngine;

namespace DeadCounty.Core
{
    // Advances the game clock and drives the sun; the only writer of WorldLighting.NightFactor.
    // Runs first so perception reads a fresh factor. Zero allocation per frame, no scene lookup.
    [DefaultExecutionOrder(-110)]
    public class DayNightDirector : MonoBehaviour
    {
        [SerializeField] Light sun;
        [SerializeField] float startHour = DayNightModel.StartHour;
        [SerializeField] float dayLengthSeconds = DayNightModel.DayLengthSeconds;

        float hour;
        bool awake;

        // Clock shown by the HUD; gameplay reads WorldLighting.NightFactor.
        public float Hour => hour;
        // Starts at 1 and counts every midnight.
        public int Day { get; private set; } = 1;

        // Code-built setup: call on the inactive GameObject before its first activation.
        // Editor-built scenes leave the serialized fields and fall back to RenderSettings.sun.
        public void Configure(Light sunLight, float hour, float dayLength)
        {
            if (awake) throw new InvalidOperationException($"{name}: Configure must run before first activation");
            if (sunLight == null) throw new ArgumentNullException(nameof(sunLight));
            if (float.IsNaN(hour)) throw new ArgumentException("Hour must be a number", nameof(hour));
            if (dayLength <= 0f) throw new ArgumentOutOfRangeException(nameof(dayLength), dayLength, "Day length must be positive");
            sun = sunLight;
            startHour = hour;
            dayLengthSeconds = dayLength;
        }

        void OnValidate()
        {
            if (float.IsNaN(startHour)) startHour = DayNightModel.StartHour;
            dayLengthSeconds = Mathf.Max(1f, dayLengthSeconds);
        }

        void Awake()
        {
            awake = true;
            if (sun == null) sun = RenderSettings.sun;
            if (sun == null)
            {
                Debug.LogError($"{name} in {gameObject.scene.name}: no sun assigned and RenderSettings.sun is null — DayNightDirector disabled.", this);
                enabled = false;
                return;
            }
            if (!float.IsFinite(dayLengthSeconds) || dayLengthSeconds < 1f)
            {
                Debug.LogError($"{name}: invalid serialized dayLengthSeconds={dayLengthSeconds} — falling back to {DayNightModel.DayLengthSeconds} s.", this);
                dayLengthSeconds = DayNightModel.DayLengthSeconds;
            }
            if (!float.IsFinite(startHour))
            {
                Debug.LogError($"{name}: invalid serialized startHour={startHour} — falling back to {DayNightModel.StartHour}h.", this);
                startHour = DayNightModel.StartHour;
            }
            hour = DayNightModel.WrapHour(startHour);
            Apply();
        }

        // Jumps the clock without counting a day (debug, saves later).
        public void SetHour(float value)
        {
            if (!float.IsFinite(value)) throw new ArgumentException("Hour must be a number", nameof(value));
            hour = DayNightModel.WrapHour(value);
            Apply();
        }

        void Update()
        {
            // Scaled time: pause freezes the sky, like the zombie manager freezes the horde.
            if (Time.deltaTime == 0f) return;
            var next = DayNightModel.Advance(hour, Time.deltaTime, dayLengthSeconds);
            if (next < hour) Day++;
            hour = next;
            Apply();
        }

        void Apply()
        {
            var night = DayNightModel.HourToNightFactor(hour);
            WorldLighting.NightFactor = night;
            sun.transform.rotation = DayNightModel.SunRotation(hour);
            sun.color = DayNightModel.SunColor(night);
            sun.intensity = DayNightModel.SunIntensity(night);
            // Below the horizon the same light reads as moonlight; shadows stay on (never toggled at runtime).
        }
    }
}
