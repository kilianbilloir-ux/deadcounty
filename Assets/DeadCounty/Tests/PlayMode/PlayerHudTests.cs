#if UNITY_EDITOR
using System.Collections;
using DeadCounty.Core;
using DeadCounty.Player;
using DeadCounty.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DeadCounty.Tests
{
    // UI Toolkit HUD on the generated player rig.
    public class PlayerHudTests : PlayerRigTests
    {
        VisualElement Root => Rig.GetComponent<UIDocument>().rootVisualElement;

        T Element<T>(string name) where T : VisualElement
        {
            var element = Root.Q<T>(name);
            Assert.That(element, Is.Not.Null, $"the HUD has no '{name}'");
            return element;
        }

        static float Opacity(VisualElement element) => element.style.opacity.value;

        [UnityTest]
        public IEnumerator BarsStayHiddenWhileAllIsWellThenTrackTheStats()
        {
            var healthRow = Element<VisualElement>("health-row");
            var staminaRow = Element<VisualElement>("stamina-row");
            var healthFill = Element<VisualElement>("health-fill");
            var staminaFill = Element<VisualElement>("stamina-fill");
            Assert.That(Element<Label>("time").style.display.value, Is.EqualTo(DisplayStyle.None), "a clock without a day/night director");
            yield return new WaitForSeconds(0.6f);
            Assert.That(Opacity(healthRow), Is.EqualTo(0f).Within(0.01f), "full health must stay hidden");
            Assert.That(Opacity(staminaRow), Is.EqualTo(0f).Within(0.01f), "full stamina must stay hidden");

            Health.Pool.Apply(40f);
            Stamina.Model.Tick(StaminaActivity.Sprint, 2.5f);
            yield return new WaitForSeconds(0.6f);
            Assert.That(Opacity(healthRow), Is.EqualTo(1f).Within(0.01f), "a wounded player must see the health bar");
            Assert.That(Opacity(staminaRow), Is.EqualTo(1f).Within(0.01f), "spent stamina must show");
            Assert.That(healthFill.style.width.value.value, Is.EqualTo(60f).Within(3f));
            Assert.That(staminaFill.style.width.value.value, Is.EqualTo(50f).Within(4f));
            Assert.That(healthFill.ClassListContains("health-fill--low"), Is.False);

            Health.Pool.Apply(30f);
            yield return null;
            Assert.That(healthFill.ClassListContains("health-fill--low"), Is.True, "low health must take the danger colour");
        }

        [UnityTest]
        public IEnumerator FlashlightReadoutShowsWhenToggled()
        {
            var readouts = Element<VisualElement>("readouts");
            var flashlight = Element<Label>("flashlight");
            yield return new WaitForSeconds(5f);
            Assert.That(Opacity(readouts), Is.EqualTo(0f).Within(0.01f), "the readouts must fade once nothing changes");

            Rig.GetComponentInChildren<PlayerFlashlight>().SetOn(true);
            yield return new WaitForSeconds(0.6f);
            Assert.That(Opacity(readouts), Is.EqualTo(1f).Within(0.01f));
            Assert.That(flashlight.ClassListContains("flashlight-readout--on"), Is.True);
        }

        [UnityTest]
        public IEnumerator NeedGaugesShowOnceHalfEmptyAndTurnDangerWhenLow()
        {
            var thirst = Element<VisualElement>("thirst");
            var hunger = Element<VisualElement>("hunger");
            var fatigue = Element<VisualElement>("fatigue");
            var thirstGauge = Element<RadialGauge>("thirst-gauge");
            var needs = Rig.GetComponentInChildren<PlayerNeeds>();
            yield return new WaitForSeconds(5f);
            Assert.That(Opacity(thirst), Is.EqualTo(0f).Within(0.01f), "full needs must fade with the readouts");

            // Thirst 1/12, hunger 0.45, fatigue 0.56.
            needs.Model.Tick(11f * 60f, false);
            yield return new WaitForSeconds(0.6f);
            Assert.That(Opacity(thirst), Is.EqualTo(1f).Within(0.01f));
            Assert.That(Opacity(hunger), Is.EqualTo(1f).Within(0.01f));
            Assert.That(Opacity(fatigue), Is.EqualTo(0f).Within(0.01f), "a need above half must stay hidden");
            Assert.That(thirstGauge.Value, Is.EqualTo(needs.Model.Thirst).Within(0.01f));
            Assert.That(thirst.ClassListContains("need--low"), Is.True, "a low need must take the danger colour");
            Assert.That(hunger.ClassListContains("need--low"), Is.False);
        }

        [UnityTest]
        public IEnumerator HudHidesItsBarsOnDeath()
        {
            var hud = Element<VisualElement>("hud");
            yield return null;
            Assert.That(hud.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            Health.ApplyHit(10000f, Health.transform.position + Vector3.forward);
            yield return null;
            Assert.That(hud.style.display.value, Is.EqualTo(DisplayStyle.None), "the HUD stayed on the death screen");
        }

        [UnityTest]
        public IEnumerator GameOverShowsTheSurvivalTimeThenThePrompt()
        {
            var flow = Rig.GetComponent<PlayerDeathFlow>();
            SetPrivate(flow, "fadeDelay", 0f);
            SetPrivate(flow, "fadeTime", 0.1f);
            SetPrivate(flow, "restartDelay", 0.4f);
            var gameOver = Element<VisualElement>("game-over");
            var title = Element<Label>("game-over-title");
            var prompt = Element<Label>("game-over-prompt");
            yield return null;
            Assert.That(gameOver.style.display.value, Is.EqualTo(DisplayStyle.None));

            Health.ApplyHit(10000f, Health.transform.position + Vector3.forward);
            yield return new WaitForSeconds(0.2f);
            Assert.That(gameOver.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Opacity(gameOver), Is.EqualTo(1f).Within(0.01f));
            Assert.That(title.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Element<Label>("game-over-time").text, Does.StartWith("Temps survécu : 00:0"));
            Assert.That(prompt.style.display.value, Is.EqualTo(DisplayStyle.None), "prompt before the restart delay");

            yield return new WaitForSeconds(0.3f);
            Assert.That(prompt.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(prompt.text, Does.Contain("Recommencer"));

            yield return Tap(Keyboard, new KeyboardState(Key.Space), new KeyboardState());
            Assert.That(Health.IsAlive, Is.True);
            Assert.That(title.style.display.value, Is.EqualTo(DisplayStyle.None), "the title must go with the respawn");
        }

        static void SetPrivate(object target, string field, float value) =>
            target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(target, value);
    }
}
#endif
