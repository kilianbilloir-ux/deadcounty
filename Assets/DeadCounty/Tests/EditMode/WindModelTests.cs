using DeadCounty.Core;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    public class WindModelTests
    {
        [Test]
        public void TheWindKeepsAUnitDirectionAndNeverDropsBelowCalm()
        {
            for (var time = 0f; time < 600f; time += 0.37f)
            {
                var wind = WindModel.Sample(time);
                Assert.That(new Vector2(wind.x, wind.y).magnitude, Is.EqualTo(1f).Within(1e-4f), $"direction at {time} s");
                Assert.That(wind.z, Is.InRange(WindModel.Calm, 1f), $"strength at {time} s");
            }
        }

        [Test]
        public void TheHeadingWandersAroundItsMeanWithoutTurningBack()
        {
            var mean = Quaternion.Euler(0f, WindModel.MeanHeading, 0f) * Vector3.forward;
            for (var time = 0f; time < 600f; time += 0.37f)
            {
                var wind = WindModel.Sample(time);
                var angle = Vector3.Angle(mean, new Vector3(wind.x, 0f, wind.y));
                Assert.That(angle, Is.LessThanOrEqualTo(WindModel.HeadingWander + 0.01f), $"heading at {time} s");
            }
        }

        [Test]
        public void TheStrengthSwellsAndDropsOverAFewMinutes()
        {
            float low = 1f, high = 0f;
            for (var time = 0f; time < 300f; time += 0.5f)
            {
                var strength = WindModel.Sample(time).z;
                low = Mathf.Min(low, strength);
                high = Mathf.Max(high, strength);
            }
            Assert.That(low, Is.LessThan(0.5f), "calmest spell");
            Assert.That(high, Is.GreaterThan(0.85f), "strongest spell");
        }
    }
}
