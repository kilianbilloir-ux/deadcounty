using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadCounty.Items
{
    // What a kind of furniture holds in a kind of room (GDD 8.6): a few weighted draws among the entries.
    [CreateAssetMenu(menuName = "DeadCounty/Loot Table", fileName = "LT_")]
    public class LootTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ItemDefinition Item;
            [Min(0f)] public float Weight;
            [Min(1)] public int Min;
            [Min(1)] public int Max;
        }

        public Entry[] Entries = Array.Empty<Entry>();
        [Tooltip("Draws per search, picked between the two bounds; 0 leaves the furniture empty.")]
        [Min(0)] public int MinDraws;
        [Min(0)] public int MaxDraws = 1;

        // Adds the drawn items to `into`, stacking equal items.
        public void Roll(System.Random random, List<ItemStack> into)
        {
            var total = 0f;
            foreach (var entry in Entries) total += entry.Weight;
            if (total <= 0f) return;
            var draws = random.Next(MinDraws, Math.Max(MinDraws, MaxDraws) + 1);
            for (var i = 0; i < draws; i++)
            {
                var entry = Pick((float)random.NextDouble() * total);
                Stack(into, entry.Item, random.Next(entry.Min, Math.Max(entry.Min, entry.Max) + 1));
            }
        }

        Entry Pick(float point)
        {
            foreach (var entry in Entries)
            {
                if (point < entry.Weight) return entry;
                point -= entry.Weight;
            }
            return Entries[^1];
        }

        public static void Stack(List<ItemStack> stacks, ItemDefinition item, int count)
        {
            for (var i = 0; i < stacks.Count; i++)
            {
                if (stacks[i].Item != item) continue;
                stacks[i] = new ItemStack(item, stacks[i].Count + count);
                return;
            }
            stacks.Add(new ItemStack(item, count));
        }
    }
}
