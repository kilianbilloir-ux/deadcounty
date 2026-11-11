using UnityEngine;

namespace DeadCounty.EditorTools
{
    // Detailed pieces for the gas station, the church and the warehouse, and the street furniture of Main Street.
    // Pieces that replace a former box keep its footprint as their only collider (see Piece), so the NavMesh, the
    // loot and the test zones stay as they were.
    public static partial class TestSceneBuilder
    {
        // Fuel pump served from both sides (local ±Z): body, head with a screen, a nozzle and its hose on each face.
        static void FuelPump(Transform parent, Vector3 pos, float yaw)
        {
            var p = Piece(parent, "Pompe", pos, yaw, new Vector3(0.5f, 1.6f, 0.7f));
            Bit(p, "Corps", new Vector3(0f, 0.6f, 0f), new Vector3(0.48f, 1.2f, 0.66f), "SignRed");
            Bit(p, "Tete", new Vector3(0f, 1.4f, 0f), new Vector3(0.52f, 0.4f, 0.7f), "SignWhite");
            Bit(p, "Bande", new Vector3(0f, 1.58f, 0f), new Vector3(0.54f, 0.05f, 0.72f), "SignRed");
            foreach (var side in new[] { -1f, 1f })
            {
                var face = side * 0.355f;
                Bit(p, "Ecran", new Vector3(0f, 1.44f, face), new Vector3(0.32f, 0.16f, 0.01f), "CarBlack");
                for (var i = 0; i < 3; i++)
                    Bit(p, "Bouton", new Vector3(-0.12f + i * 0.12f, 1.28f, face), new Vector3(0.07f, 0.04f, 0.015f), "SignYellow");
                Bit(p, "Support", new Vector3(0.14f, 1f, face + side * 0.03f), new Vector3(0.1f, 0.14f, 0.06f), "CarBlack");
                Bit(p, "Pistolet", new Vector3(0.14f, 1.02f, face + side * 0.08f), new Vector3(0.05f, 0.18f, 0.05f), "CarBlack", Quaternion.Euler(side * 20f, 0f, 0f));
                // Hose hanging from the nozzle in a loop and back into the body.
                Bit(p, "Tuyau", new Vector3(0.14f, 0.7f, face + side * 0.1f), new Vector3(0.035f, 0.5f, 0.035f), "Strap");
                Bit(p, "Tuyau", new Vector3(0.0f, 0.45f, face + side * 0.08f), new Vector3(0.3f, 0.035f, 0.035f), "Strap", Quaternion.Euler(0f, 0f, 20f));
            }
        }

        // Fridges with open-framed doors: drinks on wire shelves, seen through the frames (local front -Z).
        static void ColdCase(Transform parent, Vector3 pos, float yaw, float length, System.Random rng)
        {
            const float height = 2f, depth = 0.7f;
            var p = Piece(parent, "Vitrine_Froide", pos, yaw, new Vector3(length, height, depth));
            Bit(p, "Fond", new Vector3(0f, height / 2f, depth / 2f - 0.03f), new Vector3(length, height, 0.06f), "Porcelain");
            Bit(p, "Socle", new Vector3(0f, 0.08f, 0f), new Vector3(length, 0.16f, depth), "CarBlack");
            Bit(p, "Fronton", new Vector3(0f, height - 0.1f, -depth / 2f + 0.03f), new Vector3(length, 0.2f, 0.06f), "SignRed");
            string[] drinks = { "SignRed", "CarBlue", "SignYellow", "ShutterGreen", "Water", "SignWhite" };
            for (var shelf = 0; shelf < 4; shelf++)
            {
                var y = 0.2f + shelf * 0.42f;
                Bit(p, "Grille", new Vector3(0f, y, 0.02f), new Vector3(length - 0.06f, 0.02f, depth - 0.15f), "MetalLight");
                for (var x = -length / 2f + 0.12f; x < length / 2f - 0.1f; x += 0.11f)
                {
                    if (rng.NextDouble() < 0.4) continue;
                    var tall = rng.NextDouble() < 0.5;
                    Cylinder(tall ? "Bouteille" : "Canette", p, new Vector3(x, y + (tall ? 0.12f : 0.07f), Range(rng, -0.08f, 0.12f)), tall ? 0.035f : 0.033f, tall ? 0.24f : 0.13f,
                        drinks[rng.Next(drinks.Length)], solid: false);
                }
            }
            var doors = Mathf.Max(1, Mathf.RoundToInt(length / 0.9f));
            for (var i = 0; i <= doors; i++)
                Bit(p, "Montant", new Vector3(-length / 2f + length * i / doors, height / 2f, -depth / 2f + 0.02f), new Vector3(0.05f, height - 0.2f, 0.04f), "Metal");
            for (var i = 0; i < doors; i++)
                Bit(p, "Poignee", new Vector3(-length / 2f + length * (i + 0.85f) / doors, 1.1f, -depth / 2f - 0.01f), new Vector3(0.03f, 0.4f, 0.03f), "MetalLight");
        }

        static void PropaneCage(Transform parent, Vector3 pos)
        {
            var p = Piece(parent, "Cage_Gaz", pos, 0f, new Vector3(1.6f, 1.5f, 0.8f));
            foreach (var x in new[] { -0.78f, 0.78f })
            foreach (var z in new[] { -0.38f, 0.38f })
                Bit(p, "Montant", new Vector3(x, 0.75f, z), new Vector3(0.04f, 1.5f, 0.04f), "MetalLight");
            foreach (var y in new[] { 0.05f, 0.75f, 1.48f })
                Bit(p, "Etagere", new Vector3(0f, y, 0f), new Vector3(1.6f, 0.03f, 0.8f), "MetalLight");
            for (var x = -0.55f; x <= 0.56f; x += 0.37f)
            foreach (var y in new[] { 0.07f, 0.77f })
            {
                Cylinder("Bouteille_Gaz", p, new Vector3(x, y + 0.27f, 0f), 0.15f, 0.5f, "SignWhite", solid: false);
                Cylinder("Robinet", p, new Vector3(x, y + 0.57f, 0f), 0.05f, 0.1f, "CarBlue", solid: false);
            }
            // Wire front: thin bars so the bottles show through.
            for (var x = -0.7f; x <= 0.71f; x += 0.14f)
                Bit(p, "Grillage", new Vector3(x, 0.75f, -0.4f), new Vector3(0.01f, 1.45f, 0.01f), "Metal");
        }

        static void IceChest(Transform parent, Vector3 pos)
        {
            var p = Piece(parent, "Glaciere", pos, 0f, new Vector3(1.6f, 1.1f, 0.8f));
            Bit(p, "Caisse", new Vector3(0f, 0.55f, 0f), new Vector3(1.6f, 1.1f, 0.8f), "SignWhite");
            Bit(p, "Bande", new Vector3(0f, 0.75f, -0.405f), new Vector3(1.2f, 0.22f, 0.01f), "CarBlue");
            Bit(p, "Joint", new Vector3(0f, 0.55f, -0.405f), new Vector3(0.015f, 1f, 0.01f), "MetalLight");
            foreach (var x in new[] { -0.12f, 0.12f })
                Bit(p, "Poignee", new Vector3(x, 0.45f, -0.42f), new Vector3(0.03f, 0.25f, 0.03f), "MetalLight");
        }

        static void TireStack(Transform parent, Vector3 pos, int count, System.Random rng)
        {
            var stack = Group(parent, "Pneus", pos);
            for (var i = 0; i < count; i++)
                Cylinder("Pneu", stack, new Vector3(Range(rng, -0.05f, 0.05f), 0.11f + i * 0.22f, Range(rng, -0.05f, 0.05f)), 0.33f, 0.22f, "Tire", solid: false);
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
            const float length = 2.6f;
            var p = Piece(parent, "Banc", pos, 0f, new Vector3(length, 0.9f, 0.5f));
            Bit(p, "Assise", new Vector3(0f, 0.44f, 0.03f), new Vector3(length - 0.1f, 0.06f, 0.42f), "Wood");
            Bit(p, "Dossier", new Vector3(0f, 0.72f, -0.2f), new Vector3(length - 0.1f, 0.46f, 0.06f), "Wood");
            Bit(p, "Tablette", new Vector3(0f, 0.86f, -0.26f), new Vector3(length - 0.1f, 0.03f, 0.14f), "WoodDark");
            foreach (var x in new[] { -length / 2f + 0.03f, length / 2f - 0.03f })
                Bit(p, "Joue", new Vector3(x, 0.45f, 0f), new Vector3(0.06f, 0.9f, 0.5f), "WoodDark");
        }

        // Altar cloth, candles, lectern, cross on the back wall and a red runner down the aisle (church frame).
        static void ChurchDetails(Transform church)
        {
            Box("Tapis_Allee", church, new Vector3(0f, 0.105f, 7.5f), new Vector3(1.1f, 0.01f, 13f), "BlanketRed", solid: false);
            Box("Nappe", church, new Vector3(0f, 0.85f, 14.28f), new Vector3(2.4f, 0.5f, 0.02f), "Linen", solid: false);
            Box("Nappe_Dessus", church, new Vector3(0f, 1.005f, 14.8f), new Vector3(2.6f, 0.01f, 1.02f), "Linen", solid: false);
            foreach (var x in new[] { -1.1f, 1.1f })
            {
                Cylinder("Cierge", church, new Vector3(x, 1.2f, 14.9f), 0.04f, 0.4f, "Linen", solid: false);
                Box("Flamme", church, new Vector3(x, 1.43f, 14.9f), new Vector3(0.03f, 0.06f, 0.03f), "LampGlow", solid: false);
            }
            Box("Croix", church, new Vector3(0f, 2.9f, 15.85f), new Vector3(0.18f, 2f, 0.08f), "WoodDark", solid: false);
            Box("Croix", church, new Vector3(0f, 3.3f, 15.85f), new Vector3(1.1f, 0.18f, 0.08f), "WoodDark", solid: false);
            var lectern = Group(church, "Pupitre", new Vector3(2.6f, 0f, 13f));
            Box("Pied", lectern, new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 1.1f, 0.12f), "Wood", solid: false);
            Box("Plateau", lectern, new Vector3(0f, 1.15f, 0f), new Vector3(0.6f, 0.04f, 0.45f), "Wood", Quaternion.Euler(-20f, 0f, 0f), solid: false);
            Box("Livre", lectern, new Vector3(0f, 1.19f, 0.01f), new Vector3(0.4f, 0.04f, 0.3f), "BlanketRed", Quaternion.Euler(-20f, 0f, 0f), solid: false);
        }

        // Pallet racking along local X: orange beams on blue frames, pallets of boxes on three levels, gaps where
        // goods were taken.
        static void PalletRack(Transform parent, Vector3 pos, System.Random rng)
        {
            const float length = 8f, height = 3.2f, depth = 0.8f;
            var p = Piece(parent, "Etagere", pos, 0f, new Vector3(length, height, depth));
            for (var i = 0; i <= 3; i++)
            foreach (var z in new[] { -depth / 2f + 0.04f, depth / 2f - 0.04f })
                Bit(p, "Montant", new Vector3(-length / 2f + 0.05f + i * (length - 0.1f) / 3f, height / 2f, z), new Vector3(0.08f, height, 0.08f), "CarBlue");
            foreach (var y in new[] { 0.15f, 1.2f, 2.25f })
            {
                foreach (var z in new[] { -depth / 2f + 0.04f, depth / 2f - 0.04f })
                    Bit(p, "Lisse", new Vector3(0f, y, z), new Vector3(length, 0.1f, 0.06f), "Obstacle");
                for (var bay = 0; bay < 3; bay++)
                {
                    if (rng.NextDouble() < 0.3) continue;
                    var x = -length / 2f + (bay + 0.5f) * length / 3f;
                    Bit(p, "Palette", new Vector3(x, y + 0.11f, 0f), new Vector3(1.2f, 0.12f, depth - 0.1f), "Wood");
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
            Cylinder("Corps", h, new Vector3(0f, 0.28f, 0f), 0.12f, 0.56f, "SignRed");
            Cylinder("Chapeau", h, new Vector3(0f, 0.6f, 0f), 0.1f, 0.08f, "SignRed", solid: false);
            Cylinder("Ecrou", h, new Vector3(0f, 0.67f, 0f), 0.03f, 0.05f, "MetalLight", solid: false);
            Cylinder("Sortie", h, new Vector3(0f, 0.38f, 0f), 0.05f, 0.34f, "SignRed", Quaternion.Euler(0f, 0f, 90f), solid: false);
            Cylinder("Sortie", h, new Vector3(0f, 0.38f, 0f), 0.06f, 0.3f, "MetalLight", Quaternion.Euler(90f, 0f, 0f), solid: false);
        }

        static void Bench(Transform parent, Vector3 pos, float yaw)
        {
            var p = Piece(parent, "Banc_Public", pos, yaw, new Vector3(1.6f, 0.85f, 0.5f));
            for (var i = 0; i < 3; i++)
                Bit(p, "Latte", new Vector3(0f, 0.45f, -0.15f + i * 0.13f), new Vector3(1.6f, 0.04f, 0.1f), "Wood");
            for (var i = 0; i < 2; i++)
                Bit(p, "Dossier", new Vector3(0f, 0.62f + i * 0.14f, 0.24f), new Vector3(1.6f, 0.1f, 0.03f), "Wood", Quaternion.Euler(-10f, 0f, 0f));
            foreach (var x in new[] { -0.7f, 0.7f })
                Bit(p, "Pied", new Vector3(x, 0.38f, 0.02f), new Vector3(0.05f, 0.76f, 0.5f), "Metal");
        }

        static void NewsBox(Transform parent, Vector3 pos, float yaw, string mat)
        {
            var p = Piece(parent, "Distributeur_Journaux", pos, yaw, new Vector3(0.45f, 1.05f, 0.4f));
            Bit(p, "Pieds", new Vector3(0f, 0.15f, 0f), new Vector3(0.4f, 0.3f, 0.35f), "Metal");
            Bit(p, "Caisse", new Vector3(0f, 0.68f, 0f), new Vector3(0.45f, 0.76f, 0.4f), mat);
            Bit(p, "Vitre", new Vector3(0f, 0.78f, -0.205f), new Vector3(0.34f, 0.3f, 0.01f), "Glass");
            Bit(p, "Journal", new Vector3(0f, 0.74f, -0.2f), new Vector3(0.28f, 0.2f, 0.005f), "Paper");
        }
    }
}
