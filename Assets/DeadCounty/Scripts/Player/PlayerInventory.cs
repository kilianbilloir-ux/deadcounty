using System;
using System.Collections.Generic;
using DeadCounty.Items;
using UnityEngine;

namespace DeadCounty.Player
{
    // Carrying more than the capacity encumbers the controller (GDD 8.5); respawning restores the starting items.
    // One carried weapon may be in hand: using it takes it out or puts it away, and it stays in the inventory.
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
        // The weapon in hand, null with bare hands.
        public ItemDefinition Weapon { get; private set; }
        public event Action WeaponChanged;

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
            // Dropped, or lost with everything else on respawn.
            Items.Changed += () =>
            {
                if (Weapon != null && Items.Count(Weapon) == 0) SetWeapon(null);
            };
            AddStartingItems();
        }

        void Update() => controller.Encumbered = Items.Overloaded;

        // False when the item cannot be used, is not carried or the player is dead.
        public bool Use(ItemDefinition item)
        {
            if (!item.Usable || !health.IsAlive) return false;
            if (item.IsWeapon)
            {
                if (Items.Count(item) == 0) return false;
                SetWeapon(Weapon == item ? null : item);
                return true;
            }
            if (!Items.Remove(item)) return false;
            needs.Model.Restore(item.Hunger, item.Thirst);
            health.Heal(item.Health);
            if (item.StopsBleeding) health.StopBleeding();
            Used?.Invoke(item);
            return true;
        }

        // What using the item would do now: a weapon in hand is put away.
        public string UseVerb(ItemDefinition item) => item == Weapon ? "Ranger" : item.UseVerb;

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
            // Empty hands take the first weapon found.
            if (Weapon == null)
            {
                foreach (var stack in items)
                {
                    if (!stack.Item.IsWeapon) continue;
                    SetWeapon(stack.Item);
                    break;
                }
            }
            Received?.Invoke(items);
        }

        public void ResetFull()
        {
            Items.Clear();
            AddStartingItems();
        }

        void SetWeapon(ItemDefinition weapon)
        {
            if (weapon == Weapon) return;
            Weapon = weapon;
            WeaponChanged?.Invoke();
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
