using DeadCounty.Core;
using UnityEngine;

namespace DeadCounty.Player
{
    // Holds the player's StaminaModel. ThirdPersonController ticks it every frame and gates gaits and jumps on it.
    public class PlayerStamina : MonoBehaviour
    {
        [SerializeField] StaminaSettings settings = StaminaSettings.Default;

        public StaminaModel Model { get; private set; }

        void Awake() => Model = new StaminaModel(settings);

        public void ResetFull() => Model.Reset();
    }
}
