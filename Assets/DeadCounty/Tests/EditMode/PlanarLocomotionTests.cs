using DeadCounty.Locomotion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace DeadCounty.Tests
{
    public class PlanarLocomotionTests
    {
        const float Dt = 1f / 60f;
        static readonly LocomotionSettings Settings = LocomotionSettings.Player;

        static LocomotionState Moving(Vector3 heading, float speed) => new() { Heading = heading, Speed = speed, PlanarVelocity = heading * speed };

        static float TimeToFace(LocomotionState state, Vector3 wish, float targetSpeed)
        {
            var time = 0f;
            while (Vector3.Angle(state.Heading, wish) > 0.5f && time < 3f)
            {
                PlanarLocomotion.Step(ref state, Settings, wish, targetSpeed, true, true, Dt);
                time += Dt;
            }
            return time;
        }

        [TestCase(1.5f, 0.25f)]
        [TestCase(3.5f, 0.583f)]
        [TestCase(6f, 1f)]
        public void AcceleratesFromRest(float targetSpeed, float expectedTime)
        {
            var state = LocomotionState.Facing(Vector3.forward);
            var time = 0f;
            while (state.Speed < targetSpeed - 1e-4f && time < 3f)
            {
                PlanarLocomotion.Step(ref state, Settings, state.Heading, targetSpeed, true, true, Dt);
                time += Dt;
            }
            Assert.That(time, Is.EqualTo(expectedTime).Within(0.02f));
            Assert.That(state.PlanarVelocity, Is.EqualTo(Vector3.forward * targetSpeed).Using(new Vector3EqualityComparer(1e-3f)));
        }

        [Test]
        public void StandingHalfTurnTakesThreeTenths() => Assert.That(TimeToFace(LocomotionState.Facing(Vector3.back), Vector3.forward, 0f), Is.EqualTo(0.3f).Within(0.03f));

        // Setting off while turning slows the turn a little, since the turn rate drops as speed builds up.
        [Test]
        public void HalfTurnIntoWalkTakesAboutAThird() => Assert.That(TimeToFace(LocomotionState.Facing(Vector3.back), Vector3.forward, 1.5f), Is.EqualTo(0.333f).Within(0.03f));

        [Test]
        public void SprintReversalBrakesBeforeTurning()
        {
            var state = Moving(Vector3.forward, 6f);
            var time = 0f;
            var brakeAngle = -1f;
            PlanarLocomotion.Step(ref state, Settings, Vector3.back, 6f, true, true, Dt);
            Assert.That(state.Pivoting);
            time += Dt;
            while (Vector3.Dot(state.PlanarVelocity, Vector3.back) < 5.9f && time < 3f)
            {
                PlanarLocomotion.Step(ref state, Settings, Vector3.back, 6f, true, true, Dt);
                time += Dt;
                if (brakeAngle < 0f && state.Speed < 2.45f) brakeAngle = Vector3.Angle(Vector3.forward, state.Heading);
            }
            TestContext.WriteLine($"Heading turned {brakeAngle:0.0} deg when braked below 2.45 m/s, reversal done in {time:0.000} s");
            Assert.That(brakeAngle, Is.InRange(0f, 60f));
            Assert.That(time, Is.InRange(0.7f, 1.2f));
        }

        [Test]
        public void AirborneKeepsMomentum()
        {
            var state = Moving(Vector3.forward, 6f);
            for (var i = 0; i < 30; i++) PlanarLocomotion.Step(ref state, Settings, state.Heading, 0f, false, false, Dt);
            Assert.That(state.Speed, Is.GreaterThan(5.2f));
            Assert.That(state.Heading, Is.EqualTo(Vector3.forward).Using(new Vector3EqualityComparer(1e-4f)));
            Assert.That(state.Pivoting, Is.False);
        }

        [Test]
        public void NoInputBrakesAlongHeading()
        {
            var state = Moving(Vector3.right, 3.5f);
            for (var i = 0; i < 30; i++) PlanarLocomotion.Step(ref state, Settings, state.Heading, 0f, false, true, Dt);
            Assert.That(state.Speed, Is.Zero);
            Assert.That(state.Heading, Is.EqualTo(Vector3.right));
        }
    }
}
