using DeadCounty.Player;
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

        static readonly float[] VillageLots = { 24f, 46f, 68f, 90f, 112f };
        static readonly Vector3 ChurchOrigin = new(MainStreetX + 12f, 0f, -60f);
        const float ChurchYaw = 90f;

        static Vector3 LotFront(int lot, bool east) => new(MainStreetX + (east ? 12f : -12f), 0f, VillageLots[lot]);

        static float LotYaw(bool east) => east ? 90f : -90f;

        // World point of a lot given in its root-local frame (see Lot).
        static Vector3 LotPoint(int lot, bool east, float x, float z) => LotFront(lot, east) + Quaternion.Euler(0f, LotYaw(east), 0f) * new Vector3(x, 0f, z);

        static void BuildVillage(Transform parent)
        {
            var rng = new System.Random(11);
            for (var i = 0; i < VillageLots.Length; i++)
            {
                Lot(parent, $"Maison_O{i + 1}", LotFront(i, false), LotYaw(false), i, rng, boarded: i == 2);
                Lot(parent, $"Maison_E{i + 1}", LotFront(i, true), LotYaw(true), i + 3, rng, boarded: i == 4);
            }
            var lamps = Group(parent, "Lampadaires");
            for (var z = 14f; z <= 114f; z += 25f)
            {
                BuiltLamps.Add(StreetLamp(lamps, new Vector3(MainStreetX - 5.6f, 0f, z), 90f));
                BuiltLamps.Add(StreetLamp(lamps, new Vector3(MainStreetX + 5.6f, 0f, z + 12.5f), -90f));
            }
            BuiltLamps.Add(StreetLamp(lamps, new Vector3(MainStreetX - 5.6f, 0f, -22f), 90f));
            BuiltLamps.Add(StreetLamp(lamps, new Vector3(MainStreetX + 5.6f, 0f, -34f), -90f));
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

            Furnish(body, new System.Random(Mathf.RoundToInt(frontCenter.x * 73f + frontCenter.z * 31f)));
            return root;
        }

        // Rooms in the body frame: floor top at 0.1, living room x 0..7 z 0..6, kitchen x 0..7 z 6..10, bedroom
        // x 7..12 z 5.5..10, bathroom x 7..12 z 0..5.5. Footprints match the former single boxes, so the NavMesh,
        // the zombie spots and the searchable pieces stay where they were.
        static void Furnish(Transform body, System.Random rng)
        {
            const float floor = 0.1f;
            Vector3 At(float x, float z) => new(x, floor, z);
            var f = Group(body, "Mobilier");
            string[] fabrics = { "Fabric", "FabricRust", "FabricOlive" };
            var fabric = fabrics[rng.Next(fabrics.Length)];

            // Living room: the sofa faces the TV against the kitchen wall.
            Rug(f, new Vector3(2.2f, 0f, 2.6f), 0f, new Vector2(3f, 2f), rng.NextDouble() < 0.5 ? "Rug" : "RugBlue");
            Sofa(f, "Canape", At(2f, 1.05f), 180f, 2.2f, fabric);
            Table(f, "Table_Basse", At(2f, 2.7f), 0f, new Vector3(1.2f, 0.42f, 0.6f), "Furniture");
            Searchable(TvStand(f, At(2f, 5.6f), 0f).gameObject, "Meuble TV", "LT_Salon");
            Armchair(f, At(4.6f, 3.4f), 90f, fabric);
            Bookcase(f, At(0.3f, 2.8f), -90f, rng);
            Frame(f, new Vector3(4.6f, 1.6f, 5.88f), 0f, new Vector2(0.8f, 0.6f), "SidingSage");
            CeilingLamp(f, new Vector3(3.5f, 0f, 3f));
            Clutter(f, rng, Rect.MinMaxRect(3.4f, 0.8f, 6.4f, 2.4f), rng.Next(2, 5));

            // Kitchen: worktop and fridge along the side wall, a table with its chairs, one knocked over.
            Searchable(Counter(f, At(0.4f, 8.4f), -90f, 3f, true, rng).gameObject, "Placards", "LT_Cuisine_Placard");
            Searchable(Fridge(f, At(0.5f, 6.5f), -90f).gameObject, "Réfrigérateur", "LT_Cuisine_Frigo");
            Table(f, "Table_Cuisine", At(3.6f, 8f), 0f, new Vector3(1.6f, 0.75f, 0.9f), "Furniture");
            Chair(f, At(3.1f, 7.25f), 180f);
            Chair(f, At(4.1f, 7.25f), 180f);
            Chair(f, At(3.1f, 8.75f), 0f);
            if (rng.NextDouble() < 0.6) Chair(f, At(4.9f, 9.1f) + Vector3.up * 0.22f, Range(rng, 0f, 360f), Quaternion.Euler(90f, 0f, 0f));
            else Chair(f, At(4.1f, 8.75f), 0f);
            CeilingLamp(f, new Vector3(3.5f, 0f, 8f));
            Clutter(f, rng, Rect.MinMaxRect(1.2f, 6.4f, 6.4f, 7.1f), rng.Next(1, 4));

            // Bedroom: head of the bed against the back wall, a pulled-out drawer.
            DoubleBed(f, At(10.5f, 8.8f), 0f, rng.NextDouble() < 0.5 ? "Blanket" : "BlanketRed").gameObject.AddComponent<Bed>();
            Nightstand(f, At(9.25f, 9.65f), 0f);
            Searchable(Wardrobe(f, At(11.6f, 6.15f), 90f).gameObject, "Armoire", "LT_Chambre");
            Searchable(Dresser(f, At(8.2f, 9.67f), 0f).gameObject, "Commode", "LT_Chambre");
            Frame(f, new Vector3(10.5f, 1.75f, 9.88f), 0f, new Vector2(0.6f, 0.45f), "Hay");
            CeilingLamp(f, new Vector3(9.5f, 0f, 7.75f));
            Clutter(f, rng, Rect.MinMaxRect(7.4f, 6f, 9.4f, 8.4f), rng.Next(1, 3));

            // Bathroom.
            Bathtub(f, At(11f, 0.5f), 0f);
            Toilet(f, At(11.57f, 4.4f), 90f);
            Searchable(BathroomCabinet(f, At(7.35f, 4.6f), -90f).gameObject, "Armoire à pharmacie", "LT_SalleDeBain");
            CeilingLamp(f, new Vector3(9.5f, 0f, 2.75f));
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
            var rng = new System.Random(Mathf.RoundToInt(frontCenter.x * 53f + frontCenter.z * 17f));
            Workbench(g, new Vector3(-2.6f, 0.1f, 3.5f), -90f, rng);
            Searchable(MetalShelf(g, new Vector3(2.6f, 0.1f, 3.5f), 90f, rng).gameObject, "Étagère", "LT_Atelier");
        }

        // White wooden church facing Main Street, with steeple and a small graveyard on its south side.
        static void BuildChurch(Transform parent)
        {
            var root = Group(parent, "Eglise", ChurchOrigin, ChurchYaw);
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
