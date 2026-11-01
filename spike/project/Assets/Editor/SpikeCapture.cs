using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// spike 截图闭环：在 batchmode 下渲染一帧并落盘 PNG，供 AI 读图判断。
/// 用法：Unity -batchmode -quit -projectPath . -executeMethod SpikeCapture.Run -logFile out.log
/// 注意：不要加 -nographics，否则没有渲染设备。
/// </summary>
public static class SpikeCapture
{
    const int W = 1920;
    const int H = 1080;

    public static void Run()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 相机：按 D03 方案 A（FOV 28°，俯视 30°，距离 22 m）
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        cam.fieldOfView = 28f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1500f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);

        var target = new Vector3(0f, 0.9f, 0f);        // 脚底上方 0.9 m
        var dir = Quaternion.Euler(30f, 45f, 0f) * Vector3.forward;
        camGo.transform.position = target - dir * 22f;
        camGo.transform.LookAt(target);

        // 光
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.3f;
        sun.color = new Color(1f, 0.96f, 0.9f);
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.25f, 0.28f, 0.32f);

        // 参照物：立方体（占位教堂）+ 一个标尺（1.75 m 高，用于量人物屏高）
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "PlaceholderChurch";
        block.transform.position = new Vector3(0f, 2f, 0f);
        block.transform.localScale = new Vector3(3f, 4f, 5f);

        var ruler = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ruler.name = "Scale_1m75";
        // 放在相机右前方，站在地面上；用射线检查是否被立方体挡住
        var right = Quaternion.Euler(30f, 45f, 0f) * Vector3.right;
        var rulerBase = target + right * 4.5f;
        rulerBase.y = 0f;
        ruler.transform.position = rulerBase + Vector3.up * 0.875f;
        ruler.transform.localScale = new Vector3(0.25f, 1.75f, 0.25f);
        Debug.Log("[SPIKE] ruler world pos = " + ruler.transform.position);
        Debug.Log("[SPIKE] ruler distance to camera = " + Vector3.Distance(camGo.transform.position, ruler.transform.position).ToString("F2") + " m");

        // 遮挡诊断：从相机向标尺中心投射，看先撞到谁
        Vector3 toRuler = ruler.transform.position - camGo.transform.position;
        if (Physics.Raycast(camGo.transform.position, toRuler.normalized, out RaycastHit hit, toRuler.magnitude + 0.5f))
            Debug.Log("[SPIKE] raycast first hit = " + hit.collider.name + " at " + hit.distance.ToString("F2") + " m (本帧无 Collider 时可能为生成物自带)");
        else
            Debug.Log("[SPIKE] raycast hit nothing (无 Collider 时正常)");

        Directory.CreateDirectory("Assets/Scenes");
        const string scenePath = "Assets/Scenes/Capture.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        // 渲染一帧到 RenderTexture，再读回 PNG
        Directory.CreateDirectory("Shots");
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 1;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;

        var png = tex.EncodeToPNG();
        var outPath = Path.GetFullPath("Shots/d3_A_01_plain.png");
        File.WriteAllBytes(outPath, png);

        Debug.Log("[SPIKE] shot written: " + outPath + " bytes=" + png.Length);

        // 顺便量一下标尺在画面中的像素高度，作为 D03 指标的自动化基础
        var vTop = cam.WorldToViewportPoint(ruler.transform.position + Vector3.up * 0.875f);
        var vBottom = cam.WorldToViewportPoint(ruler.transform.position - Vector3.up * 0.875f);
        float px = Mathf.Abs(vTop.y - vBottom.y) * H;
        Debug.Log("[SPIKE] ruler screen height = " + px.ToString("F1") + " px (D03 期望 ~172 px @ 1.75 m / 22 m / FOV 28)");
        Debug.Log("[SPIKE] ruler viewport x=" + vBottom.x.ToString("F3") + " y=" + vBottom.y.ToString("F3") + " (0~1 为在画面内)");

        Debug.Log("[SPIKE] CAPTURE DONE");
    }
}
