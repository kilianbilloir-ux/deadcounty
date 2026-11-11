using DeadCounty.AI;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    public class AttackResolverTests
    {
        static readonly AttackSettings Attack = AttackSettings.Default;

        [TestCase(-2f, 1.6f)]
        [TestCase(0f, 1.6f)]
        [TestCase(1.5f, 1.15f)]
        [TestCase(3.5f, 1f)]
        public void TriggerDistanceShrinksWithAwaySpeed(float awaySpeed, float expected)
            => Assert.That(AttackResolver.TriggerDistance(Attack, awaySpeed), Is.EqualTo(expected).Within(1e-5f));

        [TestCase(0f, 1.2f, true)]
        [TestCase(0f, 1.4f, false)]
        [TestCase(0.45f, 1f, true)]
        [TestCase(0.55f, 1f, false)]
        [TestCase(0f, -0.3f, false)]
        [TestCase(0.3f, 0.1f, true)]
        public void HitsInFrontWithinReachAndWidth(float x, float z, bool expected)
            => Assert.That(AttackResolver.Hits(Attack, Vector3.zero, Vector3.forward, new Vector3(x, 0.8f, z)), Is.EqualTo(expected));
    }
}
