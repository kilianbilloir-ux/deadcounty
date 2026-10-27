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
    // Movement noise meter of the generated rig, driven by the virtual keyboard.
    public class PlayerNoiseMeterTests : PlayerRigTests
    {
        PlayerNoiseMeter noise;
        VisualElement row, meter;

        [UnitySetUp]
        public IEnumerator SetUpMeter()
        {
            noise = Rig.GetComponent<PlayerNoiseMeter>();
            var root = Rig.GetComponent<UIDocument>().rootVisualElement;
            row = root.Q("noise-row");
            meter = root.Q("noise-meter");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator ReleaseKeys()
        {
            InputSystem.QueueStateEvent(Keyboard, new KeyboardState());
            yield return null;
        }

        int LitSegments()
        {
            var lit = 0;
            for (var i = 0; i < meter.childCount; i++)
                if (meter[i].ClassListContains("noise-segment--lit")) lit++;
            return lit;
        }

        [UnityTest]
        public IEnumerator WalkingStaysHiddenRunningShowsThenFades()
        {
            InputSystem.QueueStateEvent(Keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(1.5f);
            Assert.That(noise.Level, Is.EqualTo(2), "a walk lights two segments");
            Assert.That(row.style.opacity.value, Is.Zero, "walking is the baseline and must stay hidden");

            InputSystem.QueueStateEvent(Keyboard, new KeyboardState(Key.W, Key.LeftShift));
            yield return new WaitForSeconds(1.5f);
            Assert.That(noise.Level, Is.EqualTo(3));
            Assert.That(LitSegments(), Is.EqualTo(3));
            Assert.That(meter.ClassListContains("noise-meter--loud"), Is.True);
            Assert.That(row.style.opacity.value, Is.EqualTo(1f).Within(0.01f), "running must show the meter");

            InputSystem.QueueStateEvent(Keyboard, new KeyboardState());
            yield return new WaitForSeconds(2.5f);
            Assert.That(noise.Level, Is.Zero);
            Assert.That(row.style.opacity.value, Is.Zero, "the meter must fade once the player is quiet");
        }

        [UnityTest]
        public IEnumerator CrouchingShowsTheMeterEvenStandingStill()
        {
            yield return Tap(Keyboard, new KeyboardState(Key.C), new KeyboardState());
            Assert.That(Rig.GetComponentInChildren<ThirdPersonController>().IsCrouching, Is.True);
            yield return new WaitForSeconds(0.6f);
            Assert.That(noise.Level, Is.Zero);
            Assert.That(row.style.opacity.value, Is.EqualTo(1f).Within(0.01f), "crouching must confirm the player sneaks");
        }
    }
}
#endif
