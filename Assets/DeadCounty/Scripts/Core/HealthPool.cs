using System;
using UnityEngine;

namespace DeadCounty.Core
{
    // Hit points shared by the player and the zombies. Damage stops applying once dead.
    public class HealthPool
    {
        public HealthPool(float max)
        {
            if (!float.IsFinite(max) || max <= 0f) throw new ArgumentOutOfRangeException(nameof(max), max, "HealthPool needs a positive finite max");
            Max = max;
            Current = max;
        }

        public float Max { get; }
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;
        public float Normalized => Current / Max;

        // Returns true only on the hit that kills. !(damage > 0) rejects NaN, where <= would let it through.
        public bool Apply(float damage)
        {
            if (!(damage > 0f) || IsDead) return false;
            Current = Mathf.Max(Current - damage, 0f);
            return IsDead;
        }

        public void Reset() => Current = Max;
    }
}
