using System;
using System.Collections.Generic;
using DeadCounty.Items;
using UnityEngine;

namespace DeadCounty.Player
{
    // The player's items: using one applies its effect on needs and health, dropping one leaves it on the ground,
    // carrying more than the capacity encumbers the controller. Respawning starts again from the starting items.
    [RequireComponent(typeof(ThirdPersonController), typeof(PlayerHealth), typeof(PlayerNeeds))]
    public class PlayerInventory : MonoBehaviour, IItemReceiver
    {
        const float DropDistance = 0.6f;

        [Serializable]
        public struct StartingItem
        {
            public ItemDefinition Item;
            [Min(1)] public int Count;
        }

        [Tooltip("Carrying capacity without a backpack (kg).")]
        [SerializeField] float baseCapacity = 6f;
        [SerializeField] StartingItem[] startingItems = Array.Empty<StartingItem>();

        ThirdPersonController controller;
        PlayerHealth health;
        PlayerNeeds needs;

        public Inventory Items { get; private set; }

        // Items just found or picked up, for the HUD; empty after searching an empty piece of furniture.
        public event Action<IReadOnlyList<ItemStack>> Received;
        public event Action<ItemDefinition> Used;
        public event Action<ItemDefinition> Dropped;

        void OnValidate()
        {
            if (!(baseCapacity > 0f)) baseCapacity = 6f;
        }

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            health = GetComponent<PlayerHealth>();
            needs = GetComponent<PlayerNeeds>();
            Items = new Inventory(baseCapacity);
            AddStartingItems();
        }

        void Update() => controller.Encumbered = Items.Overloaded;

        // False when the item cannot be used, is not carried or the player is dead.
        public bool Use(ItemDefinition item)
        {
            if (!item.Usable || !health.IsAlive || !Items.Remove(item)) return false;
            needs.Model.Restore(item.Hunger, item.Thirst);
            health.Heal(item.Health);
            if (item.StopsBleeding) health.StopBleeding();
            Used?.Invoke(item);
            return true;
        }

        public bool Drop(ItemDefinition item)
        {
            if (!Items.Remove(item)) return false;
            var forward = transform.forward;
            WorldItem.Spawn(item, 1, transform.position + forward * DropDistance, Quaternion.LookRotation(forward));
            Dropped?.Invoke(item);
            return true;
        }

        public void Receive(IReadOnlyList<ItemStack> items)
        {
            foreach (var stack in items) Items.Add(stack.Item, stack.Count);
            Received?.Invoke(items);
        }

        public void ResetFull()
        {
            Items.Clear();
            AddStartingItems();
        }

        void AddStartingItems()
        {
            foreach (var start in startingItems)
            {
                if (start.Item == null) throw new InvalidOperationException($"PlayerInventory on '{name}' has an empty starting item");
                Items.Add(start.Item, start.Count);
            }
        }
    }
}
