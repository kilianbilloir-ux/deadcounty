using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    // Reads the GLB files aiblender exports (Tools/aiblender): one node per part at the identity, triangles with
    // positions, normals and UVs into a small PNG of flat colours. Gives back a triangle soup with one colour per
    // vertex, in Unity axes, cut into runs by material name. Anything else a glTF may hold is refused rather than
    // guessed.
    public static class GlbReader
    {
        public sealed class Model
        {
            public readonly List<Vector3> Positions = new();
            public readonly List<Vector3> Normals = new();
            public readonly List<Color32> Colors = new();
            // Three per triangle, wound for Unity.
            public readonly List<int> Indices = new();
            // Runs of Indices, one per part, with the name of its material.
            public readonly List<(string Material, int Start, int Count)> Parts = new();
        }

        const uint Magic = 0x46546C67;
        const uint JsonChunk = 0x4E4F534A;
        const uint BinChunk = 0x004E4942;
        const int Float = 5126;

        public static Model Read(byte[] glb)
        {
            if (glb.Length < 20 || BitConverter.ToUInt32(glb, 0) != Magic) throw new FormatException("not a GLB file");
            var jsonLength = (int)BitConverter.ToUInt32(glb, 12);
            if (BitConverter.ToUInt32(glb, 16) != JsonChunk) throw new FormatException("the first chunk is not JSON");
            var root = (Dictionary<string, object>)new Json(Encoding.UTF8.GetString(glb, 20, jsonLength)).Value();
            var binStart = 20 + jsonLength;
            if (BitConverter.ToUInt32(glb, binStart + 4) != BinChunk) throw new FormatException("the second chunk is not binary");
            var file = new File(root, glb, binStart + 8);

            var model = new Model();
            foreach (var node in List(root, "nodes"))
            {
                var n = (Dictionary<string, object>)node;
                foreach (var key in new[] { "children", "translation", "rotation", "scale", "matrix" })
                    if (n.ContainsKey(key)) throw new FormatException($"node '{n["name"]}' has a {key}: only flat scenes are read");
                if (!n.TryGetValue("mesh", out var mesh)) continue;
                foreach (var primitive in List((Dictionary<string, object>)List(root, "meshes")[Int(mesh)], "primitives"))
                    file.Add((Dictionary<string, object>)primitive, model);
            }
            return model;
        }

        sealed class File
        {
            readonly Dictionary<string, object> root;
            readonly byte[] bytes;
            readonly int bin;
            readonly Dictionary<int, Color32[]> images = new();
            readonly Dictionary<int, int> imageWidths = new();

            public File(Dictionary<string, object> root, byte[] bytes, int bin)
            {
                this.root = root;
                this.bytes = bytes;
                this.bin = bin;
            }

            public void Add(Dictionary<string, object> primitive, Model model)
            {
                if (primitive.TryGetValue("mode", out var mode) && Int(mode) != 4) throw new FormatException("only triangle lists are read");
                var attributes = (Dictionary<string, object>)primitive["attributes"];
                var positions = Floats(Int(attributes["POSITION"]), 3);
                var normals = Floats(Int(attributes["NORMAL"]), 3);
                var uvs = attributes.TryGetValue("TEXCOORD_0", out var uv) ? Floats(Int(uv), 2) : null;
                var material = (Dictionary<string, object>)List(root, "materials")[Int(primitive["material"])];
                var pbr = (Dictionary<string, object>)material["pbrMetallicRoughness"];
                var factor = pbr.TryGetValue("baseColorFactor", out var f) ? (List<object>)f : null;
                var tint = factor == null ? Color.white : new Color(Number(factor[0]), Number(factor[1]), Number(factor[2])).gamma;
                Color32[] pixels = null;
                var width = 0;
                if (pbr.TryGetValue("baseColorTexture", out var texture))
                {
                    if (uvs == null) throw new FormatException($"material '{material["name"]}' has a texture but its mesh has no UVs");
                    var source = Int(((Dictionary<string, object>)List(root, "textures")[Int(((Dictionary<string, object>)texture)["index"])])["source"]);
                    pixels = Image(source);
                    width = imageWidths[source];
                }

                var first = model.Positions.Count;
                var count = positions.Length / 3;
                for (var i = 0; i < count; i++)
                {
                    // glTF is right-handed with +Z toward the viewer: negating Z gives Unity's axes, and what the
                    // model calls its back (+Y in aiblender) lies along Unity's +Z.
                    model.Positions.Add(new Vector3(positions[i * 3], positions[i * 3 + 1], -positions[i * 3 + 2]));
                    model.Normals.Add(new Vector3(normals[i * 3], normals[i * 3 + 1], -normals[i * 3 + 2]));
                    Color color = pixels == null ? Color.white : Texel(pixels, width, uvs[i * 2], uvs[i * 2 + 1]);
                    model.Colors.Add(color * tint);
                }
                var indices = Indices(Int(primitive["indices"]));
                model.Parts.Add(((string)material["name"], model.Indices.Count, indices.Length));
                for (var i = 0; i < indices.Length; i += 3)
                {
                    model.Indices.Add(first + indices[i]);
                    model.Indices.Add(first + indices[i + 2]);
                    model.Indices.Add(first + indices[i + 1]);
                }
            }

            // glTF UVs start at the top left corner, Unity's pixel rows at the bottom.
            static Color32 Texel(Color32[] pixels, int width, float u, float v)
            {
                var height = pixels.Length / width;
                var x = Mathf.Clamp((int)(u * width), 0, width - 1);
                var y = Mathf.Clamp((int)((1f - v) * height), 0, height - 1);
                return pixels[y * width + x];
            }

            Color32[] Image(int index)
            {
                if (images.TryGetValue(index, out var cached)) return cached;
                var image = (Dictionary<string, object>)List(root, "images")[index];
                var (offset, length) = View(Int(image["bufferView"]));
                var png = new byte[length];
                Buffer.BlockCopy(bytes, offset, png, 0, length);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!texture.LoadImage(png)) throw new FormatException($"image {index} is not a PNG");
                    imageWidths[index] = texture.width;
                    return images[index] = texture.GetPixels32();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            (int offset, int length) View(int index)
            {
                var view = (Dictionary<string, object>)List(root, "bufferViews")[index];
                if (view.ContainsKey("byteStride")) throw new FormatException("interleaved buffers are not read");
                return (bin + (view.TryGetValue("byteOffset", out var o) ? Int(o) : 0), Int(view["byteLength"]));
            }

            (int offset, int count, int componentType) Accessor(int index, string type)
            {
                var accessor = (Dictionary<string, object>)List(root, "accessors")[index];
                if ((string)accessor["type"] != type) throw new FormatException($"accessor {index} is {accessor["type"]}, expected {type}");
                var (offset, _) = View(Int(accessor["bufferView"]));
                return (offset + (accessor.TryGetValue("byteOffset", out var o) ? Int(o) : 0), Int(accessor["count"]), Int(accessor["componentType"]));
            }

            float[] Floats(int index, int width)
            {
                var (offset, count, componentType) = Accessor(index, width == 3 ? "VEC3" : "VEC2");
                if (componentType != Float) throw new FormatException($"accessor {index} is not made of floats");
                var values = new float[count * width];
                Buffer.BlockCopy(bytes, offset, values, 0, values.Length * 4);
                return values;
            }

            int[] Indices(int index)
            {
                var (offset, count, componentType) = Accessor(index, "SCALAR");
                var values = new int[count];
                for (var i = 0; i < count; i++)
                    values[i] = componentType switch
                    {
                        5125 => (int)BitConverter.ToUInt32(bytes, offset + i * 4),
                        5123 => BitConverter.ToUInt16(bytes, offset + i * 2),
                        5121 => bytes[offset + i],
                        _ => throw new FormatException($"index type {componentType}"),
                    };
                return values;
            }
        }

        static List<object> List(Dictionary<string, object> o, string key) => (List<object>)o[key];
        static int Int(object value) => (int)(double)value;
        static float Number(object value) => (float)(double)value;

        // The subset of JSON a glTF header uses: objects, arrays, strings, numbers, true, false, null.
        sealed class Json
        {
            readonly string s;
            int i;

            public Json(string text) => s = text;

            public object Value()
            {
                Skip();
                switch (s[i])
                {
                    case '{':
                    {
                        var o = new Dictionary<string, object>();
                        i++;
                        for (Skip(); s[i] != '}'; Skip())
                        {
                            if (s[i] == ',') { i++; Skip(); }
                            var key = String();
                            Skip();
                            i++;
                            o[key] = Value();
                        }
                        i++;
                        return o;
                    }
                    case '[':
                    {
                        var list = new List<object>();
                        i++;
                        for (Skip(); s[i] != ']'; Skip())
                        {
                            if (s[i] == ',') { i++; continue; }
                            list.Add(Value());
                        }
                        i++;
                        return list;
                    }
                    case '"': return String();
                    case 't': i += 4; return true;
                    case 'f': i += 5; return false;
                    case 'n': i += 4; return null;
                    default:
                    {
                        var start = i;
                        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                        if (i == start) throw new FormatException($"unexpected '{s[i]}' at {i} in the glTF header");
                        return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
                    }
                }
            }

            string String()
            {
                var b = new StringBuilder();
                for (i++; s[i] != '"'; i++)
                {
                    if (s[i] != '\\') { b.Append(s[i]); continue; }
                    i++;
                    switch (s[i])
                    {
                        case 'n': b.Append('\n'); break;
                        case 't': b.Append('\t'); break;
                        case 'u': b.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; break;
                        default: b.Append(s[i]); break;
                    }
                }
                i++;
                return b.ToString();
            }

            void Skip()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }
        }
    }
}
