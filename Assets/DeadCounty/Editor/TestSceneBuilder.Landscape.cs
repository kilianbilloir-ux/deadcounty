using UnityEngine;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        // Flat playable area, hills only in the outer band, flattened where the two roads leave the map.
        static float TerrainHeight(float x, float z)
        {
            var edge = Mathf.Min(HalfMap - Mathf.Abs(x), HalfMap - Mathf.Abs(z));
            var hill = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 4f, edge));
            if (hill <= 0f) return 0f;
            var roads = Mathf.Min(Mathf.InverseLerp(7f, 20f, Mathf.Abs(z)), Mathf.InverseLerp(7f, 20f, Mathf.Abs(x - MainStreetX)));
            var noise = Mathf.PerlinNoise(x * 0.03f + 37.1f, z * 0.03f + 11.7f);
            return hill * roads * (4f + noise * 12f);
        }

        static void BuildTerrain(Transform parent)
        {
            var mesh = TerrainMesh();
            var ground = MeshObject("Sol_300m", parent, mesh, Vector3.zero, Quaternion.identity, Vector3.one, new[] { "Grass" }, false);
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;

            // Invisible walls behind the hills keep the player inside the map.
            var limits = new (string, Vector3, Vector3)[]
            {
                ("Limite_N", new Vector3(0f, 15f, HalfMap), new Vector3(MapSize, 30f, 1f)),
                ("Limite_S", new Vector3(0f, 15f, -HalfMap), new Vector3(MapSize, 30f, 1f)),
                ("Limite_E", new Vector3(HalfMap, 15f, 0f), new Vector3(1f, 30f, MapSize)),
                ("Limite_O", new Vector3(-HalfMap, 15f, 0f), new Vector3(1f, 30f, MapSize)),
            };
            foreach (var (name, pos, size) in limits)
                Object.DestroyImmediate(Box(name, parent, pos, size, "Wall").GetComponent<MeshRenderer>());
        }

        static void BuildRoads(Transform parent)
        {
            Box("Route_Principale", parent, new Vector3(0f, 0.02f, 0f), new Vector3(MapSize, 0.04f, RoadWidth), "Road");
            Box("Main_Street", parent, new Vector3(MainStreetX, 0.021f, 0f), new Vector3(RoadWidth, 0.042f, MapSize), "Road");
            Box("Chemin_Entrepot", parent, new Vector3(0f, 0.015f, -31f), new Vector3(4.5f, 0.03f, 55f), "Gravel");
            Box("Chemin_Ferme", parent, new Vector3(-85f, 0.015f, 40f), new Vector3(4.5f, 0.03f, 73f), "Dirt");
            Reserve(-HalfMap, -8f, HalfMap, 8f);
            Reserve(MainStreetX - 8f, -HalfMap, MainStreetX + 8f, HalfMap);
            Reserve(-4f, -60f, 4f, -3f);
            Reserve(-89f, 3f, -81f, 78f);

            var marks = Group(parent, "Marquages");
            for (var x = -HalfMap + 4f; x < HalfMap - 4f; x += 8f)
                if (Mathf.Abs(x - MainStreetX) > 9f)
                    Box("Ligne_Centre", marks, new Vector3(x, 0.045f, 0f), new Vector3(3f, 0.01f, 0.15f), "RoadLineYellow", solid: false);
            for (var z = -HalfMap + 4f; z < HalfMap - 4f; z += 8f)
                if (Mathf.Abs(z) > 9f)
                    Box("Ligne_Centre", marks, new Vector3(MainStreetX, 0.047f, z), new Vector3(0.15f, 0.01f, 3f), "RoadLineYellow", solid: false);

            const float edgeLine = RoadWidth / 2f - 0.3f;
            const float cut = RoadWidth / 2f;
            foreach (var side in new[] { -1f, 1f })
            {
                EdgeLine(marks, new Vector3(-HalfMap, 0f, side * edgeLine), new Vector3(MainStreetX - cut, 0f, side * edgeLine));
                EdgeLine(marks, new Vector3(MainStreetX + cut, 0f, side * edgeLine), new Vector3(HalfMap, 0f, side * edgeLine));
                EdgeLine(marks, new Vector3(MainStreetX + side * edgeLine, 0f, -HalfMap), new Vector3(MainStreetX + side * edgeLine, 0f, -cut));
                EdgeLine(marks, new Vector3(MainStreetX + side * edgeLine, 0f, cut), new Vector3(MainStreetX + side * edgeLine, 0f, HalfMap));
            }
            for (var i = 0; i < 7; i++)
                Box("Passage_Pieton", marks, new Vector3(MainStreetX - 3f + i, 0.049f, 6.3f), new Vector3(0.5f, 0.01f, 2.4f), "RoadLineWhite", solid: false);
            Box("Ligne_Stop", marks, new Vector3(MainStreetX - 1.75f, 0.049f, 8f), new Vector3(3.2f, 0.01f, 0.35f), "RoadLineWhite", solid: false);
            Box("Ligne_Stop", marks, new Vector3(MainStreetX + 1.75f, 0.049f, -8f), new Vector3(3.2f, 0.01f, 0.35f), "RoadLineWhite", solid: false);

            var walks = Group(parent, "Trottoirs");
            foreach (var side in new[] { -1f, 1f })
            {
                var x = MainStreetX + side * 4.75f;
                Box("Trottoir", walks, new Vector3(x, 0.075f, 63f), new Vector3(2.5f, 0.15f, 119f), "Sidewalk");
                Box("Trottoir", walks, new Vector3(x, 0.075f, -24.5f), new Vector3(2.5f, 0.15f, 42f), "Sidewalk");
                Box("Trottoir", walks, new Vector3(51.25f, 0.075f, side * 4.75f), new Vector3(30.5f, 0.15f, 2.5f), "Sidewalk");
                Box("Trottoir", walks, new Vector3(89.75f, 0.075f, side * 4.75f), new Vector3(32.5f, 0.15f, 2.5f), "Sidewalk");
            }
        }

        static void EdgeLine(Transform parent, Vector3 a, Vector3 b)
        {
            var mid = (a + b) / 2f;
            Box("Ligne_Rive", parent, new Vector3(mid.x, 0.046f, mid.z), new Vector3(Mathf.Max(0.12f, Mathf.Abs(b.x - a.x)), 0.01f, Mathf.Max(0.12f, Mathf.Abs(b.z - a.z))), "RoadLineWhite", solid: false);
        }

        static void BuildRoadside(Transform parent)
        {
            var posts = Group(parent, "Balises_10m");
            for (var x = -140; x <= 140; x += 10)
            {
                if (x > 30 && x < 110) continue;
                Post($"Balise_X{x:+0;-0}", posts, new Vector3(x, 0f, -5.5f), 1.1f, 0.06f, "Marker", solid: false);
                Box("Catadioptre", posts, new Vector3(x, 0.95f, -5.5f), new Vector3(0.14f, 0.18f, 0.14f), x % 50 == 0 ? "MarkerMajor" : "SignYellow", solid: false);
            }
            for (var z = -140; z <= -50; z += 10)
            {
                Post($"Balise_Z{z}", posts, new Vector3(MainStreetX + 5.5f, 0f, z), 1.1f, 0.06f, "Marker", solid: false);
                Box("Catadioptre", posts, new Vector3(MainStreetX + 5.5f, 0.95f, z), new Vector3(0.14f, 0.18f, 0.14f), z % 50 == 0 ? "MarkerMajor" : "SignYellow", solid: false);
            }

            var power = Group(parent, "Ligne_Electrique");
            Vector3? previous = null;
            for (var x = -135f; x <= 145f; x += 35f)
            {
                var basePos = new Vector3(x, 0f, 8f);
                Post("Poteau", power, basePos, 9f, 0.14f, "WoodDark");
                Box("Traverse", power, basePos + Vector3.up * 8.4f, new Vector3(0.12f, 0.12f, 2.2f), "WoodDark", solid: false);
                if (previous is Vector3 p)
                    foreach (var side in new[] { -0.9f, 0.9f })
                        Cable(power, p + new Vector3(0f, 8.5f, side), basePos + new Vector3(0f, 8.5f, side));
                previous = basePos;
            }
            Cylinder("Transformateur", power, new Vector3(-30.4f, 7.2f, 8f), 0.35f, 1f, "MetalLight", solid: false);

            var signs = Group(parent, "Panneaux");
            StopSign(signs, new Vector3(MainStreetX - 5.2f, 0f, 8.5f), 0f);
            StopSign(signs, new Vector3(MainStreetX + 5.2f, 0f, -8.5f), 180f);
            var welcome = Group(signs, "Panneau_Village", new Vector3(36f, 0f, -6.5f), -90f);
            Post("Pied", welcome, new Vector3(-0.9f, 0f, 0f), 2.4f, 0.05f, "Wood", solid: false);
            Post("Pied", welcome, new Vector3(0.9f, 0f, 0f), 2.4f, 0.05f, "Wood", solid: false);
            Box("Panneau", welcome, new Vector3(0f, 1.9f, 0f), new Vector3(2.4f, 1f, 0.08f), "SignGreen", solid: false);
            Box("Cadre", welcome, new Vector3(0f, 1.9f, -0.05f), new Vector3(2.1f, 0.75f, 0.02f), "SignWhite", solid: false);

            var wrecks = Group(parent, "Epaves");
            Car(wrecks, "Voiture_Abandonnee", new Vector3(-48f, 0f, 1.4f), 14f, "CarBlue");
            Car(wrecks, "Voiture_Brulee", new Vector3(-112f, 0f, -1.2f), 168f, "CarBurnt", burnt: true);
            Bus(wrecks, new Vector3(-20f, 0f, -7.6f), 28f);
            Pickup(wrecks, "Pickup_Accidente", new Vector3(-31.8f, 0f, 5.6f), 52f, "CarRed", tilt: 4f);
            PoliceCar(wrecks, new Vector3(MainStreetX - 1.2f, 0f, -0.8f), 72f);
            for (var i = 0; i < 3; i++) Sawhorse(wrecks, new Vector3(MainStreetX + 7f, 0f, -2.4f + i * 2.4f), 90f);
            Barrel(wrecks, new Vector3(MainStreetX + 8.2f, 0f, 3.8f), "SignRed");
            Barrel(wrecks, new Vector3(MainStreetX + 8.6f, 0f, 4.5f), "Metal");
            Car(wrecks, "Voiture_Abandonnee", new Vector3(MainStreetX + 1.6f, 0f, 38f), 184f, "CarBeige");
            Car(wrecks, "Voiture_Brulee", new Vector3(104f, 0f, 1.2f), 95f, "CarBurnt", burnt: true);
            Car(wrecks, "Voiture_Abandonnee", new Vector3(MainStreetX - 1.8f, 0f, -72f), -8f, "CarGreen", tilt: -3f);
        }

        static void Cable(Transform parent, Vector3 a, Vector3 b)
            => Box("Cable", parent, (a + b) / 2f, new Vector3(0.03f, 0.03f, Vector3.Distance(a, b)), "CarBlack", Quaternion.LookRotation(b - a), solid: false);

        static void StopSign(Transform parent, Vector3 basePos, float yaw)
        {
            Post("Panneau_Stop", parent, basePos, 2.3f, 0.04f, "Metal", solid: false);
            Cylinder("Stop", parent, basePos + Vector3.up * 2.15f, 0.38f, 0.04f, "SignRed", Quaternion.Euler(90f, yaw, 0f), solid: false);
        }

        static float ForestDensity(float x, float z)
        {
            var edge = Mathf.Min(HalfMap - Mathf.Abs(x), HalfMap - Mathf.Abs(z));
            if (edge < 3f) return 0f;
            if (edge < 32f) return 0.85f;
            if (x < -25f && z < -18f) return 0.7f;            // south-west woods around the AI clearing
            if (x > 100f && z < -40f) return 0.65f;           // south-east woods behind the church
            if (x > 104f) return 0.5f;                        // behind the east backyards
            if (z > 64f && x > -40f && x < 37f) return 0.55f; // woods north of the test zone
            return 0.06f;
        }

        static void BuildVegetation(Transform parent)
        {
            var rng = new System.Random(7);
            var trees = Group(parent, "Arbres");
            var bushes = Group(parent, "Buissons");
            var rocks = Group(parent, "Rochers");
            const float cell = 7f;
            for (var cx = -HalfMap + cell / 2f; cx < HalfMap; cx += cell)
            for (var cz = -HalfMap + cell / 2f; cz < HalfMap; cz += cell)
            {
                var x = cx + Range(rng, -0.4f, 0.4f) * cell;
                var z = cz + Range(rng, -0.4f, 0.4f) * cell;
                var roll = rng.NextDouble();
                if (IsReserved(x, z, 1.5f)) continue;
                var density = ForestDensity(x, z);
                var ground = new Vector3(x, TerrainHeight(x, z), z);
                if (roll < density) PlaceTree(trees, ground + Vector3.down * 0.15f, rng, density > 0.4f);
                else if (roll < density + 0.12f) PlaceBush(bushes, ground, rng);
                else if (roll > 0.97) PlaceRock(rocks, ground, rng);
                if (density > 0.4f && rng.NextDouble() < 0.35)
                    PlaceBush(bushes, ground + new Vector3(Range(rng, 1.5f, 3f), 0f, Range(rng, -2f, 2f)), rng);
            }
        }

        static void PlaceTree(Transform parent, Vector3 pos, System.Random rng, bool forest)
        {
            var pine = rng.NextDouble() < (forest ? 0.6 : 0.3);
            var variant = rng.Next(0, 3);
            var foliage = pine ? "Pine" : rng.NextDouble() switch
            {
                < 0.45 => "Foliage",
                < 0.7 => "FoliageOlive",
                < 0.88 => "FoliageAutumn",
                _ => "FoliageRust",
            };
            var scale = (pine ? 1.3f : 1f) * Range(rng, 0.8f, 1.4f);
            var tree = MeshObject(pine ? "Pin" : "Arbre", parent, pine ? PineMesh(variant) : TreeMesh(variant), pos,
                Quaternion.Euler(0f, rng.Next(0, 360), 0f), Vector3.one * scale, new[] { "Trunk", foliage }, false);
            var trunk = tree.AddComponent<CapsuleCollider>();
            trunk.radius = 0.3f;
            trunk.height = 4f;
            trunk.center = new Vector3(0f, 2f, 0f);
        }

        static void PlaceBush(Transform parent, Vector3 pos, System.Random rng)
        {
            var s = Range(rng, 0.7f, 1.5f);
            MeshObject("Buisson", parent, BushMesh(rng.Next(0, 2)), pos + Vector3.down * 0.1f, Quaternion.Euler(0f, rng.Next(0, 360), 0f),
                new Vector3(s, s * Range(rng, 0.8f, 1.1f), s), new[] { "Bush" }, false);
        }

        static void PlaceRock(Transform parent, Vector3 pos, System.Random rng)
        {
            var s = Range(rng, 0.6f, 2.2f);
            MeshObject("Rocher", parent, RockMesh(rng.Next(0, 3)), pos, Quaternion.Euler(0f, rng.Next(0, 360), 0f), Vector3.one * s, new[] { "Rock" }, true);
        }
    }
}
