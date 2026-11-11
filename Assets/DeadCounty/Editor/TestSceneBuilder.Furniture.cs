using UnityEngine;

namespace DeadCounty.EditorTools
{
    // Furniture assembled from a few boxes so each piece reads at a glance (legs, cushions, doors, handles).
    // Each piece is a root with one box collider over its footprint, the only solid part: the NavMesh, the
    // camera and the interaction raycasts see a simple block, LootContainer and Bed sit on the root.
    // Local frame: origin on the floor at the centre of the footprint, the front facing -Z.
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

        static void Bit(Transform piece, string name, Vector3 center, Vector3 size, string mat, Quaternion? rot = null)
            => Box(name, piece, center, size, mat, rot, solid: false);

        static void Legs(Transform piece, float width, float depth, float height, float thickness, string mat)
        {
            foreach (var x in new[] { -1f, 1f })
            foreach (var z in new[] { -1f, 1f })
                Bit(piece, "Pied", new Vector3(x * (width / 2f - thickness), height / 2f, z * (depth / 2f - thickness)), new Vector3(thickness, height, thickness), mat);
        }

        // Front panel lines and a knob per door, so a block of wood reads as a cabinet. Knobs sit near the top
        // unless handleY says otherwise (low on wall cabinets, at hand height on a wardrobe).
        static void Doors(Transform piece, float width, float bottom, float top, float depth, int count, string mat = "WoodDark", float handleY = float.NaN)
        {
            if (float.IsNaN(handleY)) handleY = top - 0.12f;
            var front = -depth / 2f - 0.005f;
            for (var i = 1; i < count; i++)
                Bit(piece, "Joint", new Vector3(-width / 2f + width * i / count, (bottom + top) / 2f, front), new Vector3(0.015f, top - bottom - 0.04f, 0.01f), mat);
            for (var i = 0; i < count; i++)
            {
                var x = -width / 2f + width * (i + 0.5f) / count + (count > 1 ? (i % 2 == 0 ? 0.3f : -0.3f) * width / count : 0f);
                Bit(piece, "Poignee", new Vector3(x, handleY, front - 0.01f), new Vector3(0.03f, 0.1f, 0.025f), "Metal");
            }
        }

        static void Drawers(Transform piece, float width, float bottom, float top, float depth, int count)
        {
            var front = -depth / 2f - 0.005f;
            var step = (top - bottom) / count;
            for (var i = 1; i < count; i++)
                Bit(piece, "Joint", new Vector3(0f, bottom + step * i, front), new Vector3(width - 0.06f, 0.015f, 0.01f), "WoodDark");
            for (var i = 0; i < count; i++)
                Bit(piece, "Poignee", new Vector3(0f, bottom + step * (i + 0.5f), front - 0.01f), new Vector3(0.16f, 0.025f, 0.025f), "Metal");
        }

        static Transform Sofa(Transform parent, string name, Vector3 pos, float yaw, float width, string fabric)
        {
            const float depth = 0.9f;
            var p = Piece(parent, name, pos, yaw, new Vector3(width, 0.85f, depth));
            Legs(p, width - 0.1f, depth - 0.1f, 0.1f, 0.05f, "WoodDark");
            Bit(p, "Assise", new Vector3(0f, 0.27f, 0.02f), new Vector3(width - 0.3f, 0.34f, depth - 0.06f), fabric);
            Bit(p, "Dossier", new Vector3(0f, 0.6f, depth / 2f - 0.11f), new Vector3(width, 0.5f, 0.22f), fabric);
            foreach (var x in new[] { -1f, 1f })
                Bit(p, "Accoudoir", new Vector3(x * (width / 2f - 0.09f), 0.4f, 0f), new Vector3(0.18f, 0.6f, depth), fabric);
            var seats = Mathf.Max(1, Mathf.RoundToInt((width - 0.3f) / 0.6f));
            var seatWidth = (width - 0.36f) / seats;
            for (var i = 0; i < seats; i++)
                Bit(p, "Coussin", new Vector3(-width / 2f + 0.18f + seatWidth * (i + 0.5f), 0.5f, -0.05f), new Vector3(seatWidth - 0.03f, 0.12f, depth - 0.3f), fabric);
            return p;
        }

        static Transform Table(Transform parent, string name, Vector3 pos, float yaw, Vector3 size, string mat)
        {
            var p = Piece(parent, name, pos, yaw, size);
            Bit(p, "Plateau", new Vector3(0f, size.y - 0.025f, 0f), new Vector3(size.x, 0.05f, size.z), mat);
            Legs(p, size.x - 0.06f, size.z - 0.06f, size.y - 0.05f, 0.06f, mat);
            return p;
        }

        // Chairs are not solid, like before: they stay out of the NavMesh and the player brushes past them.
        static Transform Chair(Transform parent, Vector3 pos, float yaw, Quaternion? tipped = null)
        {
            var p = Piece(parent, "Chaise", pos, yaw, new Vector3(0.45f, 0.9f, 0.45f), solid: false);
            if (tipped.HasValue) p.localRotation *= tipped.Value;
            Legs(p, 0.42f, 0.42f, 0.45f, 0.04f, "Wood");
            Bit(p, "Assise", new Vector3(0f, 0.47f, 0f), new Vector3(0.45f, 0.04f, 0.45f), "Wood");
            Bit(p, "Dossier", new Vector3(0f, 0.72f, 0.2f), new Vector3(0.42f, 0.45f, 0.04f), "Wood");
            return p;
        }

        static Transform TvStand(Transform parent, Vector3 pos, float yaw)
        {
            var p = Piece(parent, "Meuble_TV", pos, yaw, new Vector3(1.8f, 0.6f, 0.45f));
            Bit(p, "Caisson", new Vector3(0f, 0.3f, 0f), new Vector3(1.8f, 0.56f, 0.45f), "Furniture");
            Doors(p, 1.8f, 0.06f, 0.56f, 0.45f, 2);
            Bit(p, "Pied_TV", new Vector3(0f, 0.63f, 0.05f), new Vector3(0.3f, 0.06f, 0.2f), "CarBlack");
            Bit(p, "TV", new Vector3(0f, 0.98f, 0.05f), new Vector3(1.2f, 0.66f, 0.07f), "CarBlack");
            Bit(p, "Ecran", new Vector3(0f, 0.98f, 0.012f), new Vector3(1.12f, 0.6f, 0.01f), "Glass");
            return p;
        }

        static Transform Bookcase(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            const float width = 1.4f, height = 2f, depth = 0.38f;
            var p = Piece(parent, "Bibliotheque", pos, yaw, new Vector3(width, height, depth));
            Bit(p, "Fond", new Vector3(0f, height / 2f, depth / 2f - 0.015f), new Vector3(width, height, 0.03f), "Furniture");
            foreach (var x in new[] { -1f, 1f })
                Bit(p, "Montant", new Vector3(x * (width / 2f - 0.02f), height / 2f, 0f), new Vector3(0.04f, height, depth), "Furniture");
            string[] covers = { "SignRed", "ShutterGreen", "Fabric", "CarBeige", "ShutterDark", "Hay" };
            for (var shelf = 0; shelf < 5; shelf++)
            {
                var y = 0.04f + shelf * 0.48f;
                Bit(p, "Etagere", new Vector3(0f, y, 0f), new Vector3(width - 0.08f, 0.03f, depth - 0.03f), "Furniture");
                if (shelf == 4) continue;
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
            Bit(p, "Caissons", new Vector3(0f, 0.43f, 0.02f), new Vector3(length, 0.86f, depth - 0.04f), "Furniture");
            Bit(p, "Plinthe", new Vector3(0f, 0.05f, -depth / 2f + 0.06f), new Vector3(length, 0.1f, 0.02f), "WoodDark");
            Bit(p, "Dessus", new Vector3(0f, height - 0.02f, 0f), new Vector3(length + 0.04f, 0.04f, depth + 0.02f), "Countertop");
            Doors(p, length, 0.12f, 0.84f, depth, Mathf.Max(2, Mathf.RoundToInt(length / 0.5f)));
            if (sink)
            {
                Bit(p, "Evier", new Vector3(length * 0.2f, height - 0.015f, 0f), new Vector3(0.5f, 0.02f, 0.4f), "MetalLight");
                Bit(p, "Robinet", new Vector3(length * 0.2f, height + 0.12f, depth / 2f - 0.08f), new Vector3(0.04f, 0.24f, 0.04f), "MetalLight");
                Bit(p, "Bec", new Vector3(length * 0.2f, height + 0.23f, depth / 2f - 0.16f), new Vector3(0.03f, 0.03f, 0.16f), "MetalLight");
            }
            Bit(p, "Placard_Haut", new Vector3(0f, 1.9f, depth / 2f - 0.17f), new Vector3(length, 0.7f, 0.34f), "Furniture");
            var upper = Group(p, "Portes_Haut", new Vector3(0f, 0f, depth / 2f - 0.17f));
            Doors(upper, length, 1.56f, 2.24f, 0.34f, Mathf.Max(2, Mathf.RoundToInt(length / 0.5f)), handleY: 1.66f);
            // Whatever was left on the worktop.
            for (var i = rng.Next(2, 5); i > 0; i--)
            {
                var x = Range(rng, -length / 2f + 0.15f, length / 2f - 0.15f);
                if (rng.NextDouble() < 0.5)
                    Cylinder("Bouteille", p, new Vector3(x, height + 0.12f, Range(rng, -0.1f, 0.15f)), 0.035f, 0.24f, rng.NextDouble() < 0.5 ? "Glass" : "ShutterGreen", solid: false);
                else
                    Bit(p, "Boite", new Vector3(x, height + 0.08f, Range(rng, -0.1f, 0.15f)), new Vector3(0.12f, 0.16f, 0.08f), "Crate", Quaternion.Euler(0f, Range(rng, -30f, 30f), 0f));
            }
            return p;
        }

        static Transform Fridge(Transform parent, Vector3 pos, float yaw)
        {
            var p = Piece(parent, "Frigo", pos, yaw, new Vector3(0.7f, 1.8f, 0.7f));
            Bit(p, "Caisse", new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 1.8f, 0.7f), "Porcelain");
            Bit(p, "Joint", new Vector3(0f, 1.22f, -0.355f), new Vector3(0.68f, 0.015f, 0.01f), "MetalLight");
            Bit(p, "Poignee_Haut", new Vector3(0.28f, 1.45f, -0.37f), new Vector3(0.03f, 0.3f, 0.03f), "MetalLight");
            Bit(p, "Poignee_Bas", new Vector3(0.28f, 0.95f, -0.37f), new Vector3(0.03f, 0.3f, 0.03f), "MetalLight");
            return p;
        }

        static Transform DoubleBed(Transform parent, Vector3 pos, float yaw, string blanket)
        {
            const float width = 1.6f, length = 2.1f;
            var p = Piece(parent, "Lit", pos, yaw, new Vector3(width, 0.62f, length));
            Bit(p, "Cadre", new Vector3(0f, 0.2f, 0f), new Vector3(width, 0.2f, length), "Furniture");
            Legs(p, width, length, 0.1f, 0.06f, "Furniture");
            Bit(p, "Tete", new Vector3(0f, 0.55f, length / 2f - 0.04f), new Vector3(width + 0.06f, 1.1f, 0.08f), "Furniture");
            Bit(p, "Pied_Lit", new Vector3(0f, 0.38f, -length / 2f + 0.03f), new Vector3(width + 0.04f, 0.56f, 0.06f), "Furniture");
            Bit(p, "Matelas", new Vector3(0f, 0.4f, 0.02f), new Vector3(width - 0.08f, 0.2f, length - 0.14f), "Mattress");
            Bit(p, "Couverture", new Vector3(0f, 0.515f, -0.28f), new Vector3(width - 0.02f, 0.05f, 1.3f), blanket, Quaternion.Euler(0f, 4f, 0f));
            foreach (var x in new[] { -0.38f, 0.38f })
                Bit(p, "Oreiller", new Vector3(x, 0.55f, length / 2f - 0.32f), new Vector3(0.6f, 0.12f, 0.36f), "Linen", Quaternion.Euler(0f, x * 10f, 0f));
            return p;
        }

        static Transform Workbench(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            const float width = 2.4f, depth = 0.6f, height = 0.9f;
            var p = Piece(parent, "Etabli", pos, yaw, new Vector3(width, height, depth));
            Bit(p, "Plateau", new Vector3(0f, height - 0.03f, 0f), new Vector3(width, 0.06f, depth), "Wood");
            Legs(p, width, depth, height - 0.06f, 0.07f, "WoodDark");
            Bit(p, "Tablette", new Vector3(0f, 0.2f, 0f), new Vector3(width - 0.1f, 0.03f, depth - 0.1f), "WoodDark");
            Bit(p, "Panneau", new Vector3(0f, height + 0.55f, depth / 2f - 0.02f), new Vector3(width, 0.9f, 0.03f), "Crate");
            Bit(p, "Etau", new Vector3(width / 2f - 0.25f, height + 0.08f, -0.15f), new Vector3(0.18f, 0.12f, 0.2f), "Metal");
            for (var i = rng.Next(3, 6); i > 0; i--)
                Bit(p, "Outil", new Vector3(Range(rng, -width / 2f + 0.2f, width / 2f - 0.2f), height + Range(rng, 0.3f, 0.8f), depth / 2f - 0.05f),
                    new Vector3(Range(rng, 0.04f, 0.08f), Range(rng, 0.2f, 0.35f), 0.03f), rng.NextDouble() < 0.5 ? "Metal" : "SignRed", Quaternion.Euler(0f, 0f, Range(rng, -15f, 15f)));
            return p;
        }

        static Transform MetalShelf(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            const float width = 2f, depth = 0.5f, height = 1.8f;
            var p = Piece(parent, "Etagere", pos, yaw, new Vector3(width, height, depth));
            foreach (var x in new[] { -1f, 1f })
            foreach (var z in new[] { -1f, 1f })
                Bit(p, "Montant", new Vector3(x * (width / 2f - 0.02f), height / 2f, z * (depth / 2f - 0.02f)), new Vector3(0.04f, height, 0.04f), "Metal");
            for (var shelf = 0; shelf < 4; shelf++)
            {
                var y = 0.1f + shelf * 0.55f;
                Bit(p, "Plateau", new Vector3(0f, y, 0f), new Vector3(width, 0.03f, depth), "MetalLight");
                if (shelf == 3) continue;
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
            Bit(p, "Dos", new Vector3(0f, height / 2f, 0f), new Vector3(length, height, 0.04f), "MetalLight");
            Bit(p, "Socle", new Vector3(0f, 0.08f, 0f), new Vector3(length, 0.16f, depth), "Metal");
            Bit(p, "Fronton", new Vector3(0f, height + 0.08f, 0f), new Vector3(length, 0.16f, 0.06f), "SignGreen");
            string[] goods = { "SignRed", "SignYellow", "SignWhite", "ShutterGreen", "CarBlue", "Crate", "Hay" };
            for (var shelf = 0; shelf < 3; shelf++)
            {
                var y = 0.16f + shelf * 0.48f;
                foreach (var side in new[] { -1f, 1f })
                {
                    Bit(p, "Tablette", new Vector3(0f, y + 0.02f, side * depth / 4f), new Vector3(length - 0.04f, 0.03f, depth / 2f - 0.03f), "MetalLight");
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
            Bit(p, "Caisson", new Vector3(0f, 0.47f, 0f), new Vector3(length, 0.94f, depth), "Furniture");
            Bit(p, "Dessus", new Vector3(0f, 0.97f, 0f), new Vector3(length + 0.05f, 0.05f, depth + 0.05f), "Countertop");
            Bit(p, "Tapis", new Vector3(-length / 6f, 1.0f, 0f), new Vector3(length * 0.53f, 0.02f, 0.4f), "CarBlack");
            Bit(p, "Caisse", new Vector3(length / 2f - 0.5f, 1.1f, 0.05f), new Vector3(0.4f, 0.22f, 0.35f), "Metal");
            Bit(p, "Tiroir_Caisse", new Vector3(length / 2f - 0.5f, 0.93f, -0.33f), new Vector3(0.36f, 0.1f, 0.3f), "Metal");
            return p;
        }

        static Transform Nightstand(Transform parent, Vector3 pos, float yaw)
        {
            var p = Piece(parent, "Table_Chevet", pos, yaw, new Vector3(0.45f, 0.55f, 0.4f));
            Bit(p, "Caisson", new Vector3(0f, 0.28f, 0f), new Vector3(0.45f, 0.5f, 0.4f), "Furniture");
            Drawers(p, 0.45f, 0.06f, 0.5f, 0.4f, 2);
            Cylinder("Pied_Lampe", p, new Vector3(0f, 0.65f, 0.05f), 0.04f, 0.2f, "MetalLight", solid: false);
            Cylinder("Abat_Jour", p, new Vector3(0f, 0.84f, 0.05f), 0.13f, 0.2f, "Lampshade", solid: false);
            return p;
        }

        static Transform Wardrobe(Transform parent, Vector3 pos, float yaw)
        {
            const float width = 1f, height = 2f, depth = 0.6f;
            var p = Piece(parent, "Armoire", pos, yaw, new Vector3(width, height, depth));
            Bit(p, "Caisse", new Vector3(0f, height / 2f + 0.03f, 0f), new Vector3(width, height - 0.06f, depth), "Furniture");
            Bit(p, "Corniche", new Vector3(0f, height - 0.02f, -0.01f), new Vector3(width + 0.05f, 0.05f, depth + 0.04f), "WoodDark");
            Legs(p, width, depth, 0.06f, 0.05f, "WoodDark");
            Doors(p, width, 0.1f, height - 0.06f, depth, 2, handleY: 1.05f);
            return p;
        }

        static Transform Dresser(Transform parent, Vector3 pos, float yaw)
        {
            const float width = 1.2f, height = 0.9f, depth = 0.45f;
            var p = Piece(parent, "Commode", pos, yaw, new Vector3(width, height, depth));
            Bit(p, "Caisse", new Vector3(0f, height / 2f + 0.03f, 0f), new Vector3(width, height - 0.06f, depth), "Furniture");
            Legs(p, width, depth, 0.06f, 0.05f, "WoodDark");
            Drawers(p, width, 0.08f, height - 0.04f, depth, 3);
            // One drawer left pulled out.
            Bit(p, "Tiroir_Ouvert", new Vector3(0f, 0.6f, -depth / 2f - 0.14f), new Vector3(width - 0.1f, 0.24f, 0.3f), "Furniture");
            return p;
        }

        static Transform Bathtub(Transform parent, Vector3 pos, float yaw)
        {
            const float length = 1.7f, width = 0.8f, height = 0.6f;
            var p = Piece(parent, "Baignoire", pos, yaw, new Vector3(length, height, width));
            Bit(p, "Coque", new Vector3(0f, height / 2f, 0f), new Vector3(length, height, width), "Porcelain");
            // A darker inset reads as the hollow seen from above.
            Bit(p, "Creux", new Vector3(0.05f, height - 0.005f, 0f), new Vector3(length - 0.25f, 0.02f, width - 0.16f), "MetalLight");
            Bit(p, "Robinet", new Vector3(-length / 2f + 0.08f, height + 0.08f, 0f), new Vector3(0.05f, 0.16f, 0.05f), "MetalLight");
            return p;
        }

        static Transform Toilet(Transform parent, Vector3 pos, float yaw)
        {
            var p = Piece(parent, "Toilettes", pos, yaw, new Vector3(0.45f, 0.8f, 0.65f));
            Bit(p, "Pied", new Vector3(0f, 0.18f, 0.02f), new Vector3(0.28f, 0.36f, 0.36f), "Porcelain");
            Bit(p, "Cuvette", new Vector3(0f, 0.38f, -0.06f), new Vector3(0.4f, 0.08f, 0.48f), "Porcelain");
            Bit(p, "Abattant", new Vector3(0f, 0.43f, -0.06f), new Vector3(0.38f, 0.02f, 0.44f), "Linen");
            Bit(p, "Reservoir", new Vector3(0f, 0.6f, 0.24f), new Vector3(0.42f, 0.38f, 0.16f), "Porcelain");
            return p;
        }

        // Keeps the name GuaranteeStartingLoot looks for.
        static Transform BathroomCabinet(Transform parent, Vector3 pos, float yaw)
        {
            const float width = 1f, depth = 0.5f;
            var p = Piece(parent, "Meuble_SdB", pos, yaw, new Vector3(width, 0.9f, depth));
            Bit(p, "Caisson", new Vector3(0f, 0.43f, 0f), new Vector3(width, 0.86f, depth), "Porcelain");
            Doors(p, width, 0.08f, 0.82f, depth, 2, "MetalLight");
            Bit(p, "Vasque", new Vector3(0f, 0.88f, 0f), new Vector3(width + 0.04f, 0.04f, depth + 0.02f), "Countertop");
            Bit(p, "Robinet", new Vector3(0f, 1f, depth / 2f - 0.07f), new Vector3(0.04f, 0.2f, 0.04f), "MetalLight");
            Bit(p, "Miroir", new Vector3(0f, 1.6f, depth / 2f - 0.02f), new Vector3(0.7f, 0.8f, 0.02f), "Glass");
            return p;
        }

        static void Rug(Transform parent, Vector3 pos, float yaw, Vector2 size, string mat)
            => Box("Tapis", parent, pos + Vector3.up * 0.105f, new Vector3(size.x, 0.01f, size.y), mat, Quaternion.Euler(0f, yaw, 0f), solid: false);

        static void CeilingLamp(Transform parent, Vector3 roomCenter)
        {
            var lamp = Group(parent, "Plafonnier", new Vector3(roomCenter.x, WallHeight, roomCenter.z));
            Bit(lamp, "Fil", new Vector3(0f, -0.25f, 0f), new Vector3(0.015f, 0.5f, 0.015f), "Strap");
            Cylinder("Abat_Jour", lamp, new Vector3(0f, -0.58f, 0f), 0.22f, 0.18f, "Lampshade", solid: false);
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
