using UnityEngine;

namespace DeadCounty.EditorTools
{
    // Each piece keeps one box over its footprint as its only collider (see Piece), so the NavMesh, the loot and the
    // test zones see simple blocks.
    public static partial class TestSceneBuilder
    {
        // Fuel pump served from both sides (local ±Z): body, head with a screen, a nozzle and its hose on each face.
        static void FuelPump(Transform parent, Vector3 pos, float yaw)
        {
            var p = Piece(parent, "Pompe", pos, yaw, new Vector3(0.5f, 1.6f, 0.7f));
            Model("Modele", p, Vector3.zero, null, "pompe");
        }

        // Fridges with open-framed doors: drinks on wire shelves, seen through the frames (local front -Z).
        static void ColdCase(Transform parent, Vector3 pos, float yaw, float length, System.Random rng)
        {
            const float height = 2f, depth = 0.7f;
            var p = Piece(parent, "Vitrine_Froide", pos, yaw, new Vector3(length, height, depth));
            // The model is 7 m long, the only length asked for; another one stretches its door frames.
            Model("Modele", p, Vector3.zero, null, "vitrine_froide").transform.localScale = new Vector3(length / 7f, 1f, 1f);
            string[] drinks = { "SignRed", "CarBlue", "SignYellow", "ShutterGreen", "Water", "SignWhite" };
            for (var shelf = 0; shelf < 4; shelf++)
            {
                var top = 0.21f + shelf * 0.42f;
                for (var x = -length / 2f + 0.12f; x < length / 2f - 0.1f; x += 0.11f)
                {
                    if (rng.NextDouble() < 0.4) continue;
                    var tall = rng.NextDouble() < 0.5;
                    Model(tall ? "Bouteille" : "Canette", p, new Vector3(x, top, Range(rng, -0.08f, 0.12f)), null, tall ? "bouteille_soda" : "canette",
                        ("#A8322A", drinks[rng.Next(drinks.Length)]));
                }
            }
        }

        static void PropaneCage(Transform parent, Vector3 pos)
        {
            var p = Piece(parent, "Cage_Gaz", pos, 0f, new Vector3(1.6f, 1.5f, 0.8f));
            Model("Modele", p, Vector3.zero, null, "cage_gaz");
        }

        static void IceChest(Transform parent, Vector3 pos)
        {
            var p = Piece(parent, "Glaciere", pos, 0f, new Vector3(1.6f, 1.1f, 0.8f));
            Model("Modele", p, Vector3.zero, null, "glaciere");
        }

        static void TireStack(Transform parent, Vector3 pos, int count, System.Random rng)
        {
            var stack = Group(parent, "Pneus", pos);
            for (var i = 0; i < count; i++)
                Model("Pneu", stack, new Vector3(Range(rng, -0.05f, 0.05f), i * 0.22f, Range(rng, -0.05f, 0.05f)), null, "pneu");
        }

        // Price board digits on both faces of the station's sign.
        static void PriceDigits(Transform parent, Vector3 center)
        {
            foreach (var x in new[] { -0.16f, 0.16f })
            for (var row = 0; row < 3; row++)
            for (var d = 0; d < 3; d++)
                Box("Chiffre", parent, center + new Vector3(x, 0.3f - row * 0.3f, -0.6f + d * 0.25f + 0.35f), new Vector3(0.02f, 0.18f, 0.16f), "CarBlack", solid: false);
        }

        // Church pew facing +Z: seat, back on the -Z side, carved ends.
        static void Pew(Transform parent, Vector3 pos)
        {
            var p = Piece(parent, "Banc", pos, 0f, new Vector3(2.6f, 0.9f, 0.5f));
            Model("Modele", p, Vector3.zero, null, "banc_eglise");
        }

        // Altar cloth, candles, lectern, cross on the back wall and a red runner down the aisle (church frame).
        static void ChurchDetails(Transform church)
        {
            Box("Tapis_Allee", church, new Vector3(0f, 0.105f, 7.5f), new Vector3(1.1f, 0.01f, 13f), "BlanketRed", solid: false);
            // Dresses the altar block of BuildChurch, 3 x 1 x 1 m centred on z = 14.8.
            Model("Garniture_Autel", church, new Vector3(0f, 0f, 14.8f), null, "garniture_autel");
            Box("Croix", church, new Vector3(0f, 2.9f, 15.85f), new Vector3(0.18f, 2f, 0.08f), "WoodDark", solid: false);
            Box("Croix", church, new Vector3(0f, 3.3f, 15.85f), new Vector3(1.1f, 0.18f, 0.08f), "WoodDark", solid: false);
            var lectern = Group(church, "Pupitre", new Vector3(2.6f, 0f, 13f));
            Model("Modele", lectern, Vector3.zero, null, "pupitre");
        }

        // Pallet racking along local X: orange beams on blue frames, pallets of boxes on three levels, gaps where
        // goods were taken.
        static void PalletRack(Transform parent, Vector3 pos, System.Random rng)
        {
            const float length = 8f, height = 3.2f, depth = 0.8f;
            var p = Piece(parent, "Etagere", pos, 0f, new Vector3(length, height, depth));
            Model("Modele", p, Vector3.zero, null, "rayonnage");
            foreach (var y in new[] { 0.15f, 1.2f, 2.25f })
            {
                for (var bay = 0; bay < 3; bay++)
                {
                    if (rng.NextDouble() < 0.3) continue;
                    var x = -length / 2f + (bay + 0.5f) * length / 3f;
                    Model("Palette", p, new Vector3(x, y + 0.05f, 0f), null, "palette_rayonnage");
                    var boxes = rng.Next(1, 4);
                    for (var b = 0; b < boxes; b++)
                    {
                        var size = new Vector3(Range(rng, 0.35f, 0.55f), Range(rng, 0.3f, 0.6f), Range(rng, 0.4f, 0.6f));
                        Bit(p, "Carton", new Vector3(x - 0.38f + b * 0.38f, y + 0.17f + size.y / 2f, 0f), size, "Crate", Quaternion.Euler(0f, Range(rng, -6f, 6f), 0f));
                    }
                }
            }
        }

        // Main Street furniture: fire hydrants by the curbs, a bus stop with a bench, newspaper boxes at the store,
        // a street name sign at the crossroads.
        static void BuildStreetFurniture(Transform parent)
        {
            var root = Group(parent, "Mobilier_Urbain");
            foreach (var z in new[] { 20f, 57f, 96f })
                Hydrant(root, new Vector3(MainStreetX - 4.1f, 0.15f, z));
            foreach (var z in new[] { 33f, 66f, 110f })
                Hydrant(root, new Vector3(MainStreetX + 4.1f, 0.15f, z));

            var stop = Group(root, "Arret_Bus", new Vector3(MainStreetX - 3.8f, 0.15f, 19.5f));
            Post("Poteau", stop, Vector3.zero, 2.6f, 0.04f, "Metal", solid: false);
            Box("Panneau", stop, new Vector3(0f, 2.35f, 0f), new Vector3(0.04f, 0.45f, 0.35f), "CarBlue", solid: false);
            Box("Pictogramme", stop, new Vector3(-0.025f, 2.35f, 0f), new Vector3(0.01f, 0.2f, 0.25f), "SignWhite", solid: false);
            Bench(root, new Vector3(MainStreetX - 5.3f, 0.15f, 21f), -90f);

            foreach (var (x, mat) in new[] { (52.6f, "CarBlue"), (53.3f, "SignRed") })
                NewsBox(root, new Vector3(x, 0.06f, -21.2f), 180f, mat);

            var sign = Group(root, "Plaque_Rue", new Vector3(MainStreetX - 4.9f, 0.15f, -5.3f));
            Post("Poteau", sign, Vector3.zero, 2.8f, 0.035f, "Metal", solid: false);
            Box("Lame", sign, new Vector3(0f, 2.7f, 0f), new Vector3(0.9f, 0.18f, 0.02f), "SignGreen", solid: false);
            Box("Lame", sign, new Vector3(0f, 2.48f, 0f), new Vector3(0.02f, 0.18f, 0.9f), "SignGreen", solid: false);
            foreach (var side in new[] { -1f, 1f })
            {
                Label(sign, "COUNTY RD", new Vector3(0f, 2.7f, side * 0.016f), new Vector3(0f, 0f, side), 0.024f, "SignWhite");
                Label(sign, "MAIN ST", new Vector3(side * 0.016f, 2.48f, 0f), new Vector3(side, 0f, 0f), 0.024f, "SignWhite");
            }
        }

        static void Hydrant(Transform parent, Vector3 pos)
        {
            var h = Group(parent, "Borne_Incendie", pos);
            Model("Modele", h, Vector3.zero, null, "borne_incendie");
            Solid(h, new Vector3(0f, 0.28f, 0f), new Vector3(0.24f, 0.56f, 0.24f));
        }

        static void Bench(Transform parent, Vector3 pos, float yaw)
        {
            var p = Piece(parent, "Banc_Public", pos, yaw, new Vector3(1.6f, 0.85f, 0.5f));
            Model("Modele", p, Vector3.zero, null, "banc");
        }

        static void NewsBox(Transform parent, Vector3 pos, float yaw, string mat)
        {
            var p = Piece(parent, "Distributeur_Journaux", pos, yaw, new Vector3(0.45f, 1.05f, 0.4f));
            Model("Modele", p, Vector3.zero, null, "distributeur_journaux", ("#2E4766", mat), ("#3A5578", Shade(mat, 0.12f)));
        }
    }
}
