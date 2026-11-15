using System;
using DeadCounty.Core;
using UnityEngine;

namespace DeadCounty.AI
{
    // ZombieAgent reels on Damaged, falls on Died and restores the pool in ResetToSpawn.
    [DisallowMultipleComponent]
    public class ZombieHealth : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;

        public HealthPool Pool { get; private set; }
        public bool IsAlive => !Pool.IsDead;
        public Vector3 LastHitSource { get; private set; }
        // Seconds of stagger and metres of knockback the last blow carried.
        public float LastStagger { get; private set; }
        public float LastKnockback { get; private set; }

        public event Action<ZombieHealth> Damaged;
        public event Action<ZombieHealth> Died;

        void Awake() => Pool = new HealthPool(maxHealth);

        // Returns true when the damage was applied, false once dead.
        // !(damage > 0) rejects NaN, where <= would let it through.
        public bool ApplyDamage(float damage, Vector3 source, float stagger = 0f, float knockback = 0f)
        {
            if (!(damage > 0f) || !IsAlive) return false;
            var killed = Pool.Apply(damage);
            LastHitSource = source;
            LastStagger = stagger;
            LastKnockback = knockback;
            Damaged?.Invoke(this);
            if (killed) Died?.Invoke(this);
            return true;
        }

        public void Restore() => Pool.Reset();
    }
}
