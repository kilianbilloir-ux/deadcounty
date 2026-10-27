using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        // Material keys of a body; zombies drop the backpack and get a blood stain.
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

        static readonly BodyLook SurvivorLook = new("Skin", "Hair", "Eye", "Jacket", "JacketDark", "Jeans", "Boots", true);
        static readonly BodyLook ZombieLook = new("ZombieSkin", "ZombieHair", "ZombieEye", "ZombieShirt", "ZombieShirt", "ZombiePants", "Boots", false);

        static Dictionary<string, Transform> BuildSurvivor(Transform player) => BuildHumanoid(player, SurvivorLook);

        // Segmented low poly human (1.8 m, legs 0.95 m i.e. 53 % of the height like a real adult): rigid
        // flat-shaded parts parented to joints that ProceduralHumanoidAnimator rotates. Returns the joints keyed
        // by the animator's field names.
        static Dictionary<string, Transform> BuildHumanoid(Transform body, BodyLook look)
        {
            var joints = new Dictionary<string, Transform>();
            var model = Group(body, "Model");

            var hips = joints["hips"] = Group(model, "Hanches", new Vector3(0f, 1f, 0f));
            Part(hips, "Bassin", Frustum("Pelvis", -0.1f, 0.1f, new(0.33f, 0.2f), new(0.35f, 0.21f)), look.Pants);
            Part(hips, "Ceinture", Frustum("Belt", 0.06f, 0.11f, new(0.358f, 0.218f), new(0.358f, 0.218f)), "Strap");

            var spine = joints["spine"] = Group(hips, "Colonne", new Vector3(0f, 0.06f, 0f));
            Part(spine, "Torse", Frustum("Torso", 0f, 0.42f, new(0.35f, 0.21f), new(0.44f, 0.25f)), look.Top);
            Part(spine, "Col", Frustum("Collar", 0.38f, 0.46f, new(0.26f, 0.2f), new(0.2f, 0.16f)), look.TopDark);
            if (look.Survivor)
            {
                Part(spine, "Sac_A_Dos", Frustum("Backpack", 0.06f, 0.4f, new(0.32f, 0.16f), new(0.3f, 0.14f), new(0f, -0.2f)), "Backpack");
                Part(spine, "Rabat", Frustum("BackpackFlap", 0.3f, 0.42f, new(0.31f, 0.17f), new(0.28f, 0.15f), new(0f, -0.2f)), "BackpackDark");
                foreach (var x in new[] { -0.1f, 0.1f })
                    Part(spine, "Bretelle", Frustum("Strap", 0.05f, 0.43f, new(0.05f, 0.02f), new(0.05f, 0.02f), new(0f, 0.12f)), "Strap", new Vector3(x, 0f, 0f));
            }
            else
            {
                Part(spine, "Sang", Frustum("BloodStain", 0.15f, 0.26f, new(0.12f, 0.01f), new(0.08f, 0.01f), new(0.05f, 0.12f)), "Blood");
            }

            var neck = joints["neck"] = Group(spine, "Cou", new Vector3(0f, 0.42f, 0f));
            Part(neck, "Cou", Frustum("Neck", 0f, 0.09f, new(0.11f, 0.11f), new(0.1f, 0.1f)), look.Skin);
            var head = Group(neck, "Tete", new Vector3(0f, 0.07f, 0f));
            Part(head, "Visage", Blob("Head", new Vector3(0f, 0.11f, 0.01f), new Vector3(0.105f, 0.125f, 0.115f)), look.Skin);
            Part(head, "Cheveux", Blob("Hair", new Vector3(0f, 0.165f, -0.03f), new Vector3(0.116f, 0.095f, 0.12f)), look.Hair);
            Part(head, "Nez", Frustum("Nose", 0.04f, 0.09f, new(0.035f, 0.03f), new(0.025f, 0.02f), new(0f, 0.12f)), look.Skin);
            foreach (var x in new[] { -0.04f, 0.04f })
                Part(head, "Oeil", Frustum("Eye", 0.1f, 0.12f, new(0.03f, 0.01f), new(0.03f, 0.01f), new(0f, 0.122f)), look.Eye, new Vector3(x, 0f, 0f));

            foreach (var side in new[] { -1f, 1f })
            {
                var left = side < 0f;
                var shoulder = joints[left ? "leftShoulder" : "rightShoulder"] = Group(spine, left ? "Epaule_G" : "Epaule_D", new Vector3(side * 0.24f, 0.36f, 0f));
                Part(shoulder, "Bras", Frustum("UpperArm", -0.28f, 0.03f, new(0.09f, 0.1f), new(0.12f, 0.13f)), look.Top);
                var elbow = joints[left ? "leftElbow" : "rightElbow"] = Group(shoulder, left ? "Coude_G" : "Coude_D", new Vector3(0f, -0.28f, 0f));
                Part(elbow, "Avant_Bras", Frustum("Forearm", -0.25f, 0f, new(0.075f, 0.08f), new(0.09f, 0.1f)), look.Top);
                Part(elbow, "Main", Frustum("Hand", -0.34f, -0.24f, new(0.05f, 0.08f), new(0.065f, 0.075f)), look.Skin);

                var hip = joints[left ? "leftHip" : "rightHip"] = Group(hips, left ? "Hanche_G" : "Hanche_D", new Vector3(side * 0.095f, -0.05f, 0f));
                Part(hip, "Cuisse", Frustum("Thigh", -0.44f, 0.04f, new(0.12f, 0.13f), new(0.16f, 0.17f)), look.Pants);
                var knee = joints[left ? "leftKnee" : "rightKnee"] = Group(hip, left ? "Genou_G" : "Genou_D", new Vector3(0f, -0.44f, 0f));
                Part(knee, "Tibia", Frustum("Shin", -0.41f, 0.02f, new(0.1f, 0.11f), new(0.12f, 0.13f)), look.Pants);
                var ankle = joints[left ? "leftAnkle" : "rightAnkle"] = Group(knee, left ? "Cheville_G" : "Cheville_D", new Vector3(0f, -0.43f, 0f));
                Part(ankle, "Botte", Frustum("Boot", -0.08f, 0.05f, new(0.12f, 0.27f), new(0.11f, 0.15f), new(0f, 0.04f)), look.Boots);
            }
            AddBodyDetails(model, look);
            return joints;
        }

        // Thumbs and ears on every body; a zipped jacket, thigh pockets and eyebrows on the survivor; blood
        // around a zombie's mouth. Adds only what is missing, so bodies saved in older prefabs get them too.
        // Returns whether anything was added.
        static bool AddBodyDetails(Transform model, BodyLook look)
        {
            var added = false;
            void Detail(string jointPath, string name, Mesh mesh, string mat, Vector3 offset = default)
            {
                var joint = model.Find(jointPath) ?? throw new System.InvalidOperationException($"{model.name} has no joint {jointPath}");
                if (joint.Find(name) != null) return;
                var part = MeshObject(name, joint, mesh, offset, Quaternion.identity, Vector3.one, new[] { mat }, false);
                part.layer = joint.gameObject.layer;
                added = true;
            }

            const string spine = "Hanches/Colonne";
            const string head = spine + "/Cou/Tete";
            foreach (var side in new[] { -1f, 1f })
            {
                var left = side < 0f;
                var elbow = $"{spine}/{(left ? "Epaule_G/Coude_G" : "Epaule_D/Coude_D")}";
                // On the inner front edge of the hand, toward the body.
                Detail(elbow, "Pouce", Frustum(left ? "ThumbL" : "ThumbR", -0.31f, -0.25f, new(0.025f, 0.03f), new(0.03f, 0.035f), new(-side * 0.025f, 0.035f)), look.Skin);
                Detail(head, left ? "Oreille_G" : "Oreille_D", Blob(left ? "EarL" : "EarR", new Vector3(side * 0.103f, 0.115f, 0f), new Vector3(0.02f, 0.038f, 0.03f)), look.Skin);
                if (!look.Survivor) continue;
                Detail(head, left ? "Sourcil_G" : "Sourcil_D", Frustum("Brow", 0.138f, 0.15f, new(0.045f, 0.012f), new(0.04f, 0.01f), new(0f, 0.122f)), look.Hair, new Vector3(side * 0.042f, 0f, 0f));
                Detail($"Hanches/{(left ? "Hanche_G" : "Hanche_D")}", "Poche", Frustum(left ? "PocketL" : "PocketR", -0.22f, -0.08f, new(0.02f, 0.11f), new(0.02f, 0.1f), new(side * 0.074f, 0f)), "JeansDark");
            }
            if (look.Survivor)
            {
                Detail(spine, "Fermeture", Frustum("Zip", 0.02f, 0.4f, new(0.014f, 0.012f), new(0.014f, 0.012f), new(0f, 0.118f)), look.TopDark);
                Detail(spine, "Ourlet", Frustum("JacketHem", -0.01f, 0.05f, new(0.37f, 0.225f), new(0.37f, 0.225f)), look.TopDark);
            }
            else
            {
                Detail(head, "Sang_Bouche", Frustum("MouthBlood", 0.025f, 0.065f, new(0.06f, 0.012f), new(0.05f, 0.012f), new(0f, 0.118f)), "Blood");
            }
            return added;
        }

        static void Part(Transform joint, string name, Mesh mesh, string mat, Vector3 offset = default)
            => MeshObject(name, joint, mesh, offset, Quaternion.identity, Vector3.one, new[] { mat }, false);

        // Octagonal prism tapering from `bottom` (X width, Z depth) at y0 to `top` at y1, centred on `offset` in XZ:
        // a box with its vertical edges cut, which reads as a rounded limb while staying flat-shaded low poly.
        static Mesh Frustum(string name, float y0, float y1, Vector2 bottom, Vector2 top, Vector2 offset = default) => Cached($"SM_Char_{name}", () =>
        {
            const float chamfer = 0.3f;
            var m = new MeshBuilder().Sub();
            Vector3[] Ring(float y, Vector2 size)
            {
                var hx = size.x / 2f;
                var hz = size.y / 2f;
                var c = chamfer * Mathf.Min(size.x, size.y);
                return new[]
                {
                    new Vector3(-hx + c, y, -hz), new Vector3(hx - c, y, -hz), new Vector3(hx, y, -hz + c), new Vector3(hx, y, hz - c),
                    new Vector3(hx - c, y, hz), new Vector3(-hx + c, y, hz), new Vector3(-hx, y, hz - c), new Vector3(-hx, y, -hz + c),
                }.Select(v => v + new Vector3(offset.x, 0f, offset.y)).ToArray();
            }
            var b = Ring(y0, bottom);
            var t = Ring(y1, top);
            var center = new Vector3(offset.x, (y0 + y1) / 2f, offset.y);
            for (var i = 0; i < 8; i++)
            {
                var j = (i + 1) % 8;
                m.Quad(b[i], b[j], t[j], t[i], (b[i] + b[j] + t[i] + t[j]) / 4f - center);
            }
            for (var i = 1; i < 7; i++)
            {
                m.Tri(t[0], t[i], t[i + 1], Vector3.up);
                m.Tri(b[0], b[i], b[i + 1], Vector3.down);
            }
            return m.Save($"SM_Char_{name}");
        });

        static Mesh Blob(string name, Vector3 center, Vector3 radii) => Cached($"SM_Char_{name}", () =>
        {
            var m = new MeshBuilder().Sub();
            AddBlob(m, center, radii, 1, new System.Random(0), 0f);
            return m.Save($"SM_Char_{name}");
        });
    }
}
