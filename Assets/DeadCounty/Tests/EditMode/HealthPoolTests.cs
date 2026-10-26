using DeadCounty.Core;
using NUnit.Framework;

namespace DeadCounty.Tests
{
    public class HealthPoolTests
    {
        [Test]
        public void FourHitsOfTwentyFiveKillOnTheFourth()
        {
            var health = new HealthPool(100f);
            for (var i = 0; i < 3; i++) Assert.That(health.Apply(25f), Is.False);
            Assert.That(health.IsDead, Is.False);
            Assert.That(health.Normalized, Is.EqualTo(0.25f));
            Assert.That(health.Apply(25f));
            Assert.That(health.IsDead);
            Assert.That(health.Current, Is.Zero);
        }

        [Test]
        public void NoDamageAfterDeath()
        {
            var health = new HealthPool(100f);
            Assert.That(health.Apply(150f));
            Assert.That(health.Current, Is.Zero);
            Assert.That(health.Apply(25f), Is.False);
            Assert.That(health.Current, Is.Zero);
        }

        [Test]
        public void NonPositiveDamageIsIgnored()
        {
            var health = new HealthPool(100f);
            Assert.That(health.Apply(0f), Is.False);
            Assert.That(health.Apply(-10f), Is.False);
            Assert.That(health.Current, Is.EqualTo(100f));
        }

        [Test]
        public void ConstructorRejectsNonPositiveOrNonFiniteMax()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new HealthPool(0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new HealthPool(-5f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new HealthPool(float.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new HealthPool(float.PositiveInfinity));
        }

        [Test]
        public void NaNDamageIsIgnored()
        {
            var health = new HealthPool(100f);
            Assert.That(health.Apply(float.NaN), Is.False);
            Assert.That(health.Current, Is.EqualTo(100f));
            Assert.That(health.IsDead, Is.False);
            Assert.That(float.IsFinite(health.Normalized), Is.True);
        }

        [Test]
        public void ResetRestoresFullHealth()
        {
            var health = new HealthPool(100f);
            health.Apply(100f);
            health.Reset();
            Assert.That(health.IsDead, Is.False);
            Assert.That(health.Current, Is.EqualTo(health.Max));
        }
    }
}
