using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// spike 闭环验证脚本：建最小场景 → 出 macOS 包 → 出 Windows 包（Mono）。
/// 用法：Unity -batchmode -quit -projectPath . -executeMethod SpikeBuild.Run -logFile out.log
/// </summary>
public static class SpikeBuild
{
    public static void Run()
    {
        Debug.Log("[SPIKE] unity=" + Application.unityVersion);

        // 1. 最小场景：相机 + 平行光 + 一个立方体
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 1.7f, -22f);
        camGo.transform.rotation = Quaternion.Euler(30f, 0f, 0f);

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.2f;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "PlaceholderChurch";
        cube.transform.position = new Vector3(0f, 1f, 0f);

        // 优先用美术场景（若已生成），否则回退到最小场景
        string scenePath = File.Exists("Assets/Scenes/ArtSpike.unity")
            ? "Assets/Scenes/ArtSpike.unity"
            : "Assets/Scenes/Spike.unity";
        Debug.Log("[SPIKE] build scene = " + scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        Debug.Log("[SPIKE] scene saved: " + scenePath);

        // 2. 产品设置
        PlayerSettings.productName = "Mistbell";
        PlayerSettings.companyName = "Lot";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.runInBackground = true;

        // 3. macOS 构建
        Directory.CreateDirectory("Builds/Mac");
        var mac = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = "Builds/Mac/Mistbell.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        });
        Debug.Log("[SPIKE] macOS: result=" + mac.summary.result + " errors=" + mac.summary.totalErrors + " size=" + mac.summary.totalSize);

        // 4. Windows 构建（Mono 后端；本机没有 Windows IL2CPP 模块）
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        Directory.CreateDirectory("Builds/Win");
        var win = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = "Builds/Win/Mistbell.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        Debug.Log("[SPIKE] windows: result=" + win.summary.result + " errors=" + win.summary.totalErrors + " size=" + win.summary.totalSize);

        Debug.Log("[SPIKE] DONE");
    }
}
