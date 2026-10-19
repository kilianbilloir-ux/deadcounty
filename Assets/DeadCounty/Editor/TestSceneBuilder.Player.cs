using System.IO;
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
            if (prefab == null) prefab = CreatePlayerPrefab();
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

            // Capsule body with a dark visor so the facing direction reads from the camera.
            var visual = Primitive(PrimitiveType.Capsule, "Visual", player.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.6f, 0.9f, 0.6f), "Player", null, false);
            Box("Visiere", visual.transform, new Vector3(0f, 0.6f, 0.45f), new Vector3(0.7f, 0.12f, 0.2f), "CarBlack", solid: false);

            var target = new GameObject("CameraTarget").transform;
            target.SetParent(player.transform, false);
            target.localPosition = new Vector3(0f, 1.55f, 0f);

            var tpc = player.AddComponent<ThirdPersonController>();
            var so = new SerializedObject(tpc);
            so.FindProperty("cameraTarget").objectReferenceValue = target;
            so.FindProperty("visual").objectReferenceValue = visual.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

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

            var prefab = PrefabUtility.SaveAsPrefabAsset(rig, PlayerPrefabPath);
            Object.DestroyImmediate(rig);
            return prefab;
        }
    }
}
