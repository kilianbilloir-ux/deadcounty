using DeadCounty.AI;
using UnityEngine;

namespace DeadCounty.Tests
{
    // Scripted perception target: a 1.8 m tall body without collider whose hits are recorded instead of applied.
    // With InvulnerabilityTime set it accepts hits like PlayerHealth and dies on the fourth accepted one.
    sealed class StubTarget : IPerceivable
    {
        const float HeadHeight = 1.65f;
        const float ChestHeight = 1.17f;

        public Vector3 Position { get; set; }
        public Vector3 HeadPoint => Position + Vector3.up * HeadHeight;
        public Vector3 ChestPoint => Position + Vector3.up * ChestHeight;
        public Vector3 Velocity { get; set; }
        public float Speed => Velocity.magnitude;
        public bool IsCrouching { get; set; }
        public bool FlashlightOn { get; set; }
        public bool Visible { get; set; } = true;
        public bool IsAlive { get; set; } = true;
        public int HitCount { get; private set; }
        public float FirstHitTime { get; private set; } = float.NaN;
        public float InvulnerabilityTime { get; set; }
        public int AcceptedHits { get; private set; }
        public float DeathTime { get; private set; } = float.NaN;

        float invulnerableUntil = float.NegativeInfinity;

        public bool ApplyHit(float damage, Vector3 source)
        {
            if (HitCount == 0) FirstHitTime = Time.time;
            HitCount++;
            if (!IsAlive || Time.time < invulnerableUntil) return false;
            invulnerableUntil = Time.time + InvulnerabilityTime;
            if (++AcceptedHits == 4 && InvulnerabilityTime > 0f)
            {
                IsAlive = false;
                DeathTime = Time.time;
            }
            return true;
        }
    }
}
