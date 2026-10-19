using DeadCounty.Perception;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    public class PerceptionMathTests
    {
        static Vector3 Direction(float yawDeg, float length = 5f) => Quaternion.Euler(0f, yawDeg, 0f) * Vector3.forward * length;

        [TestCase(0f, false, false, 20f)]
        [TestCase(1f, false, false, 8f)]
        [TestCase(1f, true, false, 25f)]
        [TestCase(0f, false, true, 12f)]
        [TestCase(1f, false, true, 4.8f)]
        [TestCase(1f, true, true, 15f)]
        [TestCase(0.5f, false, false, 14f)]
        public void SightRange(float nightFactor, bool flashlightOn, bool crouching, float expected)
            => Assert.That(PerceptionMath.SightRange(20f, 8f, 25f, nightFactor, flashlightOn, crouching, 0.6f), Is.EqualTo(expected).Within(1e-4f));

        [TestCase(0f, true)]
        [TestCase(59f, true)]
        [TestCase(-59f, true)]
        [TestCase(61f, false)]
        [TestCase(-61f, false)]
        [TestCase(180f, false)]
        public void InCone(float yawDeg, bool expected) => Assert.That(PerceptionMath.InCone(Vector3.forward, Direction(yawDeg), 120f), Is.EqualTo(expected));

        [Test]
        public void InConeIgnoresHeightAndZeroLength()
        {
            Assert.That(PerceptionMath.InCone(Vector3.forward, new Vector3(0f, 10f, 1f), 120f));
            Assert.That(PerceptionMath.InCone(Vector3.forward, Vector3.zero, 120f));
        }

        [TestCase(0f, 1f)]
        [TestCase(39f, 1f)]
        [TestCase(41f, 0.5f)]
        [TestCase(59f, 0.5f)]
        [TestCase(61f, 0f)]
        public void ConeEdgeFactor(float yawDeg, float expected) => Assert.That(PerceptionMath.ConeEdgeFactor(Vector3.forward, Direction(yawDeg), 120f), Is.EqualTo(expected));

        [TestCase(0, 12f)]
        [TestCase(1, 6f)]
        [TestCase(2, 3f)]
        public void EffectiveHearingRadiusHalvesPerWall(int walls, float expected)
            => Assert.That(PerceptionMath.EffectiveHearingRadius(12f, 1f, walls), Is.EqualTo(expected).Within(1e-5f));

        [Test]
        public void SortThenCountWallsMergesCloseHits()
        {
            var distances = new[] { 7f, 3.1f, 3f, 99f };
            PerceptionMath.SortAscending(distances, 3);
            Assert.That(distances, Is.EqualTo(new[] { 3f, 3.1f, 7f, 99f }));
            Assert.That(PerceptionMath.CountWalls(distances, 3), Is.EqualTo(2));
        }

        [Test]
        public void CountWallsEdgeCases()
        {
            Assert.That(PerceptionMath.CountWalls(new float[4], 0), Is.Zero);
            Assert.That(PerceptionMath.CountWalls(new[] { 2f }, 1), Is.EqualTo(1));
            Assert.That(PerceptionMath.CountWalls(new[] { 1f, 2f, 3f }, 3), Is.EqualTo(3));
        }

        [TestCase(2f, 20f, 0.3f)]
        [TestCase(0.5f, 20f, 0.3f)]
        [TestCase(11f, 20f, 1.15f)]
        [TestCase(20f, 20f, 2f)]
        [TestCase(30f, 20f, 2f)]
        [TestCase(5f, 2f, 0.3f)]
        public void SuspicionFillTime(float distance, float range, float expected)
            => Assert.That(PerceptionMath.SuspicionFillTime(distance, range), Is.EqualTo(expected).Within(1e-5f));

        [TestCase(-1f, 0.6f)]
        [TestCase(0f, 0.6f)]
        [TestCase(0.5f, 0.735f)]
        [TestCase(1f, 0.87f)]
        [TestCase(1.5f, 1f)]
        [TestCase(2.5f, 1.175f)]
        [TestCase(3.5f, 1.35f)]
        [TestCase(4.75f, 1.475f)]
        [TestCase(6f, 1.6f)]
        [TestCase(10f, 1.6f)]
        public void SpeedSuspicionMultiplier(float speed, float expected)
            => Assert.That(PerceptionMath.SpeedSuspicionMultiplier(speed), Is.EqualTo(expected).Within(1e-5f));
    }
}
