using UnityEngine;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        // Vehicles point along their local +X. Pillars in the paint colour cut the glass block into windows; door
        // seams, mirrors, grille and plates make the box read as a car. An abandoned car may keep its driver door open.
        static Transform Car(Transform parent, string name, Vector3 pos, float yaw, string paint, bool burnt = false, float tilt = 0f, bool doorOpen = false)
        {
            var car = Group(parent, name, pos, yaw);
            car.localRotation *= Quaternion.Euler(tilt, 0f, tilt * 0.5f);
            var y = burnt ? -0.2f : 0f;
            var trim = burnt ? "CarBurnt" : "Metal";
            var dark = burnt ? "CarBurnt" : "CarBlack";
            Box("Carrosserie", car, new Vector3(0f, 0.7f + y, 0f), new Vector3(4.5f, 0.75f, 1.85f), paint);
            Box("Habitacle", car, new Vector3(-0.25f, 1.38f + y, 0f), new Vector3(2.3f, 0.62f, 1.62f), burnt ? "CarBurnt" : "Glass", solid: false);
            Box("Toit", car, new Vector3(-0.25f, 1.71f + y, 0f), new Vector3(2.1f, 0.06f, 1.64f), paint, solid: false);
            foreach (var x in new[] { 0.86f, -0.25f, -1.36f })
            foreach (var z in new[] { -0.8f, 0.8f })
                Box("Montant", car, new Vector3(x, 1.38f + y, z), new Vector3(0.1f, 0.62f, 0.04f), paint, solid: false);
            Box("Pare_Choc", car, new Vector3(2.28f, 0.45f + y, 0f), new Vector3(0.12f, 0.25f, 1.86f), trim, solid: false);
            Box("Pare_Choc", car, new Vector3(-2.28f, 0.45f + y, 0f), new Vector3(0.12f, 0.25f, 1.86f), trim, solid: false);
            Box("Calandre", car, new Vector3(2.26f, 0.68f + y, 0f), new Vector3(0.04f, 0.2f, 0.9f), dark, solid: false);
            foreach (var x in new[] { -2.35f, 2.35f })
                Box("Plaque", car, new Vector3(x, 0.47f + y, 0f), new Vector3(0.02f, 0.13f, 0.36f), burnt ? "CarBurnt" : "SignWhite", solid: false);
            foreach (var z in new[] { -1f, 1f })
            {
                Box("Phare", car, new Vector3(2.26f, 0.78f + y, z * 0.62f), new Vector3(0.06f, 0.14f, 0.36f), burnt ? "CarBurnt" : "SignWhite", solid: false);
                Box("Feu", car, new Vector3(-2.26f, 0.8f + y, z * 0.62f), new Vector3(0.06f, 0.14f, 0.3f), burnt ? "CarBurnt" : "SignRed", solid: false);
                foreach (var x in new[] { 0.85f, -0.3f, -1.4f })
                    Box("Joint_Porte", car, new Vector3(x, 0.72f + y, z * 0.93f), new Vector3(0.02f, 0.55f, 0.01f), dark, solid: false);
                foreach (var x in new[] { 0.5f, -0.65f })
                    Box("Poignee", car, new Vector3(x, 0.92f + y, z * 0.935f), new Vector3(0.14f, 0.03f, 0.02f), trim, solid: false);
                Box("Retroviseur", car, new Vector3(0.72f, 1.12f + y, z * 1.02f), new Vector3(0.1f, 0.1f, 0.18f), paint, solid: false);
            }
            if (doorOpen)
            {
                // Driver door swung out on its front hinge; the dark opening shows behind it.
                Box("Ouverture", car, new Vector3(0.28f, 0.95f + y, 0.926f), new Vector3(1.1f, 0.85f, 0.01f), dark, solid: false);
                var hinge = Group(car, "Portiere", new Vector3(0.85f, y, 0.93f), -65f);
                Box("Panneau", hinge, new Vector3(-0.56f, 0.72f, 0.03f), new Vector3(1.12f, 0.55f, 0.05f), paint, solid: false);
                Box("Vitre", hinge, new Vector3(-0.56f, 1.3f, 0.03f), new Vector3(1.02f, 0.55f, 0.03f), "Glass", solid: false);
            }
            if (!burnt) Wheels(car, 1.4f, 0.85f, 0.36f);
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
            Box("Chassis", truck, new Vector3(0f, 0.75f, 0f), new Vector3(5.3f, 0.7f, 1.95f), paint);
            Box("Cabine", truck, new Vector3(0.45f, 1.48f, 0f), new Vector3(1.9f, 0.76f, 1.75f), "Glass", solid: false);
            Box("Toit", truck, new Vector3(0.45f, 1.88f, 0f), new Vector3(1.8f, 0.06f, 1.77f), paint, solid: false);
            Box("Ridelle", truck, new Vector3(-1.45f, 1.3f, 0.93f), new Vector3(2.3f, 0.42f, 0.08f), paint, solid: false);
            Box("Ridelle", truck, new Vector3(-1.45f, 1.3f, -0.93f), new Vector3(2.3f, 0.42f, 0.08f), paint, solid: false);
            Box("Hayon", truck, new Vector3(-2.6f, 1.3f, 0f), new Vector3(0.08f, 0.42f, 1.95f), paint, solid: false);
            Box("Pare_Choc", truck, new Vector3(2.7f, 0.5f, 0f), new Vector3(0.14f, 0.28f, 1.96f), "MetalLight", solid: false);
            Wheels(truck, 1.7f, 0.9f, 0.42f);
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
            Box("Barre", s, new Vector3(0f, 0.95f, 0f), new Vector3(2f, 0.22f, 0.06f), "SignRed");
            Box("Pied", s, new Vector3(-0.8f, 0.45f, 0f), new Vector3(0.06f, 0.9f, 0.5f), "SignWhite", solid: false);
            Box("Pied", s, new Vector3(0.8f, 0.45f, 0f), new Vector3(0.06f, 0.9f, 0.5f), "SignWhite", solid: false);
        }

        static void Barrel(Transform parent, Vector3 pos, string mat) => Cylinder("Baril", parent, pos + Vector3.up * 0.45f, 0.3f, 0.9f, mat);

        static void Container(Transform parent, string name, Vector3 pos, float yaw, string mat)
        {
            var c = Group(parent, name, pos, yaw);
            Box("Caisson", c, new Vector3(0f, 1.3f, 0f), new Vector3(6.1f, 2.6f, 2.44f), mat);
            Box("Portes", c, new Vector3(3.06f, 1.3f, 0f), new Vector3(0.04f, 2.4f, 2.3f), "Metal", solid: false);
        }

        static void HayBale(Transform parent, Vector3 pos, float yaw)
            => Cylinder("Botte_Foin", parent, pos + Vector3.up * 0.75f, 0.75f, 1.3f, "Hay", Quaternion.Euler(0f, yaw, 90f));

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
            Post("Boite_Lettres_Pied", parent, pos, 1.05f, 0.05f, "Wood", solid: false);
            Box("Boite_Lettres", parent, pos + Vector3.up * 1.15f, new Vector3(0.28f, 0.25f, 0.5f), "Metal", solid: false);
            Box("Drapeau", parent, pos + new Vector3(0.16f, 1.3f, 0.1f), new Vector3(0.02f, 0.2f, 0.12f), "SignRed", solid: false);
        }

        static void TrashCan(Transform parent, Vector3 pos, string mat = "CarGreen")
            => Cylinder("Poubelle", parent, pos + Vector3.up * 0.5f, 0.3f, 1f, mat);

        static void Shed(Transform parent, Vector3 pos, float yaw, string roof)
        {
            var s = Group(parent, "Abri_Jardin", pos, yaw);
            Box("Corps", s, new Vector3(0f, 1.15f, 0f), new Vector3(3f, 2.3f, 2.5f), "Wood");
            Box("Porte", s, new Vector3(0f, 0.95f, -1.26f), new Vector3(1.1f, 1.9f, 0.04f), "WoodDark", solid: false);
            Roof(s, "Toit", new Vector3(0f, 2.3f, 0f), 3.4f, 2.9f, 0.8f, roof);
        }

        static void PicnicTable(Transform parent, Vector3 pos, float yaw)
        {
            var t = Group(parent, "Table_Pique_Nique", pos, yaw);
            Box("Plateau", t, new Vector3(0f, 0.75f, 0f), new Vector3(1.8f, 0.06f, 0.8f), "Wood");
            Box("Banc", t, new Vector3(0f, 0.45f, 0.6f), new Vector3(1.8f, 0.05f, 0.3f), "Wood", solid: false);
            Box("Banc", t, new Vector3(0f, 0.45f, -0.6f), new Vector3(1.8f, 0.05f, 0.3f), "Wood", solid: false);
            Box("Pied", t, new Vector3(-0.7f, 0.37f, 0f), new Vector3(0.08f, 0.74f, 1.4f), "WoodDark", solid: false);
            Box("Pied", t, new Vector3(0.7f, 0.37f, 0f), new Vector3(0.08f, 0.74f, 1.4f), "WoodDark", solid: false);
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
            Post("Mat", lamp, Vector3.zero, 6.5f, 0.1f, "Metal");
            Box("Bras", lamp, new Vector3(0f, 6.4f, 0.9f), new Vector3(0.08f, 0.08f, 1.8f), "Metal", solid: false);
            Box("Tete", lamp, new Vector3(0f, 6.3f, 1.7f), new Vector3(0.35f, 0.12f, 0.6f), "LampGlow", solid: false);
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
