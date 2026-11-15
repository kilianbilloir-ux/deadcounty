using DeadCounty.Animation;
using DeadCounty.Combat;
using UnityEngine;

namespace DeadCounty.Player
{
    // Body language on top of the procedural gait.
    [RequireComponent(typeof(ThirdPersonController), typeof(PlayerInteractor), typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerStamina), typeof(ProceduralHumanoidAnimator), typeof(PlayerLoadout))]
    [RequireComponent(typeof(PlayerCombat))]
    public class PlayerPoseDriver : MonoBehaviour
    {
        const float ReachTime = 0.45f;
        const float FlinchAngle = 16f;
        const float FlinchRecoveryTime = 0.35f;
        const float LimpBelow = 0.25f;

        ThirdPersonController controller;
        PlayerInteractor interactor;
        PlayerHealth health;
        PlayerStamina stamina;
        ProceduralHumanoidAnimator animator;
        PlayerLoadout loadout;
        PlayerCombat combat;
        float flinchPitch, flinchRoll;

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            interactor = GetComponent<PlayerInteractor>();
            health = GetComponent<PlayerHealth>();
            stamina = GetComponent<PlayerStamina>();
            animator = GetComponent<ProceduralHumanoidAnimator>();
            loadout = GetComponent<PlayerLoadout>();
            combat = GetComponent<PlayerCombat>();
        }

        void OnEnable() => health.Damaged += OnDamaged;

        void OnDisable() => health.Damaged -= OnDamaged;

        // Leans away from the source: back when hit from the front, sideways when hit from the side.
        void OnDamaged(PlayerHealth _)
        {
            var away = transform.position - health.LastHitSource;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            var local = transform.InverseTransformDirection(away.normalized);
            flinchPitch = local.z * FlinchAngle;
            flinchRoll = -local.x * FlinchAngle;
        }

        // The arm is cocked early in the wind-up and starts coming down before its end, so it crosses in front
        // of the body on the frame the blow lands, or reaches the ground for a body lying there; a shove peaks and
        // a stomp lands on that frame.
        static void Strike(ref PoseModifiers pose, bool windup, float progress, bool bareHands, bool ground)
        {
            pose.Reach = 0f;
            pose.HandToMouth = 0f;
            if (bareHands && ground)
            {
                pose.Stomp = 0.5f * (windup ? progress : 1f + progress);
                return;
            }
            if (bareHands)
            {
                pose.Shove = windup ? progress * progress : 1f - Mathf.SmoothStep(0f, 1f, progress / 0.6f);
                return;
            }
            pose.Chop = ground ? 1f : 0f;
            if (windup)
            {
                var down = Mathf.InverseLerp(0.7f, 1f, progress);
                pose.SwingWeight = Mathf.SmoothStep(0f, 1f, progress / 0.5f);
                pose.Swing = 0.5f * down * down;
            }
            else
            {
                pose.SwingWeight = 1f - Mathf.SmoothStep(0f, 1f, (progress - 0.5f) / 0.5f);
                pose.Swing = 0.5f + 0.5f * Mathf.SmoothStep(0f, 1f, progress / 0.25f);
            }
        }

        void Update()
        {
            var dt = Time.deltaTime;
            var recovery = FlinchAngle / FlinchRecoveryTime * dt;
            flinchPitch = Mathf.MoveTowards(flinchPitch, 0f, recovery);
            flinchRoll = Mathf.MoveTowards(flinchRoll, 0f, recovery);

            var pose = PoseModifiers.Neutral;
            if (interactor.Busy)
            {
                pose.ArmsForward = 0.45f;
                pose.Hunch = 22f;
                pose.Feed = 0.3f;
                pose.LookWeight = 0.2f;
                pose.Rummage = 1f;
            }
            else if (loadout.Using)
            {
                pose.HandToMouth = 1f;
            }
            else if (Time.time - interactor.LastUseTime < ReachTime)
            {
                pose.Reach = 1f;
            }
            var swing = combat.Swing;
            if (swing.Busy) Strike(ref pose, swing.Phase == SwingPhase.Windup, swing.Progress, combat.Weapon == null, combat.Ground);
            if (stamina.Model.Exhausted)
            {
                pose.Pant = 1f;
                pose.Hunch += 12f;
            }
            pose.FlinchPitch = flinchPitch;
            pose.FlinchRoll = flinchRoll;
            controller.Pose = pose;

            var limp = animator.Limp;
            var limping = health.IsAlive && health.Pool.Normalized < LimpBelow;
            if (limp.Enabled != limping)
            {
                limp.Enabled = limping;
                animator.Limp = limp;
            }
        }
    }
}
