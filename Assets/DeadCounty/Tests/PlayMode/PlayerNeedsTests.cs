#if UNITY_EDITOR
using System.Collections;
using DeadCounty.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // Needs on the generated player rig: what empty gauges do to health, stamina and the screen, and the respawn.
    public class PlayerNeedsTests : PlayerRigTests
    {
        const float Minute = 60f;

        [UnityTest]
        public IEnumerator EmptyNeedsDrainHealthCapStaminaAndDarkenTheScreen()
        {
            var needs = Rig.GetComponentInChildren<PlayerNeeds>();
            var volume = Rig.GetComponentInChildren<Volume>();
            Assert.That(volume.weight, Is.Zero);

            needs.Model.Tick(25f * Minute + 1f, false);
            var before = Health.Pool.Current;
            var start = Time.time;
            yield return new WaitForSeconds(1f);
            var elapsed = Time.time - start;
            // Hunger and thirst both empty: 100/3 + 50 health per minute.
            Assert.That(before - Health.Pool.Current, Is.EqualTo((100f / 3f + 50f) / Minute * elapsed).Within(0.1f));
            Assert.That(Stamina.Model.Current, Is.EqualTo(50f).Within(0.01f));
            Assert.That(volume.weight, Is.EqualTo(0.5f * elapsed).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator DyingOfThirstEndsInTheDeathScreenAndRespawnRefillsTheNeeds()
        {
            var needs = Rig.GetComponentInChildren<PlayerNeeds>();
            var deathFlow = Rig.GetComponent<PlayerDeathFlow>();
            needs.Model.Tick(25f * Minute + 1f, false);
            Health.Pool.Apply(Health.Pool.Max - 1f);
            var start = Time.time;
            while (Health.IsAlive && Time.time - start < 3f) yield return null;
            Assert.That(deathFlow.IsDead, Is.True, "an empty thirst gauge did not kill");

            while (!deathFlow.CanRespawn && Time.time - start < 8f) yield return null;
            yield return Tap(Keyboard, new KeyboardState(Key.Space), new KeyboardState());
            Assert.That(Health.IsAlive, Is.True);
            Assert.That(needs.Model.Thirst, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(needs.Model.Exhausted, Is.False);
            Assert.That(Rig.GetComponentInChildren<Volume>().weight, Is.Zero);
        }
    }
}
#endif
