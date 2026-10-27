using UnityEngine;

namespace DeadCounty.EditorTools
{
    // Exterior details that make the blocks read as buildings: a concrete foundation band, corner boards, gutters
    // and downspouts, a porch railing and lamp, an air conditioner or a satellite dish on some houses; the barn's
    // sliding doors, hayloft and cupola; the silo's hoops and ladder; tall grass in the yards; wheat and corn
    // fields made of plants instead of slabs. Nothing here is solid except the air conditioners.
    public static partial class TestSceneBuilder
    {
        // House body frame (see House): walls on x 0..12 and z 0..10, 0.2 m thick, front door at x 5, back door at
        // x 3, roof eaves at z -0.4 and 10.4.
        static void HouseExterior(Transform body, System.Random rng)
        {
            var ext = Group(body, "Exterieur");
            const float face = WallThickness / 2f + 0.03f;

            // Foundation band, broken at the doors.
            void Band(string name, Vector3 center, Vector3 size) => Box(name, ext, center, size, "Concrete", solid: false);
            Band("Soubassement", new Vector3(2.175f, 0.12f, -face), new Vector3(4.55f, 0.24f, 0.06f));
            Band("Soubassement", new Vector3(8.825f, 0.12f, -face), new Vector3(6.55f, 0.24f, 0.06f));
            Band("Soubassement", new Vector3(1.175f, 0.12f, 10f + face), new Vector3(2.55f, 0.24f, 0.06f));
            Band("Soubassement", new Vector3(7.825f, 0.12f, 10f + face), new Vector3(8.55f, 0.24f, 0.06f));
            Band("Soubassement", new Vector3(-face, 0.12f, 5f), new Vector3(0.06f, 0.24f, 10.26f));
            Band("Soubassement", new Vector3(12f + face, 0.12f, 5f), new Vector3(0.06f, 0.24f, 10.26f));

            foreach (var (x, z) in new[] { (-face, -face), (12f + face, -face), (-face, 10f + face), (12f + face, 10f + face) })
                Box("Planche_Angle", ext, new Vector3(x, WallHeight / 2f, z), new Vector3(0.12f, WallHeight, 0.12f), "Trim", solid: false);

            // Fascia and gutter under both eaves, a downspout at each end.
            foreach (var z in new[] { -0.42f, 10.42f })
            {
                var outward = Mathf.Sign(z - 5f);
                Box("Bandeau", ext, new Vector3(6f, WallHeight - 0.06f, z), new Vector3(12.8f, 0.16f, 0.04f), "Trim", solid: false);
                Box("Gouttiere", ext, new Vector3(6f, WallHeight - 0.12f, z + outward * 0.07f), new Vector3(12.8f, 0.1f, 0.1f), "Metal", solid: false);
                foreach (var x in new[] { -0.3f, 12.3f })
                    Box("Descente", ext, new Vector3(x, (WallHeight - 0.12f) / 2f, z + outward * 0.07f), new Vector3(0.07f, WallHeight - 0.12f, 0.07f), "Metal", solid: false);
            }

            // Front door frame, porch lamp and a railing on both sides of the step.
            Box("Linteau_Porte", ext, new Vector3(5f, 2.16f, -face), new Vector3(1.24f, 0.12f, 0.05f), "Trim", solid: false);
            foreach (var x in new[] { 4.44f, 5.56f })
                Box("Chambranle_Porte", ext, new Vector3(x, 1.05f, -face), new Vector3(0.1f, 2.1f, 0.05f), "Trim", solid: false);
            Box("Applique", ext, new Vector3(5.85f, 1.9f, -face - 0.06f), new Vector3(0.14f, 0.22f, 0.12f), "Lampshade", solid: false);
            foreach (var (from, to) in new[] { (3.3f, 4.2f), (5.8f, 6.7f) })
                Railing(ext, new Vector3(from, 0.2f, -2.5f), new Vector3(to, 0.2f, -2.5f));
            Railing(ext, new Vector3(3.3f, 0.2f, -2.5f), new Vector3(3.3f, 0.2f, -0.15f));
            Railing(ext, new Vector3(6.7f, 0.2f, -2.5f), new Vector3(6.7f, 0.2f, -0.15f));

            if (rng.NextDouble() < 0.5)
            {
                // Air conditioner against the side wall, solid like any box the player could bump into.
                var ac = Group(ext, "Climatiseur", new Vector3(12.55f, 0f, Range(rng, 6.5f, 8.5f)));
                Box("Caisson", ac, new Vector3(0f, 0.42f, 0f), new Vector3(0.7f, 0.8f, 0.8f), "MetalLight");
                Cylinder("Grille", ac, new Vector3(0f, 0.83f, 0f), 0.3f, 0.02f, "CarBlack", solid: false);
            }
            if (rng.NextDouble() < 0.35)
            {
                // Dish on the front slope of the roof, aimed south.
                var dish = Group(ext, "Parabole", new Vector3(Range(rng, 8.5f, 10.5f), 3.75f, 1.5f), 180f);
                Box("Pied", dish, new Vector3(0f, 0.15f, 0f), new Vector3(0.05f, 0.3f, 0.05f), "Metal", solid: false);
                Cylinder("Disque", dish, new Vector3(0f, 0.38f, 0.04f), 0.32f, 0.04f, "Porcelain", Quaternion.Euler(-60f, 0f, 0f), solid: false);
                Box("Bras", dish, new Vector3(0f, 0.42f, 0.22f), new Vector3(0.03f, 0.03f, 0.35f), "Metal", Quaternion.Euler(-30f, 0f, 0f), solid: false);
            }
        }

        // Porch railing between two points on the porch floor: two rails and balusters.
        static void Railing(Transform parent, Vector3 a, Vector3 b)
        {
            var length = Vector3.Distance(a, b);
            var rail = Group(parent, "Rambarde", a);
            rail.localRotation = Quaternion.LookRotation(b - a);
            foreach (var y in new[] { 0.15f, 0.85f })
                Box("Lisse", rail, new Vector3(0f, y, length / 2f), new Vector3(0.06f, 0.06f, length), "Trim", solid: false);
            for (var z = 0.06f; z < length; z += 0.18f)
                Box("Balustre", rail, new Vector3(0f, 0.5f, z), new Vector3(0.035f, 0.7f, 0.035f), "Trim", solid: false);
        }

        // Clumps of tall grass along the foundations, the fences and the edges of a lot (lot root frame, see Lot).
        static void TallGrass(Transform lot, System.Random rng)
        {
            var grass = Group(lot, "Herbes");
            for (var i = 0; i < 40; i++)
            {
                Vector3 pos;
                switch (rng.Next(4))
                {
                    case 0: pos = new Vector3(Range(rng, -6.2f, 6.2f), 0f, Range(rng, -0.55f, -0.25f)); break;
                    case 1: pos = new Vector3(Range(rng, -7.3f, 13.3f), 0f, Range(rng, 18.4f, 18.8f)); break;
                    case 2: pos = new Vector3(Range(rng, -7.3f, -7f), 0f, Range(rng, 3.5f, 18.5f)); break;
                    default: pos = new Vector3(Range(rng, -6f, 6f), 0f, Range(rng, 10.5f, 13f)); break;
                }
                MeshObject("Touffe", grass, GrassTuftMesh(rng.Next(3)), pos, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f),
                    Vector3.one * Range(rng, 1f, 1.6f), new[] { "GrassTall" }, false);
            }
        }

        // Barn front: doors slid open on their rail, a hayloft door in the gable, a cupola with a weathervane on the
        // ridge (barn frame, front wall on z = 0 facing -Z, ridge along Z at 11.5 m).
        static void BarnDetails(Transform barn)
        {
            const float face = -WallThickness / 2f - 0.05f;
            Box("Rail", barn, new Vector3(0f, 4.75f, face), new Vector3(13.6f, 0.08f, 0.06f), "Metal", solid: false);
            foreach (var x in new[] { -4.6f, 4.6f })
                XPanel(barn, "Porte_Coulissante", new Vector3(x, 2.3f, face - 0.04f), new Vector2(2.3f, 4.5f), "BarnRed");
            XPanel(barn, "Porte_Fenil", new Vector3(0f, 7.4f, -0.5f), new Vector2(2f, 2f), "BarnRed");

            var cupola = Group(barn, "Lanterneau", new Vector3(0f, 11.2f, 11f));
            Box("Caisson", cupola, new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 1f, 1.4f), "BarnRed", solid: false);
            foreach (var z in new[] { -0.71f, 0.71f })
                Box("Persienne", cupola, new Vector3(0f, 0.5f, z), new Vector3(0.9f, 0.6f, 0.02f), "Trim", solid: false);
            MeshObject("Toit", cupola, ConeMesh(4), new Vector3(0f, 1f, 0f), Quaternion.Euler(0f, 45f, 0f), new Vector3(1.15f, 0.8f, 1.15f), new[] { "RoofRust" }, false);
            Box("Mat", cupola, new Vector3(0f, 2.2f, 0f), new Vector3(0.04f, 0.9f, 0.04f), "Metal", solid: false);
            Box("Fleche", cupola, new Vector3(0f, 2.45f, 0.1f), new Vector3(0.03f, 0.12f, 0.7f), "Metal", solid: false);
        }

        // A board panel with a white border and a white X brace.
        static void XPanel(Transform parent, string name, Vector3 center, Vector2 size, string mat)
        {
            var panel = Group(parent, name, center);
            Box("Planches", panel, Vector3.zero, new Vector3(size.x, size.y, 0.06f), mat, solid: false);
            const float trim = 0.14f;
            Box("Cadre", panel, new Vector3(0f, size.y / 2f - trim / 2f, -0.04f), new Vector3(size.x, trim, 0.03f), "Trim", solid: false);
            Box("Cadre", panel, new Vector3(0f, -size.y / 2f + trim / 2f, -0.04f), new Vector3(size.x, trim, 0.03f), "Trim", solid: false);
            Box("Cadre", panel, new Vector3(-size.x / 2f + trim / 2f, 0f, -0.04f), new Vector3(trim, size.y, 0.03f), "Trim", solid: false);
            Box("Cadre", panel, new Vector3(size.x / 2f - trim / 2f, 0f, -0.04f), new Vector3(trim, size.y, 0.03f), "Trim", solid: false);
            var diagonal = Mathf.Sqrt(size.x * size.x + size.y * size.y) - trim * 2f;
            var angle = Mathf.Atan2(size.y, size.x) * Mathf.Rad2Deg;
            foreach (var sign in new[] { -1f, 1f })
                Box("Croix", panel, new Vector3(0f, 0f, -0.04f), new Vector3(diagonal, trim, 0.03f), "Trim", Quaternion.Euler(0f, 0f, sign * angle), solid: false);
        }

        static void SiloDetails(Transform parent, Vector3 baseCenter, float radius, float height)
        {
            var silo = Group(parent, "Silo_Details", baseCenter);
            for (var y = 1.5f; y < height; y += 2f)
                Cylinder("Cerclage", silo, new Vector3(0f, y, 0f), radius + 0.04f, 0.1f, "Metal", solid: false);
            // Ladder up the side facing the yard.
            var ladder = Group(silo, "Echelle", new Vector3(0f, 0f, -radius - 0.12f));
            foreach (var x in new[] { -0.22f, 0.22f })
                Box("Montant", ladder, new Vector3(x, height / 2f, 0f), new Vector3(0.05f, height, 0.05f), "Metal", solid: false);
            for (var y = 0.4f; y < height; y += 0.4f)
                Box("Barreau", ladder, new Vector3(0f, y, 0f), new Vector3(0.44f, 0.03f, 0.03f), "Metal", solid: false);
        }

        static Mesh GrassTuftMesh(int variant) => Cached($"SM_GrassTuft_{variant}", () =>
        {
            var rng = new System.Random(600 + variant);
            var m = new MeshBuilder().Sub();
            for (var i = 0; i < 14; i++)
            {
                var angle = Range(rng, 0f, Mathf.PI * 2f);
                var root = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Range(rng, 0f, 0.25f);
                Blade(m, root, Range(rng, 0f, 360f), Range(rng, 0.3f, 0.7f), 0.1f, Range(rng, -0.3f, 0.3f));
            }
            return m.Save($"SM_GrassTuft_{variant}");
        });

        // A thin leaf from `root`, leaning by `lean` along its own facing; both faces, as low poly blades are seen
        // from every side.
        static void Blade(MeshBuilder m, Vector3 root, float yaw, float height, float width, float lean)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var a = root + rotation * new Vector3(-width / 2f, 0f, 0f);
            var b = root + rotation * new Vector3(width / 2f, 0f, 0f);
            var tip = root + rotation * new Vector3(0f, height, lean * height);
            var normal = rotation * Vector3.forward;
            m.Tri(a, b, tip, normal);
            m.Tri(a, b, tip, -normal);
        }

        // Rows of wheat over the whole field, two shades, one mesh: plants on a footprint centred on the origin.
        static Mesh WheatFieldMesh(Vector2 size) => Cached("SM_WheatField", () =>
        {
            var rng = new System.Random(700);
            var m = new MeshBuilder();
            m.Sub();
            m.Sub();
            for (var x = -size.x / 2f + 0.2f; x < size.x / 2f - 0.2f; x += 0.45f)
            for (var z = -size.y / 2f + 0.2f; z < size.y / 2f - 0.2f; z += 0.38f)
            {
                m.Use(rng.NextDouble() < 0.35 ? 1 : 0);
                var root = new Vector3(x + Range(rng, -0.12f, 0.12f), 0f, z + Range(rng, -0.1f, 0.1f));
                var height = Range(rng, 0.65f, 0.95f);
                var yaw = Range(rng, 0f, 360f);
                for (var k = 0; k < 4; k++)
                    Blade(m, root, yaw + k * 45f, height * Range(rng, 0.85f, 1f), 0.15f, Range(rng, -0.12f, 0.12f));
            }
            return m.Save("SM_WheatField");
        });

        // One row of corn plants along Z, centred on the origin: a stalk, three leaves and a tassel each.
        static Mesh CornRowMesh(int variant, float length) => Cached($"SM_CornRow_{variant}", () =>
        {
            var rng = new System.Random(800 + variant);
            var m = new MeshBuilder();
            m.Sub();
            m.Sub();
            for (var z = -length / 2f + 0.2f; z < length / 2f - 0.2f; z += Range(rng, 0.3f, 0.42f))
            {
                var root = new Vector3(Range(rng, -0.08f, 0.08f), 0f, z);
                var height = Range(rng, 1.9f, 2.4f);
                m.Use(0);
                for (var k = 0; k < 2; k++) Blade(m, root, k * 90f, height, 0.06f, 0f);
                for (var leaf = 0; leaf < 3; leaf++)
                {
                    var start = root + Vector3.up * (0.5f + leaf * 0.45f);
                    Blade(m, start, Range(rng, 0f, 360f), Range(rng, 0.6f, 0.85f), 0.12f, Range(rng, 0.7f, 1.1f));
                }
                m.Use(1);
                Blade(m, root + Vector3.up * height, Range(rng, 0f, 360f), 0.25f, 0.08f, 0.2f);
            }
            return m.Save($"SM_CornRow_{variant}");
        });
    }
}
