#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using DeadCounty.Items;
using DeadCounty.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DeadCounty.Tests
{
    // The list of items just taken, at the bottom right of the generated rig's HUD.
    public class PlayerPickupFeedTests : PlayerRigTests
    {
        PlayerInventory inventory;
        ItemDefinition water, beans;
        VisualElement feed;

        static ItemDefinition Load(string file) => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/DeadCounty/Data/Items/{file}.asset");

        [UnitySetUp]
        public IEnumerator SetUpFeed()
        {
            inventory = Rig.GetComponentInChildren<PlayerInventory>();
            water = Load("IT_Eau");
            beans = Load("IT_Haricots");
            feed = Rig.GetComponent<UIDocument>().rootVisualElement.Q("pickup-feed");
            Assert.That(feed, Is.Not.Null, "the HUD has no 'pickup-feed'");
            yield return null;
        }

        VisualElement[] Rows() => feed.Children().Where(row => row.style.display.value == DisplayStyle.Flex).ToArray();

        // "Conserve de haricots ×2", top to bottom.
        string[] Lines() => Rows().Select(row =>
        {
            var count = row.Q<Label>(className: "pickup-count");
            var name = row.Q<Label>(className: "pickup-name").text;
            return count.style.display.value == DisplayStyle.Flex ? $"{name} {count.text}" : name;
        }).ToArray();

        [UnityTest]
        public IEnumerator EachItemTakenGetsALineWithItsPictureAndCount()
        {
            Assert.That(Lines(), Is.Empty, "nothing taken yet");
            inventory.Receive(new[] { new ItemStack(water, 1), new ItemStack(beans, 2) });
            yield return new WaitForSeconds(0.5f);
            Assert.That(Lines(), Is.EqualTo(new[] { "Bouteille d'eau", "Conserve de haricots ×2" }), "the newest goes at the bottom");
            Assert.That(Rows().All(row => row.style.opacity.value > 0.99f), "the lines must have faded in");
            Assert.That(water.Icon, Is.Not.Null, "the builder renders a picture for every item");
            Assert.That(Rows()[0].Q(className: "pickup-icon").style.backgroundImage.value.texture, Is.EqualTo(water.Icon));
        }

        [UnityTest]
        public IEnumerator TakingMoreOfAListedItemAddsToItsLine()
        {
            inventory.Receive(new[] { new ItemStack(water, 1) });
            inventory.Receive(new[] { new ItemStack(beans, 1) });
            yield return new WaitForSeconds(1f);
            inventory.Receive(new[] { new ItemStack(water, 2) });
            yield return null;
            Assert.That(Lines(), Is.EqualTo(new[] { "Conserve de haricots", "Bouteille d'eau ×3" }), "one line per kind of item, moved to the bottom");
        }

        [UnityTest]
        public IEnumerator AnEmptySearchSaysSo()
        {
            inventory.Receive(new ItemStack[0]);
            yield return null;
            Assert.That(Lines(), Is.EqualTo(new[] { "Rien trouvé" }));
            Assert.That(Rows()[0].ClassListContains("pickup--empty"), "an empty search reads in grey, without a picture");
        }

        [UnityTest]
        public IEnumerator LinesFadeAFewSecondsLater()
        {
            inventory.Receive(new[] { new ItemStack(water, 1) });
            yield return new WaitForSeconds(3f);
            Assert.That(Lines(), Has.Length.EqualTo(1));
            yield return new WaitForSeconds(2f);
            Assert.That(Lines(), Is.Empty, "the line must be gone after its time and its fade");
        }

        [UnityTest]
        public IEnumerator TheInventoryHidesTheFeed()
        {
            inventory.Receive(new[] { new ItemStack(water, 1) });
            Rig.GetComponent<PlayerInventoryScreen>().SetOpen(true);
            yield return null;
            Assert.That(feed.style.display.value, Is.EqualTo(DisplayStyle.None), "the open inventory lists everything already");
            Rig.GetComponent<PlayerInventoryScreen>().SetOpen(false);
            yield return null;
            Assert.That(feed.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }
    }
}
#endif
