using System;
using DeadCounty.Core;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    public class DayNightModelTests
    {
        [Test]
        public void NoonIsFullDay() =>
            Assert.That(DayNightModel.HourToNightFactor(12f), Is.EqualTo(0f));

        [Test]
        public void MidnightIsFullNight() =>
            Assert.That(DayNightModel.HourToNightFactor(0f), Is.EqualTo(1f));

        [TestCase(6f)]
        [TestCase(19f)]
        public void DawnAndDuskAreHalfway(float hour) =>
            Assert.That(DayNightModel.HourToNightFactor(hour), Is.EqualTo(0.5f).Within(1e-4f));

        // Integer hours hit the early-return literals exactly on every platform (no transcendental math).
        [TestCase(5f, 1f)]
        [TestCase(7f, 0f)]
        [TestCase(18f, 0f)]
        [TestCase(20f, 1f)]
        public void BoundariesAreExact(float hour, float expected) =>
            Assert.That(DayNightModel.HourToNightFactor(hour), Is.EqualTo(expected));

        [Test]
        public void DawnRampIsCenteredOnSix()
        {
            Assert.That(DayNightModel.HourToNightFactor(5.999f), Is.EqualTo(0.5f).Within(2e-3f));
            Assert.That(DayNightModel.HourToNightFactor(6.001f), Is.EqualTo(0.5f).Within(2e-3f));
            Assert.That(Mathf.Abs(DayNightModel.HourToNightFactor(5.999f) - DayNightModel.HourToNightFactor(6.001f)), Is.LessThan(2e-3f));
        }

        [Test]
        public void SunIsLevelAtDuskStart() =>
            Assert.That((DayNightModel.SunRotation(18f) * Vector3.forward).y, Is.EqualTo(0f).Within(1e-4f));

        [TestCase(4.999f, 5.001f)]
        [TestCase(6.999f, 7.001f)]
        [TestCase(17.999f, 18.001f)]
        [TestCase(19.999f, 20.001f)]
        public void TransitionsAreContinuous(float before, float after) =>
            Assert.That(
                Mathf.Abs(DayNightModel.HourToNightFactor(before) - DayNightModel.HourToNightFactor(after)),
                Is.LessThan(1e-3f),
                $"jump between {before}h and {after}h");

        [Test]
        public void AzimuthWrapsWithoutJump() =>
            Assert.That(
                Quaternion.Angle(DayNightModel.SunRotation(23.9f), DayNightModel.SunRotation(0.1f)),
                Is.LessThan(5f));

        [Test]
        public void ConfigureRejectsBadSetup()
        {
            var rig = new GameObject("TestConfigureGuards");
            rig.SetActive(false);
            try
            {
                var sun = rig.AddComponent<Light>();
                var director = rig.AddComponent<DayNightDirector>();
                Assert.That(() => director.Configure(sun, 11f, 0f), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => director.Configure(sun, 11f, -5f), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => director.Configure(null, 11f, 600f), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => director.Configure(sun, float.NaN, 600f), Throws.TypeOf<ArgumentException>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rig);
            }
        }

        [Test]
        public void WrapIsContinuous()
        {
            Assert.That(DayNightModel.HourToNightFactor(24f), Is.EqualTo(DayNightModel.HourToNightFactor(0f)));
            Assert.That(DayNightModel.HourToNightFactor(25.5f), Is.EqualTo(DayNightModel.HourToNightFactor(1.5f)));
            Assert.That(DayNightModel.HourToNightFactor(-1f), Is.EqualTo(DayNightModel.HourToNightFactor(23f)));
        }

        [Test]
        public void FactorNeverLeavesRange()
        {
            for (var h = -12f; h <= 36f; h += 0.25f)
                Assert.That(DayNightModel.HourToNightFactor(h), Is.InRange(0f, 1f), $"hour {h}");
        }

        [Test]
        public void TenMinuteCycleComesFullCircle()
        {
            Assert.That(DayNightModel.Advance(11f, 600f, 600f), Is.EqualTo(11f).Within(1e-4f));
            Assert.That(DayNightModel.Advance(11f, 150f, 600f), Is.EqualTo(17f).Within(1e-4f));
        }

        [Test]
        public void SunTurnsSmoothlyWithNoonHighest()
        {
            var noon = DayNightModel.SunRotation(12f) * Vector3.forward;
            var morning = DayNightModel.SunRotation(10f) * Vector3.forward;
            var evening = DayNightModel.SunRotation(15f) * Vector3.forward;
            Assert.That(noon.y, Is.LessThan(morning.y), "the noon sun shines down steepest");
            Assert.That(noon.y, Is.LessThan(evening.y), "the noon sun shines down steepest");
            Assert.That(
                Quaternion.Angle(DayNightModel.SunRotation(11f), DayNightModel.SunRotation(11.1f)),
                Is.LessThan(2f),
                "the sun moves smoothly");
        }

        [Test]
        public void SunMeetsDayAndNightBounds()
        {
            Assert.That(DayNightModel.SunColor(0f), Is.EqualTo(new Color(1f, 0.88f, 0.72f)));
            Assert.That(DayNightModel.SunIntensity(0f), Is.EqualTo(1.5f));
            Assert.That(DayNightModel.SunColor(1f), Is.EqualTo(new Color(0.35f, 0.5f, 0.8f)));
            Assert.That(DayNightModel.SunIntensity(1f), Is.EqualTo(0.12f));
        }
    }
}
