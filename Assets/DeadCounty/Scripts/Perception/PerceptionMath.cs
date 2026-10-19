using UnityEngine;

namespace DeadCounty.Perception
{
    // Pure sight and hearing rules from the GDD, kept free of scene queries so they can be unit tested.
    public static class PerceptionMath
    {
        const float NearDistance = 2f;
        const float NearFillTime = 0.3f;
        const float FarFillTime = 2f;

        static readonly float[] SpeedKeys = { 0f, 1f, 1.5f, 3.5f, 6f };
        static readonly float[] SpeedMultipliers = { 0.6f, 0.87f, 1f, 1.35f, 1.6f };

        // GDD: 20 m by day, 8 m at night, 25 m on a flashlight at night, x0.6 on a crouching target.
        public static float SightRange(float dayRange, float nightRange, float flashlightRange, float nightFactor, bool flashlightOn, bool crouching, float crouchFactor)
            => Mathf.Lerp(dayRange, flashlightOn ? flashlightRange : nightRange, nightFactor) * (crouching ? crouchFactor : 1f);

        public static bool InCone(Vector3 forward, Vector3 toTarget, float fullAngleDeg) => PlanarAngle(forward, toTarget) <= fullAngleDeg * 0.5f;

        // Peripheral vision: full weight in the inner two thirds of the half-angle, half weight at the edge.
        public static float ConeEdgeFactor(Vector3 forward, Vector3 toTarget, float fullAngleDeg)
        {
            var angle = PlanarAngle(forward, toTarget);
            var halfAngle = fullAngleDeg * 0.5f;
            return angle <= halfAngle * (2f / 3f) ? 1f : angle <= halfAngle ? 0.5f : 0f;
        }

        // Each wall between the noise and the listener halves the distance it carries.
        public static float EffectiveHearingRadius(float radius, float multiplier, int walls) => radius * multiplier * Mathf.Pow(0.5f, walls);

        // Raycast hit order is undefined, so hit distances are sorted before CountWalls clusters them.
        public static void SortAscending(float[] values, int count)
        {
            for (var i = 1; i < count; i++)
            {
                var value = values[i];
                var j = i - 1;
                for (; j >= 0 && values[j] > value; j--) values[j + 1] = values[j];
                values[j + 1] = value;
            }
        }

        // Wall pieces that overlap at corners or doubled faces return hits a few centimetres apart: one wall.
        public static int CountWalls(float[] sortedDistances, int count, float mergeGap = 0.35f)
        {
            if (count <= 0) return 0;
            var walls = 1;
            for (var i = 1; i < count; i++)
                if (sortedDistances[i] - sortedDistances[i - 1] > mergeGap) walls++;
            return walls;
        }

        // Seconds of continuous sight before a zombie is sure: near-instant up close, 2 s at the edge of its range.
        public static float SuspicionFillTime(float distance, float range)
        {
            if (range <= NearDistance) return NearFillTime;
            return NearFillTime + (FarFillTime - NearFillTime) * Mathf.Clamp01((distance - NearDistance) / (range - NearDistance));
        }

        // Moving targets catch the eye: suspicion fills slower on a still target and faster on a sprinter.
        public static float SpeedSuspicionMultiplier(float speed)
        {
            if (speed <= SpeedKeys[0]) return SpeedMultipliers[0];
            for (var i = 1; i < SpeedKeys.Length; i++)
                if (speed <= SpeedKeys[i])
                    return Mathf.Lerp(SpeedMultipliers[i - 1], SpeedMultipliers[i], Mathf.InverseLerp(SpeedKeys[i - 1], SpeedKeys[i], speed));
            return SpeedMultipliers[SpeedMultipliers.Length - 1];
        }

        static float PlanarAngle(Vector3 forward, Vector3 toTarget)
        {
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 1e-8f) return 0f;
            forward.y = 0f;
            return Vector3.Angle(forward, toTarget);
        }
    }
}
