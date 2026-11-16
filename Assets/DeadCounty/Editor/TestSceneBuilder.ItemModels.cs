using System;
using System.Collections.Generic;
using System.IO;
using DeadCounty.Items;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadCounty.EditorTools
{
    // Rebuilt in place on every build, which keeps the GUIDs the item assets and the scene point to. One aiblender
    // model each, pivot on the ground, one box collider on the root for the interaction raycasts. A weapon lies
    // along +Z with its grip at the pivot, which is how the hand takes it.
    public static partial class TestSceneBuilder
    {
        const string ItemModelDir = "Assets/DeadCounty/Prefabs/Items";

        static readonly string[] FoodItems = { "IT_Haricots", "IT_Tomates", "IT_Soupe", "IT_Mais", "IT_PetitsPois", "IT_Thon", "IT_Sardines" };

        // Model of each item (Tools/aiblender/items) and the colours repainted: the tall cans are one model.
        static readonly Dictionary<string, (string Model, (string, string)[] Swaps)> ItemModelSpecs = new()
        {
            ["IT_Haricots"] = ("conserve", new (string, string)[0]),
            ["IT_Tomates"] = ("conserve", new[] { ("#7A4636", "SignRed") }),
            ["IT_Soupe"] = ("conserve", new[] { ("#7A4636", "SignYellow"), ("#C9B79A", "Brick") }),
            ["IT_Mais"] = ("conserve", new[] { ("#7A4636", "Hay"), ("#C9B79A", "ShutterGreen") }),
            ["IT_PetitsPois"] = ("conserve", new[] { ("#7A4636", "ShutterGreen") }),
            ["IT_Thon"] = ("thon", new (string, string)[0]), ["IT_Sardines"] = ("sardines", new (string, string)[0]),
            ["IT_Eau"] = ("eau", new (string, string)[0]), ["IT_Soda"] = ("soda", new (string, string)[0]),
            ["IT_Bandage"] = ("bandage", new (string, string)[0]), ["IT_SacADos"] = ("sac_a_dos", new (string, string)[0]),
            ["IT_Batte"] = ("batte", new (string, string)[0]), ["IT_PiedDeBiche"] = ("pied_de_biche", new (string, string)[0]),
            ["IT_Couteau"] = ("couteau", new (string, string)[0]),
        };

        static Dictionary<string, ItemDefinition> Items;

        static void EnsureItemModels(Dictionary<string, ItemDefinition> items)
        {
            Items = items;
            Directory.CreateDirectory(ItemModelDir);
            foreach (var (file, item) in items)
            {
                if (!ItemModelSpecs.TryGetValue(file, out var spec)) throw new InvalidOperationException($"No ground model for {file}");
                var root = new GameObject($"PF_{file}");
                Model("Modele", root.transform, Vector3.zero, null, spec.Model, spec.Swaps);
                FitCollider(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{ItemModelDir}/PF_{file}.prefab");
                Object.DestroyImmediate(root);
                item.WorldModel = prefab;
                EditorUtility.SetDirty(item);
            }
        }

        static void FitCollider(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
        }

        // An item in sight, picked up without searching; it comes back when the player respawns.
        static void PlaceItem(Transform parent, string file, Vector3 pos, float yaw)
        {
            var item = Items[file];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(item.WorldModel, parent);
            go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var so = new SerializedObject(go.AddComponent<WorldItem>());
            so.FindProperty("item").objectReferenceValue = item;
            so.FindProperty("placed").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
