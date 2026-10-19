using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace DeadCounty.EditorTools
{
    // Generates Assets/Scenes/TestMap.unity: a 300 m blockout map for movement, camera, AI and lighting tests.
    // The scene is rebuilt from scratch on every run, so edit this file rather than the scene.
    public static class TestSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/TestMap.unity";
        const string ArtDir = "Assets/DeadCounty/Art/Prototype";
        const string NavMeshPath = "Assets/Scenes/TestMap_NavMesh.asset";
        const float MapSize = 300f;
        const float WallHeight = 3f;
        const float WallThickness = 0.2f;

        static readonly Dictionary<string, Material> Materials = new();

        struct Opening
        {
            public float Offset, Width, Bottom, Top;
            public Opening(float offset, float width, float bottom, float top)
            {
                Offset = offset; Width = width; Bottom = bottom; Top = top;
            }
        }

        static Opening Door(float offset) => new(offset, 1.0f, 0f, 2.1f);
        static Opening Window(float offset) => new(offset, 1.2f, 0.9f, 2.0f);

        [MenuItem("DeadCounty/Build Test Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(ArtDir);
            Materials.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            SetupLightAndCamera();

            var env = new GameObject("Environment");
            BuildGround(env.transform);
            BuildDistanceMarkers(Group(env, "DistanceMarkers"));
            BuildMovementCourse(Group(env, "Zone_Parcours (nord)"));
            BuildStreet(Group(env, "Zone_Rue (est)"));
            BuildAIField(Group(env, "Zone_ChampIA (ouest)"));
            BuildWarehouse(Group(env, "Zone_EntrepotSombre (sud)"));

            foreach (var t in env.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.position = Vector3.zero;
            var dummy = Box("ScaleDummy_1m80", spawn.transform, new Vector3(1.5f, 0.9f, 0f), new Vector3(0.5f, 1.8f, 0.3f), "Accent");
            Object.DestroyImmediate(dummy.GetComponent<Collider>());

            BakeNavMesh(env);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[TestSceneBuilder] {ScenePath} built");
        }

        static Transform Group(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.transform;
        }

        static void SetupLightAndCamera()
        {
            var sun = Object.FindAnyObjectByType<Light>();
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            sun.shadows = LightShadows.Soft;

            var cam = Camera.main;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 2.2f, -6f), Quaternion.Euler(10f, 0f, 0f));
            cam.farClipPlane = 500f;
        }

        static Material Mat(string key)
        {
            if (Materials.TryGetValue(key, out var cached)) return cached;

            var colors = new Dictionary<string, Color>
            {
                ["Ground"] = new(0.55f, 0.56f, 0.58f),
                ["Wall"] = new(0.82f, 0.80f, 0.76f),
                ["Floor"] = new(0.62f, 0.52f, 0.40f),
                ["Roof"] = new(0.35f, 0.25f, 0.22f),
                ["Road"] = new(0.22f, 0.22f, 0.24f),
                ["Sidewalk"] = new(0.68f, 0.68f, 0.66f),
                ["Obstacle"] = new(0.90f, 0.55f, 0.20f),
                ["Ramp"] = new(0.30f, 0.50f, 0.85f),
                ["Stairs"] = new(0.35f, 0.70f, 0.40f),
                ["Furniture"] = new(0.45f, 0.33f, 0.25f),
                ["Crate"] = new(0.55f, 0.45f, 0.30f),
                ["Marker"] = new(0.95f, 0.95f, 0.95f),
                ["MarkerMajor"] = new(0.85f, 0.15f, 0.15f),
                ["Accent"] = new(0.95f, 0.80f, 0.10f),
                ["Metal"] = new(0.30f, 0.32f, 0.35f),
            };

            var path = $"{ArtDir}/M_Proto_{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", colors[key]);
            mat.SetFloat("_Smoothness", 0.15f);
            if (key == "Ground")
            {
                mat.SetTexture("_BaseMap", GridTexture());
                // Plane UVs span the whole map: one texture tile = 10 m, one cell = 1 m.
                mat.SetTextureScale("_BaseMap", Vector2.one * (MapSize / 10f));
            }
            EditorUtility.SetDirty(mat);
            Materials[key] = mat;
            return mat;
        }

        static Texture2D GridTexture()
        {
            var path = $"{ArtDir}/T_Proto_Grid.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 512;
            const int cells = 10;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var major = x < 3 || y < 3 || x >= size - 3 || y >= size - 3;
                var minor = x % (size / cells) < 1 || y % (size / cells) < 1;
                byte v = major ? (byte)70 : minor ? (byte)150 : (byte)205;
                px[y * size + x] = new Color32(v, v, v, 255);
            }
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, string mat, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Mat(mat);
            return go;
        }

        static GameObject Post(string name, Transform parent, Vector3 basePos, float height, float radius, string mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = basePos + Vector3.up * height / 2f;
            go.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = Mat(mat);
            return go;
        }

        // Wall from a to b (local XZ), split around door/window openings measured from a.
        static void Wall(Transform parent, string name, Vector2 a, Vector2 b, params Opening[] openings)
        {
            var dir = (b - a).normalized;
            var length = Vector2.Distance(a, b);
            var rot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
            var wall = new GameObject(name).transform;
            wall.SetParent(parent, false);
            wall.localPosition = new Vector3(a.x, 0f, a.y);
            wall.localRotation = rot;

            void Piece(float from, float to, float bottom, float top)
            {
                if (to - from < 0.01f || top - bottom < 0.01f) return;
                Box("Piece", wall, new Vector3(0f, (bottom + top) / 2f, (from + to) / 2f),
                    new Vector3(WallThickness, top - bottom, to - from), "Wall");
            }

            var cursor = 0f;
            foreach (var o in openings.OrderBy(o => o.Offset))
            {
                var start = o.Offset - o.Width / 2f;
                var end = o.Offset + o.Width / 2f;
                Piece(cursor, start, 0f, WallHeight);
                Piece(start, end, 0f, o.Bottom);
                Piece(start, end, o.Top, WallHeight);
                cursor = end;
            }
            Piece(cursor, length, 0f, WallHeight);
        }

        static void BuildGround(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground_300m";
            ground.transform.SetParent(parent, false);
            ground.transform.localScale = new Vector3(MapSize / 10f, 1f, MapSize / 10f);
            ground.GetComponent<Renderer>().sharedMaterial = Mat("Ground");

            var half = MapSize / 2f;
            Box("Border_N", parent, new Vector3(0f, 1.5f, half), new Vector3(MapSize, 3f, 1f), "Wall");
            Box("Border_S", parent, new Vector3(0f, 1.5f, -half), new Vector3(MapSize, 3f, 1f), "Wall");
            Box("Border_E", parent, new Vector3(half, 1.5f, 0f), new Vector3(1f, 3f, MapSize), "Wall");
            Box("Border_W", parent, new Vector3(-half, 1.5f, 0f), new Vector3(1f, 3f, MapSize), "Wall");
        }

        // Posts every 10 m along both axes, red every 50 m.
        static void BuildDistanceMarkers(Transform parent)
        {
            for (var d = -140; d <= 140; d += 10)
            {
                if (d == 0) continue;
                var mat = d % 50 == 0 ? "MarkerMajor" : "Marker";
                var post = Post($"X{d:+0;-0}m", parent, new Vector3(d, 0f, -3f), 1.5f, 0.1f, mat);
                Object.DestroyImmediate(post.GetComponent<Collider>());
                post = Post($"Z{d:+0;-0}m", parent, new Vector3(-3f, 0f, d), 1.5f, 0.1f, mat);
                Object.DestroyImmediate(post.GetComponent<Collider>());
            }
        }

        static void BuildMovementCourse(Transform parent)
        {
            parent.localPosition = new Vector3(0f, 0f, 20f);

            var obstacles = Group(parent.gameObject, "Obstacles_hauteurs");
            float[] heights = { 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };
            for (var i = 0; i < heights.Length; i++)
                Box($"Obstacle_{heights[i]:0.00}m", obstacles, new Vector3(-30f + i * 5f, heights[i] / 2f, 0f), new Vector3(3f, heights[i], 1f), "Obstacle");

            var ramps = Group(parent.gameObject, "Rampes_pentes");
            float[] angles = { 10f, 20f, 30f, 40f, 50f };
            const float rampLength = 8f;
            for (var i = 0; i < angles.Length; i++)
            {
                var a = angles[i] * Mathf.Deg2Rad;
                var center = new Vector3(10f + i * 5f, Mathf.Sin(a) * rampLength / 2f, 5f + Mathf.Cos(a) * rampLength / 2f);
                Box($"Rampe_{angles[i]}deg", ramps, center, new Vector3(3f, 0.2f, rampLength), "Ramp", Quaternion.Euler(-angles[i], 0f, 0f));
            }

            var stairs = Group(parent.gameObject, "Escaliers");
            BuildStairs(stairs, "Escalier_18cm", new Vector3(-30f, 0f, 12f), 0.18f, 0.30f, 15);
            BuildStairs(stairs, "Escalier_25cm", new Vector3(-24f, 0f, 12f), 0.25f, 0.30f, 11);
            BuildStairs(stairs, "Escalier_35cm", new Vector3(-18f, 0f, 12f), 0.35f, 0.35f, 8);

            var crouch = Group(parent.gameObject, "Tunnel_accroupi_1m20");
            crouch.localPosition = new Vector3(-5f, 0f, 15f);
            Box("Plafond", crouch, new Vector3(0f, 1.3f, 4f), new Vector3(2.4f, 0.2f, 8f), "Wall");
            Box("Mur_G", crouch, new Vector3(-1.3f, 0.7f, 4f), new Vector3(0.2f, 1.4f, 8f), "Wall");
            Box("Mur_D", crouch, new Vector3(1.3f, 0.7f, 4f), new Vector3(0.2f, 1.4f, 8f), "Wall");

            // Narrow L-shaped corridor to test camera collision against close walls.
            var corridor = Group(parent.gameObject, "Couloir_etroit_1m20");
            corridor.localPosition = new Vector3(5f, 0f, 20f);
            Wall(corridor, "Mur_G", new Vector2(-0.7f, 0f), new Vector2(-0.7f, 16.4f));
            Wall(corridor, "Mur_D", new Vector2(0.7f, 0f), new Vector2(0.7f, 15f));
            Wall(corridor, "Mur_Fond", new Vector2(-0.7f, 16.4f), new Vector2(12f, 16.4f));
            Wall(corridor, "Mur_Coude", new Vector2(0.7f, 15f), new Vector2(12f, 15f));
            Box("Plafond", corridor, new Vector3(0f, WallHeight + 0.1f, 8f), new Vector3(1.6f, 0.2f, 16.4f), "Roof");
        }

        static void BuildStairs(Transform parent, string name, Vector3 origin, float rise, float run, int steps)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = origin;
            for (var i = 0; i < steps; i++)
            {
                var h = rise * (i + 1);
                Box($"Marche_{i + 1}", root, new Vector3(0f, h / 2f, i * run + run / 2f), new Vector3(2f, h, run), "Stairs");
            }
            var top = rise * steps;
            Box("Palier", root, new Vector3(0f, top / 2f, steps * run + 1.5f), new Vector3(2f, top, 3f), "Stairs");
        }

        static void BuildStreet(Transform parent)
        {
            parent.localPosition = new Vector3(50f, 0f, 0f);
            Box("Route", parent, new Vector3(45f, 0.01f, 0f), new Vector3(90f, 0.02f, 8f), "Road");
            Box("Trottoir_N", parent, new Vector3(45f, 0.075f, 5.5f), new Vector3(90f, 0.15f, 3f), "Sidewalk");
            Box("Trottoir_S", parent, new Vector3(45f, 0.075f, -5.5f), new Vector3(90f, 0.15f, 3f), "Sidewalk");

            for (var i = 0; i < 4; i++)
            {
                BuildHouse(parent, $"Maison_N{i + 1}", new Vector3(8f + i * 20f, 0f, 10f), false);
                BuildHouse(parent, $"Maison_S{i + 1}", new Vector3(8f + i * 20f, 0f, -20f), true);
            }

            var lamps = Group(parent.gameObject, "Lampadaires");
            for (var i = 0; i < 6; i++)
            {
                var side = i % 2 == 0 ? 6.5f : -6.5f;
                var basePos = new Vector3(5f + i * 15f, 0f, side);
                Post($"Lampadaire_{i + 1}", lamps, basePos, 6f, 0.12f, "Metal");
                var lightGo = new GameObject($"Lampadaire_{i + 1}_Light");
                lightGo.transform.SetParent(lamps, false);
                lightGo.transform.SetLocalPositionAndRotation(basePos + Vector3.up * 5.8f, Quaternion.Euler(90f, 0f, 0f));
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = new Color(1f, 0.78f, 0.45f);
                light.range = 14f;
                light.spotAngle = 110f;
                light.intensity = 8f;
                light.shadows = LightShadows.Soft;
            }

            var cars = Group(parent.gameObject, "Voitures");
            Box("Voiture_1", cars, new Vector3(20f, 0.75f, 2f), new Vector3(4.5f, 1.5f, 1.9f), "Metal");
            Box("Voiture_2", cars, new Vector3(57f, 0.75f, -2.2f), new Vector3(4.5f, 1.5f, 1.9f), "Metal", Quaternion.Euler(0f, 12f, 0f));
        }

        // 12 x 10 m house: living room + kitchen + bedroom + bathroom, front door facing the street.
        static void BuildHouse(Transform parent, string name, Vector3 origin, bool facesNorth)
        {
            var house = new GameObject(name).transform;
            house.SetParent(parent, false);
            house.localPosition = origin;
            if (facesNorth)
            {
                house.localPosition += new Vector3(12f, 0f, 10f);
                house.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }

            Box("Sol", house, new Vector3(6f, 0.05f, 5f), new Vector3(12f, 0.1f, 10f), "Floor");

            Wall(house, "Mur_Facade", new Vector2(0f, 0f), new Vector2(12f, 0f), Window(2f), Door(5f), Window(9.5f));
            Wall(house, "Mur_Arriere", new Vector2(12f, 10f), new Vector2(0f, 10f), Window(3f), Door(9f));
            Wall(house, "Mur_Gauche", new Vector2(0f, 10f), new Vector2(0f, 0f), Window(5f));
            Wall(house, "Mur_Droit", new Vector2(12f, 0f), new Vector2(12f, 10f), Window(2.5f), Window(7.5f));
            Wall(house, "Cloison_Salon", new Vector2(7f, 0f), new Vector2(7f, 10f), Door(3f), Door(7.5f));
            Wall(house, "Cloison_Chambre", new Vector2(7f, 5.5f), new Vector2(12f, 5.5f), Door(1.5f));
            Wall(house, "Cloison_Cuisine", new Vector2(0f, 6f), new Vector2(7f, 6f), Door(5.5f));

            var props = Group(house.gameObject, "Mobilier");
            Box("Canape", props, new Vector3(2f, 0.4f, 1.2f), new Vector3(2.2f, 0.8f, 0.9f), "Furniture");
            Box("Table_basse", props, new Vector3(2f, 0.25f, 3f), new Vector3(1.2f, 0.5f, 0.6f), "Furniture");
            Box("Plan_travail", props, new Vector3(3f, 0.45f, 9.6f), new Vector3(5f, 0.9f, 0.6f), "Furniture");
            Box("Frigo", props, new Vector3(6.2f, 0.9f, 9.6f), new Vector3(0.8f, 1.8f, 0.7f), "Marker");
            Box("Placard_haut", props, new Vector3(3f, 1.9f, 9.75f), new Vector3(5f, 0.7f, 0.35f), "Furniture");
            Box("Table_cuisine", props, new Vector3(3f, 0.38f, 7.8f), new Vector3(1.6f, 0.75f, 0.9f), "Furniture");
            Box("Lit", props, new Vector3(10.5f, 0.25f, 8.5f), new Vector3(1.6f, 0.5f, 2.1f), "Furniture");
            Box("Armoire", props, new Vector3(8f, 1f, 9.6f), new Vector3(1.6f, 2f, 0.6f), "Furniture");
            Box("Commode", props, new Vector3(11.6f, 0.45f, 6.5f), new Vector3(0.5f, 0.9f, 1.2f), "Furniture");
            Box("Baignoire", props, new Vector3(10.8f, 0.3f, 1.2f), new Vector3(1.7f, 0.6f, 0.8f), "Marker");
            Box("Meuble_SdB", props, new Vector3(8f, 0.45f, 0.5f), new Vector3(1f, 0.9f, 0.5f), "Furniture");

            Box("Toit", house, new Vector3(6f, WallHeight + 0.1f, 5f), new Vector3(12.6f, 0.2f, 10.6f), "Roof");
        }

        // Open field with cover and rings of posts at 5/10/20/30/50 m from NoiseOrigin, matching the GDD noise radii.
        static void BuildAIField(Transform parent)
        {
            parent.localPosition = new Vector3(-95f, 0f, 0f);
            var origin = new GameObject("NoiseOrigin").transform;
            origin.SetParent(parent, false);
            Post("NoiseOrigin_Mat", origin, Vector3.zero, 3f, 0.25f, "Accent");

            int[] radii = { 5, 10, 20, 30, 50 };
            foreach (var r in radii)
            {
                var ring = Group(parent.gameObject, $"Rayon_{r}m");
                const int count = 16;
                for (var i = 0; i < count; i++)
                {
                    var a = i * Mathf.PI * 2f / count;
                    var p = Post($"Poteau_{i}", ring, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), 0.8f, 0.08f, r % 10 == 0 ? "MarkerMajor" : "Marker");
                    Object.DestroyImmediate(p.GetComponent<Collider>());
                }
            }

            var cover = Group(parent.gameObject, "Couverts");
            var rng = new System.Random(42);
            for (var i = 0; i < 40; i++)
            {
                var pos = new Vector3(rng.Next(-48, 48), 0f, rng.Next(-48, 48));
                if (pos.magnitude < 7f) continue;
                var h = rng.Next(0, 3) switch { 0 => 0.9f, 1 => 1.4f, _ => 2.2f };
                Box($"Caisse_{i}", cover, pos + Vector3.up * h / 2f, new Vector3(1.5f, h, 1.5f), "Crate", Quaternion.Euler(0f, rng.Next(0, 90), 0f));
            }
        }

        // Closed 30 x 20 m building with a single door: flashlight and dark interior tests.
        static void BuildWarehouse(Transform parent)
        {
            parent.localPosition = new Vector3(-15f, 0f, -80f);
            Box("Sol", parent, new Vector3(15f, 0.05f, 10f), new Vector3(30f, 0.1f, 20f), "Floor");
            Wall(parent, "Mur_Nord", new Vector2(30f, 20f), new Vector2(0f, 20f), new Opening(15f, 2f, 0f, 2.4f));
            Wall(parent, "Mur_Sud", new Vector2(0f, 0f), new Vector2(30f, 0f));
            Wall(parent, "Mur_Est", new Vector2(30f, 0f), new Vector2(30f, 20f));
            Wall(parent, "Mur_Ouest", new Vector2(0f, 20f), new Vector2(0f, 0f));
            Box("Toit", parent, new Vector3(15f, WallHeight + 0.1f, 10f), new Vector3(30.4f, 0.2f, 20.4f), "Roof");

            var shelves = Group(parent.gameObject, "Etageres");
            for (var row = 0; row < 4; row++)
            for (var i = 0; i < 2; i++)
                Box($"Etagere_{row}_{i}", shelves, new Vector3(6f + i * 14f, 1.1f, 4f + row * 3.5f), new Vector3(8f, 2.2f, 0.8f), "Metal");
        }

        static void BakeNavMesh(GameObject env)
        {
            var surface = env.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
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
