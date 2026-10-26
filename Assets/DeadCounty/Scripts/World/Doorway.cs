using UnityEngine;

namespace DeadCounty.World
{
    // Ground plan of a door opening: HalfWidth on each side of Center along Along, crossed along Normal.
    public readonly struct Doorway
    {
        public readonly Vector3 Center;
        public readonly Vector3 Normal;
        public readonly Vector3 Along;
        public readonly float HalfWidth;

        public Doorway(Vector3 center, Vector3 normal, Vector3 along, float halfWidth)
        {
            Center = center;
            Normal = new Vector3(normal.x, 0f, normal.z).normalized;
            Along = new Vector3(along.x, 0f, along.z).normalized;
            HalfWidth = halfWidth;
        }

        // +1 on the Normal side, -1 on the other.
        public float Side(Vector3 point) => Vector3.Dot(point - Center, Normal) >= 0f ? 1f : -1f;

        // Whether the ground segment from → to passes through the opening.
        public bool Crosses(Vector3 from, Vector3 to)
        {
            var a = Vector3.Dot(from - Center, Normal);
            var b = Vector3.Dot(to - Center, Normal);
            if (a * b > 0f || a == b) return false;
            var crossing = from + (to - from) * (a / (a - b));
            return Mathf.Abs(Vector3.Dot(crossing - Center, Along)) <= HalfWidth;
        }
    }
}
