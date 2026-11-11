using System;
using DeadCounty.Core;
using UnityEngine;

namespace DeadCounty.Player
{
    // Player hit points. A hit knocks the player back away from its source and staggers them, then grants a
    // short invulnerability so one swing cannot land several times.
    [RequireComponent(typeof(ThirdPersonController))]
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;

        [Header("Hit reaction")]
        [SerializeField] float invulnerabilityTime = 0.8f;
        [SerializeField] float knockbackDistance = 0.4f;
        [SerializeField] float knockbackDuration = 0.12f;
        [SerializeField] float staggerTime = 0.4f;
        [SerializeField] float staggerSpeedFactor = 0.4f;

        ThirdPersonController controller;
        float invulnerableUntil = float.NegativeInfinity;

        void OnValidate()
        {
            if (!float.IsFinite(maxHealth) || maxHealth <= 0f) maxHealth = 100f;
        }

        public HealthPool Pool { get; private set; }
        public bool IsAlive => !Pool.IsDead;
        public float LastHitTime { get; private set; } = float.NegativeInfinity;
        // Debug god mode: every hit is ignored.
        public bool Invulnerable { get; set; }

        public event Action<PlayerHealth> Damaged;
        public event Action<PlayerHealth> Died;

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            Pool = new HealthPool(maxHealth);
        }

        // Returns true when the damage was applied, false while dead or invulnerable.
        // !(damage > 0) rejects NaN, where <= would let it through.
        public bool ApplyHit(float damage, Vector3 sourcePosition)
        {
            if (!(damage > 0f) || !IsAlive || Invulnerable || Time.time < invulnerableUntil) return false;
            var killed = Pool.Apply(damage);
            LastHitTime = Time.time;
            invulnerableUntil = Time.time + invulnerabilityTime;

            var away = transform.position - sourcePosition;
            away.y = 0f;
            // A source standing exactly on the player has no direction: push them backward.
            var direction = away.sqrMagnitude > 1e-4f ? away.normalized : -transform.forward;
            controller.Knockback(direction * knockbackDistance, knockbackDuration);
            controller.Stagger(staggerTime, staggerSpeedFactor);

            Damaged?.Invoke(this);
            if (killed) Died?.Invoke(this);
            return true;
        }

        public void Restore()
        {
            Pool.Reset();
            invulnerableUntil = float.NegativeInfinity;
        }
    }
}
