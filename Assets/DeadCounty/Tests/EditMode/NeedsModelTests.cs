using System;
using DeadCounty.Core;
using NUnit.Framework;

namespace DeadCounty.Tests
{
    // GDD 8.4 figures: hunger empties in 20 min, thirst in 12, fatigue in 25 (twice as fast sprinting); empty
    // hunger kills in 3 min, empty thirst in 2; empty fatigue halves the stamina capacity.
    public class NeedsModelTests
    {
        const float Minute = 60f;

        static NeedsModel Create() => new(NeedsSettings.Default);

        [Test]
        public void GaugesEmptyAtTheGddTimes()
        {
            var model = Create();
            model.Tick(12f * Minute - 1f, false);
            Assert.That(model.Thirst, Is.GreaterThan(0f));
            model.Tick(2f, false);
            Assert.That(model.Thirst, Is.Zero);
            Assert.That(model.Hunger, Is.EqualTo(0.4f).Within(1e-3f));
            Assert.That(model.Fatigue, Is.EqualTo(13f / 25f).Within(1e-3f));

            model.Tick(8f * Minute, false);
            Assert.That(model.Hunger, Is.Zero);
            model.Tick(5f * Minute, false);
            Assert.That(model.Fatigue, Is.Zero);
        }

        [Test]
        public void SprintingDoublesTheFatigueDrain()
        {
            var model = Create();
            model.Tick(5f * Minute, true);
            Assert.That(model.Fatigue, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(model.Hunger, Is.EqualTo(0.75f).Within(1e-4f), "sprinting must not touch hunger");
        }

        [Test]
        public void EmptyThirstKillsInTwoMinutesAndEmptyHungerInThree()
        {
            var thirsty = Create();
            thirsty.Tick(12f * Minute, false);
            Assert.That(thirsty.Tick(2f * Minute, false), Is.EqualTo(100f).Within(1e-2f));

            var settings = NeedsSettings.Default;
            settings.HungerMinutes = 1f;
            settings.ThirstMinutes = 1000f;
            settings.FatigueMinutes = 1000f;
            var starving = new NeedsModel(settings);
            starving.Tick(Minute, false);
            Assert.That(starving.Tick(3f * Minute, false), Is.EqualTo(100f).Within(1e-2f));
        }

        [Test]
        public void DamageOnlyCountsTheTimeSpentEmpty()
        {
            var oneTick = Create();
            var damage = oneTick.Tick(12f * Minute + 30f, false);
            Assert.That(damage, Is.EqualTo(25f).Within(1e-2f), "30 s without water");

            var manyTicks = Create();
            var total = 0f;
            for (var i = 0; i < 750; i++) total += manyTicks.Tick(1f, false);
            Assert.That(total, Is.EqualTo(damage).Within(0.05f));
        }

        [Test]
        public void EmptyFatigueHalvesTheStaminaCapacityUntilReset()
        {
            var model = Create();
            Assert.That(model.StaminaCapacity, Is.EqualTo(1f));
            model.Tick(25f * Minute + 1f, false);
            Assert.That(model.Exhausted);
            Assert.That(model.StaminaCapacity, Is.EqualTo(0.5f));

            model.Reset();
            Assert.That(model.Exhausted, Is.False);
            Assert.That(model.Hunger, Is.EqualTo(1f));
            Assert.That(model.Thirst, Is.EqualTo(1f));
        }

        [Test]
        public void ConstructorRejectsNonPositiveDrainTimes()
        {
            var settings = NeedsSettings.Default;
            settings.ThirstMinutes = 0f;
            Assert.Throws<ArgumentOutOfRangeException>(() => new NeedsModel(settings));
            settings = NeedsSettings.Default;
            settings.HungerMinutes = float.NaN;
            Assert.Throws<ArgumentOutOfRangeException>(() => new NeedsModel(settings));
        }

        [Test]
        public void EatingAndDrinkingRefillUpToFull()
        {
            var model = Create();
            model.Tick(6f * Minute, false);
            model.Restore(0.35f, 0.5f);
            Assert.That(model.Hunger, Is.EqualTo(1f));
            Assert.That(model.Thirst, Is.EqualTo(1f));
            model.Tick(12f * Minute, false);
            model.Restore(0.25f, 0f);
            Assert.That(model.Hunger, Is.EqualTo(0.4f + 0.25f).Within(1e-3f));
            Assert.That(model.Thirst, Is.Zero.Within(1e-4f));
        }

        [Test]
        public void SleepFillsFatigueInEightGameHoursWhileHungerAndThirstSlowDown()
        {
            var model = Create();
            model.Tick(10f * Minute, false);
            Assert.That(model.Sleep(Minute), Is.Zero);
            Assert.That(model.Fatigue, Is.EqualTo(0.6f + 60f / 200f).Within(1e-3f));
            Assert.That(model.Hunger, Is.EqualTo(0.5f - 30f / 1200f).Within(1e-3f));
            Assert.That(model.Thirst, Is.EqualTo(1f / 6f - 30f / 720f).Within(1e-3f));
            model.Sleep(Minute);
            Assert.That(model.Fatigue, Is.EqualTo(1f), "fatigue stops at full");
        }

        [Test]
        public void AnEmptyGaugeHurtsHalfAsMuchAsleep()
        {
            var model = Create();
            model.Tick(12f * Minute + 1f, false);
            Assert.That(model.Sleep(2f * Minute), Is.EqualTo(50f).Within(0.1f), "thirst kills in 2 min awake, 4 min asleep");
        }
    }
}
