using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadCounty.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeadCounty.EditorTools
{
    // Inventory pictures of the items, rendered from their ground models in a preview scene so nothing of the open
    // scene gets in the shot. Each model is shot over black then over white: what changes between the two is
    // background, which gives a clean alpha whatever the pipeline does with it. Shot four times larger and averaged
    // down for smooth edges. A PNG is only rewritten when its pixels change, so a rebuild leaves git clean. Called
    // once the new scene is open, so the fog and ambient light of the scene open before cannot tint the pictures.
    public static partial class TestSceneBuilder
    {
        const string ItemIconDir = "Assets/DeadCounty/UI/Icons";
        const int IconSize = 128;
        const int IconSupersample = 4;
        const float IconMargin = 1.12f;
        // Summed RGB difference between the two shots above which a pixel is background.
        const int BackgroundDifference = 384;
        // Three-quarter view from above: the top, the front and the right side read as three tones.
        static readonly Quaternion IconView = Quaternion.Euler(28f, -35f, 0f);

        static void EnsureItemIcons(Dictionary<string, ItemDefinition> items)
        {
            Directory.CreateDirectory(ItemIconDir);
            var size = IconSize * IconSupersample;
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var shot = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var icon = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            try
            {
                var view = IconCamera(scene, target);
                IconLight(scene, "Key", Quaternion.Euler(50f, -10f, 0f), new Color(1f, 0.97f, 0.92f), 1.2f);
                IconLight(scene, "Fill", Quaternion.Euler(20f, -65f, 0f), new Color(0.82f, 0.88f, 1f), 0.35f);
                foreach (var (file, item) in items)
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(item.WorldModel, scene);
                    Frame(view, model);
                    var overBlack = Shoot(view, Color.black, shot);
                    var overWhite = Shoot(view, Color.white, shot);
                    Object.DestroyImmediate(model);
                    icon.SetPixels32(Compose(overBlack, overWhite, size));
                    item.Icon = SaveIcon($"{ItemIconDir}/IC_{file}.png", icon.EncodeToPNG());
                    EditorUtility.SetDirty(item);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(shot);
                Object.DestroyImmediate(icon);
            }
        }

        static Camera IconCamera(Scene scene, RenderTexture target)
        {
            var go = new GameObject("IconCamera");
            SceneManager.MoveGameObjectToScene(go, scene);
            var view = go.AddComponent<Camera>();
            view.scene = scene;
            view.enabled = false;
            view.orthographic = true;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.allowHDR = false;
            view.allowMSAA = false;
            view.targetTexture = target;
            view.transform.rotation = IconView;
            return view;
        }

        static void IconLight(Scene scene, string name, Quaternion rotation, Color color, float intensity)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = rotation;
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        // Orthographic, the model's bounds filling the square with a small margin whatever their shape.
        static void Frame(Camera view, GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var toView = Quaternion.Inverse(IconView);
            var half = 0f;
            for (var i = 0; i < 8; i++)
            {
                var corner = toView * Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                half = Mathf.Max(half, Mathf.Abs(corner.x), Mathf.Abs(corner.y));
            }
            var distance = bounds.extents.magnitude + 1f;
            view.orthographicSize = half * IconMargin;
            view.transform.position = bounds.center - view.transform.forward * distance;
            view.nearClipPlane = 0.01f;
            view.farClipPlane = distance * 2f;
        }

        static Color32[] Shoot(Camera view, Color background, Texture2D shot)
        {
            view.backgroundColor = background;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = view.targetTexture };
            if (RenderPipeline.SupportsRenderRequest(view, request)) RenderPipeline.SubmitRenderRequest(view, request);
            else view.Render();
            var active = RenderTexture.active;
            RenderTexture.active = view.targetTexture;
            shot.ReadPixels(new Rect(0, 0, shot.width, shot.height), 0, 0);
            RenderTexture.active = active;
            return shot.GetPixels32();
        }

        // Each icon pixel averages the item pixels of its block; its alpha is the share of the block they cover.
        static Color32[] Compose(Color32[] overBlack, Color32[] overWhite, int size)
        {
            var pixels = new Color32[IconSize * IconSize];
            for (var y = 0; y < IconSize; y++)
            for (var x = 0; x < IconSize; x++)
            {
                int r = 0, g = 0, b = 0, covered = 0;
                for (var dy = 0; dy < IconSupersample; dy++)
                for (var dx = 0; dx < IconSupersample; dx++)
                {
                    var i = (y * IconSupersample + dy) * size + x * IconSupersample + dx;
                    var k = overBlack[i];
                    var w = overWhite[i];
                    if (w.r - k.r + w.g - k.g + w.b - k.b > BackgroundDifference) continue;
                    r += k.r;
                    g += k.g;
                    b += k.b;
                    covered++;
                }
                pixels[y * IconSize + x] = covered == 0
                    ? new Color32(0, 0, 0, 0)
                    : new Color32((byte)(r / covered), (byte)(g / covered), (byte)(b / covered), (byte)(covered * 255 / (IconSupersample * IconSupersample)));
            }
            return pixels;
        }

        static Texture2D SaveIcon(string path, byte[] png)
        {
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(png))
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            // Small pictures: no mipmaps nor compression blur, and the colour bled into the clear edges so the
            // filtering does not draw a dark outline.
            if (!importer.alphaIsTransparency || importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp
                || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
