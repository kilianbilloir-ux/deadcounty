using UnityEngine;

namespace DeadCounty.EditorTools
{
    // Signs of the outbreak along the streets (GDD 8.12). Solid pieces keep clear of the zombie spots, the spawn and
    // the doors; the small clutter is not solid.
    public static partial class TestSceneBuilder
    {
        const float RoadblockZ = 124f;
        // Litter models in the order of the materials the boxes they replaced drew from: paper, crate, glass, metal,
        // black plastic.
        static readonly string[] DebrisModels = { "debris_papier", "debris_planche", "debris_verre", "debris_canette", "debris_plastique" };
        // Bounding box of every litter model, 0.2 x 0.05 x 0.16 m.
        const float DebrisVolume = 0.2f * 0.05f * 0.16f;

        static void BuildStreetDressing(Transform parent)
        {
            var rng = new System.Random(57);
            var root = Group(parent, "Rues");

            // Nose in the lamp post of the east sidewalk.
            var crash = Car(root, "Voiture_Encastree", new Vector3(MainStreetX + 3.8f, 0f, 74.7f), -44f, "CarGreen", tilt: 2.5f, doorOpen: true);
            Debris(root, crash.position + new Vector3(-1.5f, 0f, -0.5f), rng, 6);
            BloodStain(root, crash.position + new Vector3(-2.6f, 0f, -0.4f), rng);
            Car(root, "Voiture_Garee", new Vector3(MainStreetX - 2.2f, 0f, 60f), -87f, "CarWhite");
            Car(root, "Voiture_Garee", new Vector3(MainStreetX + 2.2f, 0f, 99f), 88f, "CarRed", doorOpen: true);
            Car(root, "Voiture_Trottoir", new Vector3(MainStreetX - 4.2f, 0f, 84f), -76f, "CarBeige", tilt: -2f);

            // The family car left in the street: its luggage on the road, a trail of papers.
            Suitcase(root, new Vector3(MainStreetX - 1.2f, 0f, 35.6f), 30f, "FabricRust", rng.NextDouble() < 0.5);
            Suitcase(root, new Vector3(MainStreetX - 0.3f, 0f, 34.2f), 105f, "ShutterDark", true);
            Papers(root, new Vector3(MainStreetX, 0f, 30f), new Vector2(6f, 14f), rng, 14);
            Papers(root, new Vector3(MainStreetX, 0f, 70f), new Vector2(7f, 30f), rng, 18);

            BuildRoadblock(Group(root, "Barrage_Police"), rng);

            for (var lot = 0; lot < VillageLots.Length; lot++)
            foreach (var east in new[] { false, true })
            {
                if (rng.NextDouble() < 0.7) TrashBags(root, LotPoint(lot, east, Range(rng, 13.1f, 13.5f), Range(rng, -5.6f, -4.8f)), rng, rng.Next(1, 5));
                if (rng.NextDouble() < 0.3) TippedBin(root, LotPoint(lot, east, Range(rng, 9.5f, 13f), -6.4f), rng);
            }
            Bicycle(root, LotPoint(3, true, -3.5f, -2.5f), 35f);
            Bicycle(root, LotPoint(0, false, 3f, -3.2f), 160f);

            // The store's carts, one knocked over by the door.
            ShoppingCart(root, new Vector3(46.6f, 0.06f, -18.6f), 20f, false);
            ShoppingCart(root, new Vector3(56.2f, 0.06f, -15.2f), 150f, true);
            ShoppingCart(root, new Vector3(40.5f, 0.06f, -11f), 80f, false);
            BloodStain(root, new Vector3(MainStreetX + 2.5f, 0f, 3.5f), rng);
            BloodStain(root, new Vector3(MainStreetX - 0.5f, 0f, 46.5f), rng);
            BuildStreetFurniture(root);
            BuildEvacuationTraces(root);
        }

        // Main Street closed to the north, with gaps a person can squeeze through.
        static void BuildRoadblock(Transform root, System.Random rng)
        {
            foreach (var x in new[] { -2.4f, 2.4f })
                Sawhorse(root, new Vector3(MainStreetX + x, 0f, RoadblockZ), 0f);
            PoliceCar(root, new Vector3(MainStreetX + 0.8f, 0f, RoadblockZ + 4.5f), 168f);
            RoadClosedSign(root, new Vector3(MainStreetX - 2.4f, 0f, RoadblockZ - 0.045f), 180f);
            Box("Herse", root, new Vector3(MainStreetX, 0.03f, RoadblockZ - 1.6f), new Vector3(6.4f, 0.04f, 0.3f), "CarBlack", solid: false);
            foreach (var side in new[] { -1f, 1f })
                SandbagWall(root, new Vector3(MainStreetX + side * 4.75f, 0f, RoadblockZ), 2.4f, rng);
            for (var i = 0; i < 5; i++)
                TrafficCone(root, new Vector3(MainStreetX - 3f + i * 1.5f + Range(rng, -0.2f, 0.2f), 0f, RoadblockZ - 3.2f + Range(rng, -0.3f, 0.3f)), i == 3);
        }

        static void SandbagWall(Transform parent, Vector3 center, float length, System.Random rng)
        {
            var wall = Group(parent, "Sacs_Sable", center);
            var collider = wall.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.45f, 0f);
            collider.size = new Vector3(length, 0.9f, 0.6f);
            for (var row = 0; row < 3; row++)
            for (var x = -length / 2f + 0.3f + (row % 2) * 0.25f; x < length / 2f - 0.25f; x += 0.55f)
                Model("Sac", wall, new Vector3(x, 0.01f + row * 0.25f, Range(rng, -0.05f, 0.05f)), Quaternion.Euler(0f, Range(rng, -8f, 8f), 0f), "sac_sable");
        }

        static void TrafficCone(Transform parent, Vector3 pos, bool knocked)
        {
            var cone = Group(parent, "Cone", pos, 0f);
            if (knocked)
            {
                cone.localRotation = Quaternion.Euler(0f, 40f, 90f);
                cone.localPosition += Vector3.up * 0.16f;
            }
            Model("Modele", cone, Vector3.zero, null, "cone");
        }

        static void Suitcase(Transform parent, Vector3 pos, float yaw, string mat, bool open)
        {
            var c = Group(parent, "Valise", pos, yaw);
            Model("Modele", c, Vector3.zero, null, open ? "valise_ouverte" : "valise", ("#7A4C3A", mat));
        }

        static void Papers(Transform parent, Vector3 center, Vector2 area, System.Random rng, int count)
        {
            var group = Group(parent, "Papiers", center);
            for (var i = 0; i < count; i++)
                Box("Papier", group, new Vector3(Range(rng, -area.x / 2f, area.x / 2f), 0.05f, Range(rng, -area.y / 2f, area.y / 2f)),
                    new Vector3(0.21f, 0.004f, 0.29f), "Paper", Quaternion.Euler(Range(rng, -6f, 6f), Range(rng, 0f, 360f), 0f), solid: false);
        }

        static void TrashBags(Transform parent, Vector3 pos, System.Random rng, int count)
        {
            var pile = Group(parent, "Sacs_Poubelle", pos);
            for (var i = 0; i < count; i++)
                Model("Sac", pile, new Vector3(Range(rng, -0.5f, 0.5f), 0f, Range(rng, -0.4f, 0.4f)),
                    Quaternion.Euler(0f, Range(rng, 0f, 360f), Range(rng, -10f, 10f)), "sac_poubelle").transform.localScale = Vector3.one * Range(rng, 0.85f, 1.15f);
        }

        // On its side, lid off, rubbish spilt on the pavement.
        static void TippedBin(Transform parent, Vector3 pos, System.Random rng)
        {
            var yaw = Range(rng, 0f, 360f);
            Model("Poubelle_Renversee", parent, pos, Quaternion.Euler(0f, yaw, 0f), "poubelle_renversee");
            Debris(parent, pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.9f, 0f, 0f), rng, 5);
        }

        static void Debris(Transform parent, Vector3 pos, System.Random rng, int count)
        {
            var pile = Group(parent, "Debris", pos);
            for (var i = 0; i < count; i++)
            {
                // Same draws, in the same order, as the boxes these pieces replaced, so the rest of the street keeps its
                // layout: each piece takes the volume of its box.
                var size = new Vector3(Range(rng, 0.08f, 0.3f), Range(rng, 0.02f, 0.08f), Range(rng, 0.08f, 0.25f));
                var position = new Vector3(Range(rng, -0.7f, 0.7f), 0.02f, Range(rng, -0.6f, 0.6f));
                var model = DebrisModels[rng.Next(DebrisModels.Length)];
                Model("Dechet", pile, position, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), model).transform.localScale
                    = Vector3.one * Mathf.Pow(size.x * size.y * size.z / DebrisVolume, 1f / 3f);
            }
        }

        // Dark irregular patches overlapping on the ground.
        static void BloodStain(Transform parent, Vector3 pos, System.Random rng)
        {
            var stain = Group(parent, "Sang", pos);
            for (var i = rng.Next(3, 6); i > 0; i--)
                Box("Tache", stain, new Vector3(Range(rng, -0.6f, 0.6f), 0.052f, Range(rng, -0.6f, 0.6f)),
                    new Vector3(Range(rng, 0.3f, 0.9f), 0.003f, Range(rng, 0.2f, 0.6f)), "Blood", Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), solid: false);
        }

        // Lying on its side.
        static void Bicycle(Transform parent, Vector3 pos, float yaw)
        {
            var bike = Group(parent, "Velo", pos, yaw);
            bike.localRotation *= Quaternion.Euler(80f, 0f, 0f);
            bike.localPosition += Vector3.up * 0.05f;
            Model("Modele", bike, Vector3.zero, null, "velo");
        }

        static void ShoppingCart(Transform parent, Vector3 pos, float yaw, bool tipped)
        {
            var cart = Group(parent, "Caddie", pos, yaw);
            if (tipped)
            {
                cart.localRotation *= Quaternion.Euler(0f, 0f, 90f);
                cart.localPosition += Vector3.up * 0.3f;
            }
            Model("Modele", cart, Vector3.zero, null, "caddie");
        }
    }
}
