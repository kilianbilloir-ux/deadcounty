using UnityEngine;

namespace DeadCounty.AI
{
    // Something zombies can see, hear the position of and strike: the player today, decoys or survivors later.
    // Positions are in world space, Position at ground level.
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
