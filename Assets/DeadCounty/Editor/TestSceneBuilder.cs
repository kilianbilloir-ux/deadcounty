using System.Collections.Generic;
using DeadCounty.Core;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace DeadCounty.EditorTools
{
    // Generates Assets/Scenes/TestMap.unity: a 300 m rural county blockout (village, gas station, farm, forest)
    // wrapped around the movement, AI and lighting test zones. The scene is rebuilt from scratch on every run,
    // so edit the TestSceneBuilder.*.cs files rather than the scene.
    public static partial class TestSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/TestMap.unity";
        const string ArtDir = "Assets/DeadCounty/Art/Prototype";
        const string MeshDir = ArtDir + "/Meshes";
        const string NavMeshPath = "Assets/Scenes/TestMap_NavMesh.asset";
        const float MapSize = 300f;
        const float HalfMap = MapSize / 2f;
        const float MainStreetX = 70f;
        const float RoadWidth = 7f;
        const float WallHeight = 3f;
        const float WallThickness = 0.2f;

        static readonly List<Rect> Reserved = new();
        static readonly List<Vector3> ReservedCircles = new(); // x, z, radius
        static readonly List<Light> BuiltLamps = new();

        [MenuItem("DeadCounty/Build Test Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(MeshDir);
            Materials.Clear();
            UsedMaterials.Clear();
            MeshCache.Clear();
            Reserved.Clear();
            ReservedCircles.Clear();
            BuiltLamps.Clear();
            DoorSpots.Clear();
            ConfigureProject();
            var items = EnsureItems();
            EnsureItemModels(items);
            EnsureLootTables(items);
            EnsureSounds();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EnsureItemIcons(items);
            var env = new GameObject("Environment").transform;

            BuildTerrain(Group(env, "Terrain"));
            BuildRoads(Group(env, "Routes"));
            BuildTestCourse(Group(env, "Zone_Essais"));
            BuildVillage(Group(env, "Village"));
            BuildGasStation(Group(env, "Station_Service"));
            BuildGeneralStore(Group(env, "Epicerie"));
            BuildChurch(Group(env, "Eglise"));
            BuildFarm(Group(env, "Ferme"));
            BuildWarehouse(Group(env, "Entrepot_Sombre"));
            BuildAIClearing(Group(env, "Clairiere_IA"));
            BuildRoadside(Group(env, "Bord_de_route"));
            BuildStreetDressing(Group(env, "Habillage"));
            BuildVegetation(Group(env, "Vegetation"));
            GuaranteeStartingLoot(env);

            foreach (var t in env.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

            SetupAtmosphere();
            EnsureGaitProfiles();
            BuildPlayer();
            BuildTitleScreen();
            BuildSoundscape();
            BakeNavMesh(env.gameObject);
            BuildDoors();
            BuildZombies();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            DeleteUnusedMaterials();
            AssetDatabase.SaveAssets();
            Debug.Log($"[TestSceneBuilder] {ScenePath} built");
        }

        static Transform Group(Transform parent, string name, Vector3? position = null, float yaw = 0f)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetLocalPositionAndRotation(position ?? Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, string mat, Quaternion? rot = null, bool solid = true)
            => Primitive(PrimitiveType.Cube, name, parent, center, size, mat, rot, solid);

        // Unity cylinders are 2 units tall, hence the halved Y scale.
        static GameObject Cylinder(string name, Transform parent, Vector3 center, float radius, float height, string mat, Quaternion? rot = null, bool solid = true)
            => Primitive(PrimitiveType.Cylinder, name, parent, center, new Vector3(radius * 2f, height / 2f, radius * 2f), mat, rot, solid);

        static GameObject Post(string name, Transform parent, Vector3 basePos, float height, float radius, string mat, bool solid = true)
            => Cylinder(name, parent, basePos + Vector3.up * height / 2f, radius, height, mat, null, solid);

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 center, Vector3 scale, string mat, Quaternion? rot, bool solid)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(center, rot ?? Quaternion.identity);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(mat);
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale, string[] mats, bool convexCollider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats.Select(Mat).ToArray();
            if (convexCollider)
            {
                var col = go.AddComponent<MeshCollider>();
                col.sharedMesh = mesh;
                col.convex = true;
            }
            return go;
        }

        // Gable roof whose ridge runs along local X before the yaw is applied.
        static GameObject Roof(Transform parent, string name, Vector3 baseCenter, float ridgeLength, float span, float height, string mat, float yaw = 0f)
            => MeshObject(name, parent, RoofMesh(), baseCenter, Quaternion.Euler(0f, yaw, 0f), new Vector3(ridgeLength, height, span), new[] { mat }, true);

        static Light SpotLight(Transform parent, string name, Vector3 pos, Quaternion rot, Color color, float intensity, float range, float angle, bool shadows)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.SetLocalPositionAndRotation(pos, rot);
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return light;
        }

        readonly struct Opening
        {
            public readonly float Offset, Width, Bottom, Top;
            public Opening(float offset, float width, float bottom, float top)
            {
                Offset = offset; Width = width; Bottom = bottom; Top = top;
            }
        }

        static Opening Door(float offset, float width = 1f, float height = 2.1f) => new(offset, width, 0f, height);
        static Opening Window(float offset, float width = 1.2f, float bottom = 0.9f, float top = 2f) => new(offset, width, bottom, top);

        readonly struct WallStyle
        {
            public readonly string Mat, Shutter;
            public readonly float Height;
            public WallStyle(string mat, float height = WallHeight, string shutter = null)
            {
                Mat = mat; Height = height; Shutter = shutter;
            }
        }

        // Wall from a to b (parent-local XZ), split around openings measured from a. The wall's local +X is the
        // exterior when an outline is walked like the house outlines below; sills and shutters go on that side.
        static Transform Wall(Transform parent, string name, Vector2 a, Vector2 b, WallStyle style, params Opening[] openings)
        {
            var dir = (b - a).normalized;
            var length = Vector2.Distance(a, b);
            var wall = new GameObject(name).transform;
            wall.SetParent(parent, false);
            wall.SetLocalPositionAndRotation(new Vector3(a.x, 0f, a.y), Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)));

            void Piece(float from, float to, float bottom, float top)
            {
                if (to - from < 0.01f || top - bottom < 0.01f) return;
                Box("Piece", wall, new Vector3(0f, (bottom + top) / 2f, (from + to) / 2f), new Vector3(WallThickness, top - bottom, to - from), style.Mat).tag = GameLayers.WallTag;
            }

            var cursor = 0f;
            foreach (var o in openings.OrderBy(o => o.Offset))
            {
                var start = o.Offset - o.Width / 2f;
                var end = o.Offset + o.Width / 2f;
                Piece(cursor, start, 0f, style.Height);
                Piece(start, end, 0f, o.Bottom);
                Piece(start, end, o.Top, style.Height);
                cursor = end;
                if (style.Shutter != null && o.Bottom > 0f) WindowTrim(wall, o, style.Shutter);
                RecordDoorSpot(wall, o);
            }
            Piece(cursor, length, 0f, style.Height);
            return wall;
        }

        // Sill, frame, a cross of muntins in the opening and shutters: a hole in the wall reads as a window from
        // both sides. None of it is solid, so sight and noise go through as before.
        static void WindowTrim(Transform wall, Opening o, string shutter)
        {
            var x = WallThickness / 2f + 0.04f;
            var h = o.Top - o.Bottom;
            var y = (o.Bottom + o.Top) / 2f;
            Box("Appui", wall, new Vector3(x + 0.03f, o.Bottom - 0.03f, o.Offset), new Vector3(0.14f, 0.06f, o.Width + 0.2f), "Trim", solid: false);
            Box("Linteau", wall, new Vector3(x - 0.01f, o.Top + 0.05f, o.Offset), new Vector3(0.06f, 0.1f, o.Width + 0.16f), "Trim", solid: false);
            foreach (var side in new[] { -1f, 1f })
                Box("Chambranle", wall, new Vector3(x - 0.01f, y, o.Offset + side * (o.Width / 2f + 0.04f)), new Vector3(0.06f, h, 0.08f), "Trim", solid: false);
            Box("Croisillon", wall, new Vector3(0f, y, o.Offset), new Vector3(0.04f, h, 0.04f), "Trim", solid: false);
            Box("Croisillon", wall, new Vector3(0f, y, o.Offset), new Vector3(0.04f, 0.04f, o.Width), "Trim", solid: false);
            Box("Volet", wall, new Vector3(x, y, o.Offset - o.Width / 2f - 0.3f), new Vector3(0.05f, h, 0.5f), shutter, solid: false);
            Box("Volet", wall, new Vector3(x, y, o.Offset + o.Width / 2f + 0.3f), new Vector3(0.05f, h, 0.5f), shutter, solid: false);
        }

        static void Glaze(Transform wall, Opening o)
            => Box("Vitre", wall, new Vector3(0f, (o.Bottom + o.Top) / 2f, o.Offset), new Vector3(0.04f, o.Top - o.Bottom, o.Width), "Glass").tag = GameLayers.GlassTag;

        static void Reserve(float xMin, float zMin, float xMax, float zMax) => Reserved.Add(Rect.MinMaxRect(xMin, zMin, xMax, zMax));

        static void ReserveLocal(Transform root, float xMin, float zMin, float xMax, float zMax)
        {
            var corners = new[] { new Vector3(xMin, 0f, zMin), new Vector3(xMax, 0f, zMin), new Vector3(xMin, 0f, zMax), new Vector3(xMax, 0f, zMax) }
                .Select(root.TransformPoint).ToArray();
            Reserve(corners.Min(c => c.x), corners.Min(c => c.z), corners.Max(c => c.x), corners.Max(c => c.z));
        }

        static bool IsReserved(float x, float z, float margin)
        {
            foreach (var r in Reserved)
                if (x > r.xMin - margin && x < r.xMax + margin && z > r.yMin - margin && z < r.yMax + margin) return true;
            foreach (var c in ReservedCircles)
                if (new Vector2(x - c.x, z - c.y).magnitude < c.z + margin) return true;
            return false;
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        static void BakeNavMesh(GameObject env)
        {
            ConfigureHumanoidAgent();
            var surface = env.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
        }

        // Tags and layers used by gameplay queries; zombies never collide with each other (NavMesh avoidance
        // keeps them apart, physics contacts between them would only jitter).
        static void ConfigureProject()
        {
            EnsureTag(GameLayers.WallTag);
            EnsureTag(GameLayers.GlassTag);
            EnsureLayer(GameLayers.Player, "Player");
            EnsureLayer(GameLayers.Zombie, "Zombie");
            Physics.IgnoreLayerCollision(GameLayers.Zombie, GameLayers.Zombie, true);
        }

        static SerializedObject TagManager() => new(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

        static void EnsureTag(string tag)
        {
            var tagManager = TagManager();
            var tags = tagManager.FindProperty("tags");
            for (var i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureLayer(int index, string name)
        {
            var tagManager = TagManager();
            var slot = tagManager.FindProperty("layers").GetArrayElementAtIndex(index);
            if (slot.stringValue == name) return;
            if (!string.IsNullOrEmpty(slot.stringValue))
                throw new System.InvalidOperationException($"Layer {index} is already used by '{slot.stringValue}'");
            slot.stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        // Humanoid agent sized like the player, so baked paths fit through 1 m doors and climb the same steps.
        static void ConfigureHumanoidAgent()
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            var humanoid = settings.FindProperty("m_Settings").GetArrayElementAtIndex(0);
            humanoid.FindPropertyRelative("agentRadius").floatValue = 0.3f;
            humanoid.FindPropertyRelative("agentHeight").floatValue = 1.8f;
            humanoid.FindPropertyRelative("agentClimb").floatValue = 0.3f;
            humanoid.FindPropertyRelative("agentSlope").floatValue = 45f;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath))
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
