using DeadCounty.Audio;
using NUnit.Framework;

namespace DeadCounty.Tests
{
    public class AmbienceMixTests
    {
        [Test]
        public void DayAndNightBedsCrossfadeAtEqualPower()
        {
            Assert.That(AmbienceMix.DayWeight(0f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(AmbienceMix.NightWeight(0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(AmbienceMix.DayWeight(1f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(AmbienceMix.NightWeight(1f), Is.EqualTo(1f).Within(1e-5f));
            var previous = AmbienceMix.NightWeight(0f);
            for (var night = 0f; night <= 1f; night += 0.05f)
            {
                var day = AmbienceMix.DayWeight(night);
                var dark = AmbienceMix.NightWeight(night);
                Assert.That(day * day + dark * dark, Is.EqualTo(1f).Within(1e-4f), $"power dips or bumps at night factor {night}");
                Assert.That(dark, Is.GreaterThanOrEqualTo(previous), "the night bed must only grow as it gets darker");
                previous = dark;
            }
        }

        [Test]
        public void BirdsSingMostAtDawnAndNotAtNight()
        {
            Assert.That(AmbienceMix.BirdRate(0f, 12f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(AmbienceMix.BirdRate(0f, 7.25f), Is.GreaterThan(2f), "the morning chorus");
            Assert.That(AmbienceMix.BirdRate(1f, 7.25f), Is.Zero, "no birds in the dark, even at chorus time");
            Assert.That(AmbienceMix.BirdRate(0.5f, 19f), Is.LessThan(1f));
        }

        [Test]
        public void TheWindCalmsAfterDark()
        {
            Assert.That(AmbienceMix.WindLevel(0f), Is.EqualTo(1f));
            Assert.That(AmbienceMix.WindLevel(1f), Is.LessThan(AmbienceMix.WindLevel(0.5f)));
            Assert.That(AmbienceMix.WindLevel(1f), Is.GreaterThan(0f), "the night keeps some wind");
        }

        [Test]
        public void ARoomMufflesTheOutsideEvenly()
        {
            Assert.That(AmbienceMix.OutdoorGain(0f), Is.EqualTo(1f));
            Assert.That(AmbienceMix.OutdoorGain(1f), Is.LessThan(0.5f));
            Assert.That(AmbienceMix.Cutoff(0f), Is.EqualTo(AmbienceMix.OpenCutoff).Within(1f));
            Assert.That(AmbienceMix.Cutoff(1f), Is.EqualTo(AmbienceMix.RoomCutoff).Within(1f));
            // Geometric: halfway in is the geometric mean, not the arithmetic one.
            var expected = UnityEngine.Mathf.Sqrt(AmbienceMix.OpenCutoff * AmbienceMix.RoomCutoff);
            Assert.That(AmbienceMix.Cutoff(0.5f), Is.EqualTo(expected).Within(expected * 1e-3f));
        }

        [Test]
        public void IndoorSmoothingDoesNotDependOnTheFrameRate()
        {
            float Run(int frames)
            {
                var value = 0f;
                for (var i = 0; i < frames; i++) value = AmbienceMix.Approach(value, 1f, 1f / frames, 0.5f);
                return value;
            }
            Assert.That(Run(10), Is.EqualTo(Run(240)).Within(1e-4f));
            Assert.That(Run(1), Is.EqualTo(Run(60)).Within(1e-4f));
            Assert.That(Run(60), Is.GreaterThan(0.85f).And.LessThan(0.9f), "two time constants: about 86 %");
            Assert.That(AmbienceMix.Approach(0.3f, 1f, 0.1f, 0f), Is.EqualTo(1f), "no time constant jumps to the target");
        }

        [Test]
        public void RandomDelaysAverageTheirMean()
        {
            const int samples = 10000;
            var sum = 0f;
            for (var i = 0; i < samples; i++)
            {
                var delay = AmbienceMix.NextDelay(60f, (i + 0.5f) / samples);
                Assert.That(delay, Is.GreaterThanOrEqualTo(0f));
                sum += delay;
            }
            Assert.That(sum / samples, Is.EqualTo(60f).Within(2f));
            Assert.That(float.IsFinite(AmbienceMix.NextDelay(60f, 1f)), "a uniform draw of 1 must not wait forever");
        }
    }
}
