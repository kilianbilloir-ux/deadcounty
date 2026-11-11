using System.Collections.Generic;
using DeadCounty.Items;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    public class LootTableTests
    {
        ItemDefinition water, soda;
        LootTable table;

        [SetUp]
        public void CreateTable()
        {
            water = ScriptableObject.CreateInstance<ItemDefinition>();
            soda = ScriptableObject.CreateInstance<ItemDefinition>();
            table = ScriptableObject.CreateInstance<LootTable>();
            table.Entries = new[]
            {
                new LootTable.Entry { Item = water, Weight = 3f, Min = 1, Max = 1 },
                new LootTable.Entry { Item = soda, Weight = 1f, Min = 1, Max = 1 },
            };
            table.MinDraws = 0;
            table.MaxDraws = 2;
        }

        [TearDown]
        public void DestroyTable()
        {
            Object.DestroyImmediate(water);
            Object.DestroyImmediate(soda);
            Object.DestroyImmediate(table);
        }

        static int Total(List<ItemStack> stacks)
        {
            var total = 0;
            foreach (var stack in stacks) total += stack.Count;
            return total;
        }

        [Test]
        public void TheSameSeedDrawsTheSameItems()
        {
            var first = new List<ItemStack>();
            var second = new List<ItemStack>();
            table.Roll(new System.Random(42), first);
            table.Roll(new System.Random(42), second);
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void DrawsStayWithinTheBoundsAndFollowTheWeights()
        {
            int empty = 0, waters = 0, sodas = 0;
            for (var seed = 0; seed < 2000; seed++)
            {
                var found = new List<ItemStack>();
                table.Roll(new System.Random(seed), found);
                Assert.That(found.Count, Is.LessThanOrEqualTo(2), "equal items must stack");
                Assert.That(Total(found), Is.LessThanOrEqualTo(2));
                if (found.Count == 0) empty++;
                foreach (var stack in found)
                {
                    if (stack.Item == water) waters += stack.Count;
                    else sodas += stack.Count;
                }
            }
            Assert.That(empty, Is.InRange(580, 750), "a third of the searches draw nothing");
            Assert.That(waters / (float)(waters + sodas), Is.InRange(0.68f, 0.82f), "water is three times as likely as soda");
        }

        [Test]
        public void EachDrawGivesBetweenMinAndMaxOfTheItem()
        {
            table.Entries = new[] { new LootTable.Entry { Item = water, Weight = 1f, Min = 2, Max = 3 } };
            table.MinDraws = 1;
            table.MaxDraws = 1;
            var counts = new HashSet<int>();
            for (var seed = 0; seed < 200; seed++)
            {
                var found = new List<ItemStack>();
                table.Roll(new System.Random(seed), found);
                counts.Add(Total(found));
            }
            Assert.That(counts, Is.EquivalentTo(new[] { 2, 3 }));
        }

        [Test]
        public void ATableWithoutWeightGivesNothing()
        {
            table.Entries = new[] { new LootTable.Entry { Item = water, Weight = 0f, Min = 1, Max = 1 } };
            table.MinDraws = 2;
            var found = new List<ItemStack>();
            table.Roll(new System.Random(1), found);
            Assert.That(found, Is.Empty);
        }
    }
}
