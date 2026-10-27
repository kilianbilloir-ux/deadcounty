using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        static readonly Dictionary<string, Mesh> MeshCache = new();

        static Mesh Cached(string name, System.Func<Mesh> build)
        {
            if (!MeshCache.TryGetValue(name, out var mesh)) MeshCache[name] = mesh = build();
            return mesh;
        }

        // Triangle soup where every triangle owns its vertices: flat shading, i.e. the low poly look.
        class MeshBuilder
        {
            readonly List<List<Vector3>> submeshes = new();
            List<Vector3> current;

            public MeshBuilder Sub()
            {
                current = new List<Vector3>();
                submeshes.Add(current);
                return this;
            }

            // Flips the winding when needed so the face normal points along outward.
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) (b, c) = (c, b);
                current.Add(a);
                current.Add(b);
                current.Add(c);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                Tri(a, b, c, outward);
                Tri(a, c, d, outward);
            }

            // Saves into an existing asset when there is one, so GUIDs stay stable across rebuilds.
            public Mesh Save(string name, bool worldUv = false)
            {
                var verts = submeshes.SelectMany(s => s).ToArray();
                var normals = new Vector3[verts.Length];
                for (var i = 0; i < verts.Length; i += 3)
                    normals[i] = normals[i + 1] = normals[i + 2] = Vector3.Cross(verts[i + 1] - verts[i], verts[i + 2] - verts[i]).normalized;

                var path = $"{MeshDir}/{name}.asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                var isNew = mesh == null;
                if (isNew) mesh = new Mesh();
                mesh.Clear();
                mesh.name = name;
                mesh.indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.vertices = verts;
                mesh.normals = normals;
                if (worldUv) mesh.uv = verts.Select(v => new Vector2(v.x, v.z) / 10f).ToArray();
                mesh.subMeshCount = submeshes.Count;
                var offset = 0;
                for (var s = 0; s < submeshes.Count; s++)
                {
                    mesh.SetTriangles(Enumerable.Range(offset, submeshes[s].Count).ToArray(), s);
                    offset += submeshes[s].Count;
                }
                mesh.RecalculateBounds();
                if (isNew) AssetDatabase.CreateAsset(mesh, path);
                else EditorUtility.SetDirty(mesh);
                return mesh;
            }
        }

        // Unit prism: 1 x 1 footprint centered on the origin, ridge along X at height 1.
        static Mesh RoofMesh() => Cached("SM_Roof_Gable", () =>
        {
            var m = new MeshBuilder().Sub();
            Vector3 a = new(-0.5f, 0f, -0.5f), b = new(0.5f, 0f, -0.5f), c = new(0.5f, 0f, 0.5f), d = new(-0.5f, 0f, 0.5f);
            Vector3 e = new(-0.5f, 1f, 0f), f = new(0.5f, 1f, 0f);
            m.Quad(a, b, f, e, new Vector3(0f, 1f, -1f));
            m.Quad(d, c, f, e, new Vector3(0f, 1f, 1f));
            m.Tri(a, e, d, Vector3.left);
            m.Tri(b, f, c, Vector3.right);
            m.Quad(a, b, c, d, Vector3.down);
            return m.Save("SM_Roof_Gable");
        });

        // Unit cone: radius 1, height 1, base on the origin.
        static Mesh ConeMesh(int sides) => Cached($"SM_Cone_{sides}", () =>
        {
            var m = new MeshBuilder().Sub();
            AddCone(m, Vector3.zero, 1f, 1f, sides, 0f, null);
            return m.Save($"SM_Cone_{sides}");
        });

        static Mesh PineMesh(int variant) => Cached($"SM_Pine_{variant}", () =>
        {
            var rng = new System.Random(100 + variant);
            var m = new MeshBuilder().Sub();
            AddPrism(m, Vector3.zero, 0.24f, 0.14f, 2.4f, 6);
            m.Sub();
            var tiers = 3 + variant % 2;
            var y = 1.5f;
            for (var i = 0; i < tiers; i++)
            {
                AddCone(m, new Vector3(0f, y, 0f), 2.3f - i * 0.45f, 2.7f - i * 0.25f, 7, i * 0.6f, rng);
                y += 1.45f;
            }
            return m.Save($"SM_Pine_{variant}");
        });

        static Mesh TreeMesh(int variant) => Cached($"SM_Tree_{variant}", () =>
        {
            var rng = new System.Random(200 + variant);
            var m = new MeshBuilder().Sub();
            AddPrism(m, Vector3.zero, 0.3f, 0.18f, 3.4f, 6);
            m.Sub();
            AddBlob(m, new Vector3(0f, 4.4f, 0f), new Vector3(2.5f, 2.1f, 2.5f), 1, rng, 0.18f);
            if (variant != 1) AddBlob(m, new Vector3(1f, 3.8f, 0.7f), new Vector3(1.6f, 1.4f, 1.6f), 1, rng, 0.2f);
            if (variant == 2) AddBlob(m, new Vector3(-0.9f, 5.2f, -0.6f), new Vector3(1.5f, 1.3f, 1.5f), 1, rng, 0.2f);
            return m.Save($"SM_Tree_{variant}");
        });

        static Mesh BushMesh(int variant) => Cached($"SM_Bush_{variant}", () =>
        {
            var m = new MeshBuilder().Sub();
            AddBlob(m, new Vector3(0f, 0.45f, 0f), new Vector3(1f, 0.7f, 1f), variant, new System.Random(300 + variant), 0.22f);
            return m.Save($"SM_Bush_{variant}");
        });

        static Mesh TrashBagMesh() => Cached("SM_TrashBag", () =>
        {
            var m = new MeshBuilder().Sub();
            AddBlob(m, new Vector3(0f, 0.28f, 0f), new Vector3(0.3f, 0.3f, 0.27f), 1, new System.Random(500), 0.18f);
            AddBlob(m, new Vector3(0f, 0.58f, 0f), new Vector3(0.07f, 0.08f, 0.07f), 0, new System.Random(501), 0.1f);
            return m.Save("SM_TrashBag");
        });

        static Mesh SandbagMesh() => Cached("SM_Sandbag", () =>
        {
            var m = new MeshBuilder().Sub();
            AddBlob(m, Vector3.zero, new Vector3(0.3f, 0.13f, 0.22f), 1, new System.Random(502), 0.08f);
            return m.Save("SM_Sandbag");
        });

        static Mesh RockMesh(int variant) => Cached($"SM_Rock_{variant}", () =>
        {
            var m = new MeshBuilder().Sub();
            AddBlob(m, new Vector3(0f, 0.25f, 0f), new Vector3(1f, 0.65f, 0.85f), 0, new System.Random(400 + variant), 0.28f);
            return m.Save($"SM_Rock_{variant}");
        });

        static Mesh TerrainMesh() => Cached("SM_Terrain", () =>
        {
            const int cells = 100;
            const float step = MapSize / cells;
            var m = new MeshBuilder().Sub();
            Vector3 P(int i, int j)
            {
                var x = -HalfMap + i * step;
                var z = -HalfMap + j * step;
                return new Vector3(x, TerrainHeight(x, z), z);
            }
            for (var i = 0; i < cells; i++)
            for (var j = 0; j < cells; j++)
                m.Quad(P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1), Vector3.up);
            return m.Save("SM_Terrain", worldUv: true);
        });

        static void AddCone(MeshBuilder m, Vector3 baseCenter, float radius, float height, int sides, float angleOffset, System.Random jitter)
        {
            var apex = baseCenter + Vector3.up * height;
            var ring = new Vector3[sides];
            for (var i = 0; i < sides; i++)
            {
                var a = angleOffset + i * Mathf.PI * 2f / sides;
                var r = radius * (jitter == null ? 1f : Range(jitter, 0.85f, 1.15f));
                ring[i] = baseCenter + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            }
            for (var i = 0; i < sides; i++)
            {
                var p = ring[i];
                var q = ring[(i + 1) % sides];
                var mid = (p + q) / 2f - baseCenter;
                m.Tri(p, q, apex, new Vector3(mid.x, 0f, mid.z));
                m.Tri(p, q, baseCenter, Vector3.down);
            }
        }

        static void AddPrism(MeshBuilder m, Vector3 baseCenter, float bottomRadius, float topRadius, float height, int sides)
        {
            for (var i = 0; i < sides; i++)
            {
                var a0 = i * Mathf.PI * 2f / sides;
                var a1 = (i + 1) * Mathf.PI * 2f / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                var top = Vector3.up * height;
                m.Quad(baseCenter + d0 * bottomRadius, baseCenter + d1 * bottomRadius, baseCenter + top + d1 * topRadius, baseCenter + top + d0 * topRadius, d0 + d1);
            }
        }

        static void AddBlob(MeshBuilder m, Vector3 center, Vector3 radii, int subdivisions, System.Random rng, float jitter)
        {
            var (verts, tris) = Icosphere(subdivisions);
            var shaped = verts.Select(v => center + Vector3.Scale(v * (1f + Range(rng, -jitter, jitter)), radii)).ToArray();
            for (var i = 0; i < tris.Count; i += 3)
            {
                var a = shaped[tris[i]];
                var b = shaped[tris[i + 1]];
                var c = shaped[tris[i + 2]];
                m.Tri(a, b, c, (a + b + c) / 3f - center);
            }
        }

        static (List<Vector3> verts, List<int> tris) Icosphere(int subdivisions)
        {
            var t = (1f + Mathf.Sqrt(5f)) / 2f;
            var verts = new List<Vector3>
            {
                new(-1f, t, 0f), new(1f, t, 0f), new(-1f, -t, 0f), new(1f, -t, 0f),
                new(0f, -1f, t), new(0f, 1f, t), new(0f, -1f, -t), new(0f, 1f, -t),
                new(t, 0f, -1f), new(t, 0f, 1f), new(-t, 0f, -1f), new(-t, 0f, 1f),
            }.ConvertAll(v => v.normalized);
            var tris = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (var s = 0; s < subdivisions; s++)
            {
                var midpoints = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    var key = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
                    if (midpoints.TryGetValue(key, out var index)) return index;
                    verts.Add(((verts[a] + verts[b]) / 2f).normalized);
                    return midpoints[key] = verts.Count - 1;
                }
                var next = new List<int>();
                for (var i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }
            return (verts, tris);
        }
    }
}
