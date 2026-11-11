#if UNITY_EDITOR
using System.Collections;
using DeadCounty.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // The death screen respawns on Player/Jump: Space on keyboard, the south button on gamepad.
    public class PlayerDeathFlowRespawnTests : PlayerRigTests
    {
        const float RestartDelay = 0.2f;

        [UnityTest]
        public IEnumerator GamepadSouthRespawnsAfterDelay()
        {
            yield return Die();
            yield return Tap(Gamepad, new GamepadState().WithButton(GamepadButton.South), new GamepadState());
            Assert.That(Health.IsAlive, Is.True, "the south button did not respawn");
        }

        [UnityTest]
        public IEnumerator SpaceRespawnsAfterDelay()
        {
            yield return Die();
            yield return Tap(Keyboard, new KeyboardState(Key.Space), new KeyboardState());
            Assert.That(Health.IsAlive, Is.True, "Space did not respawn");
        }

        [UnityTest]
        public IEnumerator PressBeforeTheDelayIsIgnored()
        {
            SetRestartDelay(5f);
            Health.ApplyHit(10000f, Health.transform.position + Vector3.forward);
            yield return null;
            yield return Tap(Keyboard, new KeyboardState(Key.Space), new KeyboardState());
            Assert.That(Health.IsAlive, Is.False, "respawned before the restart delay");
        }

        IEnumerator Die()
        {
            SetRestartDelay(RestartDelay);
            Health.ApplyHit(10000f, Health.transform.position + Vector3.forward);
            Assert.That(Health.IsAlive, Is.False);
            yield return new WaitForSeconds(RestartDelay + 0.1f);
        }

        void SetRestartDelay(float seconds) =>
            typeof(PlayerDeathFlow).GetField("restartDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(Rig.GetComponent<PlayerDeathFlow>(), seconds);
    }
}
#endif
