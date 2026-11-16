using DeadCounty.Player;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    // Each piece is a root with one box collider over its footprint, the only solid part: the NavMesh, the camera
    // and the interaction raycasts see a simple block. Local frame: origin on the floor at the centre of the
    // footprint, the front facing -Z.
    public static partial class TestSceneBuilder
    {
        static Transform Piece(Transform parent, string name, Vector3 pos, float yaw, Vector3 footprint, bool solid = true)
        {
            var root = Group(parent, name, pos, yaw);
            if (solid)
            {
                var collider = root.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, footprint.y / 2f, 0f);
                collider.size = footprint;
            }
            return root;
        }

        // Somewhere to sit and rest: `height` is the top of the seat, `halfWidth` how far from the middle one can sit,
        // `depth` the sitter's local Z; furniture faces -Z, a pew the other way.
        static void Sittable(Transform piece, string name, float height, float halfWidth, float depth, float facing = 180f)
            => piece.gameObject.AddComponent<Seat>().Configure(name, height, halfWidth, depth, facing);

        static void Bit(Transform piece, string name, Vector3 center, Vector3 size, string mat, Quaternion? rot = null)
            => Box(name, piece, center, size, mat, rot, solid: false);

        static Transform Sofa(Transform parent, string name, Vector3 pos, float yaw, float width, string fabric)
        {
            const float depth = 0.9f;
            var p = Piece(parent, name, pos, yaw, new Vector3(width, 0.85f, depth));
            // One model per size: the armchair, or the three-seater whatever the width asked for.
            Model("Modele", p, Vector3.zero, null, width < 1.5f ? "fauteuil" : "canape", ("#5E6B7A", fabric), ("#6C7A8A", Shade(fabric, 0.12f)));
            Sittable(p, width < 1.5f ? "Fauteuil" : "Canapé", 0.48f, Mathf.Max(0f, width / 2f - 0.45f), -0.1f);
            return p;
        }

        static Transform Table(Transform parent, string name, Vector3 pos, float yaw, Vector3 size)
        {
            var p = Piece(parent, name, pos, yaw, size);
            // Two models, stretched to the size asked for: the low one for coffee tables.
            var model = size.y < 0.6f ? "table_basse" : "table_cuisine";
            var made = ModelMesh(model).Mesh.bounds.size;
            Model("Modele", p, Vector3.zero, null, model).transform.localScale = new Vector3(size.x / made.x, size.y / made.y, size.z / made.z);
            return p;
        }

        // Chairs are not solid: they stay out of the NavMesh and the player brushes past them.
        static Transform Chair(Transform parent, Vector3 pos, float yaw, Quaternion? tipped = null)
        {
            var p = Piece(parent, "Chaise", pos, yaw, new Vector3(0.45f, 0.9f, 0.45f), solid: false);
            if (tipped.HasValue) p.localRotation *= tipped.Value;
            Model("Modele", p, Vector3.zero, null, "chaise");
            return p;
        }

        static Transform TvStand(Transform parent, Vector3 pos, float yaw)
        {
            var p = Piece(parent, "Meuble_TV", pos, yaw, new Vector3(1.8f, 0.6f, 0.45f));
            Model("Modele", p, Vector3.zero, null, "meuble_tv");
            return p;
        }

        static Transform Bookcase(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            const float width = 1.4f, height = 2f, depth = 0.38f;
            var p = Piece(parent, "Bibliotheque", pos, yaw, new Vector3(width, height, depth));
            Model("Modele", p, Vector3.zero, null, "bibliotheque");
            string[] covers = { "SignRed", "ShutterGreen", "Fabric", "CarBeige", "ShutterDark", "Hay" };
            // The model's shelves, top shelf aside: centred at y, 3 cm thick.
            for (var shelf = 0; shelf < 4; shelf++)
            {
                var y = 0.04f + shelf * 0.48f;
                // Half-emptied shelves: looters went through them.
                var x = -width / 2f + 0.08f;
                while (x < width / 2f - 0.12f)
                {
                    var thick = Range(rng, 0.03f, 0.07f);
                    if (rng.NextDouble() < 0.7)
                    {
                        var tall = Range(rng, 0.22f, 0.32f);
                        Bit(p, "Livre", new Vector3(x + thick / 2f, y + 0.015f + tall / 2f, 0f), new Vector3(thick, tall, 0.22f), covers[rng.Next(covers.Length)]);
                    }
                    x += thick + 0.005f;
                }
            }
            return p;
        }

        static Transform Armchair(Transform parent, Vector3 pos, float yaw, string fabric) => Sofa(parent, "Fauteuil", pos, yaw, 0.95f, fabric);

        // Base units under a worktop, an optional sink, the upper cabinets on the wall behind.
        static Transform Counter(Transform parent, Vector3 pos, float yaw, float length, bool sink, System.Random rng)
        {
            const float depth = 0.6f, height = 0.9f;
            var p = Piece(parent, "Plan_Travail", pos, yaw, new Vector3(length, height, depth));
            // Modelled 3 m long, the sink a fifth of the way along: stretched to the length asked for.
            Model("Modele", p, Vector3.zero, null, sink ? "plan_travail_evier" : "plan_travail").transform.localScale = new Vector3(length / 3f, 1f, 1f);
            // Whatever was left on the worktop, or in the sink: its basin, floor at 0.75, is 0.42 m wide before stretching.
            for (var i = rng.Next(2, 5); i > 0; i--)
            {
                var x = Range(rng, -length / 2f + 0.15f, length / 2f - 0.15f);
                var bottom = sink && Mathf.Abs(x - length * 0.2f) < 0.21f * length / 3f ? 0.75f : height;
                if (rng.NextDouble() < 0.5)
                    Cylinder("Bouteille", p, new Vector3(x, bottom + 0.12f, Range(rng, -0.1f, 0.15f)), 0.035f, 0.24f, rng.NextDouble() < 0.5 ? "Glass" : "ShutterGreen", solid: false);
                else
                    Bit(p, "Boite", new Vector3(x, bottom + 0.08f, Range(rng, -0.1f, 0.15f)), new Vector3(0.12f, 0.16f, 0.08f), "Crate", Quaternion.Euler(0f, Range(rng, -30f, 30f), 0f));
            }
            return p;
        }

        static Transform Fridge(Transform parent, Vector3 pos, float yaw)
        {
            var p = Piece(parent, "Frigo", pos, yaw, new Vector3(0.7f, 1.8f, 0.7f));
            Model("Modele", p, Vector3.zero, null, "frigo");
            return p;
        }

        static Transform DoubleBed(Transform parent, Vector3 pos, float yaw, string blanket)
        {
            const float width = 1.6f, length = 2.1f;
            var p = Piece(parent, "Lit", pos, yaw, new Vector3(width, 0.62f, length));
            Model("Modele", p, Vector3.zero, null, "lit", ("#6E5A48", blanket), ("#5C4A3A", Shade(blanket, -0.15f)));
            return p;
        }

        static Transform Workbench(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            const float width = 2.4f, depth = 0.6f, height = 0.9f;
            var p = Piece(parent, "Etabli", pos, yaw, new Vector3(width, height, depth));
            Model("Modele", p, Vector3.zero, null, "etabli");
            // Hung on the model's pegboard, whose face is at z = 0.265.
            for (var i = rng.Next(3, 6); i > 0; i--)
                Bit(p, "Outil", new Vector3(Range(rng, -width / 2f + 0.2f, width / 2f - 0.2f), height + Range(rng, 0.3f, 0.8f), depth / 2f - 0.05f),
                    new Vector3(Range(rng, 0.04f, 0.08f), Range(rng, 0.2f, 0.35f), 0.03f), rng.NextDouble() < 0.5 ? "Metal" : "SignRed", Quaternion.Euler(0f, 0f, Range(rng, -15f, 15f)));
            return p;
        }

        static Transform MetalShelf(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            const float width = 2f, depth = 0.5f, height = 1.8f;
            var p = Piece(parent, "Etagere", pos, yaw, new Vector3(width, height, depth));
            Model("Modele", p, Vector3.zero, null, "etagere_metal");
            // The model's shelves, top shelf aside: their tops at y + 0.015.
            for (var shelf = 0; shelf < 3; shelf++)
            {
                var y = 0.1f + shelf * 0.55f;
                for (var x = -width / 2f + 0.2f; x < width / 2f - 0.15f; x += Range(rng, 0.3f, 0.55f))
                {
                    if (rng.NextDouble() < 0.35) continue;
                    if (rng.NextDouble() < 0.5)
                        Cylinder("Pot_Peinture", p, new Vector3(x, y + 0.11f, Range(rng, -0.1f, 0.1f)), 0.09f, 0.2f, rng.NextDouble() < 0.5 ? "MetalLight" : "SignYellow", solid: false);
                    else
                        Bit(p, "Carton", new Vector3(x, y + 0.14f, 0f), new Vector3(Range(rng, 0.25f, 0.4f), 0.25f, 0.35f), "Crate", Quaternion.Euler(0f, Range(rng, -10f, 10f), 0f));
                }
            }
            return p;
        }

        // Double-sided store shelving, long axis on local X, products on both faces; most shelves half looted.
        static Transform GroceryShelf(Transform parent, Vector3 pos, float yaw, System.Random rng, float length = 6f)
        {
            const float depth = 0.7f, height = 1.6f;
            var p = Piece(parent, "Rayon", pos, yaw, new Vector3(length, height, depth));
            // Bays of 1.2 m, stretched a little when the length is not a whole number of them.
            var bays = Mathf.Max(1, Mathf.RoundToInt(length / 1.2f));
            for (var i = 0; i < bays; i++)
                Model("Modele", p, new Vector3(-length / 2f + (i + 0.5f) * length / bays, 0f, 0f), null, "rayon").transform.localScale = new Vector3(length / bays / 1.2f, 1f, 1f);
            string[] goods = { "SignRed", "SignYellow", "SignWhite", "ShutterGreen", "CarBlue", "Crate", "Hay" };
            for (var shelf = 0; shelf < 3; shelf++)
            {
                var y = 0.16f + shelf * 0.48f;
                foreach (var side in new[] { -1f, 1f })
                {
                    for (var x = -length / 2f + 0.2f; x < length / 2f - 0.2f; x += Range(rng, 0.25f, 0.6f))
                    {
                        if (rng.NextDouble() < 0.45) continue;
                        var size = new Vector3(Range(rng, 0.12f, 0.28f), Range(rng, 0.12f, 0.3f), Range(rng, 0.1f, 0.2f));
                        var z = side * Range(rng, 0.12f, 0.24f);
                        if (rng.NextDouble() < 0.4)
                            Cylinder("Conserve", p, new Vector3(x, y + 0.035f + 0.06f, z), 0.045f, 0.12f, goods[rng.Next(goods.Length)], solid: false);
                        else
                            Bit(p, "Produit", new Vector3(x, y + 0.035f + size.y / 2f, z), size, goods[rng.Next(goods.Length)], Quaternion.Euler(0f, Range(rng, -10f, 10f), 0f));
                    }
                }
            }
            return p;
        }

        static Transform Checkout(Transform parent, Vector3 pos, float yaw, float length = 3f)
        {
            const float depth = 0.7f;
            var p = Piece(parent, "Comptoir", pos, yaw, new Vector3(length, 1f, depth));
            // The counter, modelled 3 m long, stretches to the length asked for; the register on its top keeps its size.
            Model("Modele", p, Vector3.zero, null, "comptoir").transform.localScale = new Vector3(length / 3f, 1f, 1f);
            Model("Caisse", p, new Vector3(length / 2f - 0.5f, 0.995f, 0f), null, "caisse_enregistreuse");
            return p;
        }

        static Transform Nightstand(Transform parent, Vector3 pos, float yaw)
        {
            var p = Piece(parent, "Table_Chevet", pos, yaw, new Vector3(0.45f, 0.55f, 0.4f));
            Model("Modele", p, Vector3.zero, null, "chevet");
            return p;
        }

        static Transform Wardrobe(Transform parent, Vector3 pos, float yaw)
        {
            const float width = 1f, height = 2f, depth = 0.6f;
            var p = Piece(parent, "Armoire", pos, yaw, new Vector3(width, height, depth));
            Model("Modele", p, Vector3.zero, null, "armoire");
            return p;
        }

        static Transform Dresser(Transform parent, Vector3 pos, float yaw)
        {
            const float width = 1.2f, height = 0.9f, depth = 0.45f;
            var p = Piece(parent, "Commode", pos, yaw, new Vector3(width, height, depth));
            Model("Modele", p, Vector3.zero, null, "commode");
            return p;
        }

        static Transform Bathtub(Transform parent, Vector3 pos, float yaw)
        {
            const float length = 1.7f, width = 0.8f, height = 0.6f;
            var p = Piece(parent, "Baignoire", pos, yaw, new Vector3(length, height, width));
            Model("Modele", p, Vector3.zero, null, "baignoire");
            return p;
        }

        static Transform Toilet(Transform parent, Vector3 pos, float yaw)
        {
            var p = Piece(parent, "Toilettes", pos, yaw, new Vector3(0.45f, 0.8f, 0.65f));
            Model("Modele", p, Vector3.zero, null, "toilettes");
            return p;
        }

        // Keeps the name GuaranteeStartingLoot looks for.
        static Transform BathroomCabinet(Transform parent, Vector3 pos, float yaw)
        {
            const float width = 1f, depth = 0.5f;
            var p = Piece(parent, "Meuble_SdB", pos, yaw, new Vector3(width, 0.9f, depth));
            Model("Modele", p, Vector3.zero, null, "meuble_sdb");
            return p;
        }

        // Modelled 3 x 2 m, stretched to the size asked for.
        static void Rug(Transform parent, Vector3 pos, float yaw, Vector2 size, string mat)
            => Model("Tapis", parent, pos + Vector3.up * 0.1f, Quaternion.Euler(0f, yaw, 0f), "tapis", ("#6B4A3C", mat), ("#5B3F33", Shade(mat, -0.15f)), ("#8A6E5E", Shade(mat, 0.25f)))
                .transform.localScale = new Vector3(size.x / 3f, 1f, size.y / 2f);

        static void CeilingLamp(Transform parent, Vector3 roomCenter)
        {
            var lamp = Group(parent, "Plafonnier", new Vector3(roomCenter.x, WallHeight, roomCenter.z));
            Model("Modele", lamp, Vector3.zero, null, "plafonnier");
        }

        static void Frame(Transform parent, Vector3 pos, float yaw, Vector2 size, string picture)
        {
            var frame = Group(parent, "Cadre", pos, yaw);
            Bit(frame, "Bordure", Vector3.zero, new Vector3(size.x, size.y, 0.03f), "WoodDark");
            Bit(frame, "Image", new Vector3(0f, 0f, -0.017f), new Vector3(size.x - 0.08f, size.y - 0.08f, 0.005f), picture);
        }

        // Signs of a hasty departure: boxes, papers, bottles on the floor.
        static void Clutter(Transform parent, System.Random rng, Rect area, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var pos = new Vector3(Range(rng, area.xMin, area.xMax), 0.1f, Range(rng, area.yMin, area.yMax));
                var yaw = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
                switch (rng.Next(3))
                {
                    case 0:
                        var size = new Vector3(Range(rng, 0.35f, 0.55f), Range(rng, 0.25f, 0.4f), Range(rng, 0.3f, 0.45f));
                        Box("Carton", parent, pos + Vector3.up * size.y / 2f, size, "Crate", yaw, solid: false);
                        break;
                    case 1:
                        for (var k = rng.Next(2, 5); k > 0; k--)
                            Box("Papier", parent, pos + new Vector3(Range(rng, -0.4f, 0.4f), 0.006f, Range(rng, -0.4f, 0.4f)), new Vector3(0.21f, 0.002f, 0.29f), "Paper",
                                Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), solid: false);
                        break;
                    default:
                        Cylinder("Bouteille", parent, pos + Vector3.up * 0.035f, 0.035f, 0.24f, rng.NextDouble() < 0.5 ? "Glass" : "ShutterGreen",
                            yaw * Quaternion.Euler(90f, 0f, 0f), solid: false);
                        break;
                }
            }
        }
    }
}
