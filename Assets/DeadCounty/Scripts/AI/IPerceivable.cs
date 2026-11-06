using UnityEngine;

namespace DeadCounty.AI
{
    // The player today, decoys or survivors later. World space, Position at ground level.
    public interface IPerceivable
    {
        Vector3 HeadPoint { get; }
        Vector3 ChestPoint { get; }
        Vector3 Position { get; }
        Vector3 Velocity { get; }
        float Speed { get; }
        bool IsCrouching { get; }
        bool FlashlightOn { get; }
        // False in ghost mode: zombies neither see nor feel it.
        bool Visible { get; }
        bool IsAlive { get; }
        // Returns true when the damage was applied, false while dead or invulnerable.
        bool ApplyHit(float damage, Vector3 source);
    }
}
