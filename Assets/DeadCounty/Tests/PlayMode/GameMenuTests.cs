#if UNITY_EDITOR
using System.Collections;
using DeadCounty.Player;
using DeadCounty.UI;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DeadCounty.Tests
{
    // Escape, arrows and Enter driving the title, pause and options menus of the generated rig.
    public class GameMenuTests : PlayerRigTests
    {
        GameMenu pause;
        ThirdPersonController controller;
        float sensitivity;
        GameObject titleCamera;

        VisualElement Root => Rig.GetComponent<UIDocument>().rootVisualElement;

        [UnitySetUp]
        public IEnumerator SetUpPause()
        {
            pause = Rig.GetComponent<GameMenu>();
            controller = Rig.GetComponentInChildren<ThirdPersonController>();
            sensitivity = controller.MouseSensitivity;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownPause()
        {
            Time.timeScale = 1f;
            if (titleCamera != null) Object.Destroy(titleCamera);
            controller.MouseSensitivity = sensitivity;
            PlayerPrefs.DeleteKey("dc.mouseSensitivity");
            yield return null;
        }

        IEnumerator Press(Key key) => Tap(Keyboard, new KeyboardState(key), new KeyboardState());

        // A title camera and its TitleScreen, as the builder puts them in the scene.
        IEnumerator ShowTitle()
        {
            titleCamera = new GameObject("TestTitleCamera");
            titleCamera.SetActive(false);
            var vcam = titleCamera.AddComponent<CinemachineCamera>();
            var title = titleCamera.AddComponent<TitleScreen>();
            typeof(TitleScreen).GetField("titleCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(title, vcam);
            titleCamera.SetActive(true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TitleHoldsTheGameUntilPlay()
        {
            yield return ShowTitle();
            Assert.That(pause.OnTitle);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(controller.MenuOpen);
            Assert.That(Root.Q("title").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Root.Q("hud").style.display.value, Is.EqualTo(DisplayStyle.None));

            yield return Press(Key.Escape);
            Assert.That(pause.OnTitle, "Escape left the title");
            Assert.That(pause.Paused, Is.False);

            yield return Press(Key.Enter);
            Assert.That(pause.OnTitle, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(controller.MenuOpen, Is.False);
            Assert.That(Root.Q("title").style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(titleCamera.GetComponent<CinemachineCamera>().enabled, Is.False, "the view must go back to the player camera");
        }

        [UnityTest]
        public IEnumerator OptionsOpenedFromTheTitleGoBackToIt()
        {
            yield return ShowTitle();
            yield return Press(Key.DownArrow);
            yield return Press(Key.Enter);
            Assert.That(pause.InSettings);
            Assert.That(Root.Q("pause").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Root.Q("title").style.display.value, Is.EqualTo(DisplayStyle.None));

            yield return Press(Key.Escape);
            Assert.That(pause.InSettings, Is.False);
            Assert.That(pause.OnTitle);
            Assert.That(Root.Q("title").style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [UnityTest]
        public IEnumerator TheTitleKeepsTheWorldSharpButNotItsOptions()
        {
            var blur = Rig.transform.Find("MenuBlurVolume").GetComponent<Volume>();
            yield return ShowTitle();
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(blur.weight, Is.Zero, "the world behind the title must not be blurred");

            yield return Press(Key.DownArrow);
            yield return Press(Key.Enter);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(blur.weight, Is.EqualTo(1f), "the options list needs the blurred, darkened backdrop to read");
        }

        [UnityTest]
        public IEnumerator EscapeFreezesTheGameAndEscapeAgainResumes()
        {
            yield return Press(Key.Escape);
            Assert.That(pause.Paused);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(controller.MenuOpen);
            Assert.That(Root.Q("pause").style.display.value, Is.EqualTo(DisplayStyle.Flex));

            yield return Press(Key.Escape);
            Assert.That(pause.Paused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(controller.MenuOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator EscapeClosesTheInventoryWithoutPausing()
        {
            yield return Press(Key.Tab);
            var inventory = Rig.GetComponent<PlayerInventoryScreen>();
            Assert.That(inventory.IsOpen);
            yield return Press(Key.Escape);
            Assert.That(inventory.IsOpen, Is.False);
            Assert.That(pause.Paused, Is.False);
            yield return Press(Key.Tab);
            Assert.That(inventory.IsOpen, "the inventory no longer opens");
        }

        [UnityTest]
        public IEnumerator OptionsChangeTheMouseSensitivityThenGoBack()
        {
            yield return Press(Key.Escape);
            yield return Press(Key.DownArrow);
            yield return Press(Key.Enter);
            Assert.That(pause.InSettings, "Enter on Options did not open them");
            yield return Press(Key.RightArrow);
            Assert.That(controller.MouseSensitivity, Is.EqualTo(sensitivity + 0.01f).Within(1e-4f));
            yield return Press(Key.Escape);
            Assert.That(pause.InSettings, Is.False);
            Assert.That(pause.Paused, "Escape in the options must go back, not resume");
        }

        [UnityTest]
        public IEnumerator OptionsChangeTheInterfaceSizeAndTheTips()
        {
            var savedScale = GameSettings.UiScale;
            var savedTips = GameSettings.Tips;
            try
            {
                GameSettings.UiScale = 1f;
                yield return Press(Key.Escape);
                yield return Press(Key.DownArrow);
                yield return Press(Key.Enter);
                for (var i = 0; i < 3; i++) yield return Press(Key.DownArrow);
                yield return Press(Key.RightArrow);
                Assert.That(GameSettings.UiScale, Is.EqualTo(1.1f).Within(1e-4f));
                Assert.That(Root.Q<Label>("option-ui-scale-value").text, Is.EqualTo("110\u00A0%"));
                yield return Press(Key.DownArrow);
                yield return Press(Key.Enter);
                Assert.That(GameSettings.Tips, Is.EqualTo(!savedTips));
                Assert.That(Root.Q<Label>("option-tips-value").text, Is.EqualTo(savedTips ? "Non" : "Oui"));
            }
            finally
            {
                GameSettings.UiScale = savedScale;
                GameSettings.Tips = savedTips;
            }
        }

        [UnityTest]
        public IEnumerator ControlsRebindAKeySwapTheOneItTookAndEscapeCancels()
        {
            var savedOverrides = InputSystem.actions.SaveBindingOverridesAsJson();
            var savedPrefs = PlayerPrefs.GetString("dc.bindings", null);
            var jump = KeyBindings.Rows[7];
            var interact = KeyBindings.Rows[8];
            try
            {
                InputSystem.actions.RemoveAllBindingOverrides();
                yield return Press(Key.Escape);
                yield return Press(Key.DownArrow);
                yield return Press(Key.Enter);
                for (var i = 0; i < 6; i++) yield return Press(Key.DownArrow);
                yield return Press(Key.Enter);
                Assert.That(pause.InControls, "Enter on Contrôles did not open the keys");
                for (var i = 0; i < 7; i++) yield return Press(Key.DownArrow);
                Assert.That(jump.Label, Is.EqualTo("Sauter"));
                yield return Press(Key.Enter);
                Assert.That(KeyBindings.Rebinding, "Enter on a key must wait for the new one");

                yield return Press(Key.E);
                Assert.That(KeyBindings.Rebinding, Is.False);
                Assert.That(KeyBindings.Key(jump), Is.EqualTo("E"));
                Assert.That(KeyBindings.Key(interact), Is.EqualTo("Espace"), "the action that had the key must take the old one");
                Assert.That(PlayerPrefs.GetString("dc.bindings", ""), Does.Contain("<Keyboard>/e"));

                yield return Press(Key.Enter);
                yield return Press(Key.Escape);
                Assert.That(KeyBindings.Rebinding, Is.False);
                Assert.That(pause.InControls, "Escape must cancel the wait, not leave the page");
                Assert.That(KeyBindings.Key(jump), Is.EqualTo("E"));
            }
            finally
            {
                KeyBindings.Cancel();
                InputSystem.actions.RemoveAllBindingOverrides();
                InputSystem.actions.LoadBindingOverridesFromJson(savedOverrides);
                if (savedPrefs == null) PlayerPrefs.DeleteKey("dc.bindings");
                else PlayerPrefs.SetString("dc.bindings", savedPrefs);
            }
        }

        [UnityTest]
        public IEnumerator TheInventoryStaysShutWhilePaused()
        {
            yield return Press(Key.Escape);
            yield return Press(Key.Tab);
            Assert.That(Rig.GetComponent<PlayerInventoryScreen>().IsOpen, Is.False);
        }
    }
}
#endif
