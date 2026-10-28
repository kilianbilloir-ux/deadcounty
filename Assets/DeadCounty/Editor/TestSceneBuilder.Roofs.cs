using System.Linq;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    // Gable roofs with some depth: shingle courses and a ridge cap, white fascia, rake boards and soffits, the gable
    // ends closed in the wall colour with a louvred vent, a flat ceiling inside; lap siding on the house and church
    // walls. The former prism stays under each roof as its invisible collider, so physics, the NavMesh and the camera
    // see the same roof as before. Nothing added here is solid.
    public static partial class TestSceneBuilder
    {
        const float ClapboardHeight = 0.23f;
        const float ClapboardProud = 0.035f;
        const float ClapboardTopProud = 0.005f;

        static int Cm(float metres) => Mathf.RoundToInt(metres * 100f);

        // walls: half extents of the outer wall faces along the ridge (x) and across it (y), in the roof frame.
        // ceilingMat is null when the body under the roof is solid.
        static void GableRoof(Transform parent, Vector3 baseCenter, float ridgeLength, float span, float height, float yaw, Vector2 walls,
            string roofMat, string gableMat, string ceilingMat, bool clapboard = false)
        {
            var shell = Roof(parent, "Toit", baseCenter, ridgeLength, span, height, roofMat, yaw);
            Object.DestroyImmediate(shell.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(shell.GetComponent<MeshFilter>());
            var mats = ceilingMat == null ? new[] { roofMat, "Trim", gableMat } : new[] { roofMat, "Trim", gableMat, ceilingMat };
            MeshObject("Toiture", parent, GableRoofMesh(ridgeLength, span, height, walls, clapboard, ceilingMat != null), baseCenter,
                Quaternion.Euler(0f, yaw, 0f), Vector3.one, mats, false);
        }

        // Origin on the wall tops, ridge along X, the top of the slopes on the former prism. Submeshes: roof, trim,
        // gables, ceiling.
        static Mesh GableRoofMesh(float length, float span, float height, Vector2 walls, bool clapboard, bool ceiling)
        {
            var name = $"SM_Toit_{Cm(length)}_{Cm(span)}_{Cm(height)}_{Cm(walls.x)}_{Cm(walls.y)}{(clapboard ? "_Bardage" : "")}{(ceiling ? "_Plafond" : "")}";
            return Cached(name, () =>
            {
                float hx = length / 2f, hz = span / 2f;
                var thickness = Mathf.Clamp(span * 0.013f, 0.08f, 0.2f);
                var lip = thickness * 0.25f;
                var down = Vector3.down * thickness;
                var raised = Vector3.up * lip;
                var rake = Vector3.up * (lip + 0.025f);
                var m = new MeshBuilder();
                m.Sub();
                m.Sub();
                m.Sub();
                if (ceiling) m.Sub();
                foreach (var s in new[] { -1f, 1f })
                {
                    Vector3 P(float u, float x) => new(x, height * u, s * hz * (1f - u));
                    var normal = new Vector3(0f, hz, s * height).normalized;
                    var eave = new Vector3(0f, 0f, s);
                    var courses = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(P(0f, 0f), P(1f, 0f)) / 0.35f));
                    m.Use(0);
                    // The courses are flatter than the slope, so from a low camera near the eaves their tops face away:
                    // the slope itself, just under them, keeps the roof opaque.
                    var deck = Vector3.down * 0.004f;
                    m.Quad(P(0f, -hx) + deck, P(0f, hx) + deck, P(1f, hx) + deck, P(1f, -hx) + deck, normal);
                    for (var i = 0; i < courses; i++)
                    {
                        float u0 = (float)i / courses, u1 = (float)(i + 1) / courses;
                        m.Quad(P(u0, -hx) + raised, P(u0, hx) + raised, P(u1, hx), P(u1, -hx), normal);
                        if (i > 0) m.Quad(P(u0, -hx), P(u0, hx), P(u0, hx) + raised, P(u0, -hx) + raised, eave);
                    }
                    m.Use(1);
                    m.Quad(P(0f, -hx) + down, P(0f, hx) + down, P(0f, hx) + raised, P(0f, -hx) + raised, eave);
                    m.Quad(P(0f, -hx) + down, P(0f, hx) + down, P(1f, hx) + down, P(1f, -hx) + down, -normal);
                    foreach (var e in new[] { -1f, 1f })
                    {
                        float outer = e * hx, inner = e * (hx - 0.12f);
                        m.Quad(P(0f, outer) + down, P(1f, outer) + down, P(1f, outer) + rake, P(0f, outer) + rake, new Vector3(e, 0f, 0f));
                        m.Quad(P(0f, inner) + down, P(1f, inner) + down, P(1f, inner) + rake, P(0f, inner) + rake, new Vector3(-e, 0f, 0f));
                        m.Quad(P(0f, outer) + rake, P(1f, outer) + rake, P(1f, inner) + rake, P(0f, inner) + rake, normal);
                        m.Quad(P(0f, outer) + down, P(0f, inner) + down, P(0f, inner) + rake, P(0f, outer) + rake, eave);
                    }
                }

                m.Use(0);
                var cap = Mathf.Min(0.18f, hz * 0.1f);
                var capLow = height * (1f - cap / hz) + lip + 0.005f;
                Vector3 L(float x) => new(x, capLow, -cap);
                Vector3 R(float x) => new(x, capLow, cap);
                Vector3 T(float x) => new(x, height + lip + 0.07f, 0f);
                m.Quad(L(-hx), L(hx), T(hx), T(-hx), new Vector3(0f, 1f, -1f));
                m.Quad(T(-hx), T(hx), R(hx), R(-hx), new Vector3(0f, 1f, 1f));
                foreach (var e in new[] { -1f, 1f })
                {
                    m.Tri(L(e * hx), T(e * hx), R(e * hx), new Vector3(e, 0f, 0f));
                    Gable(m, e, walls, hz, height, thickness, clapboard);
                }

                if (ceiling)
                {
                    m.Use(3);
                    m.Quad(new Vector3(-walls.x, 0f, -walls.y), new Vector3(walls.x, 0f, -walls.y), new Vector3(walls.x, 0f, walls.y), new Vector3(-walls.x, 0f, walls.y), Vector3.down);
                }
                return m.Save(name);
            });
        }

        // Gable end on the outer wall face at x = side * walls.x, tucked 2 cm into the slab: rows of lap siding (flat
        // rows otherwise) and a louvred vent when it is tall enough.
        static void Gable(MeshBuilder m, float side, Vector2 walls, float hz, float height, float thickness, bool clapboard)
        {
            var tucked = thickness - 0.02f;
            var apex = height - tucked;
            var rowHeight = clapboard ? ClapboardHeight : 0.5f;
            var outward = new Vector3(side, 0f, 0f);
            float Half(float y) => Mathf.Min(walls.y, hz * (1f - (y + tucked) / height));
            Vector3 G(float y, float z, float proud) => new(side * (walls.x + proud), y, z);

            m.Use(2);
            for (var y0 = 0f; y0 < apex - 0.001f; y0 += rowHeight)
            {
                var y1 = Mathf.Min(y0 + rowHeight, apex);
                var bottom = clapboard ? ClapboardProud : 0f;
                var top = clapboard ? Mathf.Lerp(ClapboardProud, ClapboardTopProud, (y1 - y0) / rowHeight) : 0f;
                float h0 = Half(y0), h1 = Half(y1);
                m.Quad(G(y0, -h0, bottom), G(y0, h0, bottom), G(y1, h1, top), G(y1, -h1, top), outward);
                if (clapboard) m.Quad(G(y0, -h0, 0f), G(y0, h0, 0f), G(y0, h0, bottom), G(y0, -h0, bottom), Vector3.down);
            }
            if (apex < 1.5f) return;

            var vent = apex * 0.5f;
            const float w = 0.56f, h = 0.42f, bar = 0.05f;
            m.Use(0);
            m.Quad(G(vent - h / 2f, -w / 2f, 0.045f), G(vent - h / 2f, w / 2f, 0.045f), G(vent + h / 2f, w / 2f, 0.045f), G(vent + h / 2f, -w / 2f, 0.045f), outward);
            m.Use(1);
            void Bar(float y, float z, float barHeight, float barWidth, float proud, float depth)
                => AddCuboid(m, G(y, z, proud + depth / 2f), new Vector3(depth, barHeight, barWidth), Quaternion.identity);
            foreach (var sign in new[] { -1f, 1f })
            {
                Bar(vent + sign * (h + bar) / 2f, 0f, bar, w + 2f * bar, 0f, 0.07f);
                Bar(vent, sign * (w + bar) / 2f, h, bar, 0f, 0.07f);
            }
            for (var i = 1; i <= 3; i++)
                Bar(vent - h / 2f + i * h / 4f, 0f, 0.035f, w, 0.045f, 0.025f);
        }

        // Lap siding on the outside of the walls of a shell (the list the walls were built from): rows of boards
        // tilted out at the bottom, cut around the window trims and the doors; the lips under the boards read as
        // shadow lines.
        static Mesh ClapboardMesh(string name, (string Name, Vector2 From, Vector2 To, Opening[] Openings)[] shell, float bottom, float top) => Cached(name, () =>
        {
            var m = new MeshBuilder().Sub();
            var rows = Mathf.Max(1, Mathf.RoundToInt((top - bottom) / ClapboardHeight));
            var rowHeight = (top - bottom) / rows;
            // Window trims stand 8 cm past the opening and from the sill to the lintel; door frames 12 cm.
            (float From, float To, float Low, float High) Hole(Opening o) => o.Bottom > 0f
                ? (o.Offset - o.Width / 2f - 0.08f, o.Offset + o.Width / 2f + 0.08f, o.Bottom - 0.06f, o.Top + 0.1f)
                : (o.Offset - o.Width / 2f - 0.12f, o.Offset + o.Width / 2f + 0.12f, float.NegativeInfinity, o.Top + 0.12f);

            foreach (var wall in shell)
            {
                var dir = (wall.To - wall.From).normalized;
                var along = new Vector3(dir.x, 0f, dir.y);
                var outward = new Vector3(dir.y, 0f, -dir.x);
                var origin = new Vector3(wall.From.x, 0f, wall.From.y) + outward * (WallThickness / 2f);
                var length = Vector2.Distance(wall.From, wall.To);
                var holes = wall.Openings.Select(Hole).OrderBy(h => h.From).ToArray();
                Vector3 W(float s, float y, float proud) => origin + along * s + Vector3.up * y + outward * proud;

                for (var r = 0; r < rows; r++)
                {
                    var y0 = bottom + r * rowHeight;
                    var y1 = y0 + rowHeight;
                    float Proud(float y) => Mathf.Lerp(ClapboardProud, ClapboardTopProud, (y - y0) / rowHeight);
                    void Board(float s0, float s1, float yb, float yt)
                    {
                        if (s1 - s0 < 0.02f || yt - yb < 0.01f) return;
                        float pb = Proud(yb), pt = Proud(yt);
                        m.Quad(W(s0, yb, pb), W(s1, yb, pb), W(s1, yt, pt), W(s0, yt, pt), outward);
                        m.Quad(W(s0, yb, 0f), W(s1, yb, 0f), W(s1, yb, pb), W(s0, yb, pb), Vector3.down);
                        if (yt < y1 - 0.001f) m.Quad(W(s0, yt, 0f), W(s1, yt, 0f), W(s1, yt, pt), W(s0, yt, pt), Vector3.up);
                        m.Quad(W(s0, yb, 0f), W(s0, yt, 0f), W(s0, yt, pt), W(s0, yb, pb), -along);
                        m.Quad(W(s1, yb, 0f), W(s1, yt, 0f), W(s1, yt, pt), W(s1, yb, pb), along);
                    }

                    var cursor = -WallThickness / 2f;
                    foreach (var h in holes)
                    {
                        if (h.Low >= y1 || h.High <= y0) continue;
                        Board(cursor, h.From, y0, y1);
                        Board(h.From, h.To, y0, Mathf.Min(y1, h.Low));
                        Board(h.From, h.To, Mathf.Max(y0, h.High), y1);
                        cursor = h.To;
                    }
                    Board(cursor, length + WallThickness / 2f, y0, y1);
                }
            }
            return m.Save(name);
        });

        static void ChimneyCap(Transform parent, Vector3 top, float width)
        {
            Box("Chapeau_Cheminee", parent, top + Vector3.up * 0.04f, new Vector3(width + 0.14f, 0.08f, width + 0.14f), "Concrete", solid: false);
            Cylinder("Conduit", parent, top + Vector3.up * 0.18f, 0.09f, 0.2f, "Metal", solid: false);
        }
    }
}
