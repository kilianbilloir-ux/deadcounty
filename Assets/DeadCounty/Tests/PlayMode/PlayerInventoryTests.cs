#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using DeadCounty.Items;
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
    // The generated rig's inventory: starting kit, eating, bandaging, dropping and picking up, overload, the screen
    // and its item detail.
    public class PlayerInventoryTests : PlayerRigTests
    {
        const string ItemsDir = "Assets/DeadCounty/Data/Items/";

        PlayerInventory inventory;
        PlayerNeeds needs;
        ThirdPersonController controller;
        ItemDefinition heavy;

        static ItemDefinition Load(string file) => AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemsDir + file + ".asset");

        VisualElement Root => Rig.GetComponent<UIDocument>().rootVisualElement;

        // "Faim 50 % → 85 %" for each effect line shown under the list.
        string[] Effects() => Root.Q("item-effects").Children()
            .Where(row => row.style.display.value == DisplayStyle.Flex)
            .Select(row => string.Join(" ", row.Query<Label>().ToList().Select(label => label.text)))
            .ToArray();

        [UnitySetUp]
        public IEnumerator SetUpInventory()
        {
            inventory = Rig.GetComponentInChildren<PlayerInventory>();
            needs = Rig.GetComponentInChildren<PlayerNeeds>();
            controller = Rig.GetComponentInChildren<ThirdPersonController>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownItems()
        {
            if (heavy != null) Object.Destroy(heavy);
            foreach (var pickup in Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include)) Object.Destroy(pickup.gameObject);
            yield return null;
        }

        [Test]
        public void StartsWithABackpackWaterAndACan()
        {
            Assert.That(inventory.Items.Count(Load("IT_SacADos")), Is.EqualTo(1));
            Assert.That(inventory.Items.Count(Load("IT_Eau")), Is.EqualTo(1));
            Assert.That(inventory.Items.Count(Load("IT_Haricots")), Is.EqualTo(1));
            Assert.That(inventory.Items.Capacity, Is.EqualTo(12f), "6 kg plus the backpack");
        }

        [Test]
        public void EatingRestoresHungerAndUsesUpTheCan()
        {
            var beans = Load("IT_Haricots");
            needs.Model.Tick(10f * 60f, false);
            Assert.That(inventory.Use(beans), Is.True);
            Assert.That(needs.Model.Hunger, Is.EqualTo(0.85f).Within(1e-3f));
            Assert.That(inventory.Items.Count(beans), Is.Zero);
            Assert.That(inventory.Use(beans), Is.False, "ate a can that was not there");
            Assert.That(inventory.Use(Load("IT_SacADos")), Is.False, "a backpack cannot be used");
        }

        [Test]
        public void ABandageHealsTwentyPoints()
        {
            var bandage = Load("IT_Bandage");
            inventory.Items.Add(bandage, 1);
            Health.Pool.Apply(50f);
            Assert.That(inventory.Use(bandage), Is.True);
            Assert.That(Health.Pool.Current, Is.EqualTo(70f));
        }

        [UnityTest]
        public IEnumerator ADroppedItemCanBePickedUpAgain()
        {
            var water = Load("IT_Eau");
            Assert.That(inventory.Drop(water), Is.True);
            Assert.That(inventory.Items.Count(water), Is.Zero);
            var pickup = Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Exclude).Single();
            Assert.That(pickup.Item, Is.EqualTo(water));
            Assert.That(Vector3.Distance(pickup.transform.position, inventory.transform.position), Is.LessThan(1f));
            Assert.That(pickup.Action, Is.EqualTo("Ramasser"));

            pickup.Interact(inventory.transform, false);
            yield return null;
            Assert.That(inventory.Items.Count(water), Is.EqualTo(1));
            Assert.That(pickup == null, "the pickup stayed in the world");
        }

        [UnityTest]
        public IEnumerator OverloadForbidsRunningAndSlowsTheWalk()
        {
            heavy = ScriptableObject.CreateInstance<ItemDefinition>();
            heavy.DisplayName = "Parpaing";
            heavy.Weight = 15f;
            inventory.Items.Add(heavy, 1);
            yield return null;
            Assert.That(controller.Encumbered);

            InputSystem.QueueStateEvent(Keyboard, new KeyboardState(Key.W, Key.LeftShift));
            yield return new WaitForSeconds(1.5f);
            Assert.That(controller.Gait, Is.EqualTo(Gait.Walk), "ran while overloaded");
            Assert.That(controller.CurrentSpeed, Is.EqualTo(controller.WalkSpeed * 0.75f).Within(0.05f));
            InputSystem.QueueStateEvent(Keyboard, new KeyboardState());
            yield return null;

            inventory.Drop(heavy);
            yield return null;
            Assert.That(controller.Encumbered, Is.False);
        }

        [UnityTest]
        public IEnumerator RespawnGivesBackTheStartingKit()
        {
            inventory.Items.Add(Load("IT_Soda"), 3);
            inventory.Drop(Load("IT_Eau"));
            Health.ApplyHit(10000f, Health.transform.position + Vector3.forward);
            var deathFlow = Rig.GetComponent<PlayerDeathFlow>();
            var start = Time.time;
            while (!deathFlow.CanRespawn && Time.time - start < 5f) yield return null;
            yield return Tap(Keyboard, new KeyboardState(Key.Space), new KeyboardState());
            Assert.That(Health.IsAlive);
            Assert.That(inventory.Items.Count(Load("IT_Soda")), Is.Zero);
            Assert.That(inventory.Items.Count(Load("IT_Eau")), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TabOpensTheScreenWhereArrowsPickAndEUses()
        {
            var screen = Rig.GetComponent<PlayerInventoryScreen>();
            var root = Rig.GetComponent<UIDocument>().rootVisualElement;
            var panel = root.Q("inventory");
            Assert.That(panel.style.display.value, Is.EqualTo(DisplayStyle.None));

            yield return Tap(Keyboard, new KeyboardState(Key.Tab), new KeyboardState());
            Assert.That(screen.IsOpen);
            Assert.That(controller.MenuOpen, "the player must stop while the inventory is open");
            Assert.That(panel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Label>("inventory-weight").text, Does.EndWith("/ 12 kg"));
            // Sorted by name: Bouteille d'eau, Conserve de haricots, Sac à dos.
            Assert.That(screen.Selected, Is.EqualTo(Load("IT_Eau")));

            yield return Tap(Keyboard, new KeyboardState(Key.DownArrow), new KeyboardState());
            Assert.That(screen.Selected, Is.EqualTo(Load("IT_Haricots")));
            needs.Model.Tick(10f * 60f, false);
            yield return Tap(Keyboard, new KeyboardState(Key.E), new KeyboardState());
            Assert.That(needs.Model.Hunger, Is.GreaterThan(0.8f), "E did not eat the selected can");
            Assert.That(inventory.Items.Count(Load("IT_Haricots")), Is.Zero);

            yield return Tap(Keyboard, new KeyboardState(Key.Escape), new KeyboardState());
            Assert.That(screen.IsOpen, Is.False);
            Assert.That(controller.MenuOpen, Is.False);
            Assert.That(panel.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator SortingByWeightPutsTheHeaviestFirst()
        {
            var screen = Rig.GetComponent<PlayerInventoryScreen>();
            screen.SetOpen(true);
            screen.SetSort(PlayerInventoryScreen.SortMode.Weight);
            yield return null;
            Assert.That(screen.Selected, Is.EqualTo(Load("IT_Eau")), "the selection must follow its item through the sort");
            screen.Select(0);
            yield return null;
            Assert.That(screen.Selected, Is.EqualTo(Load("IT_SacADos")));
            screen.SetOpen(false);
        }

        [UnityTest]
        public IEnumerator TheDetailShowsWhatUsingTheItemWouldChangeNow()
        {
            var screen = Rig.GetComponent<PlayerInventoryScreen>();
            // Hunger 0.5, thirst 1/6.
            needs.Model.Tick(10f * 60f, false);
            screen.SetOpen(true);
            screen.Select(1);
            yield return null;
            Assert.That(screen.Selected, Is.EqualTo(Load("IT_Haricots")));
            Assert.That(Root.Q<Label>("item-detail-name").text, Is.EqualTo("Conserve de haricots"));
            Assert.That(Root.Q<Label>("item-detail-meta").text, Is.EqualTo("Nourriture · 0,4 kg"));
            Assert.That(Effects(), Is.EqualTo(new[] { "Faim 50\u00A0% → 85\u00A0%" }));

            screen.Select(0);
            yield return null;
            Assert.That(Effects(), Is.EqualTo(new[] { "Soif 17\u00A0% → 67\u00A0%" }));

            needs.Model.Restore(0f, 0.5f);
            yield return null;
            Assert.That(Effects(), Is.EqualTo(new[] { "Soif 67\u00A0% → 100\u00A0%" }), "the detail must follow the gauges while open");
            screen.SetOpen(false);
        }

        [UnityTest]
        public IEnumerator UsesThatWouldBeWastedShowInGrey()
        {
            var screen = Rig.GetComponent<PlayerInventoryScreen>();
            var bandage = Load("IT_Bandage");
            inventory.Items.Add(bandage, 1);
            screen.SetOpen(true);
            // Bandage, Bouteille d'eau, Conserve de haricots, Sac à dos.
            screen.Select(0);
            yield return null;
            Assert.That(screen.Selected, Is.EqualTo(bandage));
            Assert.That(Effects(), Is.EqualTo(new[] { "Santé 100\u00A0% → 100\u00A0%", "Saignement Aucun" }));
            Assert.That(Root.Q("item-effects").Children().First().ClassListContains("item-effect--wasted"), "a bandage on a healthy player is wasted");

            Health.Pool.Apply(50f);
            yield return null;
            Assert.That(Effects()[0], Is.EqualTo("Santé 50\u00A0% → 70\u00A0%"));
            Assert.That(Root.Q("item-effects").Children().First().ClassListContains("item-effect--wasted"), Is.False);

            screen.Select(3);
            yield return null;
            Assert.That(Effects(), Is.EqualTo(new[] { "Capacité +6 kg" }));
            screen.SetOpen(false);
        }

        [UnityTest]
        public IEnumerator RowsAndDetailShowTheItemPicture()
        {
            var water = Load("IT_Eau");
            Assert.That(water.Icon, Is.Not.Null, "the builder renders a picture for every item");
            var screen = Rig.GetComponent<PlayerInventoryScreen>();
            screen.SetOpen(true);
            yield return null;
            Assert.That(screen.Selected, Is.EqualTo(water));
            var firstRow = Root.Q("item-list").Q(className: "item-row");
            Assert.That(firstRow.Q(className: "item-icon").style.backgroundImage.value.texture, Is.EqualTo(water.Icon));
            Assert.That(Root.Q("item-detail-icon").style.backgroundImage.value.texture, Is.EqualTo(water.Icon));
            screen.SetOpen(false);
        }
    }
}
#endif
