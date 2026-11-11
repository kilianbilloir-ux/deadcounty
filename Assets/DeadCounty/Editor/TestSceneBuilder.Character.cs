using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        const string BodyMeshPrefix = "SM_Char_";

        // Material keys of a body; zombies drop the backpack and get blood stains.
        readonly struct BodyLook
        {
            public readonly string Skin, Hair, Eye, Top, TopDark, Pants, Boots;
            public readonly bool Survivor;

            public BodyLook(string skin, string hair, string eye, string top, string topDark, string pants, string boots, bool survivor)
            {
                Skin = skin;
                Hair = hair;
                Eye = eye;
                Top = top;
                TopDark = topDark;
                Pants = pants;
                Boots = boots;
                Survivor = survivor;
            }
        }

        // One cross-section of a limb: X width and Z depth at height Y, centred on (X, Z).
        readonly struct Ring
        {
            public readonly float Y, Width, Depth, Z, X;

            public Ring(float y, float width, float depth, float z, float x)
            {
                Y = y;
                Width = width;
                Depth = depth;
                Z = z;
                X = x;
            }
        }

        static readonly BodyLook SurvivorLook = new("Skin", "Hair", "Eye", "Jacket", "JacketDark", "Jeans", "Boots", true);
        static readonly BodyLook ZombieLook = new("ZombieSkin", "ZombieHair", "ZombieEye", "ZombieShirt", "ZombieShirt", "ZombiePants", "Boots", false);

        static Ring R(float y, float width, float depth, float z = 0f, float x = 0f) => new(y, width, depth, z, x);

        static Dictionary<string, Transform> BuildSurvivor(Transform player) => BuildHumanoid(player, SurvivorLook);

        // Segmented low poly human (1.8 m, legs 0.95 m i.e. 53 % of the height like a real adult): rigid
        // flat-shaded parts parented to joints that ProceduralHumanoidAnimator rotates. Returns the joints keyed
        // by the animator's field names.
        static Dictionary<string, Transform> BuildHumanoid(Transform body, BodyLook look)
        {
            var joints = new Dictionary<string, Transform>();
            var model = Group(body, "Model");
            var hips = joints["hips"] = Group(model, "Hanches", new Vector3(0f, 1f, 0f));
            var spine = joints["spine"] = Group(hips, "Colonne", new Vector3(0f, 0.06f, 0f));
            var neck = joints["neck"] = Group(spine, "Cou", new Vector3(0f, 0.42f, 0f));
            Group(neck, "Tete", new Vector3(0f, 0.07f, 0f));
            foreach (var side in new[] { -1f, 1f })
            {
                var left = side < 0f;
                var shoulder = joints[left ? "leftShoulder" : "rightShoulder"] = Group(spine, left ? "Epaule_G" : "Epaule_D", new Vector3(side * 0.24f, 0.36f, 0f));
                joints[left ? "leftElbow" : "rightElbow"] = Group(shoulder, left ? "Coude_G" : "Coude_D", new Vector3(0f, -0.28f, 0f));
                var hip = joints[left ? "leftHip" : "rightHip"] = Group(hips, left ? "Hanche_G" : "Hanche_D", new Vector3(side * 0.095f, -0.05f, 0f));
                var knee = joints[left ? "leftKnee" : "rightKnee"] = Group(hip, left ? "Genou_G" : "Genou_D", new Vector3(0f, -0.44f, 0f));
                joints[left ? "leftAnkle" : "rightAnkle"] = Group(knee, left ? "Cheville_G" : "Cheville_D", new Vector3(0f, -0.43f, 0f));
            }
            SyncBody(model, look);
            return joints;
        }

        // Brings the parts of a body in line with BodyParts: prefabs are never regenerated, so older ones get the
        // current shapes in place and lose the parts that no longer exist. True when the hierarchy changed.
        static bool SyncBody(Transform model, BodyLook look)
        {
            var changed = false;
            var current = new HashSet<Transform>();
            foreach (var (jointPath, name, mesh, mat) in BodyParts(look))
            {
                var joint = model.Find(jointPath) ?? throw new System.InvalidOperationException($"{model.name} has no joint {jointPath}");
                var part = joint.Find(name);
                if (part == null)
                {
                    part = MeshObject(name, joint, mesh, Vector3.zero, Quaternion.identity, Vector3.one, new[] { mat }, false).transform;
                    part.gameObject.layer = joint.gameObject.layer;
                    changed = true;
                }
                else
                {
                    var filter = part.GetComponent<MeshFilter>();
                    var renderer = part.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh != mesh || renderer.sharedMaterial != Mat(mat) || part.localPosition != Vector3.zero)
                    {
                        filter.sharedMesh = mesh;
                        renderer.sharedMaterial = Mat(mat);
                        part.localPosition = Vector3.zero;
                        changed = true;
                    }
                }
                current.Add(part);
            }
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (current.Contains(filter.transform)) continue;
                if (filter.sharedMesh != null && !filter.sharedMesh.name.StartsWith(BodyMeshPrefix)) continue;
                Object.DestroyImmediate(filter.gameObject);
                changed = true;
            }
            return changed;
        }

        static Mesh Loft(string name, params Ring[] rings) => Loft(name, 0.3f, Quaternion.identity, Vector3.zero, rings);

        // Octagonal sections joined from the first ring to the last and capped at both ends: a limb that swells
        // and tapers while staying flat-shaded. `chamfer` is the share of the narrow side cut off each corner.
        static Mesh Loft(string name, float chamfer, Quaternion rotation, Vector3 offset, Ring[] rings) => Cached(BodyMeshPrefix + name, () =>
        {
            var m = new MeshBuilder().Sub();
            var pose = Matrix4x4.TRS(offset, rotation, Vector3.one);
            Vector3[] Section(Ring r)
            {
                var hx = r.Width / 2f;
                var hz = r.Depth / 2f;
                var c = chamfer * Mathf.Min(r.Width, r.Depth);
                return new[]
                {
                    new Vector3(-hx + c, 0f, -hz), new Vector3(hx - c, 0f, -hz), new Vector3(hx, 0f, -hz + c), new Vector3(hx, 0f, hz - c),
                    new Vector3(hx - c, 0f, hz), new Vector3(-hx + c, 0f, hz), new Vector3(-hx, 0f, hz - c), new Vector3(-hx, 0f, -hz + c),
                }.Select(v => pose.MultiplyPoint3x4(v + new Vector3(r.X, r.Y, r.Z))).ToArray();
            }
            static Vector3 Centre(Vector3[] section) => section.Aggregate(Vector3.zero, (sum, v) => sum + v) / section.Length;

            var sections = rings.Select(Section).ToArray();
            for (var s = 0; s < sections.Length - 1; s++)
            {
                var b = sections[s];
                var t = sections[s + 1];
                var centre = (Centre(b) + Centre(t)) / 2f;
                for (var i = 0; i < 8; i++)
                {
                    var j = (i + 1) % 8;
                    m.Quad(b[i], b[j], t[j], t[i], (b[i] + b[j] + t[i] + t[j]) / 4f - centre);
                }
            }
            var first = sections[0];
            var last = sections[^1];
            var axis = Centre(last) - Centre(first);
            for (var i = 1; i < 7; i++)
            {
                m.Tri(first[0], first[i], first[i + 1], -axis);
                m.Tri(last[0], last[i], last[i + 1], axis);
            }
            return m.Save(BodyMeshPrefix + name);
        });
    }
}
