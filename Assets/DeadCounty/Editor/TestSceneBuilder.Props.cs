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
            Solid(car, new Vector3(0f, 0.7f + y, 0f), new Vector3(4.5f, 0.75f, 1.85f));
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

        static void Wheels(Transform vehicle, float x, float z, float radius)
        {
            foreach (var (wx, wz) in new[] { (x, z), (x, -z), (-x, z), (-x, -z) })
                Cylinder("Roue", vehicle, new Vector3(wx, radius, wz), radius, 0.26f, "Tire", Quaternion.Euler(90f, 0f, 0f), solid: false);
        }

        static void PoliceCar(Transform parent, Vector3 pos, float yaw)
        {
            var car = Car(parent, "Voiture_Police", pos, yaw, "CarWhite");
            Box("Portieres", car, new Vector3(-0.1f, 0.72f, 0f), new Vector3(1.8f, 0.5f, 1.87f), "CarBlack", solid: false);
            Box("Gyrophare_R", car, new Vector3(-0.25f, 1.82f, 0.32f), new Vector3(0.3f, 0.14f, 0.55f), "PoliceRed", solid: false);
            Box("Gyrophare_B", car, new Vector3(-0.25f, 1.82f, -0.32f), new Vector3(0.3f, 0.14f, 0.55f), "PoliceBlue", solid: false);
        }

        static void Pickup(Transform parent, string name, Vector3 pos, float yaw, string paint, float tilt = 0f)
        {
            var truck = Group(parent, name, pos, yaw);
            truck.localRotation *= Quaternion.Euler(tilt, 0f, 0f);
            Model("Modele", truck, Vector3.zero, null, "pickup", ("#7A2420", paint));
            Solid(truck, new Vector3(0f, 0.75f, 0f), new Vector3(5.3f, 0.7f, 1.95f));
        }

        static void Bus(Transform parent, Vector3 pos, float yaw)
        {
            var bus = Group(parent, "Bus_Scolaire_Accidente", pos, yaw);
            bus.localRotation *= Quaternion.Euler(0f, 0f, 7f);
            Box("Caisse", bus, new Vector3(0f, 1.75f, 0f), new Vector3(11f, 2.5f, 2.5f), "BusYellow");
            Box("Vitres", bus, new Vector3(-0.4f, 2.3f, 0f), new Vector3(9.4f, 0.75f, 2.52f), "Glass", solid: false);
            Box("Bandeau", bus, new Vector3(0f, 1.35f, 0f), new Vector3(11.02f, 0.12f, 2.52f), "CarBlack", solid: false);
            Box("Pare_Brise", bus, new Vector3(5.51f, 2.2f, 0f), new Vector3(0.04f, 1f, 2.2f), "Glass", solid: false);
            Box("Pare_Choc", bus, new Vector3(5.6f, 0.7f, 0f), new Vector3(0.2f, 0.3f, 2.5f), "CarBlack", solid: false);
            Wheels(bus, 3.6f, 1.05f, 0.5f);
        }

        static void Tractor(Transform parent, Vector3 pos, float yaw)
        {
            var t = Group(parent, "Tracteur", pos, yaw);
            Box("Moteur", t, new Vector3(0.9f, 1.15f, 0f), new Vector3(1.9f, 0.85f, 0.85f), "TractorGreen");
            Box("Corps", t, new Vector3(-0.6f, 1.25f, 0f), new Vector3(1.4f, 1f, 1.1f), "TractorGreen");
            Box("Cabine", t, new Vector3(-0.7f, 2.35f, 0f), new Vector3(1.2f, 1.2f, 1.05f), "Glass", solid: false);
            Box("Toit", t, new Vector3(-0.7f, 3f, 0f), new Vector3(1.4f, 0.08f, 1.3f), "TractorGreen", solid: false);
            Post("Echappement", t, new Vector3(1.4f, 1.55f, 0.25f), 1.3f, 0.05f, "Metal", solid: false);
            foreach (var z in new[] { -0.85f, 0.85f })
            {
                Cylinder("Roue_AR", t, new Vector3(-0.7f, 0.8f, z), 0.8f, 0.45f, "Tire", Quaternion.Euler(90f, 0f, 0f), solid: false);
                Cylinder("Roue_AV", t, new Vector3(1.4f, 0.45f, z * 0.8f), 0.45f, 0.3f, "Tire", Quaternion.Euler(90f, 0f, 0f), solid: false);
            }
        }

        static void Forklift(Transform parent, Vector3 pos, float yaw)
        {
            var f = Group(parent, "Chariot_Elevateur", pos, yaw);
            Box("Corps", f, new Vector3(0f, 0.75f, 0f), new Vector3(2f, 1.1f, 1.1f), "SignYellow");
            Box("Toit", f, new Vector3(-0.1f, 2.1f, 0f), new Vector3(1.2f, 0.06f, 1f), "Metal", solid: false);
            Post("Montant", f, new Vector3(0.45f, 1.3f, 0.45f), 0.8f, 0.04f, "Metal", solid: false);
            Post("Montant", f, new Vector3(0.45f, 1.3f, -0.45f), 0.8f, 0.04f, "Metal", solid: false);
            Box("Mat", f, new Vector3(1.1f, 1.3f, 0f), new Vector3(0.12f, 2.6f, 0.9f), "Metal", solid: false);
            Box("Fourche", f, new Vector3(1.7f, 0.12f, 0.3f), new Vector3(1.1f, 0.05f, 0.12f), "Metal", solid: false);
            Box("Fourche", f, new Vector3(1.7f, 0.12f, -0.3f), new Vector3(1.1f, 0.05f, 0.12f), "Metal", solid: false);
            Wheels(f, 0.7f, 0.5f, 0.3f);
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
            Box("Caisson", c, new Vector3(0f, 1.3f, 0f), new Vector3(6.1f, 2.6f, 2.44f), mat);
            Box("Portes", c, new Vector3(3.06f, 1.3f, 0f), new Vector3(0.04f, 2.4f, 2.3f), "Metal", solid: false);
        }

        static void HayBale(Transform parent, Vector3 pos, float yaw)
            => Solid(Model("Botte_Foin", parent, pos, Quaternion.Euler(0f, yaw, 0f), "botte_foin").transform, new Vector3(0f, 0.75f, 0f), new Vector3(1.3f, 1.5f, 1.3f));

        static void Log(Transform parent, Vector3 pos, float yaw)
            => Cylinder("Tronc_Couche", parent, pos + Vector3.up * 0.3f, 0.3f, 4f, "Trunk", Quaternion.Euler(0f, yaw, 90f));

        static void PalletStack(Transform parent, Vector3 pos, int count)
            => Box("Palettes", parent, pos + Vector3.up * count * 0.075f, new Vector3(1.2f, count * 0.15f, 1f), "Wood");

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
            Box("Corps", s, new Vector3(0f, 1.15f, 0f), new Vector3(3f, 2.3f, 2.5f), "Wood");
            Box("Porte", s, new Vector3(0f, 0.95f, -1.26f), new Vector3(1.1f, 1.9f, 0.04f), "WoodDark", solid: false);
            GableRoof(s, new Vector3(0f, 2.3f, 0f), 3.4f, 2.9f, 0.8f, 0f, new Vector2(1.5f, 1.25f), roof, "Wood", null);
        }

        static void PicnicTable(Transform parent, Vector3 pos, float yaw)
        {
            var t = Group(parent, "Table_Pique_Nique", pos, yaw);
            Model("Modele", t, Vector3.zero, null, "table_pique_nique");
            Solid(t, new Vector3(0f, 0.75f, 0f), new Vector3(1.8f, 0.06f, 0.8f));
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
            Post("Mat", mast, Vector3.zero, 10f, 0.15f, "Metal");
            var head = mast.position + Vector3.up * 10f;
            var rot = Quaternion.LookRotation(target - head);
            Box("Tete", mast, Vector3.up * 10f, new Vector3(1.2f, 0.7f, 0.3f), "PanelGlow", rot, solid: false);
            return SpotLight(mast, "Lumiere", Vector3.up * 10f, rot, new Color(0.92f, 0.95f, 1f), 25f, 70f, 75f, true);
        }
    }
}
