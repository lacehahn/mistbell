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
