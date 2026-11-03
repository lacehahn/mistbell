using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 一次性诊断：列出场景里"又高又窄"的渲染体（bounds 高度 > 15 m 且水平尺寸 < 1.5 m）。
/// 用于定位截图上那条从天空垂到雪地的细竖线到底是哪个物件，还是天空穹顶的接缝。
/// 用法：Unity -batchmode -quit -projectPath spike/project -executeMethod ProbeTallProps.Run
/// </summary>
public static class ProbeTallProps
{
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[TALL] scan start");
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");   // batchmode 默认不打开场景，不打开就什么都查不到
        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            sb.AppendLine($"[TALL] particles name={ps.name} root={ps.transform.root.name} pos={ps.transform.position}");
        foreach (var lr in Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None))
            sb.AppendLine($"[TALL] line name={lr.name} pos={lr.transform.position}");
        foreach (var tr in Object.FindObjectsByType<TrailRenderer>(FindObjectsSortMode.None))
            sb.AppendLine($"[TALL] trail name={tr.name} pos={tr.transform.position}");
        int count = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var b = r.bounds;
            bool tall = b.size.y > 3f && b.size.x < 2.0f && b.size.z < 2.0f;
            if (!tall) continue;
            count++;
            sb.AppendLine($"[TALL] name={r.name} size=({b.size.x:F2},{b.size.y:F2},{b.size.z:F2}) " +
                          $"center=({b.center.x:F1},{b.center.y:F1},{b.center.z:F1}) " +
                          $"root={r.transform.root.name} active={r.enabled}");
        }
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if (r.name.StartsWith("TerraceWall_") || r.name.StartsWith("TerraceStep_"))
                sb.AppendLine($"[MAT] name={r.name} mat={(r.sharedMaterial!=null?r.sharedMaterial.name:"null")} shader={(r.sharedMaterial!=null&&r.sharedMaterial.shader!=null?r.sharedMaterial.shader.name:"null")}");
        sb.AppendLine($"[TALL] done count={count}");
        Debug.Log(sb.ToString());
    }
}
