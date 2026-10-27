#if UNITY_EDITOR
using System.Collections;
using DeadCounty.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

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
