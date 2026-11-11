using System.IO;
using DeadCounty.Animation;
using DeadCounty.Core;
using DeadCounty.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        const string PlayerPrefabPath = "Assets/DeadCounty/Prefabs/PlayerRig.prefab";
        const string HudPanelSettingsPath = "Assets/DeadCounty/UI/HudPanelSettings.asset";
        const string HudUxmlPath = "Assets/DeadCounty/UI/Hud.uxml";
        const string HudUssPath = "Assets/DeadCounty/UI/Hud.uss";
        const string ExhaustionProfilePath = ArtDir + "/VP_Exhausted.asset";
        static readonly Vector3 SpawnPosition = new(65.2f, 0.15f, -10f);

        // The prefab is only generated when missing, so Inspector tweaks survive scene rebuilds.
        // Delete it to regenerate it from this code.
        static void BuildPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            prefab = prefab == null ? CreatePlayerPrefab() : EnsurePlayerComponents(prefab);
            MarkMaterialsUsed(prefab);
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

            var spot = SpotLight(target, "Flashlight", new Vector3(0f, 0.05f, 0.25f), Quaternion.identity,
                new Color(1f, 0.95f, 0.85f), 18f, 25f, 40f, false);
            spot.enabled = false;

            // Own layer so camera probes and future AI sight checks can ignore the player's colliders.
            foreach (var t in player.GetComponentsInChildren<Transform>()) t.gameObject.layer = GameLayers.Player;

            var tpc = player.AddComponent<ThirdPersonController>();
            var so = new SerializedObject(tpc);
            so.FindProperty("cameraTarget").objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();

            var flashlight = new SerializedObject(player.AddComponent<PlayerFlashlight>());
            flashlight.FindProperty("spot").objectReferenceValue = spot;
            flashlight.FindProperty("startOn").boolValue = false;
            flashlight.ApplyModifiedPropertiesWithoutUndo();

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
            rig.AddComponent<UIDocument>();
            AddMissingPlayerComponents(rig, player);
            AssignHudAssets(rig);

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
                changed |= AddMissing<UIDocument>(root);
                AssignHudAssets(root);
                // AssignHudAssets writes straight into the prefab contents; save unconditionally.
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
            added |= AddMissing<PlayerFlashlight>(player);
            added |= AddMissing<PlayerNeeds>(player);
            added |= AddMissing<PlayerInteractor>(player);
            added |= AddMissing<PlayerInventory>(player);
            added |= AddMissing<PlayerSleep>(player);
            added |= AddMissing<PlayerDeathFlow>(rig);
            added |= AddMissing<PlayerHudController>(rig);
            added |= AddMissing<PlayerInventoryScreen>(rig);
            added |= AddMissing<PlayerConditionHud>(rig);
            added |= AddMissing<PauseMenu>(rig);
            added |= EnsureFlashlight(player);
            added |= EnsureExhaustionVolume(rig, player);
            added |= AssignStartingItems(player);
            return added;
        }

        // Global volume that PlayerNeeds blends in while fatigue is empty (GDD 8.4: dark vignette).
        static bool EnsureExhaustionVolume(GameObject rig, GameObject player)
        {
            var so = new SerializedObject(player.GetComponent<PlayerNeeds>());
            var property = so.FindProperty("exhaustionVolume");
            if (property.objectReferenceValue != null) return false;
            var holder = new GameObject("ExhaustionVolume");
            holder.transform.SetParent(rig.transform, false);
            var volume = holder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 0f;
            volume.sharedProfile = ExhaustionProfile();
            property.objectReferenceValue = volume;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        static VolumeProfile ExhaustionProfile()
        {
            var profile = EmptiedProfile(ExhaustionProfilePath);
            var vignette = AddOverride<Vignette>(profile);
            vignette.intensity.Override(0.55f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(Color.black);
            AddOverride<ColorAdjustments>(profile).saturation.Override(-45f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // Old rigs predate the flashlight: add the spot under the camera target and wire it.
        static bool EnsureFlashlight(GameObject player)
        {
            var target = player.transform.Find("CameraTarget");
            if (target == null) throw new System.InvalidOperationException("PlayerRig has no CameraTarget for the flashlight");
            var flash = target.Find("Flashlight");
            Light spot;
            if (flash == null)
            {
                spot = SpotLight(target, "Flashlight", new Vector3(0f, 0.05f, 0.25f), Quaternion.identity,
                    new Color(1f, 0.95f, 0.85f), 18f, 25f, 40f, false);
                spot.enabled = false;
            }
            else
            {
                spot = flash.GetComponent<Light>();
            }
            var so = new SerializedObject(player.GetComponent<PlayerFlashlight>());
            if (so.FindProperty("spot").objectReferenceValue != null) return flash == null;
            so.FindProperty("spot").objectReferenceValue = spot;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // The HUD needs the UI Toolkit assets; kept in the project so the rig stays a plain prefab.
        static void AssignHudAssets(GameObject rig)
        {
            var hud = rig.GetComponent<PlayerHudController>();
            if (hud == null) return;
            var so = new SerializedObject(hud);
            var panel = LoadOrCreate<PanelSettings>(HudPanelSettingsPath);
            // Laid out for 1080p and scaled with the screen, so the HUD keeps its proportions at any resolution.
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);
            so.FindProperty("panelSettings").objectReferenceValue = panel;
            so.FindProperty("hudLayout").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudUxmlPath);
            so.FindProperty("hudStyles").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>(HudUssPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
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
