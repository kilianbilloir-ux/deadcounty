#if UNITY_EDITOR
using System.Collections;
using DeadCounty.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DeadCounty.Tests
{
    // Escape, arrows and Enter driving the pause menu of the generated rig.
    public class PauseMenuTests : PlayerRigTests
    {
        PauseMenu pause;
        ThirdPersonController controller;
        float sensitivity;

        VisualElement Root => Rig.GetComponent<UIDocument>().rootVisualElement;

        [UnitySetUp]
        public IEnumerator SetUpPause()
        {
            pause = Rig.GetComponent<PauseMenu>();
            controller = Rig.GetComponentInChildren<ThirdPersonController>();
            sensitivity = controller.MouseSensitivity;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownPause()
        {
            Time.timeScale = 1f;
            controller.MouseSensitivity = sensitivity;
            PlayerPrefs.DeleteKey("dc.mouseSensitivity");
            yield return null;
        }

        IEnumerator Press(Key key) => Tap(Keyboard, new KeyboardState(key), new KeyboardState());

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
        public IEnumerator TheInventoryStaysShutWhilePaused()
        {
            yield return Press(Key.Escape);
            yield return Press(Key.Tab);
            Assert.That(Rig.GetComponent<PlayerInventoryScreen>().IsOpen, Is.False);
        }
    }
}
#endif
