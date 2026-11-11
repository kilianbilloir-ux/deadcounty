using System;
using System.Collections.Generic;
using System.IO;
using DeadCounty.Items;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadCounty.EditorTools
{
    // Ground models of the items, rebuilt in place on every build (pure code, nothing to tune in the Inspector),
    // which keeps the GUIDs the item assets and the scene point to. Pivot on the ground, one box collider on the
    // root for the interaction raycasts. Also places a few items in sight around the level.
    public static partial class TestSceneBuilder
    {
        const string ItemModelDir = "Assets/DeadCounty/Prefabs/Items";

        static readonly string[] FoodItems = { "IT_Haricots", "IT_Tomates", "IT_Soupe", "IT_Mais", "IT_PetitsPois", "IT_Thon", "IT_Sardines" };

        // Label colour of each tall can, so they tell apart on the ground.
        static readonly Dictionary<string, string> CanLabels = new()
        {
            ["IT_Haricots"] = "Brick", ["IT_Tomates"] = "SignRed", ["IT_Soupe"] = "SignYellow",
            ["IT_Mais"] = "Hay", ["IT_PetitsPois"] = "ShutterGreen",
        };

        static Dictionary<string, ItemDefinition> Items;

        static void EnsureItemModels(Dictionary<string, ItemDefinition> items)
        {
            Items = items;
            Directory.CreateDirectory(ItemModelDir);
            foreach (var (file, item) in items)
            {
                var root = new GameObject($"PF_{file}");
                BuildItemModel(root.transform, file);
                FitCollider(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{ItemModelDir}/PF_{file}.prefab");
                Object.DestroyImmediate(root);
                item.WorldModel = prefab;
                EditorUtility.SetDirty(item);
            }
        }

        static void BuildItemModel(Transform m, string file)
        {
            switch (file)
            {
                case var can when CanLabels.TryGetValue(can, out var label):
                    Can(m, 0.04f, 0.11f, label);
                    break;
                case "IT_Thon":
                    Can(m, 0.045f, 0.035f, "CarBlue");
                    break;
                case "IT_Sardines":
                    Box("Boite", m, new Vector3(0f, 0.0125f, 0f), new Vector3(0.11f, 0.025f, 0.07f), "MetalLight", solid: false);
                    Box("Etiquette", m, new Vector3(0f, 0.026f, 0f), new Vector3(0.1f, 0.002f, 0.06f), "SignYellow", solid: false);
                    Box("Anneau", m, new Vector3(0.045f, 0.028f, 0f), new Vector3(0.015f, 0.003f, 0.02f), "Metal", solid: false);
                    break;
                case "IT_Eau":
                    Cylinder("Bouteille", m, new Vector3(0f, 0.1f, 0f), 0.035f, 0.2f, "Water", solid: false);
                    Cylinder("Etiquette", m, new Vector3(0f, 0.1f, 0f), 0.0355f, 0.06f, "SignWhite", solid: false);
                    Cylinder("Goulot", m, new Vector3(0f, 0.215f, 0f), 0.015f, 0.03f, "Water", solid: false);
                    Cylinder("Bouchon", m, new Vector3(0f, 0.237f, 0f), 0.017f, 0.015f, "CarBlue", solid: false);
                    break;
                case "IT_Soda":
                    Cylinder("Canette", m, new Vector3(0f, 0.06f, 0f), 0.033f, 0.12f, "SignRed", solid: false);
                    Cylinder("Bande", m, new Vector3(0f, 0.07f, 0f), 0.0335f, 0.03f, "SignWhite", solid: false);
                    Cylinder("Dessus", m, new Vector3(0f, 0.122f, 0f), 0.03f, 0.004f, "MetalLight", solid: false);
                    break;
                case "IT_Bandage":
                    Cylinder("Rouleau", m, new Vector3(0f, 0.035f, 0f), 0.035f, 0.06f, "Linen", Quaternion.Euler(0f, 0f, 90f), solid: false);
                    Box("Bout", m, new Vector3(0f, 0.001f, 0.06f), new Vector3(0.05f, 0.002f, 0.06f), "Linen", solid: false);
                    break;
                case "IT_SacADos":
                    Box("Sac", m, new Vector3(0f, 0.21f, 0f), new Vector3(0.32f, 0.42f, 0.18f), "Backpack", solid: false);
                    Box("Rabat", m, new Vector3(0f, 0.38f, -0.005f), new Vector3(0.31f, 0.12f, 0.19f), "BackpackDark", solid: false);
                    Box("Poche", m, new Vector3(0f, 0.14f, -0.11f), new Vector3(0.24f, 0.16f, 0.05f), "BackpackDark", solid: false);
                    // Distinct names: saving over the prefab matches children by name and would swap twins on each build.
                    foreach (var (x, name) in new[] { (-0.08f, "Bretelle_G"), (0.08f, "Bretelle_D") })
                        Box(name, m, new Vector3(x, 0.22f, 0.1f), new Vector3(0.05f, 0.36f, 0.02f), "Strap", solid: false);
                    Box("Poignee", m, new Vector3(0f, 0.44f, 0.04f), new Vector3(0.08f, 0.03f, 0.03f), "Strap", solid: false);
                    break;
                default:
                    throw new InvalidOperationException($"No ground model for {file}");
            }
        }

        static void Can(Transform m, float radius, float height, string label)
        {
            Cylinder("Boite", m, new Vector3(0f, height / 2f, 0f), radius, height, "MetalLight", solid: false);
            Cylinder("Etiquette", m, new Vector3(0f, height / 2f, 0f), radius + 0.002f, height * 0.7f, label, solid: false);
            Cylinder("Couvercle", m, new Vector3(0f, height + 0.002f, 0f), radius * 0.9f, 0.004f, "Metal", solid: false);
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
