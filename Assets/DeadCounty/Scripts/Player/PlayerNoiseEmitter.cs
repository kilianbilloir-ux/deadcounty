using DeadCounty.Perception;
using UnityEngine;

namespace DeadCounty.Player
{
    // Turns the controller's per-frame NoiseRadius into discrete NoiseSystem events: one footstep per interval
    // with the loudest radius reached during it (so a landing between two samples is not lost), plus panting
    // while exhausted. A dead or invisible (ghost) player makes no noise.
    [DefaultExecutionOrder(-80)]
    [RequireComponent(typeof(ThirdPersonController), typeof(PlayerStamina), typeof(PerceptionTarget))]
    public class PlayerNoiseEmitter : MonoBehaviour
    {
        [SerializeField] float stepInterval = 0.25f;
        [SerializeField] float pantingRadius = 2f;
        [SerializeField] float pantingInterval = 0.5f;

        ThirdPersonController controller;
        PlayerStamina stamina;
        PerceptionTarget target;
        int sourceId;
        float stepTimer, pantingTimer, loudest;

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            stamina = GetComponent<PlayerStamina>();
            target = GetComponent<PerceptionTarget>();
            // GetInstanceID and the EntityId to int cast are compile errors in Unity 6.6; NoiseEvent keeps an int id.
            sourceId = gameObject.GetEntityId().GetHashCode();
        }

        void Update()
        {
            if (!target.Visible || !target.IsAlive)
            {
                stepTimer = pantingTimer = loudest = 0f;
                return;
            }

            var dt = Time.deltaTime;
            loudest = Mathf.Max(loudest, controller.NoiseRadius);
            stepTimer += dt;
            if (stepTimer >= stepInterval)
            {
                // Repeat instead of subtracting so a frame hitch does not fire a burst of catch-up events.
                stepTimer = Mathf.Repeat(stepTimer, stepInterval);
                if (loudest > 0f) NoiseSystem.Emit(transform.position, loudest, NoiseKind.Footstep, sourceId);
                loudest = 0f;
            }

            if (!stamina.Model.Exhausted)
            {
                pantingTimer = 0f;
                return;
            }
            pantingTimer += dt;
            if (pantingTimer < pantingInterval) return;
            pantingTimer = Mathf.Repeat(pantingTimer, pantingInterval);
            NoiseSystem.Emit(transform.position, pantingRadius, NoiseKind.Panting, sourceId);
        }
    }
}
