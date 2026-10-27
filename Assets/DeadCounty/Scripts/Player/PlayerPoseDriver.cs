using System;
using DeadCounty.Animation;
using UnityEngine;

namespace DeadCounty.Player
{
    // Body language of the player on top of the procedural gait: bent over and digging while searching, a hand
    // reaching out when opening a door or picking something up, out of breath once stamina is spent, a recoil
    // away from where a hit came from, and a limp below a quarter of health.
    [RequireComponent(typeof(ThirdPersonController), typeof(PlayerInteractor), typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerStamina), typeof(ProceduralHumanoidAnimator))]
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
        float flinchPitch, flinchRoll;

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            interactor = GetComponent<PlayerInteractor>();
            health = GetComponent<PlayerHealth>();
            stamina = GetComponent<PlayerStamina>();
            animator = GetComponent<ProceduralHumanoidAnimator>();
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
            else if (Time.time - interactor.LastUseTime < ReachTime)
            {
                pose.ArmsForward = 0.5f;
            }
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
