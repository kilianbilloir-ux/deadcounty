using System.Collections.Generic;
using DeadCounty.Core;
using DeadCounty.World;
using UnityEditor;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        // Wider openings (garage, barn, church, warehouse) stay open.
        const float MaxLeafWidth = 1.2f;
        const float LeafGap = 0.02f;
        // Clears the house floors and porches when the leaf swings.
        const float LeafBottom = 0.12f;
        const float LeafThickness = 0.05f;

        static readonly List<(Transform Wall, Opening Opening)> DoorSpots = new();

        static void RecordDoorSpot(Transform wall, Opening o)
        {
            if (o.Bottom <= 0f && o.Width <= MaxLeafWidth) DoorSpots.Add((wall, o));
        }

        // Hinged leaves for the narrow door openings, built after the NavMesh bake and outside Environment: the
        // NavMesh keeps every doorway passable and the leaves stay movable. Exterior doors start shut, interior
        // ones (walls named Cloison) are left open half the time.
        static void BuildDoors()
        {
            var root = new GameObject("Portes").transform;
            var rng = new System.Random(7);
            for (var i = 0; i < DoorSpots.Count; i++)
            {
                var (wall, o) = DoorSpots[i];
                var hinge = new GameObject($"Porte_{i:00}_{wall.parent.name}_{wall.name}").transform;
                hinge.SetParent(root, false);
                hinge.SetPositionAndRotation(wall.TransformPoint(0f, 0f, o.Offset - o.Width / 2f), wall.rotation);
                var leaf = new GameObject("Battant").transform;
                leaf.SetParent(hinge, false);
                var width = o.Width - 2f * LeafGap;
                var height = o.Top - LeafBottom - LeafGap;
                Box("Panneau", leaf, new Vector3(0f, LeafBottom + height / 2f, LeafGap + width / 2f), new Vector3(LeafThickness, height, width), "Wood").tag = GameLayers.WallTag;
                Box("Poignee", leaf, new Vector3(0f, 1f, LeafGap + width - 0.1f), new Vector3(LeafThickness + 0.06f, 0.04f, 0.04f), "Metal", solid: false);

                var interior = wall.name.StartsWith("Cloison");
                var startAngle = interior && rng.NextDouble() < 0.5 ? (rng.NextDouble() < 0.5 ? -1f : 1f) * 95f : 0f;
                var door = new SerializedObject(hinge.gameObject.AddComponent<Door>());
                door.FindProperty("leaf").objectReferenceValue = leaf;
                door.FindProperty("width").floatValue = o.Width;
                door.FindProperty("height").floatValue = o.Top;
                door.FindProperty("startAngle").floatValue = startAngle;
                door.ApplyModifiedPropertiesWithoutUndo();
                leaf.localRotation = Quaternion.Euler(0f, startAngle, 0f);
                AddDoorAudio(hinge.gameObject);
            }
        }
    }
}
