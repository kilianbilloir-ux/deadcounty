using UnityEngine;

namespace DeadCounty.EditorTools
{
    // Small nature and wear details: wildflowers in the meadows, stumps, branches and fallen trunks in the woods,
    // birches among the broadleaf trees; cracks, potholes, patches and manholes on the roads. A separate seed from
    // the vegetation pass, so the trees and bushes stay where they were. Only the fallen trunks are solid.
    public static partial class TestSceneBuilder
    {
        static void BuildUndergrowth(Transform parent)
        {
            var rng = new System.Random(8);
            var flowers = Group(parent, "Fleurs");
            var deadwood = Group(parent, "Bois_Mort");
            const float cell = 7f;
            string[] colours = { "Flower", "FlowerWhite", "FlowerPurple" };
            for (var cx = -HalfMap + cell / 2f; cx < HalfMap; cx += cell)
            for (var cz = -HalfMap + cell / 2f; cz < HalfMap; cz += cell)
            {
                var x = cx + Range(rng, -0.45f, 0.45f) * cell;
                var z = cz + Range(rng, -0.45f, 0.45f) * cell;
                var roll = rng.NextDouble();
                if (IsReserved(x, z, 1.5f)) continue;
                var density = ForestDensity(x, z);
                var ground = new Vector3(x, TerrainHeight(x, z), z);
                if (density < 0.2f)
                {
                    if (roll < 0.22)
                    {
                        var colour = colours[rng.Next(colours.Length)];
                        for (var i = rng.Next(3, 7); i > 0; i--)
                            MeshObject("Fleurs", flowers, FlowerClumpMesh(rng.Next(3)), ground + new Vector3(Range(rng, -1.2f, 1.2f), 0f, Range(rng, -1.2f, 1.2f)),
                                Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 0.8f, 1.2f), new[] { "GrassTall", colour }, false);
                    }
                    continue;
                }
                if (roll < 0.06) Stump(deadwood, ground, rng);
                else if (roll < 0.14) Branch(deadwood, ground, rng);
                else if (roll < 0.165) Log(deadwood, ground, Range(rng, 0f, 180f));
            }
        }

        static void Stump(Transform parent, Vector3 ground, System.Random rng)
        {
            var radius = Range(rng, 0.2f, 0.35f);
            var height = Range(rng, 0.25f, 0.5f);
            Cylinder("Souche", parent, ground + Vector3.up * height / 2f, radius, height, "Trunk", Quaternion.Euler(Range(rng, -4f, 4f), 0f, Range(rng, -4f, 4f)), solid: false);
            Cylinder("Coupe", parent, ground + Vector3.up * (height + 0.005f), radius * 0.9f, 0.01f, "Crate", solid: false);
        }

        // A dead branch on the ground with a twig or two.
        static void Branch(Transform parent, Vector3 ground, System.Random rng)
        {
            var b = Group(parent, "Branche", ground, Range(rng, 0f, 360f));
            var length = Range(rng, 1.2f, 2.4f);
            Box("Bois", b, new Vector3(0f, 0.05f, 0f), new Vector3(0.08f, 0.08f, length), "Trunk", Quaternion.Euler(0f, 0f, Range(rng, -10f, 10f)), solid: false);
            for (var i = rng.Next(1, 3); i > 0; i--)
                Box("Rameau", b, new Vector3(0.15f, 0.04f, Range(rng, -length / 3f, length / 3f)), new Vector3(0.04f, 0.04f, 0.5f), "Trunk",
                    Quaternion.Euler(0f, Range(rng, 30f, 60f) * (i % 2 == 0 ? 1f : -1f), 0f), solid: false);
        }

        // Grass blades with a few coloured heads; submesh 0 the leaves, 1 the flowers.
        static Mesh FlowerClumpMesh(int variant) => Cached($"SM_FlowerClump_{variant}", () =>
        {
            var rng = new System.Random(900 + variant);
            var m = new MeshBuilder();
            m.Sub();
            m.Sub();
            for (var i = 0; i < 8; i++)
            {
                var angle = Range(rng, 0f, Mathf.PI * 2f);
                var root = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Range(rng, 0f, 0.2f);
                var height = Range(rng, 0.25f, 0.45f);
                m.Use(0);
                Blade(m, root, Range(rng, 0f, 360f), height, 0.05f, Range(rng, -0.1f, 0.1f));
                if (i % 2 != 0) continue;
                m.Use(1);
                AddBlob(m, root + Vector3.up * height, Vector3.one * 0.045f, 0, rng, 0.1f);
            }
            return m.Save($"SM_FlowerClump_{variant}");
        });

        // Slender white trunk with dark marks and a light crown; submeshes: bark, marks, foliage.
        static Mesh BirchMesh(int variant) => Cached($"SM_Birch_{variant}", () =>
        {
            var rng = new System.Random(1000 + variant);
            var m = new MeshBuilder();
            m.Sub();
            AddPrism(m, Vector3.zero, 0.17f, 0.09f, 5f, 6);
            m.Sub();
            for (var y = 0.6f; y < 4.6f; y += Range(rng, 0.4f, 0.8f))
            {
                var radius = Mathf.Lerp(0.172f, 0.092f, y / 5f);
                AddPrism(m, Vector3.up * y, radius, radius, 0.06f, 6);
            }
            m.Sub();
            AddBlob(m, new Vector3(0f, 5f, 0f), new Vector3(1.5f, 1.8f, 1.5f), 1, rng, 0.2f);
            AddBlob(m, new Vector3(0.6f, 4.1f, 0.3f), new Vector3(1f, 1f, 1f), 1, rng, 0.2f);
            return m.Save($"SM_Birch_{variant}");
        });

        // Road wear along Main Street and the main road: cracks, potholes, patched squares, manholes.
        static void RoadWear(Transform parent)
        {
            var rng = new System.Random(64);
            var wear = Group(parent, "Usure");
            Vector3 OnRoad()
            {
                var mainStreet = rng.NextDouble() < 0.5;
                return mainStreet
                    ? new Vector3(MainStreetX + Range(rng, -3f, 3f), 0.043f, Range(rng, 10f, 140f))
                    : new Vector3(Range(rng, -140f, 60f), 0.041f, Range(rng, -3f, 3f));
            }
            for (var i = 0; i < 40; i++)
            {
                var pos = OnRoad();
                var yaw = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
                for (var k = rng.Next(2, 5); k > 0; k--)
                {
                    Box("Fissure", wear, pos, new Vector3(0.04f, 0.002f, Range(rng, 0.4f, 1.2f)), "RoadCrack", yaw, solid: false);
                    pos += yaw * new Vector3(Range(rng, -0.1f, 0.1f), 0f, 0.5f);
                    yaw *= Quaternion.Euler(0f, Range(rng, -35f, 35f), 0f);
                }
            }
            for (var i = 0; i < 12; i++)
                Cylinder("Nid_De_Poule", wear, OnRoad(), Range(rng, 0.2f, 0.45f), 0.003f, "RoadCrack", solid: false);
            for (var i = 0; i < 8; i++)
                Box("Rustine", wear, OnRoad() + Vector3.down * 0.001f, new Vector3(Range(rng, 1f, 2.2f), 0.002f, Range(rng, 1f, 2.5f)), "RoadPatch",
                    Quaternion.Euler(0f, Range(rng, -5f, 5f), 0f), solid: false);
            foreach (var z in new[] { 28f, 63f, 101f })
            {
                Cylinder("Plaque_Egout", wear, new Vector3(MainStreetX + 1.6f, 0.044f, z), 0.35f, 0.004f, "Metal", solid: false);
                Cylinder("Plaque_Egout_Bord", wear, new Vector3(MainStreetX + 1.6f, 0.043f, z), 0.4f, 0.002f, "RoadCrack", solid: false);
            }
        }
    }
}
