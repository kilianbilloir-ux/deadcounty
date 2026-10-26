using System.Reflection;
using DeadCounty.Perception;
using DeadCounty.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Tests
{
    public class PlayerFlashlightTests
    {
        const string RigPath = "Assets/DeadCounty/Prefabs/PlayerRig.prefab";
        const string ActionsPath = "Assets/InputSystem_Actions.inputactions";

        static void RunAwake(Component component) =>
            component.GetType().GetMethod("Awake", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(component, null);

        [Test]
        public void SetOnSyncsFlagAndSpot()
        {
            var previous = InputSystem.actions;
            InputSystem.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            GameObject player = null;
            GameObject spotGo = null;
            try
            {
                player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
                var target = player.GetComponentInChildren<PerceptionTarget>();
                var flash = target.gameObject.AddComponent<PlayerFlashlight>();
                spotGo = new GameObject("TestSpot");
                var spot = spotGo.AddComponent<Light>();
                flash.Configure(spot, false);
                RunAwake(flash);

                flash.SetOn(true);
                Assert.That(target.FlashlightOn, Is.True);
                Assert.That(spot.enabled, Is.True);
                Assert.That(flash.IsOn, Is.True);

                flash.SetOn(false);
                Assert.That(target.FlashlightOn, Is.False);
                Assert.That(spot.enabled, Is.False);
                Assert.That(flash.IsOn, Is.False);
            }
            finally
            {
                if (player != null) Object.DestroyImmediate(player);
                if (spotGo != null) Object.DestroyImmediate(spotGo);
                InputSystem.actions = previous;
                NoiseSystem.ResetStatics();
            }
        }
    }
}
