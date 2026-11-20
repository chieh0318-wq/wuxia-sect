using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Wuxia.EditorTools
{
    public static class WuxiaSetup
    {
        [MenuItem("Wuxia/Set up courtyard rendering")]
        public static void Run()
        {
            Directory.CreateDirectory("Assets/Settings");
            Directory.CreateDirectory("Assets/Resources/Materials");
            AssetDatabase.Refresh();

            var renderer = LoadOrCreate("Assets/Settings/CourtyardRenderer.asset",
                ScriptableObject.CreateInstance<UniversalRendererData>);
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            if (!renderer.postProcessData)
                throw new InvalidOperationException("URP post-processing resources could not be loaded.");
            if (!renderer.rendererFeatures.Exists(feature => feature && feature.name == "Courtyard SSAO"))
            {
                var type = typeof(UniversalRenderPipelineAsset).Assembly.GetType(
                    "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion", true);
                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                feature.name = "Courtyard SSAO";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                var serialized = new SerializedObject(renderer);
                var map = serialized.FindProperty("m_RendererFeatureMap");
                map.arraySize = renderer.rendererFeatures.Count;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var occlusion = renderer.rendererFeatures.Find(feature => feature && feature.name == "Courtyard SSAO");
            var ao = new SerializedObject(occlusion).FindProperty("m_Settings");
            ao.FindPropertyRelative("Intensity").floatValue = 1.25f;
            ao.FindPropertyRelative("Radius").floatValue = .12f;
            ao.FindPropertyRelative("Downsample").boolValue = true;
            ao.serializedObject.ApplyModifiedPropertiesWithoutUndo();

            var pipeline = LoadOrCreate("Assets/Settings/CourtyardPipeline.asset",
                () => UniversalRenderPipelineAsset.Create(renderer));
            pipeline.supportsHDR = true;
            pipeline.shadowDistance = 120;
            pipeline.shadowCascadeCount = 4;
            var settings = new SerializedObject(pipeline);
            settings.FindProperty("m_SoftShadowsSupported").boolValue = true;
            settings.FindProperty("m_MainLightShadowmapResolution").intValue = 4096;
            settings.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            for (int level = 0; level < levels.arraySize; level++)
                levels.GetArrayElementAtIndex(level).FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            quality.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.colorSpace = ColorSpace.Linear;

            var profile = LoadOrCreate("Assets/Resources/CourtyardVolume.asset",
                ScriptableObject.CreateInstance<VolumeProfile>);
            Component<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            Component<Bloom>(profile).intensity.Override(0.12f);
            var grade = Component<ColorAdjustments>(profile);
            grade.saturation.Override(-10);
            grade.postExposure.Override(.27f);
            grade.colorFilter.Override(new Color(1f, 0.97f, 0.93f));
            Component<Vignette>(profile).intensity.Override(0.13f);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP Lit could not be loaded.");
            foreach (string surface in new[] { "Solid", "Tile", "Paving", "Earth", "Plaster", "Timber", "Grass" })
            {
                var material = LoadOrCreate($"Assets/Resources/Materials/{surface}.mat", () => new Material(shader));
                material.SetFloat("_Smoothness", 0.13f);
                material.enableInstancing = true;
                ApplyTexture(material, surface, "_BaseMap", "color");
                ApplyTexture(material, surface, "_BumpMap", "normal");
                if (material.GetTexture("_BumpMap"))
                    material.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(material);
            }
            foreach (string surface in new[] { "Fir", "Broadleaf" })
            {
                var material = LoadOrCreate($"Assets/Resources/Materials/{surface}.mat", () => new Material(shader));
                material.SetFloat("_Smoothness", .09f);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.SetFloat("_AlphaClip", 1);
                material.SetFloat("_Cutoff", .38f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
                material.SetOverrideTag("RenderType", "TransparentCutout");
                string path = $"Assets/Art/PolyHaven/Foliage/{surface}.png";
                var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (!atlas)
                    throw new InvalidOperationException($"Missing foliage atlas at {path}");
                material.SetTexture("_BaseMap", atlas);
                EditorUtility.SetDirty(material);
            }
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Courtyard rendering assets saved.");
        }

        private static T LoadOrCreate<T>(string path, Func<T> create) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>();
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(component);
            return component;
        }

        private static void ApplyTexture(Material material, string surface, string property, string suffix)
        {
            string path = $"Assets/Art/PolyHaven/{surface}/{suffix}.jpg";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture)
                return;
            if (suffix == "normal" && AssetImporter.GetAtPath(path) is TextureImporter importer &&
                importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            material.SetTexture(property, texture);
        }
    }
}
