using DeadCounty.Core;
using NUnit.Framework;

namespace DeadCounty.Tests
{
    public class WorldLightingTests
    {
        [TearDown]
        public void TearDown() => WorldLighting.ResetStatics();

        [Test]
        public void NightFactorIsClampedAndResets()
        {
            WorldLighting.NightFactor = 2f;
            Assert.That(WorldLighting.NightFactor, Is.EqualTo(1f));
            Assert.That(WorldLighting.IsNight);
            WorldLighting.NightFactor = 0.5f;
            Assert.That(WorldLighting.IsNight, Is.False);
            WorldLighting.ResetStatics();
            Assert.That(WorldLighting.NightFactor, Is.Zero);
        }
    }
}
