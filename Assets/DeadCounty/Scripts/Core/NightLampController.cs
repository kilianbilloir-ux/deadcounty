using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadCounty.Core
{
    // Switches the built night lamps with NightFactor, only on hysteresis fronts.
    [DefaultExecutionOrder(-105)]
    public class NightLampController : MonoBehaviour
    {
        [SerializeField] List<Light> lamps = new();
        bool awake;
        bool lightsOn;

        // Code-built setup: call on the inactive GameObject before its first activation.
        public void Configure(List<Light> builtLamps)
        {
            if (awake) throw new InvalidOperationException($"{name}: Configure must run before first activation");
            lamps = new List<Light>(builtLamps ?? throw new ArgumentNullException(nameof(builtLamps)));
        }

        void Awake()
        {
            awake = true;
            lightsOn = NightLamps.ShouldBeOn(false, WorldLighting.NightFactor);
            Apply();
        }

        void Update()
        {
            var want = NightLamps.ShouldBeOn(lightsOn, WorldLighting.NightFactor);
            if (want == lightsOn) return;
            lightsOn = want;
            Apply();
        }

        void Apply()
        {
            for (var i = 0; i < lamps.Count; i++)
                if (lamps[i] != null) lamps[i].enabled = lightsOn;
        }
    }
}
