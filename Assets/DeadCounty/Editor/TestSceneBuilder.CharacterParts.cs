using System.Collections.Generic;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        const string HipsPath = "Hanches";
        const string SpinePath = HipsPath + "/Colonne";
        const string HeadPath = SpinePath + "/Cou/Tete";
        // The head's corners are cut less than a limb's so the face keeps a flat front for the eyes.
        const float HeadChamfer = 0.22f;

        // Every visible part of a body: joint path from the model, part name, mesh in the joint's frame, material
        // key. Rings run from the bottom of a part to its top. Limbs are thicker than a real adult's and the head
        // larger, as the stylised references are.
        static IEnumerable<(string joint, string name, Mesh mesh, string mat)> BodyParts(BodyLook look)
        {
            yield return (HipsPath, "Bassin", Loft("Pelvis", R(-0.15f, 0.22f, 0.17f), R(-0.08f, 0.36f, 0.23f), R(0.02f, 0.37f, 0.235f), R(0.1f, 0.34f, 0.22f)), look.Pants);
            yield return (HipsPath, "Ceinture", Loft("Belt", R(0.045f, 0.372f, 0.24f), R(0.095f, 0.36f, 0.232f)), "Strap");

            yield return (SpinePath, "Torse", Loft("Torso", R(0f, 0.345f, 0.225f), R(0.13f, 0.35f, 0.225f), R(0.29f, 0.43f, 0.265f, 0.005f), R(0.385f, 0.45f, 0.25f), R(0.44f, 0.27f, 0.19f)), look.Top);
            yield return (SpinePath, "Col", Loft("Collar", R(0.41f, 0.25f, 0.2f), R(0.47f, 0.19f, 0.17f, -0.01f)), look.TopDark);
            yield return (SpinePath + "/Cou", "Cou", Loft("Neck", R(-0.02f, 0.115f, 0.115f), R(0.09f, 0.1f, 0.105f)), look.Skin);
            foreach (var part in look.Survivor ? SurvivorGear(look) : ZombieStains(look)) yield return part;
            foreach (var part in HeadParts(look)) yield return part;

            foreach (var side in new[] { -1f, 1f })
            {
                var left = side < 0f;
                var shoulder = $"{SpinePath}/{(left ? "Epaule_G" : "Epaule_D")}";
                var elbow = $"{shoulder}/{(left ? "Coude_G" : "Coude_D")}";
                yield return (shoulder, "Bras", Loft("UpperArm", R(-0.29f, 0.1f, 0.11f), R(-0.13f, 0.115f, 0.125f), R(0f, 0.135f, 0.15f), R(0.045f, 0.1f, 0.11f)), look.Top);
                yield return (elbow, "Avant_Bras", Loft("Forearm", R(-0.25f, 0.08f, 0.085f), R(-0.07f, 0.105f, 0.115f), R(0.015f, 0.1f, 0.11f)), look.Top);
                yield return (elbow, "Main", Loft("Hand", R(-0.37f, 0.045f, 0.075f, 0.005f), R(-0.31f, 0.06f, 0.1f, 0.005f), R(-0.245f, 0.062f, 0.08f)), look.Skin);
                // On the inner front edge of the hand, toward the body.
                yield return (elbow, "Pouce", Loft(left ? "ThumbL" : "ThumbR", R(-0.335f, 0.026f, 0.03f, 0.048f, -side * 0.03f), R(-0.27f, 0.03f, 0.036f, 0.042f, -side * 0.028f)), look.Skin);

                var hip = $"{HipsPath}/{(left ? "Hanche_G" : "Hanche_D")}";
                var knee = $"{hip}/{(left ? "Genou_G" : "Genou_D")}";
                var ankle = $"{knee}/{(left ? "Cheville_G" : "Cheville_D")}";
                yield return (hip, "Cuisse", Loft("Thigh", R(-0.45f, 0.125f, 0.14f), R(-0.2f, 0.155f, 0.175f), R(0.04f, 0.17f, 0.19f)), look.Pants);
                yield return (knee, "Tibia", Loft("Shin", R(-0.37f, 0.1f, 0.11f), R(-0.14f, 0.125f, 0.15f, -0.012f), R(0.03f, 0.13f, 0.145f)), look.Pants);
                // The shaft rises over the trouser leg; the ankle joint is 8 cm above the ground.
                yield return (ankle, "Botte", Loft("Boot", R(-0.06f, 0.125f, 0.3f, 0.045f), R(-0.025f, 0.125f, 0.3f, 0.045f), R(0.02f, 0.115f, 0.17f, -0.01f), R(0.11f, 0.115f, 0.13f, -0.005f)), look.Boots);
                yield return (ankle, "Semelle", Loft("Sole", R(-0.08f, 0.135f, 0.31f, 0.045f), R(-0.055f, 0.135f, 0.31f, 0.045f)), "Strap");

                if (!look.Survivor) continue;
                yield return (elbow, "Manchette", Loft("Cuff", R(-0.255f, 0.092f, 0.097f), R(-0.215f, 0.097f, 0.102f)), look.TopDark);
                yield return (hip, "Poche", Loft(left ? "PocketL" : "PocketR", R(-0.22f, 0.02f, 0.085f, 0f, side * 0.074f), R(-0.08f, 0.02f, 0.085f, 0f, side * 0.08f)), "JeansDark");
            }
        }

        static IEnumerable<(string joint, string name, Mesh mesh, string mat)> HeadParts(BodyLook look)
        {
            Mesh Head(string name, params Ring[] rings) => Loft(name, HeadChamfer, Quaternion.identity, Vector3.zero, rings);

            yield return (HeadPath, "Visage", Head("Head", R(0f, 0.12f, 0.13f, 0.012f), R(0.045f, 0.17f, 0.19f, 0.008f), R(0.11f, 0.196f, 0.222f), R(0.185f, 0.2f, 0.225f), R(0.228f, 0.182f, 0.206f), R(0.252f, 0.12f, 0.14f)), look.Skin);
            // The cap starts inside the forehead, so the hairline shows no brim; the second part comes down the nape.
            yield return (HeadPath, "Cheveux", Head("Hair", R(0.186f, 0.214f, 0.236f, -0.014f), R(0.2f, 0.214f, 0.236f, -0.004f), R(0.235f, 0.196f, 0.218f, -0.006f), R(0.272f, 0.12f, 0.14f, -0.01f)), look.Hair);
            yield return (HeadPath, "Cheveux_Nuque", Head("HairBack", R(0.09f, 0.196f, 0.12f, -0.062f), R(0.19f, 0.208f, 0.16f, -0.045f)), look.Hair);
            yield return (HeadPath, "Nez", Loft("Nose", R(0.065f, 0.036f, 0.04f, 0.118f), R(0.125f, 0.024f, 0.02f, 0.112f)), look.Skin);
            foreach (var side in new[] { -1f, 1f })
            {
                var left = side < 0f;
                yield return (HeadPath, left ? "Oeil_G" : "Oeil_D", Loft(left ? "EyeL" : "EyeR", R(0.115f, 0.032f, 0.012f, 0.111f, side * 0.04f), R(0.137f, 0.032f, 0.012f, 0.111f, side * 0.04f)), look.Eye);
                yield return (HeadPath, left ? "Oreille_G" : "Oreille_D", Loft(left ? "EarL" : "EarR", R(0.085f, 0.02f, 0.035f, 0.01f, side * 0.103f), R(0.145f, 0.024f, 0.045f, 0.01f, side * 0.103f)), look.Skin);
                if (look.Survivor)
                    yield return (HeadPath, left ? "Sourcil_G" : "Sourcil_D", Loft(left ? "BrowL" : "BrowR", R(0.148f, 0.046f, 0.014f, 0.112f, side * 0.04f), R(0.16f, 0.042f, 0.012f, 0.112f, side * 0.04f)), look.Hair);
            }
            if (!look.Survivor)
                yield return (HeadPath, "Sang_Bouche", Loft("MouthBlood", R(0.015f, 0.06f, 0.014f, 0.087f), R(0.055f, 0.07f, 0.014f, 0.104f)), "Blood");
        }

        // Jacket skirt and zip, then the backpack with its bedroll and the straps that run over the shoulders.
        static IEnumerable<(string joint, string name, Mesh mesh, string mat)> SurvivorGear(BodyLook look)
        {
            yield return (SpinePath, "Veste_Bas", Loft("JacketSkirt", R(-0.05f, 0.39f, 0.255f), R(0.06f, 0.38f, 0.25f)), look.Top);
            yield return (SpinePath, "Ourlet", Loft("JacketHem", R(-0.06f, 0.396f, 0.261f), R(-0.035f, 0.394f, 0.259f)), look.TopDark);
            yield return (SpinePath, "Fermeture", Loft("Zip", R(-0.05f, 0.018f, 0.014f, 0.128f), R(0.06f, 0.018f, 0.014f, 0.126f), R(0.13f, 0.018f, 0.014f, 0.1135f),
                R(0.29f, 0.018f, 0.014f, 0.1385f), R(0.385f, 0.018f, 0.014f, 0.126f)), look.TopDark);

            yield return (SpinePath, "Sac_A_Dos", Loft("Backpack", R(0.02f, 0.3f, 0.15f, -0.195f), R(0.1f, 0.33f, 0.18f, -0.21f), R(0.33f, 0.32f, 0.17f, -0.205f), R(0.43f, 0.26f, 0.12f, -0.185f)), "Backpack");
            yield return (SpinePath, "Rabat", Loft("BackpackFlap", R(0.31f, 0.335f, 0.185f, -0.208f), R(0.4f, 0.3f, 0.15f, -0.195f), R(0.445f, 0.24f, 0.1f, -0.18f)), "BackpackDark");
            yield return (SpinePath, "Poche_Sac", Loft("BackpackPocket", R(0.06f, 0.2f, 0.05f, -0.31f), R(0.2f, 0.21f, 0.06f, -0.315f), R(0.24f, 0.19f, 0.04f, -0.31f)), "BackpackDark");
            // Rolled along X on top of the pack: lofts run along Y, hence the quarter turn.
            var across = Quaternion.Euler(0f, 0f, 90f);
            var rollCentre = new Vector3(0f, 0.5f, -0.2f);
            yield return (SpinePath, "Couverture", Loft("Bedroll", 0.3f, across, rollCentre, new[] { R(-0.19f, 0.115f, 0.115f), R(0.19f, 0.115f, 0.115f) }), "BlanketRed");
            foreach (var side in new[] { -1f, 1f })
            {
                var left = side < 0f;
                yield return (SpinePath, left ? "Sangle_G" : "Sangle_D", Loft(left ? "BedrollStrapL" : "BedrollStrapR", 0.3f, across, rollCentre,
                    new[] { R(side * 0.11f - 0.012f, 0.124f, 0.124f), R(side * 0.11f + 0.012f, 0.124f, 0.124f) }), "Strap");
                yield return (SpinePath, left ? "Bretelle_G" : "Bretelle_D", Loft(left ? "StrapL" : "StrapR", R(0.05f, 0.045f, 0.02f, 0.128f, side * 0.11f), R(0.13f, 0.045f, 0.02f, 0.1195f, side * 0.11f),
                    R(0.29f, 0.045f, 0.02f, 0.1445f, side * 0.11f), R(0.385f, 0.045f, 0.02f, 0.132f, side * 0.11f), R(0.452f, 0.045f, 0.2f, -0.02f, side * 0.11f)), "Strap");
            }
        }

        // An untucked shirt tail, and blood down the chest.
        static IEnumerable<(string joint, string name, Mesh mesh, string mat)> ZombieStains(BodyLook look)
        {
            yield return (SpinePath, "Pan_Chemise", Loft("ShirtTail", R(-0.08f, 0.15f, 0.252f, 0f, 0.09f), R(0.02f, 0.2f, 0.248f, 0f, 0.075f)), look.Top);
            yield return (SpinePath, "Sang", Loft("BloodStain", R(0.14f, 0.13f, 0.012f, 0.117f, 0.05f), R(0.27f, 0.09f, 0.012f, 0.1355f, 0.05f)), "Blood");
        }
    }
}
