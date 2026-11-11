using UnityEngine;

namespace DeadCounty.Items
{
    public enum ItemCategory { Food, Drink, Medical, Gear }

    // One kind of item (GDD 8.5): what it weighs and what using it does. Need effects are shares of a full gauge
    // (0.35 restores 35 %), health effects are hit points. Read-only at runtime.
    [CreateAssetMenu(menuName = "DeadCounty/Item", fileName = "IT_")]
    public class ItemDefinition : ScriptableObject
    {
        public string DisplayName;
        public ItemCategory Category;
        [Min(0f)] public float Weight;

        [Header("Use")]
        [Tooltip("Verb of the use action (Manger, Boire); empty when the item cannot be used.")]
        public string UseVerb;
        [Range(0f, 1f)] public float Hunger;
        [Range(0f, 1f)] public float Thirst;
        [Min(0f)] public float Health;
        public bool StopsBleeding;

        [Tooltip("Carrying capacity added while the item is carried (backpack), in kg; only the largest counts.")]
        [Min(0f)] public float CapacityBonus;

        public bool Usable => !string.IsNullOrEmpty(UseVerb);

        public static string CategoryName(ItemCategory category) => category switch
        {
            ItemCategory.Food => "Nourriture",
            ItemCategory.Drink => "Boisson",
            ItemCategory.Medical => "Soin",
            _ => "Équipement",
        };
    }
}
