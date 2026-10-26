using System.Reflection;
using DeadCounty.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Tests
{
    public class PlayerFlashlightTests
    {
        const string ActionsPath = "Assets/InputSystem_Actions.inputactions";

        static void RunAwake(Component component) =>
            component.GetType().GetMethod("Awake", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(component, null);

        // The flashlight owns the state; the spot and what zombies perceive follow it.
        [Test]
        public void SpotAndPerceptionFollowTheSwitch()
        {
            var previous = InputSystem.actions;
            InputSystem.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            var player = new GameObject("TestPlayer");
            var spotObject = new GameObject("TestSpot");
            try
            {
                var spot = spotObject.AddComponent<Light>();
                var flashlight = player.AddComponent<PlayerFlashlight>();
                flashlight.Configure(spot, false);
                var target = player.AddComponent<PerceptionTarget>();
                RunAwake(flashlight);
                RunAwake(target);
                Assert.That(spot.enabled || target.FlashlightOn, Is.False, "starts off");

                flashlight.SetOn(true);
                Assert.That(flashlight.IsOn && spot.enabled && target.FlashlightOn, Is.True);

                flashlight.SetOn(false);
                Assert.That(flashlight.IsOn || spot.enabled || target.FlashlightOn, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(spotObject);
                InputSystem.actions = previous;
            }
        }
    }
}
