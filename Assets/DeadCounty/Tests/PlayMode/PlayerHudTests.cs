#if UNITY_EDITOR
using System.Collections;
using DeadCounty.Core;
using DeadCounty.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DeadCounty.Tests
{
    // UI Toolkit HUD on the generated player rig.
    public class PlayerHudTests : PlayerRigTests
    {
        [UnityTest]
        public IEnumerator HudBuildsItsElementsAndTracksBars()
        {
            var (hud, health, stamina) = BuildHud();

            var root = hud.GetComponent<UIDocument>().rootVisualElement;
            var healthFill = root.Q<VisualElement>("health-fill");
            var staminaFill = root.Q<VisualElement>("stamina-fill");
            var lamp = root.Q<Label>("lamp");
            var time = root.Q<Label>("time");
            Assert.That(healthFill, Is.Not.Null, "no health bar");
            Assert.That(staminaFill, Is.Not.Null, "no stamina bar");
            Assert.That(lamp.text, Is.Not.Empty);
            Assert.That(time.text, Does.Contain("Jour"), "the clock never rendered");

            yield return null;
            Assert.That(healthFill.style.width.value.value, Is.EqualTo(100f).Within(1f), "full health must fill the bar");
            Assert.That(staminaFill.style.opacity.value, Is.EqualTo(0f).Within(0.02f), "stamina is hidden while full");

            health.Pool.Apply(40f);
            stamina.Model.Tick(StaminaActivity.Sprint, 2.5f);
            yield return new WaitForSeconds(0.6f);
            Assert.That(healthFill.style.width.value.value, Is.EqualTo(60f).Within(3f));
            Assert.That(staminaFill.style.width.value.value, Is.EqualTo(50f).Within(4f));
            Assert.That(staminaFill.style.opacity.value, Is.EqualTo(1f).Within(0.02f), "stamina must show once spent");
        }

        [UnityTest]
        public IEnumerator HudHidesItsBarsOnDeath()
        {
            var (hud, health, _) = BuildHud();
            var root = hud.GetComponent<UIDocument>().rootVisualElement;
            var hudElement = root.Q<VisualElement>("hud");
            yield return null;
            Assert.That(hudElement.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            health.ApplyHit(10000f, hud.transform.position + Vector3.forward);
            yield return new WaitForSeconds(0.6f);
            Assert.That(hudElement.style.display.value, Is.EqualTo(DisplayStyle.None), "the HUD stayed on the death screen");
        }

        (PlayerHudController, PlayerHealth, PlayerStamina) BuildHud()
        {
            var hud = Rig.GetComponent<PlayerHudController>();
            Assert.That(hud, Is.Not.Null, "the rig has no PlayerHudController");
            return (hud, Health, Stamina);
        }
    }
}
#endif
