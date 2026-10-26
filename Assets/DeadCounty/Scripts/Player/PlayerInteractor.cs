using DeadCounty.Core;
using DeadCounty.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    // Picks the interactable the player looks at within reach, for the HUD prompt, and uses it on Interact.
    [RequireComponent(typeof(ThirdPersonController), typeof(PlayerHealth))]
    public class PlayerInteractor : MonoBehaviour
    {
        const float EyeHeight = 1.5f;

        [SerializeField] float reach = 1.8f;
        [Tooltip("Widest angle between the camera heading and an interactable (deg).")]
        [SerializeField] float maxAngle = 60f;

        ThirdPersonController controller;
        PlayerHealth health;
        InputAction interact;

        public Interactable Current { get; private set; }

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            health = GetComponent<PlayerHealth>();
            interact = InputSystem.actions.FindAction("Player/Interact", true);
        }

        void Update()
        {
            Current = health.IsAlive && controller.Controllable ? Pick() : null;
            if (Current != null && interact.WasPressedThisFrame()) Current.Interact(transform, controller.IsSprinting);
        }

        // The candidate closest to the camera heading, weighted by distance, with nothing in between.
        Interactable Pick()
        {
            var eye = transform.position + Vector3.up * EyeHeight;
            var heading = Quaternion.Euler(0f, controller.AimYaw, 0f) * Vector3.forward;
            Interactable best = null;
            var bestScore = float.PositiveInfinity;
            var all = Interactable.All;
            for (var i = 0; i < all.Count; i++)
            {
                var candidate = all[i];
                var offset = candidate.PromptPoint - transform.position;
                offset.y = 0f;
                var distance = offset.magnitude;
                if (distance > reach) continue;
                var angle = distance > 1e-3f ? Vector3.Angle(heading, offset) : 0f;
                if (angle > maxAngle) continue;
                var score = angle / maxAngle + distance / reach;
                if (score >= bestScore || !InSight(eye, candidate)) continue;
                best = candidate;
                bestScore = score;
            }
            return best;
        }

        static bool InSight(Vector3 eye, Interactable candidate)
        {
            if (!Physics.Linecast(eye, candidate.PromptPoint, out var hit, GameLayers.EnvironmentMask, QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.GetComponentInParent<Interactable>() == candidate;
        }
    }
}
