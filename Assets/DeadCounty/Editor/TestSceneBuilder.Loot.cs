using System.Collections.Generic;
using System.Linq;
using DeadCounty.Items;
using UnityEditor;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        const string LootDir = DataDir + "/Loot";

        static readonly (string Item, float Weight)[] Cans =
        {
            ("IT_Haricots", 2f), ("IT_Tomates", 2f), ("IT_Soupe", 2f), ("IT_Mais", 2f), ("IT_PetitsPois", 2f), ("IT_Thon", 1f), ("IT_Sardines", 1f),
        };

        // Loot by room and furniture (GDD 8.6): kitchens feed, fridges water, bathrooms bandage; the shops are the
        // richest. Every house holds about two meals and one or two drinks.
        static readonly (string File, int MinDraws, int MaxDraws, (string Item, float Weight)[] Entries)[] LootSpecs =
        {
            ("LT_Cuisine_Placard", 0, 2, Cans.Append(("IT_Soda", 1f)).ToArray()),
            ("LT_Cuisine_Frigo", 0, 2, new[] { ("IT_Eau", 3f), ("IT_Soda", 2f) }),
            ("LT_SalleDeBain", 0, 1, new[] { ("IT_Bandage", 1f) }),
            ("LT_Chambre", 0, 1, new[] { ("IT_Bandage", 1f), ("IT_Soda", 1f), ("IT_Sardines", 1f) }),
            ("LT_Salon", 0, 1, new[] { ("IT_Soda", 1f), ("IT_Bandage", 1f) }),
            ("LT_Magasin_Rayon", 1, 3, Cans.Append(("IT_Eau", 2f)).Append(("IT_Soda", 2f)).ToArray()),
            ("LT_Magasin_Comptoir", 0, 2, new[] { ("IT_Soda", 2f), ("IT_Eau", 1f), ("IT_Bandage", 1f) }),
            ("LT_Atelier", 0, 1, new[] { ("IT_Eau", 1f), ("IT_Bandage", 1f) }),
        };

        static readonly Dictionary<string, LootTable> LootTables = new();
        static int lootSeed;

        // Rebuilt in place so the GUIDs referenced by the scene stay stable.
        static void EnsureLootTables(Dictionary<string, ItemDefinition> items)
        {
            System.IO.Directory.CreateDirectory(LootDir);
            LootTables.Clear();
            lootSeed = 0;
            foreach (var spec in LootSpecs)
            {
                var table = LoadOrCreate<LootTable>($"{LootDir}/{spec.File}.asset");
                table.MinDraws = spec.MinDraws;
                table.MaxDraws = spec.MaxDraws;
                table.Entries = spec.Entries
                    .Select(e => new LootTable.Entry { Item = items[e.Item], Weight = e.Weight, Min = 1, Max = 1 })
                    .ToArray();
                EditorUtility.SetDirty(table);
                LootTables[spec.File] = table;
            }
        }

        // Each piece gets its own seed, so two cupboards never hold the same draw.
        static void Searchable(GameObject furniture, string displayName, string table)
        {
            var so = new SerializedObject(furniture.AddComponent<LootContainer>());
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("table").objectReferenceValue = LootTables[table];
            so.FindProperty("seed").intValue = ++lootSeed * 7919;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // GDD 8.6: the house nearest to the spawn always has a bandage, so a bad draw cannot ruin a short session.
        static void GuaranteeStartingLoot(Transform environment)
        {
            var cabinet = environment.GetComponentsInChildren<LootContainer>()
                .Where(c => c.name == "Meuble_SdB")
                .OrderBy(c => Vector3.Distance(c.transform.position, SpawnPosition))
                .First();
            var so = new SerializedObject(cabinet);
            var guaranteed = so.FindProperty("guaranteed");
            guaranteed.arraySize = 1;
            guaranteed.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsDir}/IT_Bandage.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
