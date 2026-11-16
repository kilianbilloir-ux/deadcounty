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

        [Test]
        public void ABlowFellsAtItsChanceAndTwiceAsOftenFromBehind()
        {
            Assert.That(MeleeResolver.Fells(0.35f, false, 0.34f), Is.True);
            Assert.That(MeleeResolver.Fells(0.35f, false, 0.36f), Is.False);
            Assert.That(MeleeResolver.Fells(0.35f, true, 0.69f), Is.True);
            Assert.That(MeleeResolver.Fells(0.35f, true, 0.71f), Is.False);
            Assert.That(MeleeResolver.Fells(0.5f, true, 1f), Is.True, "a certain fall failed on the top roll");
            Assert.That(MeleeResolver.Fells(0f, true, 0f), Is.False, "a blow that never fells did");
        }

        [Test]
        public void TheAttackerIsBehindATargetFacingAway()
        {
            var target = new Vector3(0f, 0f, 1f);
            Assert.That(MeleeResolver.FromBehind(Vector3.zero, target, Vector3.forward), Is.True);
            Assert.That(MeleeResolver.FromBehind(Vector3.zero, target, Vector3.back), Is.False);
            Assert.That(MeleeResolver.FromBehind(Vector3.zero, target, new Vector3(1f, 0f, 0.2f)), Is.True);
            Assert.That(MeleeResolver.FromBehind(Vector3.zero, target, new Vector3(1f, 0f, -0.2f)), Is.False);
        }
    }
}
