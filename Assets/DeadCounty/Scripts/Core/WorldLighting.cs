using UnityEngine;

namespace DeadCounty.Core
{
    // Global darkness level (0 day, 1 night). Runtime: DayNightDirector is the only writer.
    // Tests and scene setup write it directly to force a time of day.
    public static class WorldLighting
    {
        static float nightFactor;

        public static float NightFactor
        {
            get => nightFactor;
            set => nightFactor = Mathf.Clamp01(value);
        }

        public static bool IsNight => NightFactor > 0.5f;

        // Domain reload is disabled on entering Play mode, so statics must be reset by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics() => nightFactor = 0f;
    }
}
