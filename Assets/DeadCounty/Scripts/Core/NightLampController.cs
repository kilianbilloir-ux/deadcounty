using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadCounty.Core
{
    // Polls WorldLighting once per frame and flips the registry only on hysteresis fronts.
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
            foreach (var lamp in lamps) NightLamps.Register(lamp);
            lightsOn = NightLamps.ShouldBeOn(false, WorldLighting.NightFactor);
            NightLamps.ApplyAll(lightsOn);
        }

        void OnEnable()
        {
            foreach (var lamp in lamps) NightLamps.Register(lamp);
        }

        void OnDisable()
        {
            foreach (var lamp in lamps) NightLamps.Unregister(lamp);
        }

        void Update()
        {
            var want = NightLamps.ShouldBeOn(lightsOn, WorldLighting.NightFactor);
            if (want == lightsOn) return;
            lightsOn = want;
            NightLamps.ApplyAll(lightsOn);
        }
    }
}
