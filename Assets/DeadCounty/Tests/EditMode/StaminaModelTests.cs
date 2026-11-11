using DeadCounty.Core;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    public class StaminaModelTests
    {
        const float Dt = 1f / 60f;

        static StaminaModel Create() => new(StaminaSettings.Default);

        static void Tick(StaminaModel model, StaminaActivity activity, float seconds)
        {
            var frames = Mathf.RoundToInt(seconds / Dt);
            for (var i = 0; i < frames; i++) model.Tick(activity, Dt);
        }

        static void SprintUntilExhausted(StaminaModel model)
        {
            for (var i = 0; i < 1000 && !model.Exhausted; i++) model.Tick(StaminaActivity.Sprint, Dt);
        }

        [Test]
        public void FullSprintEmptiesInFiveSeconds()
        {
            var model = Create();
            var frames = 0;
            for (; frames < 1000 && !model.Exhausted; frames++)
            {
                Assert.That(model.CanSprint);
                model.Tick(StaminaActivity.Sprint, Dt);
            }
            Assert.That(frames, Is.InRange(299, 301));
            Assert.That(model.Current, Is.Zero);
            Assert.That(model.CanSprint, Is.False);
        }

        [Test]
        public void RunAndAirborneNeitherDrainNorRegenerate()
        {
            var model = Create();
            Tick(model, StaminaActivity.Sprint, 2f);
            var afterSprint = model.Current;
            Tick(model, StaminaActivity.Run, 3f);
            Assert.That(model.Current, Is.EqualTo(afterSprint));
            Tick(model, StaminaActivity.Airborne, 1f);
            Assert.That(model.Current, Is.EqualTo(afterSprint));
        }

        [Test]
        public void ExhaustionBlocksRunAndSprintUntilForty()
        {
            var model = Create();
            SprintUntilExhausted(model);
            for (var i = 0; i < 1000 && model.Current < 40f; i++)
            {
                Assert.That(model.CanRun, Is.False);
                Assert.That(model.CanSprint, Is.False);
                model.Tick(StaminaActivity.Idle, Dt);
            }
            Assert.That(model.Current, Is.GreaterThanOrEqualTo(40f));
            Assert.That(model.Exhausted, Is.False);
            Assert.That(model.CanRun);
            Assert.That(model.CanSprint);
        }

        [Test]
        public void IdleRegeneratesOnlyAfterTheDelay()
        {
            var model = Create();
            Tick(model, StaminaActivity.Sprint, 2f);
            Assert.That(model.Current, Is.EqualTo(60f).Within(0.01f));
            Tick(model, StaminaActivity.Idle, 1f);
            Assert.That(model.Current, Is.EqualTo(60f).Within(0.01f));
            Tick(model, StaminaActivity.Idle, 1f);
            Assert.That(model.Current, Is.EqualTo(85f).Within(0.01f));
        }

        [Test]
        public void WalkRegeneratesEighteenPerSecond()
        {
            var model = Create();
            Tick(model, StaminaActivity.Sprint, 2f);
            Tick(model, StaminaActivity.Walk, 2f);
            Assert.That(model.Current, Is.EqualTo(78f).Within(0.01f));
        }

        [Test]
        public void RegenDelayCountsFromTheLastCost()
        {
            var model = Create();
            Tick(model, StaminaActivity.Sprint, 2f);
            Tick(model, StaminaActivity.Run, 1f);
            Tick(model, StaminaActivity.Idle, 1f);
            Assert.That(model.Current, Is.EqualTo(85f).Within(0.01f));
        }

        [Test]
        public void RegenStopsAtMax()
        {
            var model = Create();
            Tick(model, StaminaActivity.Sprint, 1f);
            Tick(model, StaminaActivity.Idle, 5f);
            Assert.That(model.Current, Is.EqualTo(100f));
            Assert.That(model.Normalized, Is.EqualTo(1f));
        }

        [Test]
        public void JumpCostsTenAndResetsTheRegenDelay()
        {
            var model = Create();
            Tick(model, StaminaActivity.Sprint, 2f);
            Tick(model, StaminaActivity.Idle, 1.5f);
            var beforeJump = model.Current;
            Assert.That(model.TryJump());
            Assert.That(model.Current, Is.EqualTo(beforeJump - 10f).Within(1e-4f));
            Tick(model, StaminaActivity.Idle, 0.5f);
            Assert.That(model.Current, Is.EqualTo(beforeJump - 10f).Within(1e-4f));
        }

        [Test]
        public void JumpRefusedBelowTen()
        {
            var model = Create();
            Tick(model, StaminaActivity.Sprint, 4.55f);
            Assert.That(model.Current, Is.EqualTo(9f).Within(0.01f));
            var before = model.Current;
            Assert.That(model.TryJump(), Is.False);
            Assert.That(model.Current, Is.EqualTo(before));
        }

        [Test]
        public void JumpRefusedWhenExhausted()
        {
            var model = Create();
            SprintUntilExhausted(model);
            Tick(model, StaminaActivity.Idle, 2f);
            Assert.That(model.Current, Is.GreaterThan(10f));
            Assert.That(model.Exhausted);
            var before = model.Current;
            Assert.That(model.TryJump(), Is.False);
            Assert.That(model.Current, Is.EqualTo(before));
        }

        [Test]
        public void ConstructorRejectsNonPositiveOrNonFiniteMax()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new StaminaModel(new StaminaSettings { Max = 0f }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new StaminaModel(new StaminaSettings { Max = -10f }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new StaminaModel(new StaminaSettings { Max = float.NaN }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new StaminaModel(new StaminaSettings { Max = float.PositiveInfinity }));
        }

        [Test]
        public void NormalizedIsOneOnFullDefaultModel() =>
            Assert.That(Create().Normalized, Is.EqualTo(1f));

        [Test]
        public void ResetRefillsAndClearsExhaustion()
        {
            var model = Create();
            SprintUntilExhausted(model);
            model.Reset();
            Assert.That(model.Current, Is.EqualTo(100f));
            Assert.That(model.Exhausted, Is.False);
        }
    }
}
