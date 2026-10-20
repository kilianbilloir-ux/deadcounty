using System.IO;
using DeadCounty.Animation;
using DeadCounty.Core;
using DeadCounty.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        const string PlayerPrefabPath = "Assets/DeadCounty/Prefabs/PlayerRig.prefab";
        static readonly Vector3 SpawnPosition = new(65.2f, 0.15f, -10f);

        // The prefab is only generated when missing, so Inspector tweaks survive scene rebuilds.
        // Delete it to regenerate it from this code.
        static void BuildPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            prefab = prefab == null ? CreatePlayerPrefab() : EnsurePlayerComponents(prefab);
            // Keeps DeleteUnusedMaterials from removing materials only the existing prefab references.
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
                if (m != null) UsedMaterials.Add(AssetDatabase.GetAssetPath(m));
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.transform.SetPositionAndRotation(SpawnPosition, Quaternion.identity);
            Camera.main.gameObject.AddComponent<CinemachineBrain>();
        }

        static GameObject CreatePlayerPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPrefabPath));
            var rig = new GameObject("PlayerRig");

            var player = new GameObject("Player") { tag = "Player" };
            player.transform.SetParent(rig.transform, false);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.03f;
            // The 1 mm default swallows the first steps of a start from rest above ~300 fps; the controller then
            // reads the lost motion as a wall and bleeds its speed back to zero.
            controller.minMoveDistance = 0f;

            var joints = BuildSurvivor(player.transform);

            var target = new GameObject("CameraTarget").transform;
            target.SetParent(player.transform, false);
            target.localPosition = new Vector3(0f, 1.55f, 0f);

            // Own layer so camera probes and future AI sight checks can ignore the player's colliders.
            foreach (var t in player.GetComponentsInChildren<Transform>()) t.gameObject.layer = GameLayers.Player;

            var tpc = player.AddComponent<ThirdPersonController>();
            var so = new SerializedObject(tpc);
            so.FindProperty("cameraTarget").objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();

            var animator = new SerializedObject(player.AddComponent<ProceduralHumanoidAnimator>());
            animator.FindProperty("controller").objectReferenceValue = tpc;
            animator.FindProperty("gait").objectReferenceValue = SurvivorGait();
            foreach (var (field, joint) in joints) animator.FindProperty(field).objectReferenceValue = joint;
            animator.ApplyModifiedPropertiesWithoutUndo();

            var camera = new GameObject("PlayerFollowCamera");
            camera.transform.SetParent(rig.transform, false);
            var vcam = camera.AddComponent<CinemachineCamera>();
            vcam.Target.TrackingTarget = target;
            vcam.Lens.FieldOfView = 55f;
            vcam.Lens.NearClipPlane = 0.1f;
            vcam.Lens.FarClipPlane = 600f;

            var follow = camera.AddComponent<CinemachineThirdPersonFollow>();
            follow.ShoulderOffset = new Vector3(0.55f, 0.05f, 0f);
            follow.VerticalArmLength = 0.2f;
            follow.CameraSide = 1f;
            follow.CameraDistance = 3.2f;
            follow.Damping = new Vector3(0.1f, 0.3f, 0.2f);
            var obstacles = follow.AvoidObstacles;
            obstacles.Enabled = true;
            obstacles.CollisionFilter = 1 << 0;
            obstacles.IgnoreTag = "Player";
            obstacles.CameraRadius = 0.2f;
            obstacles.DampingIntoCollision = 0f;
            obstacles.DampingFromCollision = 0.4f;
            follow.AvoidObstacles = obstacles;
            camera.AddComponent<PlayerCameraDirector>();
            rig.AddComponent<PlayerDebugOverlay>();
            AddMissingPlayerComponents(rig, player);

            var prefab = PrefabUtility.SaveAsPrefabAsset(rig, PlayerPrefabPath);
            Object.DestroyImmediate(rig);
            return prefab;
        }

        // The prefab is never regenerated, so components and references added to the player since it was created
        // are added in place; existing components and their serialized values are left untouched to keep
        // Inspector tweaks.
        static GameObject EnsurePlayerComponents(GameObject prefabAsset)
        {
            var path = AssetDatabase.GetAssetPath(prefabAsset);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var controller = root.GetComponentInChildren<ThirdPersonController>(true);
                if (controller == null) throw new System.InvalidOperationException($"{path} has no ThirdPersonController");
                var animator = root.GetComponentInChildren<ProceduralHumanoidAnimator>(true);
                if (animator == null) throw new System.InvalidOperationException($"{path} has no ProceduralHumanoidAnimator");
                var changed = AddMissingPlayerComponents(root, controller.gameObject);
                changed |= AssignMissingGait(animator);
                if (!changed) return prefabAsset;
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Checked one by one because RequireComponent may already have added some of them (PlayerStamina with the controller).
        static bool AddMissingPlayerComponents(GameObject rig, GameObject player)
        {
            var added = false;
            added |= AddMissing<PlayerStamina>(player);
            added |= AddMissing<PlayerHealth>(player);
            added |= AddMissing<PerceptionTarget>(player);
            added |= AddMissing<PlayerNoiseEmitter>(player);
            added |= AddMissing<PlayerDeathFlow>(rig);
            added |= AddMissing<PlayerHud>(rig);
            return added;
        }

        // Animators saved before GaitProfile existed get the survivor profile; an assigned profile is kept.
        static bool AssignMissingGait(ProceduralHumanoidAnimator animator)
        {
            var so = new SerializedObject(animator);
            var gait = so.FindProperty("gait");
            if (gait.objectReferenceValue != null) return false;
            gait.objectReferenceValue = SurvivorGait();
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        static bool AddMissing<T>(GameObject target) where T : Component
        {
            if (target.TryGetComponent<T>(out _)) return false;
            target.AddComponent<T>();
            return true;
        }
    }
}
