using System;
using System.Collections.Generic;

namespace DeadCounty.Items
{
    public readonly struct ItemStack
    {
        public readonly ItemDefinition Item;
        public readonly int Count;

        public ItemStack(ItemDefinition item, int count)
        {
            Item = item;
            Count = count;
        }

        public float Weight => Item.Weight * Count;
    }

    // Items carried by the player, one stack per kind, in pickup order. The weight may exceed the capacity: the
    // player is then overloaded and slowed (GDD 8.5). A carried container (backpack) raises the capacity.
    public class Inventory
    {
        readonly List<ItemStack> stacks = new();

        public Inventory(float baseCapacity)
        {
            if (!(baseCapacity > 0f)) throw new ArgumentOutOfRangeException(nameof(baseCapacity), baseCapacity, "Inventory needs a positive capacity");
            BaseCapacity = baseCapacity;
            Capacity = baseCapacity;
        }

        public float BaseCapacity { get; }
        public IReadOnlyList<ItemStack> Stacks => stacks;
        public float Weight { get; private set; }
        public float Capacity { get; private set; }
        public bool Overloaded => Weight > Capacity;

        public event Action Changed;

        public int Count(ItemDefinition item)
        {
            var index = IndexOf(item);
            return index < 0 ? 0 : stacks[index].Count;
        }

        public void Add(ItemDefinition item, int count = 1)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Add needs a positive count");
            var index = IndexOf(item);
            if (index < 0) stacks.Add(new ItemStack(item, count));
            else stacks[index] = new ItemStack(item, stacks[index].Count + count);
            Refresh();
        }

        // False, and nothing removed, when fewer than `count` are carried.
        public bool Remove(ItemDefinition item, int count = 1)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Remove needs a positive count");
            var index = IndexOf(item);
            if (index < 0 || stacks[index].Count < count) return false;
            var left = stacks[index].Count - count;
            if (left == 0) stacks.RemoveAt(index);
            else stacks[index] = new ItemStack(item, left);
            Refresh();
            return true;
        }

        public void Clear()
        {
            stacks.Clear();
            Refresh();
        }

        int IndexOf(ItemDefinition item)
        {
            for (var i = 0; i < stacks.Count; i++)
                if (stacks[i].Item == item) return i;
            return -1;
        }

        void Refresh()
        {
            var weight = 0f;
            var bonus = 0f;
            foreach (var stack in stacks)
            {
                weight += stack.Weight;
                bonus = Math.Max(bonus, stack.Item.CapacityBonus);
            }
            Weight = weight;
            Capacity = BaseCapacity + bonus;
            Changed?.Invoke();
        }
    }
}
