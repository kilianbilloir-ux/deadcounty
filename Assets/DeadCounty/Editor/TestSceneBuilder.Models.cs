using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCounty.EditorTools
{
    // Models made with aiblender (Tools/aiblender, exported as GLB into Art/Models/Source) become one mesh each,
    // drawn with a single material: every flat colour is a cell of one shared palette texture, so models batch
    // together whatever their colours. A part whose material is named mat_<Key> keeps the builder's own material
    // of that key instead (glass, lamps that glow), as a submesh of its own.
    public static partial class TestSceneBuilder
    {
        const string ModelDir = "Assets/DeadCounty/Art/Models";
        const string ModelSourceDir = ModelDir + "/Source";
        const string ModelPalettePath = ModelDir + "/T_Models_Palette.png";
        const string ModelMaterialPath = ModelDir + "/M_Models.mat";
        const string KeyedMaterial = "mat_";
        // Swapped for every colour of a model: a burnt wreck.
        const string AnyColor = "*";
        const int PaletteSize = 128;
        const int PaletteCell = 4;
        const int PaletteColumns = PaletteSize / PaletteCell;

        static readonly Dictionary<string, GlbReader.Model> ModelSources = new();
        static readonly Dictionary<string, (Mesh Mesh, Material[] Materials)> Models = new();
        // Cells are handed out as the build meets colours: the build is deterministic, so is the palette.
        static readonly Dictionary<Color32, int> PaletteCells = new();
        static Material modelMaterial;

        static void EnsureModels()
        {
            ModelSources.Clear();
            Models.Clear();
            PaletteCells.Clear();
            foreach (var path in Directory.GetFiles(ModelSourceDir, "*.glb"))
                ModelSources[Path.GetFileNameWithoutExtension(path)] = GlbReader.Read(File.ReadAllBytes(path));
            modelMaterial = ModelMaterial(WritePalette());
        }

        // Writes the colours met so far into the palette: before anything renders the models, and once the build
        // has asked for all of them.
        static void FinishModels() => WritePalette();

        // The model, or the same one repainted: each swap turns a colour written in the model's file (hex) into
        // another (hex or palette key).
        static (Mesh Mesh, Material[] Materials) ModelMesh(string name, params (string From, string To)[] swaps)
        {
            var id = swaps.Length == 0 ? name : $"{name}_{string.Join("_", swaps.Select(s => s.To.TrimStart('#')))}";
            if (Models.TryGetValue(id, out var built)) return built;
            if (!ModelSources.TryGetValue(name, out var source))
                throw new InvalidOperationException($"No model {name}: export it to {ModelSourceDir} (Tools/aiblender/build.sh) and run EnsureModels first");

            var everything = swaps.Length == 1 && swaps[0].From == AnyColor;
            var map = everything ? null : swaps.ToDictionary(s => Hex(s.From), s => PaintColor(s.To));
            var used = new HashSet<Color32>();
            var uvs = new List<Vector2>(source.Colors.Count);
            foreach (var color in source.Colors)
            {
                var painted = color;
                if (everything) painted = PaintColor(swaps[0].To);
                else if (map.TryGetValue(color, out var to))
                {
                    painted = to;
                    used.Add(color);
                }
                if (!PaletteCells.TryGetValue(painted, out var cell)) PaletteCells[painted] = cell = PaletteCells.Count;
                uvs.Add(new Vector2((cell % PaletteColumns + 0.5f) / PaletteColumns, (cell / PaletteColumns + 0.5f) / PaletteColumns));
            }
            if (PaletteCells.Count > PaletteColumns * PaletteColumns)
                throw new InvalidOperationException($"The models use more than the {PaletteColumns * PaletteColumns} colours the palette holds");
            // A colour changed in the model's file would otherwise leave the variant silently unpainted.
            if (!everything && used.Count < map.Count)
                throw new InvalidOperationException($"Model {id}: the source has no colour {string.Join(", ", swaps.Where(s => !used.Contains(Hex(s.From))).Select(s => s.From))}");

            var keys = everything ? new List<string>() : source.Parts.Select(p => MaterialKey(p.Material)).Where(k => k != null).Distinct().OrderBy(k => k, StringComparer.Ordinal).ToList();
            var submeshes = Enumerable.Range(0, keys.Count + 1).Select(_ => new List<int>()).ToArray();
            foreach (var (material, start, count) in source.Parts)
                submeshes[everything ? 0 : keys.IndexOf(MaterialKey(material)) + 1].AddRange(source.Indices.GetRange(start, count));

            var path = $"{ModelDir}/SM_{id}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = $"SM_{id}";
            mesh.indexFormat = source.Positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(source.Positions);
            mesh.SetNormals(source.Normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshes.Length;
            for (var i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return Models[id] = (mesh, keys.Select(Mat).Prepend(modelMaterial).ToArray());
        }

        // "mat_Glass" or "mat_Glass@part" gives "Glass"; null for a material of the palette.
        static string MaterialKey(string material)
        {
            if (!material.StartsWith(KeyedMaterial)) return null;
            var key = material.Substring(KeyedMaterial.Length).Split('@')[0];
            return Palette.ContainsKey(key) ? key : throw new InvalidOperationException($"Model material {material}: the palette has no {key}");
        }

        static Color32 PaintColor(string hexOrKey) => hexOrKey.StartsWith("#") ? Hex(hexOrKey) : (Color32)Palette[hexOrKey];

        // A palette colour pulled toward white (amount > 0) or black, as hex: the second tone of a repainted model.
        static string Shade(string key, float amount)
            => "#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(Palette[key], amount > 0f ? Color.white : Color.black, Mathf.Abs(amount)));

        static Color32 Hex(string hex)
            => ColorUtility.TryParseHtmlString(hex, out var color) ? (Color32)color : throw new FormatException($"'{hex}' is not a colour");

        static Texture2D WritePalette()
        {
            var pixels = new Color32[PaletteSize * PaletteSize];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 255);
            foreach (var (color, cell) in PaletteCells)
            {
                var opaque = new Color32(color.r, color.g, color.b, 255);
                for (var y = 0; y < PaletteCell; y++)
                for (var x = 0; x < PaletteCell; x++)
                    pixels[(cell / PaletteColumns * PaletteCell + y) * PaletteSize + cell % PaletteColumns * PaletteCell + x] = opaque;
            }
            var texture = new Texture2D(PaletteSize, PaletteSize, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            var png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            if (!File.Exists(ModelPalettePath) || !File.ReadAllBytes(ModelPalettePath).AsSpan().SequenceEqual(png))
            {
                File.WriteAllBytes(ModelPalettePath, png);
                AssetDatabase.ImportAsset(ModelPalettePath);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(ModelPalettePath);
            if (importer.mipmapEnabled || importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ModelPalettePath);
        }

        static Material ModelMaterial(Texture2D palette)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("Shader Universal Render Pipeline/Lit not found");
            var material = AssetDatabase.LoadAssetAtPath<Material>(ModelMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, ModelMaterialPath);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", palette);
            material.SetTexture("_MainTex", palette);
            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // An aiblender model, pivot where its file puts the origin; never solid, see Solid.
        static GameObject Model(string name, Transform parent, Vector3 position, Quaternion? rotation, string model, params (string From, string To)[] swaps)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation ?? Quaternion.identity);
            var (mesh, materials) = ModelMesh(model, swaps);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        // What a model made of one mesh leaves out: the block the player, the camera and the NavMesh run into.
        static void Solid(Transform parent, Vector3 center, Vector3 size)
        {
            var collider = parent.gameObject.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }
    }
}
