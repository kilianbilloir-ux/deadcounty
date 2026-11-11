using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        static readonly Dictionary<string, Material> Materials = new();
        static readonly HashSet<string> UsedMaterials = new();
        static readonly Vector2 TestPadSize = new(74f, 48f);

        static Color Rgb(int hex) => new Color32((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);

        // Desaturated rural palette derived from the GDD v0.2 colors (olive, rust, fog grey, sodium light).
        static readonly Dictionary<string, Color> Palette = new()
        {
            ["Grass"] = Rgb(0x6E7149), ["Dirt"] = Rgb(0x6A5640), ["Gravel"] = Rgb(0x8A857C),
            ["Road"] = Rgb(0x3A3B3E), ["RoadLineYellow"] = Rgb(0xC8A24A), ["RoadLineWhite"] = Rgb(0xD8D5CC),
            ["Sidewalk"] = Rgb(0xA8A6A0), ["Concrete"] = Rgb(0x9C9A94), ["TestPad"] = Rgb(0xB8B6B0),
            ["Wall"] = Rgb(0xCFCBC2), ["Interior"] = Rgb(0xC9BFAE), ["Floor"] = Rgb(0x8A6E52), ["Trim"] = Rgb(0xE4E1D8),
            ["SidingCream"] = Rgb(0xD6CBB0), ["SidingBlue"] = Rgb(0x8A9DA8), ["SidingSage"] = Rgb(0x8E9A80),
            ["SidingRed"] = Rgb(0x8C4A3C), ["SidingWhite"] = Rgb(0xDCDCD4), ["SidingYellow"] = Rgb(0xCDB77E),
            ["ShutterDark"] = Rgb(0x2F3B4A), ["ShutterGreen"] = Rgb(0x3E5240), ["ShutterRed"] = Rgb(0x6B2E25),
            ["Roof"] = Rgb(0x4A3A33), ["RoofDark"] = Rgb(0x2E2F33), ["RoofRust"] = Rgb(0x8A4B30), ["RoofGrey"] = Rgb(0x6A6F73),
            ["Brick"] = Rgb(0x7A4636), ["BarnRed"] = Rgb(0x8A2E22), ["MetalSiding"] = Rgb(0x6C7A82),
            ["Wood"] = Rgb(0x7A5A3C), ["WoodDark"] = Rgb(0x4A3828),
            ["Glass"] = Rgb(0x34424D), ["Metal"] = Rgb(0x4D5257), ["MetalLight"] = Rgb(0x9AA0A4),
            ["SignRed"] = Rgb(0xA8322A), ["SignWhite"] = Rgb(0xE6E6E0), ["SignGreen"] = Rgb(0x2F5A3A), ["SignYellow"] = Rgb(0xD8B030),
            ["LampGlow"] = Rgb(0xD4B976), ["PanelGlow"] = Rgb(0xE8EEF2), ["PoliceRed"] = Rgb(0xC8261E), ["PoliceBlue"] = Rgb(0x1E4FC8),
            ["Trunk"] = Rgb(0x4E3B2C), ["Pine"] = Rgb(0x2E4433), ["Foliage"] = Rgb(0x4E6234), ["FoliageOlive"] = Rgb(0x6D6A3A),
            ["FoliageAutumn"] = Rgb(0xA0602A), ["FoliageRust"] = Rgb(0x8A3F24), ["Bush"] = Rgb(0x445A30), ["Rock"] = Rgb(0x77777A),
            ["Wheat"] = Rgb(0xBFA25A), ["Corn"] = Rgb(0x7E8A3E), ["Hay"] = Rgb(0xC7A860),
            ["CarRed"] = Rgb(0x7A2420), ["CarBlue"] = Rgb(0x2E4766), ["CarGreen"] = Rgb(0x3E5444), ["CarBeige"] = Rgb(0xB8A88A),
            ["CarWhite"] = Rgb(0xD6D6D2), ["CarBlack"] = Rgb(0x1C1D20), ["CarBurnt"] = Rgb(0x252220), ["Tire"] = Rgb(0x151515),
            ["BusYellow"] = Rgb(0xD6A21E), ["TractorGreen"] = Rgb(0x3F6B3A),
            ["Furniture"] = Rgb(0x6A4E3A), ["Fabric"] = Rgb(0x5E6B7A), ["Mattress"] = Rgb(0xCFCAC0), ["Porcelain"] = Rgb(0xE8E8E4),
            ["FabricRust"] = Rgb(0x7A4C3A), ["FabricOlive"] = Rgb(0x5C6146), ["Linen"] = Rgb(0xB8B2A4), ["Blanket"] = Rgb(0x6E5A48),
            ["BlanketRed"] = Rgb(0x6E3530), ["Rug"] = Rgb(0x6B4A3C), ["RugBlue"] = Rgb(0x3F4D5E), ["Countertop"] = Rgb(0xB5AFA2),
            ["Lampshade"] = Rgb(0xC9BC9C), ["Paper"] = Rgb(0xB9B2A2), ["Burlap"] = Rgb(0x857456), ["StoreFloor"] = Rgb(0xA9A69C),
            ["Headstone"] = Rgb(0x8E8E8A), ["Crate"] = Rgb(0x8C734C),
            ["Marker"] = Rgb(0xEDEDED), ["MarkerMajor"] = Rgb(0xC03A2E), ["Accent"] = Rgb(0xF2CC1A),
            ["Skin"] = Rgb(0xC69C7A), ["Hair"] = Rgb(0x2A211B), ["Eye"] = Rgb(0x151515), ["Jacket"] = Rgb(0x5C6340),
            ["JacketDark"] = Rgb(0x434A30), ["Jeans"] = Rgb(0x34404F), ["Boots"] = Rgb(0x3B2F25), ["Backpack"] = Rgb(0x6E5B3D),
            ["BackpackDark"] = Rgb(0x55462F), ["Strap"] = Rgb(0x2A2A2A),
            ["ZombieSkin"] = Rgb(0x8E9681), ["ZombieHair"] = Rgb(0x3A342D), ["ZombieEye"] = Rgb(0xD9CF8C), ["Blood"] = Rgb(0x3A1714),
            ["ZombieShirt"] = Rgb(0x6E6650), ["ZombieShirtBlue"] = Rgb(0x48566A), ["ZombieShirtRed"] = Rgb(0x6A3530), ["ZombieShirtWhite"] = Rgb(0x9C998C),
            ["ZombiePants"] = Rgb(0x39414E), ["ZombiePantsKhaki"] = Rgb(0x655C45), ["ZombiePantsDark"] = Rgb(0x2C2C2F),
            ["Obstacle"] = Rgb(0xE08A33), ["Ramp"] = Rgb(0x4F7FD0), ["Stairs"] = Rgb(0x5AA866),
        };

        static readonly Dictionary<string, float> Glow = new()
        {
            ["LampGlow"] = 2.5f, ["PanelGlow"] = 1.6f, ["PoliceRed"] = 3f, ["PoliceBlue"] = 3f, ["ZombieEye"] = 1.8f,
        };

        static float SmoothnessOf(string key) => key switch
        {
            "Glass" => 0.92f,
            "Road" => 0.2f,
            "CarRed" or "CarBlue" or "CarGreen" or "CarBeige" or "CarWhite" or "CarBlack" or "BusYellow" or "TractorGreen" => 0.5f,
            "Metal" or "MetalLight" or "MetalSiding" => 0.35f,
            _ => 0.1f,
        };

        static Material Mat(string key)
        {
            if (Materials.TryGetValue(key, out var cached)) return cached;

            var path = $"{ArtDir}/M_Proto_{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", Palette[key]);
            mat.SetFloat("_Smoothness", SmoothnessOf(key));
            mat.SetFloat("_Metallic", key.StartsWith("Metal") ? 0.4f : 0f);
            if (Glow.TryGetValue(key, out var glow))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Palette[key] * glow);
                // URP's material validation keeps _EMISSION only on materials flagged as emissive for GI.
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (key == "Grass")
            {
                // Terrain UVs are world XZ / 10: one tile = 10 m, one cell = 1 m. Lines stay faint on grass.
                mat.SetTexture("_BaseMap", GridTexture("T_Proto_GrassGrid", 226, 216, 202));
            }
            if (key == "TestPad")
            {
                mat.SetTexture("_BaseMap", GridTexture("T_Proto_Grid", 205, 150, 70));
                mat.SetTextureScale("_BaseMap", TestPadSize / 10f);
            }
            EditorUtility.SetDirty(mat);
            Materials[key] = mat;
            UsedMaterials.Add(path);
            return mat;
        }

        static Texture2D GridTexture(string name, byte cell, byte minor, byte major)
        {
            // Rewritten every build (same bytes, same GUID) so tweaking the values here takes effect.
            var path = $"{ArtDir}/{name}.png";
            const int size = 512;
            const int cells = 10;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var isMajor = x < 3 || y < 3 || x >= size - 3 || y >= size - 3;
                var isMinor = x % (size / cells) < 1 || y % (size / cells) < 1;
                var v = isMajor ? major : isMinor ? minor : cell;
                px[y * size + x] = new Color32(v, v, v, 255);
            }
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void DeleteUnusedMaterials()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { ArtDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!UsedMaterials.Contains(path)) AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
