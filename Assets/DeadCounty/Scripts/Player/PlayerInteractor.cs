using DeadCounty.Core;
using DeadCounty.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    // Picks the interactable the player looks at within reach, for the HUD prompt, and uses it on Interact. Holding
    // Run or Sprint hurries the use. A timed use (searching) runs while the player keeps still and is cancelled by
    // moving, pressing Interact again, getting hit or leaving its reach.
    [RequireComponent(typeof(ThirdPersonController), typeof(PlayerHealth))]
    public class PlayerInteractor : MonoBehaviour
    {
        const float EyeHeight = 1.5f;
        const float MoveCancel = 0.1f;

        [SerializeField] float reach = 1.8f;
        [Tooltip("Widest angle between the camera heading and an interactable (deg).")]
        [SerializeField] float maxAngle = 60f;

        ThirdPersonController controller;
        PlayerHealth health;
        InputAction interact, move, run, sprint;
        Interactable usedTarget;
        float useTime, useElapsed, useStart;
        bool useHurried;

        public Interactable Current { get; private set; }
        public bool Busy => usedTarget != null;
        // When Interact last used something, timed or not; the body reaches out (PlayerPoseDriver).
        public float LastUseTime { get; private set; } = float.NegativeInfinity;
        // 0 to 1 through a timed use, 0 otherwise.
        public float UseProgress => usedTarget != null ? useElapsed / useTime : 0f;

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            health = GetComponent<PlayerHealth>();
            var actions = InputSystem.actions;
            interact = actions.FindAction("Player/Interact", true);
            move = actions.FindAction("Player/Move", true);
            run = actions.FindAction("Player/Run", true);
            sprint = actions.FindAction("Player/Sprint", true);
        }

        void Update()
        {
            if (usedTarget != null)
            {
                ContinueUse();
                return;
            }
            Current = health.IsAlive && controller.Controllable ? Pick() : null;
            if (Current == null || !interact.WasPressedThisFrame()) return;
            var hurried = run.IsPressed() || sprint.IsPressed();
            var time = Current.UseTime(hurried);
            LastUseTime = Time.time;
            if (time <= 0f)
            {
                Current.Interact(transform, hurried);
                return;
            }
            usedTarget = Current;
            useTime = time;
            useElapsed = 0f;
            useStart = Time.time;
            useHurried = hurried;
            usedTarget.BeginUse(hurried);
        }

        void ContinueUse()
        {
            var cancelled = !health.IsAlive || !controller.Controllable || health.LastHitTime >= useStart
                || move.ReadValue<Vector2>().sqrMagnitude > MoveCancel * MoveCancel
                || interact.WasPressedThisFrame() || PlanarDistance(usedTarget) > reach;
            if (cancelled)
            {
                usedTarget = null;
                return;
            }
            useElapsed += Time.deltaTime;
            if (useElapsed < useTime) return;
            var target = usedTarget;
            usedTarget = null;
            target.Interact(transform, useHurried);
        }

        float PlanarDistance(Interactable target)
        {
            var offset = target.PromptPoint - transform.position;
            offset.y = 0f;
            return offset.magnitude;
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
