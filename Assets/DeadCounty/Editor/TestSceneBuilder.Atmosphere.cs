using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        const string VolumeProfilePath = ArtDir + "/VP_TestMap.asset";
        static readonly Color FogColor = new(0.55f, 0.58f, 0.61f);

        // Overcast late afternoon: low warm sun, grey-blue fog and a desaturated grade, per the GDD mood.
        static void SetupAtmosphere()
        {
            var sun = Object.FindObjectsByType<Light>().First(l => l.type == LightType.Directional);
            sun.name = "Soleil";
            sun.transform.rotation = Quaternion.Euler(28f, -50f, 0f);
            sun.color = new Color(1f, 0.88f, 0.72f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;

            RenderSettings.sun = sun;
            RenderSettings.skybox = SkyMaterial();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0055f;
            RenderSettings.fogColor = FogColor;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.64f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.44f, 0.45f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.22f, 0.18f);

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = VolumeProfileAsset();

            // Over-the-shoulder view from the spawn, looking up Main Street.
            var cam = Camera.main;
            var eye = new Vector3(64.4f, 1.9f, -15f);
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(new Vector3(68f, 1.6f, 12f) - eye));
            cam.farClipPlane = 600f;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        static Material SkyMaterial()
        {
            var path = $"{ArtDir}/M_Proto_Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, path);
            }
            sky.SetColor("_SkyTint", new Color(0.52f, 0.55f, 0.6f));
            sky.SetColor("_GroundColor", FogColor);
            sky.SetFloat("_AtmosphereThickness", 1.3f);
            sky.SetFloat("_Exposure", 1.1f);
            sky.SetFloat("_SunSize", 0.03f);
            EditorUtility.SetDirty(sky);
            UsedMaterials.Add(path);
            return sky;
        }

        // Rebuilt in place so the asset GUID referenced by the scene stays stable.
        static VolumeProfile VolumeProfileAsset()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }
            foreach (var component in profile.components.ToList())
            {
                profile.Remove(component.GetType());
                Object.DestroyImmediate(component, true);
            }

            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>();
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }

            Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var color = Add<ColorAdjustments>();
            color.postExposure.Override(0.25f);
            color.contrast.Override(12f);
            color.saturation.Override(-25f);
            Add<WhiteBalance>().temperature.Override(-6f);
            var vignette = Add<Vignette>();
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);
            var bloom = Add<Bloom>();
            bloom.intensity.Override(0.4f);
            bloom.threshold.Override(1.1f);
            EditorUtility.SetDirty(profile);
            return profile;
        }
    }
}
