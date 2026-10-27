#if UNITY_EDITOR
using System.Collections;
using DeadCounty.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DeadCounty.Tests
{
    // Injury vignette and menu blur volumes of the generated rig.
    public class PlayerScreenEffectsTests : PlayerRigTests
    {
        Volume Volume(string volumeName) => Rig.transform.Find(volumeName).GetComponent<Volume>();

        [UnityTearDown]
        public IEnumerator TearDownTime()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator InjuryVignetteDeepensUnderFortyPercent()
        {
            var saved = GameSettings.ReduceFlashes;
            try
            {
                // No beat: the weight is steady.
                GameSettings.ReduceFlashes = true;
                var injury = Volume("InjuryVolume");
                Health.Pool.Apply(55f);
                yield return new WaitForSeconds(0.5f);
                Assert.That(injury.weight, Is.Zero, "at 45 % the vignette must stay off");

                Health.Pool.Apply(25f);
                yield return new WaitForSeconds(1f);
                Assert.That(injury.weight, Is.EqualTo(0.8f).Within(0.02f), "at 20 %");
            }
            finally
            {
                GameSettings.ReduceFlashes = saved;
            }
        }

        [UnityTest]
        public IEnumerator TheWorldKeepsRunningSharpBehindTheInventory()
        {
            var view = new GameObject("TestCamera", typeof(Camera)) { tag = "MainCamera" };
            try
            {
                yield return Tap(Keyboard, new KeyboardState(Key.Tab), new KeyboardState());
                Assert.That(Rig.GetComponent<DeadCounty.Player.PlayerInventoryScreen>().IsOpen);
                var before = Time.time;
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.That(Time.timeScale, Is.EqualTo(1f), "the inventory must not stop the game");
                Assert.That(Time.time, Is.GreaterThan(before + 0.2f));
                Assert.That(Volume("MenuBlurVolume").weight, Is.Zero, "the world must stay sharp behind the inventory");

                Health.ApplyHit(10f, Health.transform.position + Vector3.forward);
                yield return null;
                var marker = Rig.GetComponent<UIDocument>().rootVisualElement.Q("hit-markers")[0];
                Assert.That(marker.style.opacity.value, Is.EqualTo(1f).Within(0.01f), "a hit taken in the inventory must show where it came from");
            }
            finally
            {
                Object.Destroy(view);
            }
        }

        [UnityTest]
        public IEnumerator PauseBlursTheSceneWhileTheClockIsStopped()
        {
            var blur = Volume("MenuBlurVolume");
            Assert.That(blur.weight, Is.Zero);
            yield return Tap(Keyboard, new KeyboardState(Key.Escape), new KeyboardState());
            Assert.That(Time.timeScale, Is.Zero);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(blur.weight, Is.EqualTo(1f));

            yield return Tap(Keyboard, new KeyboardState(Key.Escape), new KeyboardState());
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(blur.weight, Is.Zero);
        }
    }
}
#endif
