using UnityEngine;

namespace DeadCounty.AI
{
    // Geometry of the zombie attack: when to start it and whether the strike connects. All tests are planar.
    public static class AttackResolver
    {
        public static float TriggerDistance(in AttackSettings s, float targetAwaySpeed)
            => Mathf.Clamp(s.TriggerBase - s.TriggerPerAwaySpeed * targetAwaySpeed, s.TriggerMin, s.TriggerBase);

        // The strike lands on a target in front of the attacker, within HitReach and HitHalfWidth of the strike line.
        public static bool Hits(in AttackSettings s, Vector3 attackerPosition, Vector3 strikeDirection, Vector3 targetPosition)
        {
            var toTarget = targetPosition - attackerPosition;
            toTarget.y = 0f;
            strikeDirection.y = 0f;
            strikeDirection.Normalize();
            var along = Vector3.Dot(toTarget, strikeDirection);
            if (along < 0f || toTarget.sqrMagnitude > s.HitReach * s.HitReach) return false;
            return (toTarget - strikeDirection * along).sqrMagnitude <= s.HitHalfWidth * s.HitHalfWidth;
        }
    }
}
