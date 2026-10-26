using DeadCounty.Core;
using NUnit.Framework;

namespace DeadCounty.Tests
{
    public class NightLampsTests
    {
        [TestCase(false, 0f, false)]
        [TestCase(false, 0.54f, false)]
        [TestCase(false, 0.55f, true)]
        [TestCase(false, 1f, true)]
        [TestCase(true, 1f, true)]
        [TestCase(true, 0.46f, true)]
        [TestCase(true, 0.45f, false)]
        [TestCase(true, 0f, false)]
        public void HysteresisHoldsBetweenThresholds(bool currentlyOn, float nightFactor, bool expected) =>
            Assert.That(NightLamps.ShouldBeOn(currentlyOn, nightFactor), Is.EqualTo(expected));
    }
}
