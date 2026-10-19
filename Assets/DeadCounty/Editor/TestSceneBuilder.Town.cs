using UnityEngine;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        static readonly (string siding, string shutter, string roof)[] HouseStyles =
        {
            ("SidingCream", "ShutterGreen", "RoofDark"),
            ("SidingBlue", "ShutterDark", "Roof"),
            ("SidingSage", "ShutterRed", "RoofDark"),
            ("SidingWhite", "ShutterDark", "RoofRust"),
            ("SidingYellow", "ShutterGreen", "Roof"),
            ("SidingRed", "ShutterDark", "RoofGrey"),
        };

        static void BuildVillage(Transform parent)
        {
            var rng = new System.Random(11);
            float[] lots = { 24f, 46f, 68f, 90f, 112f };
            for (var i = 0; i < lots.Length; i++)
            {
                Lot(parent, $"Maison_O{i + 1}", new Vector3(MainStreetX - 12f, 0f, lots[i]), -90f, i, rng, boarded: i == 2);
                Lot(parent, $"Maison_E{i + 1}", new Vector3(MainStreetX + 12f, 0f, lots[i]), 90f, i + 3, rng, boarded: i == 4);
            }
            var lamps = Group(parent, "Lampadaires");
            for (var z = 14f; z <= 114f; z += 25f)
            {
                StreetLamp(lamps, new Vector3(MainStreetX - 5.6f, 0f, z), 90f);
                StreetLamp(lamps, new Vector3(MainStreetX + 5.6f, 0f, z + 12.5f), -90f);
            }
            StreetLamp(lamps, new Vector3(MainStreetX - 5.6f, 0f, -22f), 90f);
            StreetLamp(lamps, new Vector3(MainStreetX + 5.6f, 0f, -34f), -90f);
        }

        // Residential lot around a house: driveway, garage, mailbox, fenced backyard with random yard props.
        // Root-local frame: street on -Z, lot spans x -8..14 and z -6..19.5.
        static void Lot(Transform parent, string name, Vector3 frontCenter, float yaw, int style, System.Random rng, bool boarded)
        {
            var root = House(parent, name, frontCenter, yaw, style, boarded);
            var roof = HouseStyles[style % HouseStyles.Length].roof;

            Box("Allee", root, new Vector3(9.5f, 0.02f, -1.5f), new Vector3(3f, 0.04f, 9f), "Concrete");
            Box("Chemin", root, new Vector3(-1f, 0.02f, -4.4f), new Vector3(1.2f, 0.04f, 3.2f), "Concrete");
            Garage(root, new Vector3(9.5f, 0f, 3f), roof);
            Mailbox(root, new Vector3(7.4f, 0f, -5.4f));
            TrashCan(root, new Vector3(11.6f, 0f, -5.2f));
            TrashCan(root, new Vector3(12.4f, 0f, -5.1f), "Metal");
            FenceLoop(root, FenceKind.Board, new Vector3(-7.5f, 0f, 3f), new Vector3(-7.5f, 0f, 19f), new Vector3(13.5f, 0f, 19f), new Vector3(13.5f, 0f, 9.5f));

            if (rng.NextDouble() < 0.75) PlaceTree(root, new Vector3(Range(rng, -5f, -2f), 0f, Range(rng, 14f, 17f)), rng, false);
            if (rng.NextDouble() < 0.5) Shed(root, new Vector3(9.5f, 0f, 16f), 180f, roof);
            else PicnicTable(root, new Vector3(8.5f, 0f, 14.5f), Range(rng, -20f, 20f));
            if (rng.NextDouble() < 0.5) Clothesline(root, new Vector3(0.5f, 0f, 12.5f), new Vector3(5.5f, 0f, 13f));
            if (rng.NextDouble() < 0.5) Box("Tas_Bois", root, new Vector3(-6.9f, 0.5f, 12f), new Vector3(0.7f, 1f, 2.4f), "WoodDark");
            if (rng.NextDouble() < 0.6) PlaceTree(root, new Vector3(-5.6f, 0f, -3.6f), rng, false);
            if (rng.NextDouble() < 0.35) Car(root, "Voiture_Allee", new Vector3(9.5f, 0f, -1.5f), 90f + Range(rng, -4f, 4f), rng.NextDouble() < 0.5 ? "CarRed" : "CarBlue");

            ReserveLocal(root, -8f, -6f, 14f, 19.5f);
        }

        // 12 x 10 m single-storey house: living room, kitchen, bedroom, bathroom, porch and gable roof.
        // Root-local frame: front wall on z = 0 facing -Z, house spans x -6..6 and z 0..10.
        static Transform House(Transform parent, string name, Vector3 frontCenter, float yaw, int style, bool boarded)
        {
            var (siding, shutter, roof) = HouseStyles[style % HouseStyles.Length];
            var root = Group(parent, name, frontCenter, yaw);
            var body = Group(root, "Maison", new Vector3(-6f, 0f, 0f));
            var ext = new WallStyle(siding, WallHeight, shutter);
            var inner = new WallStyle("Interior");

            Box("Sol", body, new Vector3(6f, 0.05f, 5f), new Vector3(12f, 0.1f, 10f), "Floor");
            Wall(body, "Mur_Facade", new Vector2(0f, 0f), new Vector2(12f, 0f), ext, Window(2f), Door(5f), Window(9.5f));
            Wall(body, "Mur_Droit", new Vector2(12f, 0f), new Vector2(12f, 10f), ext, Window(2.5f), Window(7.5f));
            Wall(body, "Mur_Arriere", new Vector2(12f, 10f), new Vector2(0f, 10f), ext, Window(3f), Door(9f));
            Wall(body, "Mur_Gauche", new Vector2(0f, 10f), new Vector2(0f, 0f), ext, Window(5f));
            Wall(body, "Cloison_Salon", new Vector2(7f, 0f), new Vector2(7f, 10f), inner, Door(3f), Door(7.5f));
            Wall(body, "Cloison_Chambre", new Vector2(7f, 5.5f), new Vector2(12f, 5.5f), inner, Door(1.5f));
            Wall(body, "Cloison_Cuisine", new Vector2(0f, 6f), new Vector2(7f, 6f), inner, Door(5.5f));

            Roof(body, "Toit", new Vector3(6f, WallHeight, 5f), 12.8f, 10.8f, 2.2f, roof);
            Box("Cheminee", body, new Vector3(2f, 4.4f, 7.5f), new Vector3(0.9f, 2.8f, 0.9f), "Brick");

            Box("Porche", body, new Vector3(5f, 0.1f, -1.3f), new Vector3(3.6f, 0.2f, 2.6f), "Wood");
            Box("Marche", body, new Vector3(5f, 0.05f, -2.85f), new Vector3(1.6f, 0.1f, 0.5f), "Wood");
            Post("Poteau_Porche", body, new Vector3(3.4f, 0.2f, -2.4f), 2.6f, 0.07f, "Trim");
            Post("Poteau_Porche", body, new Vector3(6.6f, 0.2f, -2.4f), 2.6f, 0.07f, "Trim");
            Box("Auvent", body, new Vector3(5f, 2.9f, -1.3f), new Vector3(4f, 0.12f, 2.9f), roof, Quaternion.Euler(-8f, 0f, 0f), solid: false);
            Box("Marche_Arriere", body, new Vector3(9f, 0.05f, 10.4f), new Vector3(1.4f, 0.1f, 0.6f), "Concrete");

            foreach (var x in new[] { 1f, 2.8f, 7.5f, 9.6f })
                MeshObject("Haie", body, BushMesh(1), new Vector3(x, 0f, -0.75f), Quaternion.Euler(0f, x * 40f, 0f), new Vector3(0.8f, 0.85f, 0.6f), new[] { "Bush" }, false);

            if (boarded)
                foreach (var offset in new[] { 2f, 9.5f })
                    for (var k = 0; k < 3; k++)
                        Box("Planche", body, new Vector3(offset, 1.15f + k * 0.35f, -0.17f), new Vector3(1.5f, 0.16f, 0.04f), "WoodDark", Quaternion.Euler(0f, 0f, k == 1 ? -12f : 9f), solid: false);

            Furnish(body);
            return root;
        }

        static void Furnish(Transform body)
        {
            var f = Group(body, "Mobilier");
            // Living room (x 0..7, z 0..6)
            Box("Canape", f, new Vector3(2f, 0.4f, 1.2f), new Vector3(2.2f, 0.8f, 0.9f), "Fabric");
            Box("Table_Basse", f, new Vector3(2f, 0.25f, 2.7f), new Vector3(1.2f, 0.5f, 0.6f), "Furniture");
            Box("Meuble_TV", f, new Vector3(2f, 0.3f, 5.5f), new Vector3(1.8f, 0.6f, 0.45f), "Furniture");
            Box("TV", f, new Vector3(2f, 0.9f, 5.55f), new Vector3(1.2f, 0.62f, 0.08f), "CarBlack", solid: false);
            Box("Fauteuil", f, new Vector3(4.6f, 0.4f, 3.4f), new Vector3(0.9f, 0.8f, 0.9f), "Fabric");
            Box("Bibliotheque", f, new Vector3(0.3f, 1f, 2.8f), new Vector3(0.4f, 2f, 1.4f), "Furniture");
            // Kitchen (x 0..7, z 6..10)
            Box("Plan_Travail", f, new Vector3(0.4f, 0.45f, 8.4f), new Vector3(0.6f, 0.9f, 3f), "Furniture");
            Box("Placard_Haut", f, new Vector3(0.25f, 1.9f, 8.4f), new Vector3(0.35f, 0.7f, 3f), "Furniture");
            Box("Frigo", f, new Vector3(0.5f, 0.9f, 6.5f), new Vector3(0.7f, 1.8f, 0.7f), "Porcelain");
            Box("Table_Cuisine", f, new Vector3(3.6f, 0.38f, 8f), new Vector3(1.6f, 0.75f, 0.9f), "Furniture");
            foreach (var (x, z) in new[] { (3.1f, 7.2f), (4.1f, 7.2f), (3.1f, 8.8f), (4.1f, 8.8f) })
                Box("Chaise", f, new Vector3(x, 0.23f, z), new Vector3(0.45f, 0.46f, 0.45f), "Wood", solid: false);
            // Bedroom (x 7..12, z 5.5..10)
            Box("Lit", f, new Vector3(10.5f, 0.25f, 8.5f), new Vector3(1.6f, 0.5f, 2.1f), "Furniture");
            Box("Matelas", f, new Vector3(10.5f, 0.55f, 8.5f), new Vector3(1.5f, 0.15f, 2f), "Mattress", solid: false);
            Box("Armoire", f, new Vector3(11.65f, 1f, 6.15f), new Vector3(0.6f, 2f, 1f), "Furniture");
            Box("Commode", f, new Vector3(8.2f, 0.45f, 9.65f), new Vector3(1.2f, 0.9f, 0.45f), "Furniture");
            // Bathroom (x 7..12, z 0..5.5)
            Box("Baignoire", f, new Vector3(10.8f, 0.3f, 0.6f), new Vector3(1.7f, 0.6f, 0.8f), "Porcelain");
            Box("Toilettes", f, new Vector3(11.6f, 0.25f, 4.4f), new Vector3(0.6f, 0.5f, 0.45f), "Porcelain");
            Box("Meuble_SdB", f, new Vector3(7.4f, 0.45f, 4.6f), new Vector3(0.5f, 0.9f, 1f), "Furniture");
        }

        // 6 x 6 m garage, front wall on z = 0 facing -Z.
        static void Garage(Transform parent, Vector3 frontCenter, string roof)
        {
            var g = Group(parent, "Garage", frontCenter);
            var style = new WallStyle("Wall", 2.8f);
            Box("Dalle", g, new Vector3(0f, 0.05f, 3f), new Vector3(6f, 0.1f, 6f), "Concrete");
            Wall(g, "Mur_Avant", new Vector2(-3f, 0f), new Vector2(3f, 0f), style, Door(3f, 2.6f, 2.3f));
            Wall(g, "Mur_Droit", new Vector2(3f, 0f), new Vector2(3f, 6f), style);
            Wall(g, "Mur_Arriere", new Vector2(3f, 6f), new Vector2(-3f, 6f), style, Door(3f));
            Wall(g, "Mur_Gauche", new Vector2(-3f, 6f), new Vector2(-3f, 0f), style);
            Roof(g, "Toit", new Vector3(0f, 2.8f, 3f), 6.6f, 6.6f, 1.4f, roof);
            Box("Etabli", g, new Vector3(-2.6f, 0.45f, 3.5f), new Vector3(0.6f, 0.9f, 2.4f), "Wood");
            Box("Etagere", g, new Vector3(2.6f, 0.9f, 3.5f), new Vector3(0.5f, 1.8f, 2f), "Metal");
        }

        // White wooden church facing Main Street, with steeple and a small graveyard on its south side.
        static void BuildChurch(Transform parent)
        {
            var root = Group(parent, "Eglise", new Vector3(MainStreetX + 12f, 0f, -60f), 90f);
            var style = new WallStyle("SidingWhite", 4.5f, "ShutterDark");
            Box("Sol", root, new Vector3(0f, 0.05f, 8f), new Vector3(9f, 0.1f, 16f), "Floor");
            Wall(root, "Facade", new Vector2(-4.5f, 0f), new Vector2(4.5f, 0f), style, Door(4.5f, 1.8f, 2.7f));
            Wall(root, "Mur_Droit", new Vector2(4.5f, 0f), new Vector2(4.5f, 16f), style, Window(4f, 0.8f, 1.3f, 3.4f), Window(8f, 0.8f, 1.3f, 3.4f), Window(12f, 0.8f, 1.3f, 3.4f));
            Wall(root, "Mur_Arriere", new Vector2(4.5f, 16f), new Vector2(-4.5f, 16f), style);
            Wall(root, "Mur_Gauche", new Vector2(-4.5f, 16f), new Vector2(-4.5f, 0f), style, Window(4f, 0.8f, 1.3f, 3.4f), Window(8f, 0.8f, 1.3f, 3.4f), Window(12f, 0.8f, 1.3f, 3.4f));
            Roof(root, "Toit", new Vector3(0f, 4.5f, 8f), 16.8f, 9.8f, 4f, "RoofDark", 90f);
            Box("Clocher", root, new Vector3(0f, 8.5f, 1.6f), new Vector3(2.4f, 8f, 2.4f), "SidingWhite");
            MeshObject("Fleche", root, ConeMesh(4), new Vector3(0f, 12.5f, 1.6f), Quaternion.Euler(0f, 45f, 0f), new Vector3(1.8f, 4.5f, 1.8f), new[] { "RoofDark" }, false);
            Box("Croix", root, new Vector3(0f, 17.6f, 1.6f), new Vector3(0.1f, 1.2f, 0.1f), "Metal", solid: false);
            Box("Croix", root, new Vector3(0f, 17.8f, 1.6f), new Vector3(0.6f, 0.1f, 0.1f), "Metal", solid: false);
            Box("Parvis", root, new Vector3(0f, 0.1f, -1.5f), new Vector3(4f, 0.2f, 3f), "Concrete");
            for (var row = 0; row < 6; row++)
            foreach (var side in new[] { -1f, 1f })
                Box("Banc", root, new Vector3(side * 2.1f, 0.45f, 4.5f + row * 1.6f), new Vector3(2.6f, 0.9f, 0.5f), "Wood");
            Box("Autel", root, new Vector3(0f, 0.5f, 14.8f), new Vector3(3f, 1f, 1f), "Trim");

            var rng = new System.Random(31);
            var graves = Group(root, "Cimetiere");
            for (var i = 0; i < 4; i++)
            for (var j = 0; j < 6; j++)
                Box("Tombe", graves, new Vector3(7.5f + i * 2.4f, 0.45f, 2.5f + j * 2.2f), new Vector3(0.6f, 0.9f, 0.18f), "Headstone",
                    Quaternion.Euler(Range(rng, -6f, 6f), Range(rng, -8f, 8f), Range(rng, -5f, 5f)));
            FenceLoop(graves, FenceKind.Chain, new Vector3(6f, 0f, 0f), new Vector3(17.5f, 0f, 0f), new Vector3(17.5f, 0f, 15.5f), new Vector3(6f, 0f, 15.5f), new Vector3(6f, 0f, 2f));
            MeshObject("Arbre_Mort", graves, TreeMesh(1), new Vector3(15.5f, 0f, 13f), Quaternion.identity, Vector3.one * 1.2f, new[] { "Trunk", "FoliageRust" }, false);

            ReserveLocal(root, -6f, -4f, 18f, 18f);
        }
    }
}
