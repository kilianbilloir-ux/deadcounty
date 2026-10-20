using System;
using System.Collections.Generic;
using DeadCounty.AI;
using DeadCounty.Animation;
using DeadCounty.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        const string ZombiePrefabPath = "Assets/DeadCounty/Prefabs/Zombie.prefab";
        const string StandardProfilePath = DataDir + "/ZP_Standard.asset";
        const float MinSpawnClearance = 15f;
        const float PlacementSnap = 1.5f;

        static readonly string[] ShirtVariants = { "ZombieShirt", "ZombieShirtBlue", "ZombieShirtRed", "ZombieShirtWhite" };
        static readonly string[] PantsVariants = { "ZombiePants", "ZombiePantsKhaki", "ZombiePantsDark" };

        // Runs after the NavMesh bake: every zombie is snapped onto it, and a spot without NavMesh fails the build.
        static void BuildZombies()
        {
            new GameObject("AI").AddComponent<ZombieManager>();
            var root = new GameObject("Zombies").transform;
            var prefab = ZombiePrefab();
            MarkMaterialsUsed(prefab);
            var spots = ZombieSpots();
            for (var i = 0; i < spots.Count; i++)
            {
                var (name, position, yaw, test) = spots[i];
                var seed = i + 1;
                var offset = position - SpawnPosition;
                offset.y = 0f;
                if (!test && offset.magnitude < MinSpawnClearance)
                    throw new InvalidOperationException($"{name} stands within {MinSpawnClearance} m of the player spawn");
                if (!NavMesh.SamplePosition(position, out var hit, PlacementSnap, NavMesh.AllAreas))
                    throw new InvalidOperationException($"{name}: no NavMesh within {PlacementSnap} m of {position}");

                var zombie = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                zombie.name = $"Zombie_{name}";
                zombie.transform.SetPositionAndRotation(hit.position, Quaternion.Euler(0f, yaw, 0f));
                var agent = new SerializedObject(zombie.GetComponent<ZombieAgent>());
                agent.FindProperty("seed").intValue = seed;
                agent.FindProperty("deterministic").boolValue = test;
                agent.ApplyModifiedPropertiesWithoutUndo();
                if (!test) Dress(zombie, seed);
            }
        }

        // 18 ambient zombies, none within 15 m of the player spawn, then 6 test zombies (no speed jitter, no limp):
        // 4 facing away from the clearing's noise post at 5, 10, 20 and 30 m, and 2 in the dark warehouse.
        static List<(string name, Vector3 position, float yaw, bool test)> ZombieSpots()
        {
            Vector3 Room(int lot, bool east, float x, float z) => LotPoint(lot, east, x - 6f, z);
            Vector3 Church(float x, float z) => ChurchOrigin + Quaternion.Euler(0f, ChurchYaw, 0f) * new Vector3(x, 0f, z);
            Vector3 Clearing(float distance) => ClearingCenter + Vector3.right * distance;

            return new List<(string, Vector3, float, bool)>
            {
                ("Carrefour_1", new Vector3(79f, 0f, 12f), 30f, false),
                ("Carrefour_2", new Vector3(61f, 0f, 13f), 330f, false),
                ("Carrefour_3", new Vector3(86f, 0f, -3f), 90f, false),
                ("Rue_1", new Vector3(69.2f, 0f, 44f), 0f, false),
                ("Rue_2", new Vector3(71.3f, 0f, 45.4f), 160f, false),
                ("Rue_3", new Vector3(70f, 0f, 47f), 250f, false),
                ("Maison_O2_Salon", Room(1, false, 3.5f, 3f), 40f, false),
                ("Maison_O2_Chambre", Room(1, false, 9.5f, 8f), 200f, false),
                ("Trottoir_E1", new Vector3(77.5f, 0f, 52f), 270f, false),
                ("Trottoir_E2", new Vector3(78.5f, 0f, 71f), 200f, false),
                ("Trottoir_E3", new Vector3(76.5f, 0f, 93f), 10f, false),
                ("Jardin_E3_1", LotPoint(2, true, 1f, 14f), 120f, false),
                ("Jardin_E3_2", LotPoint(2, true, 5f, 16.5f), 300f, false),
                ("Maison_Barricadee", Room(2, false, 9.5f, 7.5f), 0f, false),
                ("Eglise_Autel", Church(0f, 13f), ChurchYaw, false),
                ("Eglise_Cimetiere", Church(11.1f, 8f), ChurchYaw + 180f, false),
                ("Ferme_Cour", new Vector3(-85f, 0f, 86f), 120f, false),
                ("Ferme_Grange", new Vector3(-96f, 0f, 106f), 180f, false),
                ("Test_Clairiere_5m", Clearing(5f), 90f, true),
                ("Test_Clairiere_10m", Clearing(10f), 90f, true),
                ("Test_Clairiere_20m", Clearing(20f), 90f, true),
                ("Test_Clairiere_30m", Clearing(30f), 90f, true),
                ("Test_Entrepot_1", WarehouseOrigin + new Vector3(13f, 0f, 9f), 0f, true),
                ("Test_Entrepot_2", WarehouseOrigin + new Vector3(25f, 0f, 16f), 270f, true),
            };
        }

        // Generated only when missing, like the player prefab: delete it to rebuild it from this code.
        static GameObject ZombiePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPath);
            if (prefab != null) return prefab;

            var body = new GameObject("Zombie");
            var capsule = body.AddComponent<CapsuleCollider>();
            capsule.radius = 0.3f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            // Kinematic: the ZombieMotor places the body, the player's CharacterController still bumps into it.
            body.AddComponent<Rigidbody>().isKinematic = true;
            var navAgent = body.AddComponent<NavMeshAgent>();
            navAgent.height = 1.8f;
            navAgent.baseOffset = 0f;

            var joints = BuildHumanoid(body.transform, ZombieLook);
            foreach (var t in body.GetComponentsInChildren<Transform>()) t.gameObject.layer = GameLayers.Zombie;

            var agent = body.AddComponent<ZombieAgent>();
            var animator = body.AddComponent<ProceduralHumanoidAnimator>();
            var animatorSo = new SerializedObject(animator);
            animatorSo.FindProperty("controller").objectReferenceValue = agent;
            animatorSo.FindProperty("gait").objectReferenceValue = ShamblerGait();
            foreach (var (field, joint) in joints) animatorSo.FindProperty(field).objectReferenceValue = joint;
            animatorSo.ApplyModifiedPropertiesWithoutUndo();

            var agentSo = new SerializedObject(agent);
            agentSo.FindProperty("profile").objectReferenceValue = StandardProfile();
            agentSo.FindProperty("animator").objectReferenceValue = animator;
            agentSo.ApplyModifiedPropertiesWithoutUndo();

            prefab = PrefabUtility.SaveAsPrefabAsset(body, ZombiePrefabPath);
            Object.DestroyImmediate(body);
            return prefab;
        }

        static ZombieProfile StandardProfile()
        {
            var profile = LoadOrCreate(StandardProfilePath, ScriptableObject.CreateInstance<ZombieProfile>);
            if (profile.Gait != null) return profile;
            profile.Gait = ShamblerGait();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // Seeded shirt and trousers so a crowd is not made of clones; stored as overrides on the scene instance.
        static void Dress(GameObject zombie, int seed)
        {
            var rng = new System.Random(seed);
            var shirt = Mat(ShirtVariants[rng.Next(ShirtVariants.Length)]);
            var pants = Mat(PantsVariants[rng.Next(PantsVariants.Length)]);
            var baseShirt = Mat(ZombieLook.Top);
            var basePants = Mat(ZombieLook.Pants);
            foreach (var r in zombie.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.sharedMaterial == baseShirt) r.sharedMaterial = shirt;
                else if (r.sharedMaterial == basePants) r.sharedMaterial = pants;
            }
        }

        // Keeps DeleteUnusedMaterials from removing materials only an existing prefab references, and applies the
        // current palette to the prototype ones.
        static void MarkMaterialsUsed(GameObject prefab)
        {
            const string prefix = "M_Proto_";
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                var key = m.name.StartsWith(prefix) ? m.name.Substring(prefix.Length) : null;
                if (key != null && Palette.ContainsKey(key)) Mat(key);
                else UsedMaterials.Add(AssetDatabase.GetAssetPath(m));
            }
        }
    }
}
