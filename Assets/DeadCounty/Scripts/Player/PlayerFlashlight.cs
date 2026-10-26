using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    // Player flashlight: toggles the headlamp and the target's FlashlightOn flag the zombies read.
    // A dumb switch on purpose: being seen from 25 m at night is the whole risk/reward.
    [RequireComponent(typeof(PerceptionTarget))]
    public class PlayerFlashlight : MonoBehaviour
    {
        [SerializeField] Light spot;
        [SerializeField] bool startOn;

        PerceptionTarget target;
        InputAction flashlight;

        public bool IsOn => target != null && target.FlashlightOn;

        // Code-built setup: call on the inactive GameObject before its first activation.
        public void Configure(Light spotLight, bool on)
        {
            spot = spotLight;
            startOn = on;
        }

        void Awake()
        {
            target = GetComponent<PerceptionTarget>();
            flashlight = InputSystem.actions.FindAction("Player/Flashlight", true);
            SetOn(startOn);
        }

        void Update()
        {
            if (flashlight.WasPressedThisFrame()) SetOn(!target.FlashlightOn);
        }

        public void SetOn(bool on)
        {
            target.FlashlightOn = on;
            if (spot != null) spot.enabled = on;
        }
    }
}
