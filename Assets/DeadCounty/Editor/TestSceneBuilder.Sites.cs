using UnityEngine;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        static readonly Color Sodium = new(1f, 0.78f, 0.45f);
        static readonly Color ColdWhite = new(0.92f, 0.95f, 1f);
        static readonly Vector3 ClearingCenter = new(-80f, 0f, -72f);
        static readonly Vector3 WarehouseOrigin = new(-15f, 0f, -80f);

        // South-east corner of the crossroads: canopy with two pump islands, shop facing the pumps.
        static void BuildGasStation(Transform parent)
        {
            var lot = Group(parent, "Station", new Vector3(91f, 0f, -20.5f));
            Box("Dalle", lot, new Vector3(0f, 0.05f, 0f), new Vector3(28f, 0.1f, 27f), "Concrete");

            var canopy = Group(lot, "Auvent", new Vector3(-2f, 0.1f, 6.5f));
            Box("Toit", canopy, new Vector3(0f, 4.85f, 0f), new Vector3(14f, 0.5f, 9f), "SignWhite");
            Box("Bandeau", canopy, new Vector3(0f, 4.8f, 0f), new Vector3(14.06f, 0.22f, 9.06f), "SignRed", solid: false);
            Box("Eclairage", canopy, new Vector3(0f, 4.59f, 0f), new Vector3(11f, 0.02f, 6.5f), "PanelGlow", solid: false);
            foreach (var x in new[] { -2.2f, 2.2f })
            {
                Box("Ilot", canopy, new Vector3(x, 0.1f, 0f), new Vector3(1.1f, 0.2f, 5.5f), "Concrete");
                Box("Pilier", canopy, new Vector3(x, 2.3f, -2.4f), new Vector3(0.4f, 4.6f, 0.4f), "SignWhite");
                Box("Pilier", canopy, new Vector3(x, 2.3f, 2.4f), new Vector3(0.4f, 4.6f, 0.4f), "SignWhite");
                foreach (var z in new[] { -1.1f, 1.1f })
                {
                    Box("Pompe", canopy, new Vector3(x, 1f, z), new Vector3(0.7f, 1.6f, 0.5f), "SignRed");
                    Box("Ecran", canopy, new Vector3(x, 1.45f, z), new Vector3(0.72f, 0.3f, 0.3f), "CarBlack", solid: false);
                }
                Post("Borne", canopy, new Vector3(x, 0f, -3.2f), 0.9f, 0.1f, "SignYellow");
                Post("Borne", canopy, new Vector3(x, 0f, 3.2f), 0.9f, 0.1f, "SignYellow");
                BuiltLamps.Add(SpotLight(canopy, "Lumiere_Auvent", new Vector3(x * 1.6f, 4.5f, 0f), Quaternion.Euler(90f, 0f, 0f), ColdWhite, 8f, 9f, 120f, false));
            }

            var shop = Group(lot, "Boutique", new Vector3(-3f, 0.1f, -13.5f));
            var style = new WallStyle("SignWhite", 3.6f);
            var w1 = Window(2.5f, 3.6f, 0.7f, 2.6f);
            var w2 = Window(9.5f, 3.6f, 0.7f, 2.6f);
            var front = Wall(shop, "Vitrine", new Vector2(12f, 8f), new Vector2(0f, 8f), style, w1, Door(6f, 1.6f, 2.3f), w2);
            Glaze(front, w1);
            Glaze(front, w2);
            Wall(shop, "Mur_O", new Vector2(0f, 8f), new Vector2(0f, 0f), style);
            Wall(shop, "Mur_S", new Vector2(0f, 0f), new Vector2(12f, 0f), style, Door(10f));
            Wall(shop, "Mur_E", new Vector2(12f, 0f), new Vector2(12f, 8f), style);
            Box("Bandeau", shop, new Vector3(6f, 3.1f, 8.13f), new Vector3(12.2f, 0.45f, 0.06f), "SignRed", solid: false);
            Box("Toit", shop, new Vector3(6f, 3.7f, 4f), new Vector3(12.6f, 0.2f, 8.6f), "RoofGrey");
            foreach (var x in new[] { 3f, 5.5f, 8f })
                Box("Rayon", shop, new Vector3(x, 0.75f, 3.6f), new Vector3(0.6f, 1.5f, 3.6f), "MetalLight");
            Box("Comptoir", shop, new Vector3(10.4f, 0.5f, 6.2f), new Vector3(2.4f, 1f, 0.7f), "Furniture");
            Box("Vitrine_Froide", shop, new Vector3(6f, 1f, 0.5f), new Vector3(7f, 2f, 0.7f), "Glass");

            Box("Mat_Enseigne", lot, new Vector3(-12f, 4f, 12f), new Vector3(0.35f, 8f, 0.35f), "Metal");
            Box("Enseigne", lot, new Vector3(-12f, 7.4f, 12f), new Vector3(0.4f, 1.8f, 2.6f), "SignRed", solid: false);
            Box("Enseigne_Face", lot, new Vector3(-12f, 7.4f, 12f), new Vector3(0.44f, 1.2f, 2f), "PanelGlow", solid: false);
            Box("Prix", lot, new Vector3(-12f, 5.9f, 12f), new Vector3(0.3f, 1f, 2f), "SignWhite", solid: false);
            Box("Glaciere", lot, new Vector3(-1.5f, 0.65f, -4.9f), new Vector3(1.6f, 1.1f, 0.8f), "SignWhite");
            Box("Cage_Gaz", lot, new Vector3(7.5f, 0.85f, -4.9f), new Vector3(1.6f, 1.5f, 0.8f), "MetalLight");
            Box("Benne", lot, new Vector3(10.5f, 0.85f, -11f), new Vector3(1.8f, 1.5f, 1.2f), "CarGreen");
            Post("Gonfleur", lot, new Vector3(-11f, 0.1f, -2f), 1.2f, 0.12f, "SignRed");
            Car(lot, "Voiture_Pompe", new Vector3(-6.9f, 0.1f, 6.5f), 90f, "CarRed");
            Car(lot, "Voiture_Brulee", new Vector3(8f, 0.1f, 9f), 30f, "CarBurnt", burnt: true);
            Pickup(lot, "Pickup", new Vector3(9f, 0.1f, 0f), 180f, "CarWhite");
            Reserve(76f, -35f, 106f, -6f);
        }

        // South-west corner of the crossroads: brick general store with a false front and a parking lot.
        static void BuildGeneralStore(Transform parent)
        {
            var lot = Group(parent, "Magasin", new Vector3(49.5f, 0f, -20.5f));
            Box("Parking", lot, new Vector3(0f, 0.03f, 0f), new Vector3(27f, 0.06f, 27f), "Road");
            for (var i = 0; i < 8; i++)
                Box("Marquage", lot, new Vector3(-10f + i * 2.8f, 0.065f, 2.5f), new Vector3(0.12f, 0.01f, 5f), "RoadLineWhite", solid: false);

            var b = Group(lot, "Batiment", new Vector3(-9f, 0.06f, -13f));
            var style = new WallStyle("Brick", 4.2f);
            var w1 = Window(3.5f, 5f, 0.8f, 2.8f);
            var w2 = Window(14.5f, 5f, 0.8f, 2.8f);
            var front = Wall(b, "Facade", new Vector2(18f, 11f), new Vector2(0f, 11f), style, w1, Door(9f, 1.8f, 2.5f), w2);
            Glaze(front, w1);
            Glaze(front, w2);
            Wall(b, "Mur_O", new Vector2(0f, 11f), new Vector2(0f, 0f), style);
            Wall(b, "Mur_S", new Vector2(0f, 0f), new Vector2(18f, 0f), style, Door(15f));
            Wall(b, "Mur_E", new Vector2(18f, 0f), new Vector2(18f, 11f), style, Window(5.5f, 1.2f, 1f, 2.2f));
            Box("Toit", b, new Vector3(9f, 4.3f, 5.5f), new Vector3(18.4f, 0.2f, 11.4f), "RoofGrey");
            Box("Fronton", b, new Vector3(9f, 5f, 11f), new Vector3(10f, 1.6f, 0.25f), "Brick");
            Box("Corniche", b, new Vector3(9f, 4.25f, 11.2f), new Vector3(18.4f, 0.25f, 0.45f), "Trim", solid: false);
            Box("Store_Banne", b, new Vector3(9f, 3.1f, 12.1f), new Vector3(17f, 0.08f, 2.2f), "SignGreen", Quaternion.Euler(12f, 0f, 0f), solid: false);
            Box("Enseigne", b, new Vector3(9f, 3.75f, 11.17f), new Vector3(6f, 0.8f, 0.1f), "SignGreen", solid: false);
            Box("Enseigne_Cadre", b, new Vector3(9f, 3.75f, 11.23f), new Vector3(5.6f, 0.5f, 0.02f), "SignWhite", solid: false);
            foreach (var x in new[] { 3f, 6f, 12f, 15f })
                Box("Rayon", b, new Vector3(x, 0.8f, 5f), new Vector3(0.7f, 1.6f, 6f), "Furniture");
            Box("Comptoir", b, new Vector3(4.5f, 0.5f, 9.6f), new Vector3(3f, 1f, 0.7f), "Furniture");
            Box("Congelateurs", b, new Vector3(9f, 0.6f, 0.6f), new Vector3(8f, 1.2f, 0.9f), "SignWhite");

            Car(lot, "Voiture_Parking", new Vector3(-5.8f, 0.06f, 2.5f), 90f, "CarGreen");
            Pickup(lot, "Pickup_Parking", new Vector3(2.6f, 0.06f, 2.6f), 92f, "CarBlack");
            Box("Benne", lot, new Vector3(11f, 0.85f, -8f), new Vector3(1.8f, 1.5f, 1.2f), "CarBlue");
            PalletStack(lot, new Vector3(11f, 0.06f, -11f), 4);
            Reserve(35f, -35f, 64f, -6f);
        }

        // North-west: farmhouse, red barn, silo, pasture, corn field (blocks sight) and wheat field.
        static void BuildFarm(Transform parent)
        {
            var rng = new System.Random(23);
            Box("Cour", parent, new Vector3(-88f, 0.015f, 87.5f), new Vector3(36f, 0.03f, 21f), "Gravel");
            House(parent, "Ferme_Maison", new Vector3(-62f, 0f, 84f), 0f, 3, false);
            Pickup(parent, "Pickup_Ferme", new Vector3(-72f, 0f, 82f), 100f, "CarBeige");
            Box("Tas_Bois", parent, new Vector3(-55f, 0.6f, 92f), new Vector3(0.8f, 1.2f, 3f), "WoodDark");
            Mailbox(parent, new Vector3(-82f, 0f, 6.5f));

            var barn = Group(parent, "Grange", new Vector3(-96f, 0f, 98f));
            var red = new WallStyle("BarnRed", 6f);
            var high = new[] { Window(5f, 1.2f, 3.6f, 4.6f), Window(11f, 1.2f, 3.6f, 4.6f), Window(17f, 1.2f, 3.6f, 4.6f) };
            Box("Sol", barn, new Vector3(0f, 0.05f, 11f), new Vector3(14f, 0.1f, 22f), "Dirt");
            Wall(barn, "Facade", new Vector2(-7f, 0f), new Vector2(7f, 0f), red, Door(7f, 4.5f, 4.6f));
            Wall(barn, "Mur_E", new Vector2(7f, 0f), new Vector2(7f, 22f), red, high);
            Wall(barn, "Mur_Arriere", new Vector2(7f, 22f), new Vector2(-7f, 22f), red, Door(7f, 3f, 3f));
            Wall(barn, "Mur_O", new Vector2(-7f, 22f), new Vector2(-7f, 0f), red, high);
            Roof(barn, "Toit", new Vector3(0f, 6f, 11f), 22.8f, 14.8f, 5.5f, "RoofRust", 90f);
            Box("Encadrement", barn, new Vector3(0f, 4.75f, -0.12f), new Vector3(4.9f, 0.2f, 0.06f), "Trim", solid: false);
            Box("Encadrement", barn, new Vector3(-2.35f, 2.3f, -0.12f), new Vector3(0.2f, 4.6f, 0.06f), "Trim", solid: false);
            Box("Encadrement", barn, new Vector3(2.35f, 2.3f, -0.12f), new Vector3(0.2f, 4.6f, 0.06f), "Trim", solid: false);
            for (var i = 0; i < 4; i++)
            foreach (var side in new[] { -1f, 1f })
                Box("Stalle", barn, new Vector3(side * 4.3f, 0.7f, 3f + i * 4f), new Vector3(5.2f, 1.4f, 0.15f), "Wood");
            Box("Ballots", barn, new Vector3(-4.5f, 0.9f, 19.5f), new Vector3(4f, 1.8f, 3f), "Hay");
            Box("Ballots", barn, new Vector3(4.5f, 0.6f, 19.5f), new Vector3(4f, 1.2f, 3f), "Hay");

            Cylinder("Silo", parent, new Vector3(-84.5f, 7f, 110f), 2.8f, 14f, "MetalLight");
            MeshObject("Silo_Toit", parent, ConeMesh(12), new Vector3(-84.5f, 14f, 110f), Quaternion.identity, new Vector3(3f, 2.4f, 3f), new[] { "RoofGrey" }, false);
            Tractor(parent, new Vector3(-80f, 0f, 90f), 30f);
            Cylinder("Cuve_Fioul", parent, new Vector3(-101f, 1.4f, 82f), 0.8f, 3f, "MetalLight", Quaternion.Euler(0f, 0f, 90f));
            for (var i = 0; i < 4; i++) HayBale(parent, new Vector3(-93f + i * 1.6f, 0f, 80.5f), 90f);

            FenceLoop(parent, FenceKind.Rail, new Vector3(-108f, 0f, 74f), new Vector3(-126f, 0f, 74f), new Vector3(-126f, 0f, 124f), new Vector3(-108f, 0f, 124f), new Vector3(-108f, 0f, 80f));
            Box("Abreuvoir", parent, new Vector3(-115f, 0.3f, 90f), new Vector3(2.4f, 0.6f, 0.7f), "Metal");
            for (var i = 0; i < 5; i++) HayBale(parent, new Vector3(Range(rng, -123f, -111f), 0f, Range(rng, 78f, 120f)), rng.Next(0, 180));

            var fields = Group(parent, "Champs");
            for (var x = -125.3f; x < -92f; x += 1.4f)
                Box("Rang_Mais", fields, new Vector3(x, 1.1f, 38f), new Vector3(0.55f, 2.2f, 46f), "Corn");
            Box("Champ_Ble", fields, new Vector3(-61f, 0.35f, 47f), new Vector3(38f, 0.7f, 30f), "Wheat", solid: false);
            Box("Chaume", fields, new Vector3(-61f, 0.02f, 23f), new Vector3(38f, 0.04f, 18f), "Dirt");
            for (var i = 0; i < 9; i++)
                HayBale(fields, new Vector3(Range(rng, -78f, -45f), 0f, Range(rng, 16f, 30f)), rng.Next(0, 180));
            Fence(fields, new Vector3(-126f, 0f, 12f), new Vector3(-88.5f, 0f, 12f), FenceKind.Rail);
            Fence(fields, new Vector3(-81.5f, 0f, 12f), new Vector3(-41f, 0f, 12f), FenceKind.Rail);

            Reserve(-127f, 11f, -40f, 64f);
            Reserve(-108f, 76f, -52f, 124f);
            Reserve(-128f, 72f, -106f, 126f);
        }

        // Closed 30 x 20 m warehouse with a single door (flashlight tests) and a container yard in front.
        static void BuildWarehouse(Transform parent)
        {
            var w = Group(parent, "Hangar", WarehouseOrigin);
            var style = new WallStyle("MetalSiding", 5f);
            Box("Sol", w, new Vector3(15f, 0.05f, 10f), new Vector3(30f, 0.1f, 20f), "Concrete");
            Wall(w, "Mur_Nord", new Vector2(30f, 20f), new Vector2(0f, 20f), style, new Opening(15f, 2f, 0f, 2.4f));
            Wall(w, "Mur_Sud", new Vector2(0f, 0f), new Vector2(30f, 0f), style);
            Wall(w, "Mur_Est", new Vector2(30f, 0f), new Vector2(30f, 20f), style);
            Wall(w, "Mur_Ouest", new Vector2(0f, 20f), new Vector2(0f, 0f), style);
            Roof(w, "Toit", new Vector3(15f, 5f, 10f), 30.8f, 20.8f, 2f, "RoofRust");
            for (var row = 0; row < 4; row++)
            for (var i = 0; i < 2; i++)
                Box("Etagere", w, new Vector3(6f + i * 14f, 1.6f, 4f + row * 3.5f), new Vector3(8f, 3.2f, 0.8f), "Metal");
            PalletStack(w, new Vector3(27f, 0.1f, 17f), 3);
            for (var i = 0; i < 3; i++) Barrel(w, new Vector3(27.5f, 0.1f, 3f + i * 0.7f), "SignRed");
            Box("Lampe_Porte", w, new Vector3(15f, 3.1f, 20.25f), new Vector3(0.4f, 0.15f, 0.3f), "LampGlow", solid: false);
            BuiltLamps.Add(SpotLight(w, "Lumiere_Porte", new Vector3(15f, 3f, 20.4f), Quaternion.Euler(60f, 0f, 0f), Sodium, 4f, 10f, 100f, false));

            Box("Cour", parent, new Vector3(0f, 0.015f, -49f), new Vector3(40f, 0.03f, 20f), "Gravel");
            Container(parent, "Conteneur", new Vector3(-13f, 0f, -47f), 8f, "CarRed");
            Container(parent, "Conteneur", new Vector3(12f, 0f, -44f), -5f, "CarBlue");
            Container(parent, "Conteneur", new Vector3(12.3f, 2.6f, -44.2f), -2f, "CarGreen");
            Forklift(parent, new Vector3(5f, 0f, -52f), 120f);
            for (var i = 0; i < 6; i++) Barrel(parent, new Vector3(-6f + i % 3 * 0.7f, 0f, -55f + i / 3 * 0.7f), i % 2 == 0 ? "SignRed" : "Metal");
            PalletStack(parent, new Vector3(-8f, 0f, -42f), 3);
            PalletStack(parent, new Vector3(-6.5f, 0f, -42f), 5);
            Box("Benne", parent, new Vector3(-17f, 0.8f, -58f), new Vector3(2f, 1.6f, 1.4f), "CarGreen");
            Reserve(-22f, -84f, 22f, -37f);
        }

        // Fenced concrete pad with a 1 m grid: obstacle heights, slopes, stairs, crouch tunnel, narrow corridor.
        static void BuildTestCourse(Transform parent)
        {
            Box("Dalle_Essais", parent, new Vector3(0f, 0.05f, 37f), new Vector3(TestPadSize.x, 0.1f, TestPadSize.y), "TestPad");
            FenceLoop(parent, FenceKind.Chain, new Vector3(-4f, 0f, 12.5f), new Vector3(-37.5f, 0f, 12.5f), new Vector3(-37.5f, 0f, 61.5f),
                new Vector3(37.5f, 0f, 61.5f), new Vector3(37.5f, 0f, 12.5f), new Vector3(4f, 0f, 12.5f));
            BuiltLamps.Add(Floodlight(parent, new Vector3(-36f, 0f, 60f), new Vector3(-8f, 0f, 35f)));
            BuiltLamps.Add(Floodlight(parent, new Vector3(36f, 0f, 60f), new Vector3(8f, 0f, 35f)));
            Reserve(-39f, 11f, 39f, 63f);

            var course = Group(parent, "Parcours", new Vector3(0f, 0.1f, 20f));
            var obstacles = Group(course, "Obstacles_Hauteurs");
            float[] heights = { 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };
            for (var i = 0; i < heights.Length; i++)
                Box($"Obstacle_{heights[i]:0.00}m", obstacles, new Vector3(-30f + i * 5f, heights[i] / 2f, 0f), new Vector3(3f, heights[i], 1f), "Obstacle");

            var ramps = Group(course, "Rampes_Pentes");
            float[] angles = { 10f, 20f, 30f, 40f, 50f };
            const float rampLength = 8f;
            for (var i = 0; i < angles.Length; i++)
            {
                var a = angles[i] * Mathf.Deg2Rad;
                var center = new Vector3(10f + i * 5f, Mathf.Sin(a) * rampLength / 2f, 5f + Mathf.Cos(a) * rampLength / 2f);
                Box($"Rampe_{angles[i]}deg", ramps, center, new Vector3(3f, 0.2f, rampLength), "Ramp", Quaternion.Euler(-angles[i], 0f, 0f));
            }

            var stairs = Group(course, "Escaliers");
            BuildStairs(stairs, "Escalier_18cm", new Vector3(-30f, 0f, 12f), 0.18f, 0.30f, 15);
            BuildStairs(stairs, "Escalier_25cm", new Vector3(-24f, 0f, 12f), 0.25f, 0.30f, 11);
            BuildStairs(stairs, "Escalier_35cm", new Vector3(-18f, 0f, 12f), 0.35f, 0.35f, 8);

            var crouch = Group(course, "Tunnel_Accroupi_1m20", new Vector3(-5f, 0f, 15f));
            Box("Plafond", crouch, new Vector3(0f, 1.3f, 4f), new Vector3(2.4f, 0.2f, 8f), "Wall");
            Box("Mur_G", crouch, new Vector3(-1.3f, 0.7f, 4f), new Vector3(0.2f, 1.4f, 8f), "Wall");
            Box("Mur_D", crouch, new Vector3(1.3f, 0.7f, 4f), new Vector3(0.2f, 1.4f, 8f), "Wall");

            // L-shaped 1.2 m corridor to test camera collision against close walls.
            var corridor = Group(course, "Couloir_Etroit_1m20", new Vector3(5f, 0f, 20f));
            var plain = new WallStyle("Wall");
            Wall(corridor, "Mur_G", new Vector2(-0.7f, 0f), new Vector2(-0.7f, 16.4f), plain);
            Wall(corridor, "Mur_D", new Vector2(0.7f, 0f), new Vector2(0.7f, 15f), plain);
            Wall(corridor, "Mur_Fond", new Vector2(-0.7f, 16.4f), new Vector2(12f, 16.4f), plain);
            Wall(corridor, "Mur_Coude", new Vector2(0.7f, 15f), new Vector2(12f, 15f), plain);
            Box("Plafond", corridor, new Vector3(0f, WallHeight + 0.1f, 8f), new Vector3(1.6f, 0.2f, 16.4f), "Roof");
        }

        static void BuildStairs(Transform parent, string name, Vector3 origin, float rise, float run, int steps)
        {
            var root = Group(parent, name, origin);
            for (var i = 0; i < steps; i++)
            {
                var h = rise * (i + 1);
                Box($"Marche_{i + 1}", root, new Vector3(0f, h / 2f, i * run + run / 2f), new Vector3(2f, h, run), "Stairs");
            }
            var top = rise * steps;
            Box("Palier", root, new Vector3(0f, top / 2f, steps * run + 1.5f), new Vector3(2f, top, 3f), "Stairs");
        }

        // Clearing in the south-west woods: rings of posts at 5/10/20/30/50 m from NoiseOrigin (GDD noise radii)
        // and mixed cover for line-of-sight tests.
        static void BuildAIClearing(Transform parent)
        {
            var center = ClearingCenter;
            var c = Group(parent, "Clairiere", center);
            ReservedCircles.Add(new Vector3(center.x, center.z, 53f));
            Post("NoiseOrigin", c, Vector3.zero, 3f, 0.25f, "Accent");

            foreach (var r in new[] { 5, 10, 20, 30, 50 })
            {
                var ring = Group(c, $"Rayon_{r}m");
                const int count = 16;
                for (var i = 0; i < count; i++)
                {
                    var a = i * Mathf.PI * 2f / count;
                    var local = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    local.y = TerrainHeight(center.x + local.x, center.z + local.z);
                    Post($"Poteau_{i}", ring, local, 0.8f, 0.08f, r % 10 == 0 ? "MarkerMajor" : "Marker", solid: false);
                }
            }

            var rng = new System.Random(42);
            var cover = Group(c, "Couverts");
            for (var i = 0; i < 45; i++)
            {
                var pos = new Vector3(rng.Next(-46, 46), 0f, rng.Next(-46, 46));
                var kind = rng.Next(0, 5);
                if (pos.magnitude < 7f || pos.magnitude > 48f) continue;
                switch (kind)
                {
                    case 0:
                    {
                        var h = rng.Next(0, 2) == 0 ? 0.9f : 1.4f;
                        Box("Caisse", cover, pos + Vector3.up * h / 2f, new Vector3(1.5f, h, 1.5f), "Crate", Quaternion.Euler(0f, rng.Next(0, 90), 0f));
                        break;
                    }
                    case 1: HayBale(cover, pos, rng.Next(0, 180)); break;
                    case 2: Log(cover, pos, rng.Next(0, 180)); break;
                    case 3: PlaceRock(cover, pos, rng); break;
                    default:
                        Box("Pile_Bois", cover, pos + Vector3.up * 0.6f, new Vector3(2.5f, 1.2f, 1f), "WoodDark", Quaternion.Euler(0f, rng.Next(0, 180), 0f));
                        break;
                }
            }
            Car(cover, "Epave_Clairiere", new Vector3(18f, 0f, -12f), 35f, "CarBurnt", burnt: true);
            foreach (var p in new[] { new Vector3(-15f, 0f, 22f), new Vector3(25f, 0f, 14f), new Vector3(-28f, 0f, -18f), new Vector3(8f, 0f, -32f) })
                PlaceTree(cover, p, rng, false);
        }
    }
}
