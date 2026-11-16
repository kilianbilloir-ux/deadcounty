using DeadCounty.Audio;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    // The village green west of Main Street, across from the church: a bandstand where two paths cross, a
    // playground, a picnic corner, half a basketball court and the triage post the army left behind.
    public static partial class TestSceneBuilder
    {
        static readonly Vector3 GreenCentre = new(51f, 0f, -58f);
        // Half extents of the fenced lawn; Main Street's kerb is 3.5 m past its east side.
        const float GreenHalfX = 12f;
        const float GreenHalfZ = 17f;

        static Vector3 GreenPoint(float x, float z) => GreenCentre + new Vector3(x, 0f, z);

        static void BuildVillageGreen(Transform parent)
        {
            var rng = new System.Random(23);
            var green = Group(parent, "Place", GreenCentre);
            Reserve(GreenCentre.x - GreenHalfX - 1.5f, GreenCentre.z - GreenHalfZ - 1.5f, GreenCentre.x + GreenHalfX + 4f, GreenCentre.z + GreenHalfZ + 1.5f);

            Box("Allee_Nord_Sud", green, new Vector3(0f, 0.015f, 0f), new Vector3(1.8f, 0.03f, GreenHalfZ * 2f), "Gravel", solid: false);
            Box("Allee_Est_Ouest", green, new Vector3(0.5f, 0.016f, 0f), new Vector3(GreenHalfX * 2f + 1f, 0.032f, 1.8f), "Gravel", solid: false);
            // The west sidewalk stopped at the gas station; it now runs along the green, with a crossing to the church.
            Box("Trottoir", green, new Vector3(GreenHalfX + 2.25f, 0.075f, -3f), new Vector3(2.5f, 0.15f, 31f), "Sidewalk");
            for (var i = 0; i < 7; i++)
                Box("Passage_Pieton", green, new Vector3(GreenHalfX + 4f + i, 0.049f, 0f), new Vector3(0.5f, 0.01f, 2.4f), "RoadLineWhite", solid: false);

            GreenFence(green);
            Bandstand(green);
            Playground(Group(green, "Aire_De_Jeux", new Vector3(-6.5f, 0f, 9.5f)));
            BasketballCourt(Group(green, "Terrain_Basket", new Vector3(-6.5f, 0f, -10f)));
            TriagePost(Group(green, "Poste_Triage", new Vector3(6.5f, 0f, -10f)), rng);

            var picnic = Group(green, "Pique_Nique", new Vector3(6.5f, 0f, 9.5f));
            PicnicTable(picnic, new Vector3(-1.5f, 0f, -2f), 12f);
            PicnicTable(picnic, new Vector3(1.8f, 0f, 3f), 78f);
            TrashCan(picnic, new Vector3(-3.6f, 0f, -5.6f));
            Post("Barbecue_Pied", picnic, new Vector3(3.2f, 0f, -2.2f), 0.8f, 0.04f, "Metal", solid: false);
            Cylinder("Barbecue", picnic, new Vector3(3.2f, 0.9f, -2.2f), 0.3f, 0.22f, "CarBlack");
            PlaceTree(picnic, new Vector3(-3f, 0f, 4.5f), rng, false);
            PlaceTree(picnic, new Vector3(3.8f, 0f, 5.5f), rng, false);

            Memorial(green, new Vector3(9f, 0f, 3.6f));
            foreach (var (x, z, yaw) in new[] { (-1.6f, 6f, 90f), (1.6f, -6f, -90f), (5.5f, 1.6f, 180f), (-5.5f, -1.6f, 0f), (-5.5f, 1.6f, 180f) })
                Bench(green, new Vector3(x, 0f, z), yaw);
            BuiltLamps.Add(StreetLamp(green, new Vector3(1.4f, 0f, 12f), -90f));
            BuiltLamps.Add(StreetLamp(green, new Vector3(-1.4f, 0f, -12f), 90f));
            for (var z = -15f; z <= 15f; z += 3f)
                PlaceBush(green, new Vector3(-GreenHalfX + 0.9f, 0f, z + Range(rng, -0.5f, 0.5f)), rng);
            PlaceTree(green, new Vector3(-9.8f, 0f, 1.8f), rng, false);
            PlaceTree(green, new Vector3(10f, 0f, -3.4f), rng, false);
        }

        // Low rails all around, open where the paths leave: north, south and east onto the sidewalk.
        static void GreenFence(Transform green)
        {
            const float gap = 1.6f;
            Vector3 P(float x, float z) => new(x, 0f, z);
            var fence = Group(green, "Cloture");
            Fence(fence, P(-GreenHalfX, -GreenHalfZ), P(-gap, -GreenHalfZ), FenceKind.Rail);
            Fence(fence, P(gap, -GreenHalfZ), P(GreenHalfX, -GreenHalfZ), FenceKind.Rail);
            Fence(fence, P(GreenHalfX, -GreenHalfZ), P(GreenHalfX, -gap), FenceKind.Rail);
            Fence(fence, P(GreenHalfX, gap), P(GreenHalfX, GreenHalfZ), FenceKind.Rail);
            Fence(fence, P(GreenHalfX, GreenHalfZ), P(gap, GreenHalfZ), FenceKind.Rail);
            Fence(fence, P(-gap, GreenHalfZ), P(-GreenHalfX, GreenHalfZ), FenceKind.Rail);
            Fence(fence, P(-GreenHalfX, GreenHalfZ), P(-GreenHalfX, -GreenHalfZ), FenceKind.Rail);
        }

        // Octagonal platform under a pointed roof; the rails leave the four sides the paths come through.
        static void Bandstand(Transform green)
        {
            const float floor = 0.25f;
            const float radius = 2.75f;
            var b = Group(green, "Kiosque");
            Model("Modele", b, Vector3.zero, null, "kiosque");
            // The base and the deck are octagons: each is the union of four boxes, two of them turned by 45 degrees.
            var turned = Group(b, "Kiosque_45", null, 45f);
            foreach (var (apothem, height) in new[] { (2.956f, 0.12f), (2.772f, floor) })
            foreach (var frame in new[] { b, turned })
            {
                var side = apothem * 2f * Mathf.Tan(22.5f * Mathf.Deg2Rad);
                Solid(frame, new Vector3(0f, height / 2f, 0f), new Vector3(apothem * 2f, height, side), Surface.Wood);
                Solid(frame, new Vector3(0f, height / 2f, 0f), new Vector3(side, height, apothem * 2f), Surface.Wood);
            }
            var posts = new Vector3[8];
            for (var i = 0; i < 8; i++)
            {
                var angle = (22.5f + i * 45f) * Mathf.Deg2Rad;
                posts[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius + Vector3.up * floor;
                Solid(b, posts[i] + Vector3.up * 1.3f, new Vector3(0.126f, 2.6f, 0.126f));
            }
            for (var i = 0; i < 8; i += 2)
            {
                var from = posts[i];
                var to = posts[(i + 1) % 8];
                TiltedSolid(b, "Rambarde", (from + to) / 2f + Vector3.up * 0.9f, Quaternion.LookRotation(to - from), new Vector3(0.07f, 0.07f, Vector3.Distance(from, to)));
            }
        }

        // Swings with one seat hanging by a single chain, a slide, a seesaw and a sandbox on wood chips.
        static void Playground(Transform p)
        {
            Box("Copeaux", p, new Vector3(0f, 0.02f, 0f), new Vector3(9.4f, 0.04f, 10f), "Dirt", solid: false);

            var swings = Group(p, "Balancoire", new Vector3(-2f, 0f, 2.6f));
            Model("Modele", swings, Vector3.zero, null, "balancoire");
            foreach (var x in new[] { -1.7f, 1.7f })
            foreach (var lean in new[] { -1f, 1f })
                TiltedSolid(swings, "Pied", new Vector3(x, 1.12f, lean * 0.42f), Quaternion.Euler(-lean * 20f, 0f, 0f), new Vector3(0.08f, 2.4f, 0.08f));

            var slide = Group(p, "Toboggan", new Vector3(2.4f, 0f, -2.6f));
            Model("Modele", slide, Vector3.zero, null, "toboggan");
            Solid(slide, new Vector3(0f, 1.4f, 0f), new Vector3(1f, 0.08f, 1f), Surface.Wood);
            TiltedSolid(slide, "Glissiere", new Vector3(0f, 0.76f, 1.75f), Quaternion.Euler(27.5f, 0f, 0f), new Vector3(0.7f, 0.05f, 2.85f), Surface.Metal);

            var seesaw = Group(p, "Bascule", new Vector3(-2.6f, 0f, -2.8f), 25f);
            Model("Modele", seesaw, Vector3.zero, null, "bascule");
            Solid(seesaw, new Vector3(0f, 0.25f, 0f), new Vector3(0.3f, 0.5f, 0.4f));

            var sandbox = Group(p, "Bac_A_Sable", new Vector3(2.6f, 0f, 2.8f));
            Model("Modele", sandbox, Vector3.zero, null, "bac_a_sable");
        }

        // The collider of a part set at an angle, on a child turned like the part.
        static void TiltedSolid(Transform parent, string name, Vector3 center, Quaternion rotation, Vector3 size, Surface? surface = null)
        {
            var holder = Group(parent, name, center);
            holder.localRotation = rotation;
            Solid(holder, Vector3.zero, size, surface);
        }

        // Half court: the hoop stands on the south baseline, a ball left where it stopped rolling, a bat beside it.
        static void BasketballCourt(Transform c)
        {
            const float halfWidth = 4.5f;
            const float halfLength = 5.5f;
            const float line = 0.07f;
            void Line(Vector3 centre, Vector3 size) => Box("Ligne", c, centre + Vector3.up * 0.045f, size, "RoadLineWhite", solid: false);

            Box("Dalle", c, new Vector3(0f, 0.02f, 0f), new Vector3(halfWidth * 2f, 0.04f, halfLength * 2f), "Concrete", solid: false);
            Line(new Vector3(0f, 0f, -halfLength + 0.1f), new Vector3(halfWidth * 2f - 0.2f, 0.01f, line));
            Line(new Vector3(0f, 0f, halfLength - 0.1f), new Vector3(halfWidth * 2f - 0.2f, 0.01f, line));
            foreach (var x in new[] { -halfWidth + 0.1f, halfWidth - 0.1f })
                Line(new Vector3(x, 0f, 0f), new Vector3(line, 0.01f, halfLength * 2f - 0.2f));
            foreach (var x in new[] { -1.8f, 1.8f })
                Line(new Vector3(x, 0f, -halfLength + 2.5f), new Vector3(line, 0.01f, 4.8f));
            Line(new Vector3(0f, 0f, -halfLength + 4.9f), new Vector3(3.67f, 0.01f, line));

            Solid(Model("Panier", c, new Vector3(0f, 0f, -halfLength - 0.4f), null, "panier_basket").transform, new Vector3(0f, 1.6f, 0f), new Vector3(0.126f, 3.2f, 0.126f));
            Primitive(PrimitiveType.Sphere, "Ballon", c, new Vector3(2.3f, 0.16f, 0.8f), Vector3.one * 0.24f, "Obstacle", null, false);
            PlaceItem(c, "IT_Batte", new Vector3(-2.6f, 0.04f, 1.6f), 35f);
        }

        // Two army tents with cots and supplies, a row of body bags behind sandbags, and a floodlight nobody turned off.
        static void TriagePost(Transform t, System.Random rng)
        {
            var ward = Tent(t, "Tente_Soins", new Vector3(-2.2f, 0f, -2.5f), 0f);
            Cot(ward, new Vector3(-1f, 0f, -0.2f), 4f, bloody: false);
            Cot(ward, new Vector3(0.9f, 0f, 0.1f), -6f, bloody: true);

            var stores = Tent(t, "Tente_Materiel", new Vector3(2.6f, 0f, 1.6f), 90f);
            Solid(Model("Table", stores, new Vector3(0f, 0f, -0.6f), null, "table_soins").transform, new Vector3(0f, 0.75f, 0f), new Vector3(1.6f, 0.04f, 0.7f));
            Solid(Model("Caisse", stores, new Vector3(1.1f, 0f, 0.7f), null, "caisse_bois").transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), Surface.Wood);
            // The same 0.6 m crate, scaled to the 0.5 m one stacked on it.
            Model("Caisse", stores, new Vector3(1.15f, 0.6f, 0.75f), Quaternion.Euler(0f, 18f, 0f), "caisse_bois").transform.localScale = Vector3.one * (0.5f / 0.6f);
            Solid(Model("Caisse", stores, new Vector3(-1.1f, 0f, 0.8f), null, "caisse_armee").transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.6f, 0.5f), Surface.Wood);

            var bags = Group(t, "Housses_Mortuaires", new Vector3(-2.5f, 0f, 3.2f));
            for (var i = 0; i < 4; i++)
                Model("Housse", bags, new Vector3(i * 0.85f - 1.3f, 0f, Range(rng, -0.1f, 0.1f)), Quaternion.Euler(0f, Range(rng, -6f, 6f), 0f), "housse_mortuaire");
            SandbagWall(t, new Vector3(-0.5f, 0f, 5.2f), 6f, rng);

            var board = Group(t, "Panneau_Triage", new Vector3(4.2f, 0f, 5.4f));
            Model("Modele", board, Vector3.zero, null, "panneau_triage");
            Label(board, "TRIAGE", new Vector3(0f, 1.6f, 0.03f), Vector3.forward, 0.055f, "SignRed");

            Solid(Model("Groupe_Electrogene", t, new Vector3(4.4f, 0f, -4.6f), null, "groupe_electrogene").transform, new Vector3(0f, 0.4f, 0f), new Vector3(1.1f, 0.8f, 0.7f));
            BuiltLamps.Add(Floodlight(t, new Vector3(5.2f, 0f, -5.6f), t.position + new Vector3(-1f, 0f, 0f)));
            Barrel(t, new Vector3(3.2f, 0f, -5.4f), "SignRed");
            Barrel(t, new Vector3(-4.6f, 0f, -5.2f), "CarBlue");
            Papers(t, new Vector3(0f, 0f, 0.5f), new Vector2(8f, 8f), rng, 12);
            BloodStain(t, new Vector3(-0.6f, 0f, 0.4f), rng);
            BloodStain(t, new Vector3(1.4f, 0f, -3.6f), rng);
        }

        // Ridge tent open at both ends, its ridge along local X.
        static Transform Tent(Transform parent, string name, Vector3 pos, float yaw)
        {
            var tent = Group(parent, name, pos, yaw);
            Model("Modele", tent, Vector3.zero, null, "tente");
            return tent;
        }

        static void Cot(Transform parent, Vector3 pos, float yaw, bool bloody)
        {
            var cot = Group(parent, "Lit_De_Camp", pos, yaw);
            Model("Modele", cot, Vector3.zero, null, "lit_de_camp");
            Solid(cot, new Vector3(0f, 0.42f, 0f), new Vector3(0.7f, 0.04f, 1.9f));
            if (bloody) Box("Tache", cot, new Vector3(0.05f, 0.445f, 0.45f), new Vector3(0.4f, 0.01f, 0.5f), "Blood", solid: false);
        }

        static void Memorial(Transform parent, Vector3 pos)
        {
            var m = Group(parent, "Monument", pos);
            Model("Modele", m, Vector3.zero, null, "monument");
            Solid(m, new Vector3(0f, 0.2f, 0f), new Vector3(1.4f, 0.4f, 1.4f), Surface.Concrete);
            Solid(m, new Vector3(0f, 1.4f, 0f), new Vector3(0.7f, 2f, 0.7f), Surface.Concrete);
        }
    }
}
