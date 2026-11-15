using DeadCounty.Combat;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    public class MeleeResolverTests
    {
        const float Radius = 0.3f;

        static WeaponStats Weapon(int maxTargets = 1) => new() { Reach = 1.5f, HalfAngle = 45f, MaxTargets = maxTargets };

        static int[] Select(in WeaponStats weapon, params Vector3[] targets)
        {
            var hits = new int[8];
            var count = MeleeResolver.Select(weapon, Vector3.zero, Vector3.forward, targets, Radius, hits);
            return hits[..count];
        }

        [Test]
        public void ATargetInFrontWithinReachIsHit()
        {
            Assert.That(Select(Weapon(), new Vector3(0f, 0f, 1.2f)), Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void ReachIsMeasuredToTheSurfaceOfTheTarget()
        {
            Assert.That(Select(Weapon(), new Vector3(0f, 0f, 1.5f + Radius - 0.01f)), Has.Length.EqualTo(1));
            Assert.That(Select(Weapon(), new Vector3(0f, 0f, 1.5f + Radius + 0.01f)), Is.Empty);
        }

        [Test]
        public void ATargetBehindOrBesideIsMissed()
        {
            Assert.That(Select(Weapon(), new Vector3(0f, 0f, -1f)), Is.Empty);
            Assert.That(Select(Weapon(), new Vector3(1.2f, 0f, 0f)), Is.Empty);
        }

        // Its centre is outside the 45 degree arc but its body reaches into it.
        [Test]
        public void ATargetStraddlingTheEdgeOfTheArcIsHit()
        {
            var edge = Quaternion.Euler(0f, 55f, 0f) * Vector3.forward;
            Assert.That(Select(Weapon(), edge), Has.Length.EqualTo(1), "1 m away, a 0.3 m body widens the arc by 17 degrees");
            Assert.That(Select(Weapon(), Quaternion.Euler(0f, 70f, 0f) * Vector3.forward), Is.Empty);
        }

        [Test]
        public void HeightDoesNotCount()
        {
            Assert.That(Select(Weapon(), new Vector3(0f, 3f, 1f)), Has.Length.EqualTo(1));
        }

        [Test]
        public void OnlyTheNearestTargetsAreHitNearestFirst()
        {
            var far = new Vector3(0.2f, 0f, 1.4f);
            var near = new Vector3(0f, 0f, 0.6f);
            var middle = new Vector3(-0.2f, 0f, 1f);
            Assert.That(Select(Weapon(2), far, near, middle), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(Select(Weapon(1), far, near, middle), Is.EqualTo(new[] { 1 }));
            Assert.That(Select(Weapon(3), far, near, middle), Is.EqualTo(new[] { 1, 2, 0 }));
        }

        [Test]
        public void StandingInsideTheTargetHitsItWhateverTheDirection()
        {
            Assert.That(Select(Weapon(), new Vector3(0f, 0f, -0.2f)), Has.Length.EqualTo(1));
        }
    }
}
