using System;
using DeadCounty.Core;
using UnityEngine;

namespace DeadCounty.Player
{
    // Player hit points. A hit knocks the player back away from its source and staggers them, then grants a
    // short invulnerability so one swing cannot land several times. A hit may also open a bleeding wound that
    // drains health until a bandage stops it (GDD 8.4).
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

        [Header("Bleeding")]
        [SerializeField, Range(0f, 1f)] float bleedChance = 0.25f;
        [Tooltip("A bleeding player who does nothing dies in about 3 min from full health.")]
        [SerializeField] float bleedDamagePerSecond = 0.5f;

        ThirdPersonController controller;
        float invulnerableUntil = float.NegativeInfinity;

        void OnValidate()
        {
            if (!float.IsFinite(maxHealth) || maxHealth <= 0f) maxHealth = 100f;
        }

        public HealthPool Pool { get; private set; }
        public bool IsAlive => !Pool.IsDead;
        public float LastHitTime { get; private set; } = float.NegativeInfinity;
        // Where the last hit came from, for the HUD to point at.
        public Vector3 LastHitSource { get; private set; }
        // Debug god mode: every hit is ignored.
        public bool Invulnerable { get; set; }
        public bool Bleeding { get; private set; }

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
            LastHitSource = sourcePosition;
            invulnerableUntil = Time.time + invulnerabilityTime;

            var away = transform.position - sourcePosition;
            away.y = 0f;
            // A source standing exactly on the player has no direction: push them backward.
            var direction = away.sqrMagnitude > 1e-4f ? away.normalized : -transform.forward;
            controller.Knockback(direction * knockbackDistance, knockbackDuration);
            controller.Stagger(staggerTime, staggerSpeedFactor);
            if (!killed && UnityEngine.Random.value < bleedChance) Bleeding = true;

            Damaged?.Invoke(this);
            if (killed) Died?.Invoke(this);
            return true;
        }

        // Damage from an empty need: no hit reaction and no invulnerability window; god mode ignores it too.
        public void ApplyDrain(float damage)
        {
            if (!(damage > 0f) || !IsAlive || Invulnerable) return;
            if (Pool.Apply(damage)) Died?.Invoke(this);
        }

        public void Heal(float amount) => Pool.Heal(amount);

        public void StopBleeding() => Bleeding = false;

        void Update()
        {
            if (Bleeding) ApplyDrain(bleedDamagePerSecond * Time.deltaTime);
        }

        public void Restore()
        {
            Pool.Reset();
            invulnerableUntil = float.NegativeInfinity;
            Bleeding = false;
        }
    }
}
