using DeadCounty.Audio;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        // Vehicles point along their local +X. An abandoned car may keep its driver door open; a burnt one is the same
        // model all in soot, sunk on its rims.
        static Transform Car(Transform parent, string name, Vector3 pos, float yaw, string paint, bool burnt = false, float tilt = 0f, bool doorOpen = false)
        {
            var car = Group(parent, name, pos, yaw);
            car.localRotation *= Quaternion.Euler(tilt, 0f, tilt * 0.5f);
            var y = burnt ? -0.2f : 0f;
            var dark = burnt ? "CarBurnt" : "CarBlack";
            Model("Modele", car, new Vector3(0f, y, 0f), null, "voiture", burnt ? (AnyColor, "CarBurnt") : ("#7A2420", paint));
            Solid(car, new Vector3(0f, 0.7f + y, 0f), new Vector3(4.5f, 0.75f, 1.85f), Surface.Metal);
            if (doorOpen)
            {
                // Driver door swung out on its front hinge; the dark opening shows behind it.
                Box("Ouverture", car, new Vector3(0.28f, 0.95f + y, 0.926f), new Vector3(1.1f, 0.85f, 0.01f), dark, solid: false);
                var hinge = Group(car, "Portiere", new Vector3(0.85f, y, 0.93f), -65f);
                Box("Panneau", hinge, new Vector3(-0.56f, 0.72f, 0.03f), new Vector3(1.12f, 0.55f, 0.05f), paint, solid: false);
                Box("Vitre", hinge, new Vector3(-0.56f, 1.3f, 0.03f), new Vector3(1.02f, 0.55f, 0.03f), "Glass", solid: false);
            }
            return car;
        }

        static void PoliceCar(Transform parent, Vector3 pos, float yaw)
        {
            var car = Car(parent, "Voiture_Police", pos, yaw, "CarWhite");
            Box("Portieres", car, new Vector3(-0.1f, 0.72f, 0f), new Vector3(1.8f, 0.5f, 1.87f), "CarBlack", solid: false);
            Model("Gyrophare", car, new Vector3(-0.25f, 1.73f, 0f), null, "gyrophare");
        }

        static void Pickup(Transform parent, string name, Vector3 pos, float yaw, string paint, float tilt = 0f)
        {
            var truck = Group(parent, name, pos, yaw);
            truck.localRotation *= Quaternion.Euler(tilt, 0f, 0f);
            Model("Modele", truck, Vector3.zero, null, "pickup", ("#7A2420", paint));
            Solid(truck, new Vector3(0f, 0.75f, 0f), new Vector3(5.3f, 0.7f, 1.95f), Surface.Metal);
        }

        static void Bus(Transform parent, Vector3 pos, float yaw)
        {
            var bus = Group(parent, "Bus_Scolaire_Accidente", pos, yaw);
            bus.localRotation *= Quaternion.Euler(0f, 0f, 7f);
            Model("Modele", bus, Vector3.zero, null, "bus");
            Solid(bus, new Vector3(0f, 1.75f, 0f), new Vector3(11f, 2.5f, 2.5f), Surface.Metal);
        }

        static void Tractor(Transform parent, Vector3 pos, float yaw)
        {
            var t = Group(parent, "Tracteur", pos, yaw);
            Model("Modele", t, Vector3.zero, null, "tracteur");
            Solid(t, new Vector3(0.9f, 1.15f, 0f), new Vector3(1.9f, 0.85f, 0.85f), Surface.Metal);
            Solid(t, new Vector3(-0.6f, 1.25f, 0f), new Vector3(1.4f, 1f, 1.1f), Surface.Metal);
        }

        static void Forklift(Transform parent, Vector3 pos, float yaw)
        {
            var f = Group(parent, "Chariot_Elevateur", pos, yaw);
            Model("Modele", f, Vector3.zero, null, "chariot_elevateur");
            Solid(f, new Vector3(0f, 0.75f, 0f), new Vector3(2f, 1.1f, 1.1f), Surface.Metal);
        }

        static void Sawhorse(Transform parent, Vector3 pos, float yaw)
        {
            var s = Group(parent, "Barriere", pos, yaw);
            Model("Modele", s, Vector3.zero, null, "barriere");
            Solid(s, new Vector3(0f, 0.95f, 0f), new Vector3(2f, 0.22f, 0.06f));
        }

        static void Barrel(Transform parent, Vector3 pos, string mat)
            => Solid(Model("Baril", parent, pos, null, "baril", ("#A8322A", mat)).transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.54f, 0.9f, 0.54f));

        static void Container(Transform parent, string name, Vector3 pos, float yaw, string mat)
        {
            var c = Group(parent, name, pos, yaw);
            Model("Modele", c, Vector3.zero, null, "conteneur", ("#7A2420", mat), ("#5C1B18", Shade(mat, -0.25f)));
            Solid(c, new Vector3(0f, 1.3f, 0f), new Vector3(6.1f, 2.6f, 2.44f), Surface.Metal);
        }

        static void HayBale(Transform parent, Vector3 pos, float yaw)
            => Solid(Model("Botte_Foin", parent, pos, Quaternion.Euler(0f, yaw, 0f), "botte_foin").transform, new Vector3(0f, 0.75f, 0f), new Vector3(1.3f, 1.5f, 1.3f), Surface.Grass);

        static void Log(Transform parent, Vector3 pos, float yaw)
            => Solid(Model("Tronc_Couche", parent, pos, Quaternion.Euler(0f, yaw, 0f), "tronc").transform, new Vector3(0f, 0.3f, 0f), new Vector3(4f, 0.54f, 0.54f), Surface.Wood);

        // Stacked by hand: every other pallet sits a little askew.
        static void PalletStack(Transform parent, Vector3 pos, int count)
        {
            var stack = Group(parent, "Palettes", pos);
            for (var i = 0; i < count; i++)
                Model("Palette", stack, Vector3.up * i * 0.15f, Quaternion.Euler(0f, i % 2 * 4f - 2f, 0f), "palette");
            Solid(stack, new Vector3(0f, count * 0.075f, 0f), new Vector3(1.2f, count * 0.15f, 1f), Surface.Wood);
        }

        enum FenceKind { Board, Rail, Chain }

        // Fence between a and b in parent-local space. Board fences are 1.7 m privacy fences that block sight.
        static void Fence(Transform parent, Vector3 a, Vector3 b, FenceKind kind)
        {
            var dir = b - a;
            var length = dir.magnitude;
            var rot = Quaternion.LookRotation(dir / length);
            var mid = (a + b) / 2f;
            var (postMat, railMat, height, spacing) = kind switch
            {
                FenceKind.Board => ("WoodDark", "Wood", 1.7f, 2.4f),
                FenceKind.Rail => ("WoodDark", "Wood", 1.2f, 3f),
                _ => ("Metal", "MetalLight", 2f, 3f),
            };
            var count = Mathf.Max(1, Mathf.CeilToInt(length / spacing));
            for (var i = 0; i <= count; i++)
                Post("Poteau", parent, a + dir * i / count, height, 0.06f, postMat, solid: false);
            switch (kind)
            {
                case FenceKind.Board:
                    Box("Planches", parent, mid + Vector3.up * 0.9f, new Vector3(0.05f, 1.6f, length), railMat, rot);
                    break;
                case FenceKind.Rail:
                    Box("Lisse", parent, mid + Vector3.up * 0.5f, new Vector3(0.06f, 0.12f, length), railMat, rot);
                    Box("Lisse", parent, mid + Vector3.up * 1.05f, new Vector3(0.06f, 0.12f, length), railMat, rot);
                    break;
                default:
                    Box("Lisse", parent, mid + Vector3.up * 1.95f, new Vector3(0.05f, 0.05f, length), railMat, rot);
                    Box("Lisse", parent, mid + Vector3.up * 1f, new Vector3(0.04f, 0.04f, length), railMat, rot);
                    Box("Lisse", parent, mid + Vector3.up * 0.1f, new Vector3(0.05f, 0.05f, length), railMat, rot);
                    break;
            }
        }

        static void FenceLoop(Transform parent, FenceKind kind, params Vector3[] points)
        {
            for (var i = 0; i < points.Length - 1; i++) Fence(parent, points[i], points[i + 1], kind);
        }

        static void Mailbox(Transform parent, Vector3 pos)
        {
            Model("Boite_Lettres", parent, pos, null, "boite_lettres");
        }

        static void TrashCan(Transform parent, Vector3 pos, string mat = "CarGreen")
            => Solid(Model("Poubelle", parent, pos, null, "poubelle", ("#3E5444", mat)).transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.54f, 1f, 0.54f));

        static void Shed(Transform parent, Vector3 pos, float yaw, string roof)
        {
            var s = Group(parent, "Abri_Jardin", pos, yaw);
            Model("Modele", s, Vector3.zero, null, "abri_jardin");
            Solid(s, new Vector3(0f, 1.15f, 0f), new Vector3(3f, 2.3f, 2.5f));
            GableRoof(s, new Vector3(0f, 2.3f, 0f), 3.4f, 2.9f, 0.8f, 0f, new Vector2(1.5f, 1.25f), roof, "Wood", null);
        }

        static void PicnicTable(Transform parent, Vector3 pos, float yaw)
        {
            var t = Group(parent, "Table_Pique_Nique", pos, yaw);
            Model("Modele", t, Vector3.zero, null, "table_pique_nique");
            Solid(t, new Vector3(0f, 0.75f, 0f), new Vector3(1.8f, 0.06f, 0.8f), Surface.Wood);
        }

        static void Clothesline(Transform parent, Vector3 a, Vector3 b)
        {
            Post("Poteau_Linge", parent, a, 1.9f, 0.04f, "Metal", solid: false);
            Post("Poteau_Linge", parent, b, 1.9f, 0.04f, "Metal", solid: false);
            var top = Vector3.up * 1.85f;
            Box("Fil", parent, (a + b) / 2f + top, new Vector3(0.015f, 0.015f, Vector3.Distance(a, b)), "SignWhite", Quaternion.LookRotation(b - a), solid: false);
            Box("Drap", parent, Vector3.Lerp(a, b, 0.35f) + top + Vector3.down * 0.45f, new Vector3(0.02f, 0.9f, 1.2f), "Mattress", Quaternion.LookRotation(b - a), solid: false);
        }

        // Street lamp whose arm points along the local +Z.
        static Light StreetLamp(Transform parent, Vector3 basePos, float yaw)
        {
            var lamp = Group(parent, "Lampadaire", basePos, yaw);
            Model("Modele", lamp, Vector3.zero, null, "lampadaire");
            Solid(lamp, new Vector3(0f, 3.25f, 0f), new Vector3(0.2f, 6.5f, 0.2f));
            return SpotLight(lamp, "Lumiere_Sodium", new Vector3(0f, 6.2f, 1.7f), Quaternion.Euler(90f, 0f, 0f), new Color(1f, 0.78f, 0.45f), 12f, 16f, 120f, false);
        }

        static Light Floodlight(Transform parent, Vector3 basePos, Vector3 target)
        {
            var mast = Group(parent, "Projecteur", basePos);
            var head = mast.position + Vector3.up * 10f;
            var rot = Quaternion.LookRotation(target - head);
            // The yoke on top of the mast turns with the lamp, which tilts in it.
            Model("Mat", mast, Vector3.zero, Quaternion.Euler(0f, rot.eulerAngles.y, 0f), "mat_projecteur");
            Solid(mast, new Vector3(0f, 5f, 0f), new Vector3(0.27f, 10f, 0.27f));
            Model("Tete", mast, Vector3.up * 10f, rot, "projecteur");
            return SpotLight(mast, "Lumiere", Vector3.up * 10f, rot, new Color(0.92f, 0.95f, 1f), 25f, 70f, 75f, true);
        }
    }
}
