using UnityEngine;

namespace DeadCounty.Core
{
    // Pure day/night math: game hour to darkness and sun pose. No scene state, EditMode tested.
    // GDD: demo starts at 11h. Phase duration = hours / 24 * dayLengthSeconds,
    // e.g. at 600 s: dawn (2 h) 50 s, day (11 h) 275 s, dusk (2 h) 50 s, night (9 h) 225 s.
    public static class DayNightModel
    {
        public const float DayLengthSeconds = 600f;
        public const float HoursPerDay = 24f;
        public const float DawnStart = 5f;
        public const float DayStart = 7f;
        public const float DuskStart = 18f;
        public const float NightStart = 20f;
        public const float StartHour = 11f;
        public const float MaxSunElevation = 65f;

        static readonly Color DayColor = new(1f, 0.88f, 0.72f);
        static readonly Color NightColor = new(0.35f, 0.5f, 0.8f);
        const float DayIntensity = 1.5f;
        const float NightIntensity = 0.12f;

        public static float WrapHour(float hour) => Mathf.Repeat(hour, HoursPerDay);

        public static float HourToNightFactor(float hour)
        {
            var h = WrapHour(hour);
            if (h < DawnStart || h >= NightStart) return 1f;
            if (h >= DayStart && h < DuskStart) return 0f;
            if (h < DayStart) return 1f - Mathf.SmoothStep(0f, 1f, (h - DawnStart) / (DayStart - DawnStart));
            return Mathf.SmoothStep(0f, 1f, (h - DuskStart) / (NightStart - DuskStart));
        }

        public static float Advance(float hour, float dt, float dayLengthSeconds = DayLengthSeconds) =>
            WrapHour(hour + dt / dayLengthSeconds * HoursPerDay);

        public static Quaternion SunRotation(float hour)
        {
            var turn = WrapHour(hour - 6f) / HoursPerDay;
            var elevation = Mathf.Sin(turn * Mathf.PI * 2f) * MaxSunElevation;
            var azimuth = 90f + turn * 360f;
            return Quaternion.Euler(elevation, azimuth, 0f);
        }

        public static Color SunColor(float nightFactor) =>
            Color.Lerp(DayColor, NightColor, Mathf.Clamp01(nightFactor));

        public static float SunIntensity(float nightFactor) =>
            Mathf.Lerp(DayIntensity, NightIntensity, Mathf.Clamp01(nightFactor));
    }
}
