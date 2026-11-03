using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 建立 URP 管线资产并设为默认：Assets/Settings/{MistbellURP.asset, MistbellRenderer.asset}
/// 用法：Unity -batchmode -quit -projectPath . -executeMethod SpikeURP.Run -logFile out.log
/// </summary>
public static class SpikeURP
{
    public static void TiltShift()
    {
        const string materialPath = "Assets/Settings/M_TiltShift.mat";
        const string featurePath = "Assets/Settings/TiltShiftFeature.asset";
        const string rendererPath = "Assets/Settings/MistbellRenderer.asset";
        Directory.CreateDirectory("Assets/Settings");
        var shader = Shader.Find("Mistbell/TiltShift");
        if (shader == null || !shader.isSupported)
            throw new System.InvalidOperationException("Mistbell/TiltShift shader missing or unsupported");
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "M_TiltShift" };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.shader = shader;
        material.SetFloat("_FocusCenter", 0.5f);
        material.SetFloat("_FocusBand", 0.26f);
        material.SetFloat("_Falloff", 0.24f);
        material.SetFloat("_MaskPower", 1.2f);
        material.SetFloat("_MaxRadius", 10f);
        EditorUtility.SetDirty(material);

        var feature = AssetDatabase.LoadAssetAtPath<FullScreenPassRendererFeature>(featurePath);
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = "TiltShiftFeature";
            AssetDatabase.CreateAsset(feature, featurePath);
        }
        feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
        // MTN15 foot palette mask reads depth to leave opaque geometry unchanged.
        feature.requirements = ScriptableRenderPassInput.Depth;
        feature.fetchColorBuffer = true;
        feature.bindDepthStencilAttachment = false;
        feature.passMaterial = material;
        feature.passIndex = 0;
        feature.SetActive(true);
        EditorUtility.SetDirty(feature);

        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
            throw new System.InvalidOperationException("Missing renderer: " + rendererPath);
        if (!renderer.rendererFeatures.Contains(feature))
            renderer.rendererFeatures.Add(feature);
        renderer.SetDirty();
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(featurePath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(rendererPath, ImportAssetOptions.ForceUpdate);

        renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        var loadedFeature = renderer.rendererFeatures.Count == 1
            ? renderer.rendererFeatures[0] as FullScreenPassRendererFeature : null;
        if (loadedFeature == null || loadedFeature.passMaterial == null
            || AssetDatabase.GetAssetPath(loadedFeature) != featurePath)
            throw new System.InvalidOperationException("Tilt-shift renderer serialization assertion failed");
        Debug.Log("[POST] tiltshift feature=" + AssetDatabase.GetAssetPath(loadedFeature)
            + " material=" + loadedFeature.passMaterial.name + " injection=" + loadedFeature.injectionPoint);
        Debug.Log("[POST] renderer features=" + renderer.rendererFeatures.Count
            + " entry0=" + loadedFeature.GetType().Name + " materialSet=" + (loadedFeature.passMaterial != null));
    }

    public static void SSAO()
    {
        const string featurePath = "Assets/Settings/SSAOFeature.asset";
        const string rendererPath = "Assets/Settings/MistbellRenderer.asset";
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
            throw new System.InvalidOperationException("Missing renderer: " + rendererPath);
        var feature = AssetDatabase.LoadAssetAtPath<ScreenSpaceAmbientOcclusion>(featurePath);
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            feature.name = "SSAOFeature";
            AssetDatabase.CreateAsset(feature, featurePath);
        }
        var settings = new SerializedObject(feature);
        settings.FindProperty("m_Settings.Intensity").floatValue = 2.2f;
        settings.FindProperty("m_Settings.DirectLightingStrength").floatValue = 0.85f;
        settings.FindProperty("m_Settings.Radius").floatValue = 0.5f;
        // TerrainHaze has DepthOnly but no DepthNormals pass: reconstruct from depth.
        settings.FindProperty("m_Settings.Source").enumValueIndex = 0;
        settings.FindProperty("m_Settings.AOMethod").enumValueIndex = 1;
        settings.FindProperty("m_Settings.Samples").enumValueIndex = 1;
        settings.FindProperty("m_Settings.BlurQuality").enumValueIndex = 0;
        settings.FindProperty("m_Settings.Falloff").floatValue = 100f;
        settings.FindProperty("m_Settings.Downsample").boolValue = false;
        settings.FindProperty("m_Settings.AfterOpaque").boolValue = false;
        settings.FindProperty("m_Settings.NormalSamples").enumValueIndex = 1;
        settings.ApplyModifiedPropertiesWithoutUndo();
        feature.SetActive(true);
        EditorUtility.SetDirty(feature);

        // Keep the existing tilt-shift object and its parameters; normalize only SSAO entries.
        for (int i = renderer.rendererFeatures.Count - 1; i >= 0; i--)
            if (renderer.rendererFeatures[i] is ScreenSpaceAmbientOcclusion)
                renderer.rendererFeatures.RemoveAt(i);
        renderer.rendererFeatures.Add(feature);
        renderer.SetDirty();
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(featurePath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(rendererPath, ImportAssetOptions.ForceUpdate);
        renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer.rendererFeatures.Count != 2
            || AssetDatabase.GetAssetPath(renderer.rendererFeatures[0]) != "Assets/Settings/TiltShiftFeature.asset"
            || AssetDatabase.GetAssetPath(renderer.rendererFeatures[1]) != featurePath
            || !AssetDatabase.IsMainAsset(renderer.rendererFeatures[1]))
            throw new System.InvalidOperationException("SSAO renderer serialization assertion failed");
        var saved = new SerializedObject(renderer.rendererFeatures[1]);
        if (saved.FindProperty("m_Settings.Source").enumValueIndex != 0)
            throw new System.InvalidOperationException("SSAO must use Depth");
        Debug.Log("[POST] ssao feature=" + featurePath + " intensity=2.2 radius=0.5 source=Depth");
        Debug.Log("[POST] renderer features=" + renderer.rendererFeatures.Count
            + " entry0=" + renderer.rendererFeatures[0].name + " entry1=" + renderer.rendererFeatures[1].name);
    }

    public static void Run()
    {
        const string dir = "Assets/Settings";
        Directory.CreateDirectory(dir);

        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        const string rendererPath = dir + "/MistbellRenderer.asset";
        AssetDatabase.DeleteAsset(rendererPath);
        AssetDatabase.CreateAsset(rendererData, rendererPath);

        var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
        pipeline.name = "MistbellURP";
        const string pipelinePath = dir + "/MistbellURP.asset";
        AssetDatabase.DeleteAsset(pipelinePath);
        AssetDatabase.CreateAsset(pipeline, pipelinePath);

        // 质量档：1080p、MSAA 关、HDR 开（Bloom 需要）
        pipeline.supportsHDR = true;
        pipeline.msaaSampleCount = 1;
        pipeline.renderScale = 1.0f;
        pipeline.shadowDistance = 80f;
        pipeline.supportsCameraDepthTexture = true;
        pipeline.supportsCameraOpaqueTexture = true;

        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SPIKE] renderer saved: " + rendererPath);
        Debug.Log("[SPIKE] pipeline saved: " + pipelinePath);
        Debug.Log("[SPIKE] current pipeline = " + (GraphicsSettings.currentRenderPipeline != null
            ? GraphicsSettings.currentRenderPipeline.GetType().Name
            : "NULL (仍在内置管线)"));
        Debug.Log("[SPIKE] urp version = " + pipeline.GetType().Assembly.GetName().Version);
        Debug.Log("[SPIKE] URP DONE");
    }
}
