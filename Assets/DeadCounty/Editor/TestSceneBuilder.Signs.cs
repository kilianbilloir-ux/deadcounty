using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    // Road signs and lettering: octagonal stop signs, speed limits, an evacuation route sign, ROAD CLOSED on the
    // roadblock, shop names and street names. Letters are blocks on a 5-row grid, one quad per run, so they stay
    // flat-shaded and sharp like the rest of the low poly set. None of it is solid.
    public static partial class TestSceneBuilder
    {
        // Rows from top to bottom, '#' lit.
        static readonly Dictionary<char, string> Glyphs = new()
        {
            ['A'] = ".#./#.#/###/#.#/#.#", ['C'] = ".##/#../#../#../.##", ['D'] = "##./#.#/#.#/#.#/##.", ['E'] = "###/#../##./#../###",
            ['F'] = "###/#../##./#../#..", ['G'] = ".##/#../#.#/#.#/.##", ['H'] = "#.#/#.#/###/#.#/#.#", ['I'] = "###/.#./.#./.#./###",
            ['K'] = "#.#/#.#/##./#.#/#.#", ['L'] = "#../#../#../#../###", ['M'] = "#...#/##.##/#.#.#/#...#/#...#",
            ['N'] = "#..#/##.#/#.##/#..#/#..#", ['O'] = ".#./#.#/#.#/#.#/.#.", ['P'] = "###/#.#/###/#../#..", ['R'] = "##./#.#/##./#.#/#.#",
            ['S'] = ".##/#../.#./..#/##.", ['T'] = "###/.#./.#./.#./.#.", ['U'] = "#.#/#.#/#.#/#.#/###", ['V'] = "#.#/#.#/#.#/#.#/.#.",
            ['Y'] = "#.#/#.#/.#./.#./.#.", ['0'] = "###/#.#/#.#/#.#/###", ['1'] = ".#./##./.#./.#./###", ['2'] = "##./..#/.#./#../###",
            ['3'] = "##./..#/.#./..#/##.", ['5'] = "###/#../##./..#/##.", ['9'] = "###/#.#/###/..#/##.",
            [' '] = "../../../../..", ['>'] = "..#../...#./#####/...#./..#..",
        };

        // Writes text into the current submesh: block letters of `cell` metres per pixel, facing -Z, reading along
        // +X, centred on `center`; '\n' starts a new line.
        static void AddText(MeshBuilder m, string text, float cell, Vector3 center)
        {
            var lines = text.Split('\n');
            var rows = lines.Length * 7 - 2;
            Vector3 P(float x, float y) => center + new Vector3(x, y, 0f) * cell;
            for (var l = 0; l < lines.Length; l++)
            {
                var glyphs = lines[l].Select(c => Glyphs[c].Split('/')).ToArray();
                var x = -(glyphs.Sum(g => g[0].Length + 1) - 1) / 2f;
                var top = rows / 2f - l * 7;
                foreach (var g in glyphs)
                {
                    for (var r = 0; r < 5; r++)
                    {
                        var start = -1;
                        for (var c = 0; c <= g[r].Length; c++)
                        {
                            var lit = c < g[r].Length && g[r][c] == '#';
                            if (lit && start < 0) start = c;
                            if (lit || start < 0) continue;
                            m.Quad(P(x + start, top - r - 1), P(x + c, top - r - 1), P(x + c, top - r), P(x + start, top - r), Vector3.back);
                            start = -1;
                        }
                    }
                    x += g[0].Length + 1;
                }
            }
        }

        static Mesh TextMesh(string text, float cell)
        {
            var name = $"SM_Texte_{new string(text.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray())}_{Mathf.RoundToInt(cell * 1000f)}";
            return Cached(name, () =>
            {
                var m = new MeshBuilder().Sub();
                AddText(m, text, cell, Vector3.zero);
                return m.Save(name);
            });
        }

        // Lettering centred on pos, readable from the side `facing` points to.
        static void Label(Transform parent, string text, Vector3 pos, Vector3 facing, float cell, string mat)
            => MeshObject("Inscription", parent, TextMesh(text, cell), pos, Quaternion.LookRotation(-facing), Vector3.one, new[] { mat }, false);

        // Sign plate centred on the origin, face towards -Z. Submeshes: face, border inset from the edge, edges and back.
        static Mesh PlateMesh(float width, float height, float border) => Cached($"SM_Plaque_{Cm(width)}x{Cm(height)}", () =>
        {
            const float thickness = 0.02f, inset = 0.012f;
            float hw = width / 2f, hh = height / 2f, front = -thickness / 2f;
            var m = new MeshBuilder().Sub();
            m.Quad(new Vector3(-hw, -hh, front), new Vector3(hw, -hh, front), new Vector3(hw, hh, front), new Vector3(-hw, hh, front), Vector3.back);
            m.Sub();
            float ow = hw - inset, oh = hh - inset, iw = ow - border, ih = oh - border, z = front - 0.005f;
            m.Quad(new Vector3(-ow, oh, z), new Vector3(ow, oh, z), new Vector3(ow, ih, z), new Vector3(-ow, ih, z), Vector3.back);
            m.Quad(new Vector3(-ow, -ih, z), new Vector3(ow, -ih, z), new Vector3(ow, -oh, z), new Vector3(-ow, -oh, z), Vector3.back);
            m.Quad(new Vector3(-ow, ih, z), new Vector3(-iw, ih, z), new Vector3(-iw, -ih, z), new Vector3(-ow, -ih, z), Vector3.back);
            m.Quad(new Vector3(iw, ih, z), new Vector3(ow, ih, z), new Vector3(ow, -ih, z), new Vector3(iw, -ih, z), Vector3.back);
            m.Sub();
            AddCuboid(m, new Vector3(0f, 0f, 0.003f), new Vector3(width, height, thickness - 0.006f), Quaternion.identity);
            return m.Save($"SM_Plaque_{Cm(width)}x{Cm(height)}");
        });

        // Stop sign plate: white octagon with a red face inset, metal back; faces -Z.
        static Mesh OctagonMesh() => Cached("SM_Octogone_Stop", () =>
        {
            const float thickness = 0.025f;
            var m = new MeshBuilder();
            Vector3 Corner(int i, float radius, float z) => new(Mathf.Cos((22.5f + i * 45f) * Mathf.Deg2Rad) * radius, Mathf.Sin((22.5f + i * 45f) * Mathf.Deg2Rad) * radius, z);
            void Face(float radius, float z, Vector3 outward)
            {
                for (var i = 0; i < 8; i++) m.Tri(new Vector3(0f, 0f, z), Corner(i, radius, z), Corner(i + 1, radius, z), outward);
            }
            m.Sub();
            Face(0.4f, -thickness / 2f, Vector3.back);
            m.Sub();
            Face(0.36f, -thickness / 2f - 0.005f, Vector3.back);
            m.Sub();
            Face(0.4f, thickness / 2f, Vector3.forward);
            for (var i = 0; i < 8; i++)
                m.Quad(Corner(i, 0.4f, -thickness / 2f), Corner(i + 1, 0.4f, -thickness / 2f), Corner(i + 1, 0.4f, thickness / 2f), Corner(i, 0.4f, thickness / 2f),
                    Corner(i, 1f, 0f) + Corner(i + 1, 1f, 0f));
            return m.Save("SM_Octogone_Stop");
        });

        // Post with its sign facing yaw (0 = +Z); in the returned frame the face side is -Z, like the plates.
        static Transform SignPost(Transform parent, string name, Vector3 basePos, float yaw, float height)
        {
            var sign = Group(parent, name, basePos, yaw + 180f);
            Post("Poteau", sign, Vector3.zero, height, 0.04f, "Metal", solid: false);
            return sign;
        }

        static void StopSign(Transform parent, Vector3 basePos, float yaw)
        {
            var sign = SignPost(parent, "Panneau_Stop", basePos, yaw, 2.3f);
            MeshObject("Octogone", sign, OctagonMesh(), new Vector3(0f, 2.15f, -0.06f), Quaternion.identity, Vector3.one, new[] { "SignWhite", "SignRed", "MetalLight" }, false);
            Label(sign, "STOP", new Vector3(0f, 2.15f, -0.083f), Vector3.back, 0.034f, "SignWhite");
        }

        static void SpeedLimitSign(Transform parent, Vector3 basePos, float yaw, string limit)
        {
            var sign = SignPost(parent, "Panneau_Vitesse", basePos, yaw, 2.35f);
            MeshObject("Plaque", sign, PlateMesh(0.46f, 0.6f, 0.012f), new Vector3(0f, 2f, -0.05f), Quaternion.identity, Vector3.one, new[] { "SignWhite", "CarBlack", "MetalLight" }, false);
            Label(sign, "SPEED\nLIMIT", new Vector3(0f, 2.158f, -0.066f), Vector3.back, 0.017f, "CarBlack");
            Label(sign, limit, new Vector3(0f, 1.853f, -0.066f), Vector3.back, 0.045f, "CarBlack");
        }

        static void EvacuationSign(Transform parent, Vector3 basePos, float yaw)
        {
            var sign = SignPost(parent, "Panneau_Evacuation", basePos, yaw, 2.5f);
            MeshObject("Plaque", sign, PlateMesh(0.6f, 0.78f, 0.015f), new Vector3(0f, 2.08f, -0.05f), Quaternion.identity, Vector3.one, new[] { "SignBlue", "SignWhite", "MetalLight" }, false);
            Label(sign, "EVAC\nROUTE\n>", new Vector3(0f, 2.08f, -0.066f), Vector3.back, 0.026f, "SignWhite");
        }

        // Sign wired on top of a sawhorse bar (pos on the ground under the bar), facing yaw.
        static void RoadClosedSign(Transform parent, Vector3 pos, float yaw)
        {
            var sign = Group(parent, "Panneau_Route_Barree", pos, yaw + 180f);
            foreach (var x in new[] { -0.55f, 0.55f })
                Box("Piquet", sign, new Vector3(x, 1.15f, 0.025f), new Vector3(0.04f, 0.6f, 0.03f), "Wood", solid: false);
            MeshObject("Plaque", sign, PlateMesh(1.5f, 0.42f, 0.014f), new Vector3(0f, 1.28f, 0f), Quaternion.identity, Vector3.one, new[] { "SignWhite", "CarBlack", "MetalLight" }, false);
            Label(sign, "ROAD CLOSED", new Vector3(0f, 1.28f, -0.016f), Vector3.back, 0.03f, "CarBlack");
        }
    }
}
