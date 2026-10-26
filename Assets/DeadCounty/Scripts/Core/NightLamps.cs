using System.Collections.Generic;
using UnityEngine;

namespace DeadCounty.Core
{
    // Global on/off switch for the built night lamps (street, canopy, dock, floodlights).
    // Filled at runtime from NightLampController's serialized list; no per-lamp behaviour.
    public static class NightLamps
    {
        public const float OnThreshold = 0.55f;
        public const float OffThreshold = 0.45f;

        static readonly List<Light> lamps = new();

        public static int Count => lamps.Count;

        public static void Register(Light lamp)
        {
            if (lamp == null || lamps.Contains(lamp)) return;
            lamps.Add(lamp);
        }

        public static void Unregister(Light lamp) => lamps.Remove(lamp);

        // Pure hysteresis: no scene state, EditMode tested.
        public static bool ShouldBeOn(bool currentlyOn, float nightFactor) =>
            currentlyOn ? nightFactor > OffThreshold : nightFactor >= OnThreshold;

        internal static void ApplyAll(bool on)
        {
            for (var i = 0; i < lamps.Count; i++)
                if (lamps[i] != null) lamps[i].enabled = on;
        }

        // Domain reload is disabled on entering Play mode, so registrations must be dropped by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics() => lamps.Clear();
    }
}
