using System.Collections.Generic;
using DeadCounty.Items;
using DeadCounty.Player;
using UnityEditor;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        const string ItemsDir = DataDir + "/Items";

        // The demo items (GDD 4 and 8.4): cans restore 35 % hunger, fish 25 %; water 50 % thirst, soda 30 % and a
        // little hunger; the backpack brings the 6 kg base capacity to 12 kg.
        static readonly (string File, string Name, ItemCategory Category, float Weight, string Verb, float Hunger, float Thirst, float Health, float Capacity)[] ItemSpecs =
        {
            ("IT_Haricots", "Conserve de haricots", ItemCategory.Food, 0.4f, "Manger", 0.35f, 0f, 0f, 0f),
            ("IT_Tomates", "Conserve de tomates", ItemCategory.Food, 0.4f, "Manger", 0.35f, 0f, 0f, 0f),
            ("IT_Soupe", "Conserve de soupe", ItemCategory.Food, 0.4f, "Manger", 0.35f, 0f, 0f, 0f),
            ("IT_Mais", "Conserve de maïs", ItemCategory.Food, 0.35f, "Manger", 0.35f, 0f, 0f, 0f),
            ("IT_PetitsPois", "Conserve de petits pois", ItemCategory.Food, 0.35f, "Manger", 0.35f, 0f, 0f, 0f),
            ("IT_Thon", "Thon en boîte", ItemCategory.Food, 0.2f, "Manger", 0.25f, 0f, 0f, 0f),
            ("IT_Sardines", "Sardines", ItemCategory.Food, 0.15f, "Manger", 0.25f, 0f, 0f, 0f),
            ("IT_Eau", "Bouteille d'eau", ItemCategory.Drink, 0.55f, "Boire", 0f, 0.5f, 0f, 0f),
            ("IT_Soda", "Soda", ItemCategory.Drink, 0.35f, "Boire", 0.05f, 0.3f, 0f, 0f),
            ("IT_Bandage", "Bandage", ItemCategory.Medical, 0.05f, "Se bander", 0f, 0f, 20f, 0f),
            ("IT_SacADos", "Sac à dos", ItemCategory.Gear, 1f, "", 0f, 0f, 0f, 6f),
        };

        static readonly (string File, int Count)[] StartingKit = { ("IT_SacADos", 1), ("IT_Eau", 1), ("IT_Haricots", 1) };

        // Rebuilt in place so the GUIDs referenced by the prefab and the loot stay stable.
        static Dictionary<string, ItemDefinition> EnsureItems()
        {
            System.IO.Directory.CreateDirectory(ItemsDir);
            var items = new Dictionary<string, ItemDefinition>();
            foreach (var spec in ItemSpecs)
            {
                var item = LoadOrCreate<ItemDefinition>($"{ItemsDir}/{spec.File}.asset");
                item.DisplayName = spec.Name;
                item.Category = spec.Category;
                item.Weight = spec.Weight;
                item.UseVerb = spec.Verb;
                item.Hunger = spec.Hunger;
                item.Thirst = spec.Thirst;
                item.Health = spec.Health;
                item.CapacityBonus = spec.Capacity;
                // The bandage, the only care item of the demo, stops bleeding (GDD 8.4).
                item.StopsBleeding = spec.Category == ItemCategory.Medical;
                EditorUtility.SetDirty(item);
                items[spec.File] = item;
            }
            return items;
        }

        // Only an empty list is filled, so the kit can be tuned in the Inspector.
        static bool AssignStartingItems(GameObject player)
        {
            var so = new SerializedObject(player.GetComponent<PlayerInventory>());
            var list = so.FindProperty("startingItems");
            if (list.arraySize > 0) return false;
            var items = EnsureItems();
            list.arraySize = StartingKit.Length;
            for (var i = 0; i < StartingKit.Length; i++)
            {
                var entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Item").objectReferenceValue = items[StartingKit[i].File];
                entry.FindPropertyRelative("Count").intValue = StartingKit[i].Count;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
