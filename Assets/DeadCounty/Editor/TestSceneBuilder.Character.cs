using System.Collections.Generic;
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
            return joints;
        }

        static void Part(Transform joint, string name, Mesh mesh, string mat, Vector3 offset = default)
            => MeshObject(name, joint, mesh, offset, Quaternion.identity, Vector3.one, new[] { mat }, false);

        // Box tapering from `bottom` (X width, Z depth) at y0 to `top` at y1, centered on `offset` in XZ.
        static Mesh Frustum(string name, float y0, float y1, Vector2 bottom, Vector2 top, Vector2 offset = default) => Cached($"SM_Char_{name}", () =>
        {
            var m = new MeshBuilder().Sub();
            Vector3 Corner(float y, Vector2 size, float sx, float sz) => new(offset.x + sx * size.x / 2f, y, offset.y + sz * size.y / 2f);
            var b = new[] { Corner(y0, bottom, -1, -1), Corner(y0, bottom, 1, -1), Corner(y0, bottom, 1, 1), Corner(y0, bottom, -1, 1) };
            var t = new[] { Corner(y1, top, -1, -1), Corner(y1, top, 1, -1), Corner(y1, top, 1, 1), Corner(y1, top, -1, 1) };
            var center = new Vector3(offset.x, (y0 + y1) / 2f, offset.y);
            for (var i = 0; i < 4; i++)
            {
                var j = (i + 1) % 4;
                m.Quad(b[i], b[j], t[j], t[i], (b[i] + b[j] + t[i] + t[j]) / 4f - center);
            }
            m.Quad(t[0], t[1], t[2], t[3], Vector3.up);
            m.Quad(b[0], b[1], b[2], b[3], Vector3.down);
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
