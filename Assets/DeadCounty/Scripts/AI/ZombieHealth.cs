using System;
using DeadCounty.Core;
using UnityEngine;

namespace DeadCounty.AI
{
    // Zombie hit points: the hook for combat. Nothing damages zombies yet; ZombieAgent stops and leaves the
    // manager on Died, and ResetToSpawn restores it.
    [DisallowMultipleComponent]
    public class ZombieHealth : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;

        public HealthPool Pool { get; private set; }
        public bool IsAlive => !Pool.IsDead;
        public Vector3 LastHitSource { get; private set; }

        public event Action<ZombieHealth> Damaged;
        public event Action<ZombieHealth> Died;

        void Awake() => Pool = new HealthPool(maxHealth);

        // Returns true when the damage was applied, false once dead.
        // !(damage > 0) rejects NaN, where <= would let it through.
        public bool ApplyDamage(float damage, Vector3 source)
        {
            if (!(damage > 0f) || !IsAlive) return false;
            var killed = Pool.Apply(damage);
            LastHitSource = source;
            Damaged?.Invoke(this);
            if (killed) Died?.Invoke(this);
            return true;
        }

        public void Restore() => Pool.Reset();
    }
}
