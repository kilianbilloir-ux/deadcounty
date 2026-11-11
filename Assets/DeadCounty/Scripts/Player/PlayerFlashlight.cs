using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    // Player flashlight: toggles the headlamp; zombies read it through PerceptionTarget.FlashlightOn.
    // A dumb switch on purpose: being seen from 25 m at night is the whole risk/reward.
    public class PlayerFlashlight : MonoBehaviour
    {
        [SerializeField] Light spot;
        [SerializeField] bool startOn;

        InputAction toggle;

        public bool IsOn { get; private set; }

        // Code-built setup: call on the inactive GameObject before its first activation.
        public void Configure(Light spotLight, bool on)
        {
            spot = spotLight;
            startOn = on;
        }

        void Awake()
        {
            toggle = InputSystem.actions.FindAction("Player/Flashlight", true);
            SetOn(startOn);
        }

        void Update()
        {
            if (toggle.WasPressedThisFrame()) SetOn(!IsOn);
        }

        public void SetOn(bool on)
        {
            IsOn = on;
            if (spot != null) spot.enabled = on;
        }
    }
}
