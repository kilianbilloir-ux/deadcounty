using System.Reflection;
using DeadCounty.Perception;
using DeadCounty.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Tests
{
    public class PlayerNoiseEmitterTests
    {
        const string RigPath = "Assets/DeadCounty/Prefabs/PlayerRig.prefab";
        const string ActionsPath = "Assets/InputSystem_Actions.inputactions";

        static void SetField(object target, string field, float value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        // EditMode never runs Awake: invoke the lifecycle explicitly, dependencies first.
        static void RunAwake(Component component) =>
            component.GetType().GetMethod("Awake", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(component, null);

        static void CallUpdate(Object target) =>
            target.GetType().GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);

        GameObject InstantiateRig(out ThirdPersonController controller, out PlayerNoiseEmitter emitter)
        {
            var previous = InputSystem.actions;
            InputSystem.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
            try
            {
                controller = player.GetComponentInChildren<ThirdPersonController>();
                var stamina = player.GetComponentInChildren<PlayerStamina>();
                var health = player.GetComponentInChildren<PlayerHealth>();
                var target = player.GetComponentInChildren<PerceptionTarget>();
                emitter = player.GetComponentInChildren<PlayerNoiseEmitter>();
                RunAwake(controller);
                RunAwake(stamina);
                RunAwake(health);
                RunAwake(target);
                RunAwake(emitter);
                target.Visible = true;
                return player;
            }
            catch
            {
                Object.DestroyImmediate(player);
                InputSystem.actions = previous;
                throw;
            }
        }

        [Test]
        public void HardLandingPeakIsEmittedAsLanding()
        {
            var previous = InputSystem.actions;
            GameObject player = null;
            try
            {
                player = InstantiateRig(out var controller, out var emitter);
                var since = NoiseSystem.Buffer.LatestId;
                SetField(controller, "landingNoiseTimer", 0.3f);
                SetField(emitter, "loudest", 6f);
                SetField(emitter, "stepTimer", 0.25f);
                CallUpdate(emitter);

                var events = new NoiseEvent[8];
                Assert.That(NoiseSystem.Buffer.CopySince(since, events), Is.EqualTo(1));
                Assert.That(events[0].Kind, Is.EqualTo(NoiseKind.Landing));
                Assert.That(events[0].Radius, Is.EqualTo(6f));
            }
            finally
            {
                if (player != null) Object.DestroyImmediate(player);
                InputSystem.actions = previous;
                NoiseSystem.ResetStatics();
            }
        }

        [Test]
        public void WalkingPeakIsEmittedAsFootstep()
        {
            var previous = InputSystem.actions;
            GameObject player = null;
            try
            {
                player = InstantiateRig(out var controller, out var emitter);
                var since = NoiseSystem.Buffer.LatestId;
                SetField(controller, "landingNoiseTimer", 0f);
                SetField(emitter, "loudest", 3f);
                SetField(emitter, "stepTimer", 0.25f);
                CallUpdate(emitter);

                var events = new NoiseEvent[8];
                Assert.That(NoiseSystem.Buffer.CopySince(since, events), Is.EqualTo(1));
                Assert.That(events[0].Kind, Is.EqualTo(NoiseKind.Footstep));
                Assert.That(events[0].Radius, Is.EqualTo(3f));
            }
            finally
            {
                if (player != null) Object.DestroyImmediate(player);
                InputSystem.actions = previous;
                NoiseSystem.ResetStatics();
            }
        }
    }
}
