using System;
using System.Collections.Generic;
using DeadCounty.Items;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadCounty.Tests
{
    public class InventoryTests
    {
        readonly List<Object> owned = new();

        [TearDown]
        public void DestroyItems()
        {
            foreach (var item in owned) Object.DestroyImmediate(item);
            owned.Clear();
        }

        ItemDefinition Item(string name, float weight, float capacityBonus = 0f)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.DisplayName = name;
            item.Weight = weight;
            item.CapacityBonus = capacityBonus;
            owned.Add(item);
            return item;
        }

        [Test]
        public void ItemsStackByKindAndAddUpTheirWeight()
        {
            var inventory = new Inventory(6f);
            var can = Item("Conserve", 0.4f);
            var water = Item("Eau", 0.55f);
            inventory.Add(can, 2);
            inventory.Add(water);
            inventory.Add(can);
            Assert.That(inventory.Stacks.Count, Is.EqualTo(2));
            Assert.That(inventory.Count(can), Is.EqualTo(3));
            Assert.That(inventory.Weight, Is.EqualTo(1.75f).Within(1e-4f));
            Assert.That(inventory.Overloaded, Is.False);
        }

        [Test]
        public void RemovingTakesFromTheStackAndDropsItWhenEmpty()
        {
            var inventory = new Inventory(6f);
            var can = Item("Conserve", 0.4f);
            inventory.Add(can, 2);
            Assert.That(inventory.Remove(can, 3), Is.False, "removed more than carried");
            Assert.That(inventory.Count(can), Is.EqualTo(2));
            Assert.That(inventory.Remove(can), Is.True);
            Assert.That(inventory.Remove(can), Is.True);
            Assert.That(inventory.Stacks, Is.Empty);
            Assert.That(inventory.Weight, Is.Zero);
            Assert.That(inventory.Remove(can), Is.False);
        }

        [Test]
        public void OnlyTheBestBackpackRaisesTheCapacity()
        {
            var inventory = new Inventory(6f);
            Assert.That(inventory.Capacity, Is.EqualTo(6f));
            var backpack = Item("Sac", 1f, 6f);
            inventory.Add(backpack, 2);
            Assert.That(inventory.Capacity, Is.EqualTo(12f));
            inventory.Remove(backpack, 2);
            Assert.That(inventory.Capacity, Is.EqualTo(6f));
        }

        [Test]
        public void CarryingMoreThanTheCapacityOverloads()
        {
            var inventory = new Inventory(6f);
            var brick = Item("Brique", 2f);
            inventory.Add(brick, 3);
            Assert.That(inventory.Overloaded, Is.False, "exactly at capacity");
            inventory.Add(brick);
            Assert.That(inventory.Overloaded);
        }

        [Test]
        public void EveryChangeIsAnnounced()
        {
            var inventory = new Inventory(6f);
            var can = Item("Conserve", 0.4f);
            var changes = 0;
            inventory.Changed += () => changes++;
            inventory.Add(can);
            inventory.Remove(can);
            inventory.Remove(can);
            inventory.Clear();
            Assert.That(changes, Is.EqualTo(3), "a failed remove is no change");
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Inventory(0f));
            var inventory = new Inventory(6f);
            Assert.Throws<ArgumentNullException>(() => inventory.Add(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.Add(Item("Conserve", 0.4f), 0));
        }
    }
}
