#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using DeadCounty.Items;
using DeadCounty.Perception;
using DeadCounty.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DeadCounty.Tests
{
    // Searching a cupboard in front of the generated rig: timing, hurrying, cancelling, what is found.
    public class LootContainerTests : PlayerRigTests
    {
        LootContainer cupboard;
        LootTable table;
        ItemDefinition water;
        PlayerInventory inventory;
        PlayerInteractor interactor;

        [UnitySetUp]
        public IEnumerator SetUpCupboard()
        {
            water = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/DeadCounty/Data/Items/IT_Eau.asset");
            inventory = Rig.GetComponentInChildren<PlayerInventory>();
            interactor = Rig.GetComponentInChildren<PlayerInteractor>();
            table = ScriptableObject.CreateInstance<LootTable>();
            table.Entries = new[] { new LootTable.Entry { Item = water, Weight = 1f, Min = 1, Max = 1 } };
            table.MinDraws = 1;
            table.MaxDraws = 1;

            // 1.2 m in front of the player, waist high.
            var player = Health.transform;
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.SetActive(false);
            box.transform.position = player.position + player.forward * 1.2f + Vector3.up * 0.45f;
            box.transform.rotation = player.rotation;
            box.transform.localScale = new Vector3(1f, 0.9f, 0.5f);
            cupboard = box.AddComponent<LootContainer>();
            typeof(LootContainer).GetField("table", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cupboard, table);
            box.SetActive(true);
            Physics.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownCupboard()
        {
            Object.Destroy(cupboard.gameObject);
            Object.Destroy(table);
            yield return null;
        }

        static NoiseEvent LatestNoise()
        {
            var events = new NoiseEvent[1];
            Assert.That(NoiseSystem.Buffer.CopySince(NoiseSystem.Buffer.LatestId - 1, events), Is.EqualTo(1), "no noise");
            return events[0];
        }

        [UnityTest]
        public IEnumerator SearchingBendsTheBodyOverToDig()
        {
            var controller = Rig.GetComponentInChildren<ThirdPersonController>();
            yield return Tap(Keyboard, new KeyboardState(Key.E), new KeyboardState());
            Assert.That(interactor.Busy);
            Assert.That(controller.Pose.Rummage, Is.EqualTo(1f));
            Assert.That(controller.Pose.Hunch, Is.GreaterThan(15f));
            Assert.That(controller.Pose.ArmsForward, Is.GreaterThan(0.3f));

            yield return new WaitForSeconds(3f);
            Assert.That(interactor.Busy, Is.False);
            Assert.That(controller.Pose.Rummage, Is.Zero, "the search is over");
        }

        [UnityTest]
        public IEnumerator SearchingTakesTwoAndAHalfSecondsThenGivesEverything()
        {
            Assert.That(interactor.Current, Is.EqualTo(cupboard));
            Assert.That(cupboard.Action, Is.EqualTo("Fouiller"));
            var noises = NoiseSystem.Buffer.LatestId;
            yield return Tap(Keyboard, new KeyboardState(Key.E), new KeyboardState());
            yield return new WaitForSeconds(1f);
            Assert.That(interactor.Busy);
            Assert.That(interactor.UseProgress, Is.InRange(0.3f, 0.5f));
            Assert.That(cupboard.Searched, Is.False);
            var heard = new NoiseEvent[16];
            var count = NoiseSystem.Buffer.CopySince(noises, heard);
            for (var i = 0; i < count; i++) Assert.That(heard[i].Kind, Is.Not.EqualTo(NoiseKind.Impact), "a calm search must be silent");

            yield return new WaitForSeconds(1.8f);
            Assert.That(cupboard.Searched);
            Assert.That(interactor.Busy, Is.False);
            Assert.That(inventory.Items.Count(water), Is.EqualTo(2), "the starting bottle and the one found");
            Assert.That(cupboard.Action, Is.Null);
            Assert.That(cupboard.DisplayName, Does.EndWith("(fouillé)"));
            var toast = Rig.GetComponent<UIDocument>().rootVisualElement.Q<Label>("loot-toast");
            Assert.That(toast.text, Is.EqualTo("Trouvé : Bouteille d'eau"));
        }

        [UnityTest]
        public IEnumerator MovingCancelsTheSearch()
        {
            yield return Tap(Keyboard, new KeyboardState(Key.E), new KeyboardState());
            yield return new WaitForSeconds(0.5f);
            Assert.That(interactor.Busy);
            yield return Tap(Keyboard, new KeyboardState(Key.S), new KeyboardState());
            Assert.That(interactor.Busy, Is.False, "walking away kept searching");
            yield return new WaitForSeconds(2.5f);
            Assert.That(cupboard.Searched, Is.False);
        }

        [UnityTest]
        public IEnumerator HurryingSearchesInASecondButIsHeard()
        {
            yield return Tap(Keyboard, new KeyboardState(Key.E, Key.LeftShift), new KeyboardState());
            var noise = LatestNoise();
            Assert.That(noise.Radius, Is.EqualTo(8f));
            Assert.That(noise.Kind, Is.EqualTo(NoiseKind.Impact));
            yield return new WaitForSeconds(1.2f);
            Assert.That(cupboard.Searched);
        }

        [UnityTest]
        public IEnumerator RespawningFillsTheFurnitureAgain()
        {
            cupboard.Interact(inventory.transform, false);
            Assert.That(cupboard.Searched);
            Health.ApplyHit(10000f, Health.transform.position + Vector3.forward);
            var deathFlow = Rig.GetComponent<PlayerDeathFlow>();
            var start = Time.time;
            while (!deathFlow.CanRespawn && Time.time - start < 5f) yield return null;
            yield return Tap(Keyboard, new KeyboardState(Key.Space), new KeyboardState());
            Assert.That(Health.IsAlive);
            Assert.That(cupboard.Searched, Is.False);
        }
    }
}
#endif
