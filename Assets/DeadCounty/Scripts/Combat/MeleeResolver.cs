using System;
using UnityEngine;

namespace DeadCounty.Combat
{
    // Who a swing reaches. All tests are planar.
    public static class MeleeResolver
    {
        // A target struck in the back is caught off balance.
        public const float BehindKnockdownFactor = 2f;

        // Whether a blow fells its target, from a uniform roll in [0, 1].
        public static bool Fells(float knockdown, bool fromBehind, float roll)
        {
            var odds = fromBehind ? knockdown * BehindKnockdownFactor : knockdown;
            return odds >= 1f || roll < odds;
        }

        // The attacker is behind the target when the target faces away from it.
        public static bool FromBehind(Vector3 attacker, Vector3 target, Vector3 targetForward)
        {
            var offset = target - attacker;
            offset.y = 0f;
            targetForward.y = 0f;
            return Vector3.Dot(offset, targetForward) > 0f;
        }

        // Writes into `hits` the indices of the targets inside the swing, nearest first, at most MaxTargets of
        // them, and returns how many. A target counts as a disc of `targetRadius`.
        public static int Select(in WeaponStats weapon, Vector3 origin, Vector3 direction, ReadOnlySpan<Vector3> targets, float targetRadius, Span<int> hits)
        {
            direction.y = 0f;
            direction.Normalize();
            var limit = Mathf.Min(Mathf.Max(weapon.MaxTargets, 1), hits.Length);
            Span<float> distances = stackalloc float[limit];
            var count = 0;
            for (var i = 0; i < targets.Length; i++)
            {
                var offset = targets[i] - origin;
                offset.y = 0f;
                var distance = offset.magnitude;
                if (distance - targetRadius > weapon.Reach) continue;
                // Standing inside the disc, every direction touches it.
                if (distance > targetRadius)
                {
                    var widened = weapon.HalfAngle + Mathf.Asin(targetRadius / distance) * Mathf.Rad2Deg;
                    if (Vector3.Angle(direction, offset) > widened) continue;
                }
                // Only the nearest `limit` are kept.
                int slot;
                if (count < limit) slot = count++;
                else if (distance < distances[limit - 1]) slot = limit - 1;
                else continue;
                while (slot > 0 && distances[slot - 1] > distance)
                {
                    distances[slot] = distances[slot - 1];
                    hits[slot] = hits[slot - 1];
                    slot--;
                }
                distances[slot] = distance;
                hits[slot] = i;
            }
            return count;
        }
    }
}
