using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 一次性探针：查室内地面到底是哪个渲染器/材质在画，以及材质当前的底色与自发光。
// 用途：室内地板改了颜色却不生效时，先确认"改的材质"是不是"画的材质"。
public static class QuickProbe
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        int n = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            string name = r.name;
            if (!(name.Contains("Floor") || name.Contains("Aisle") || name.Contains("Nave") || name.Contains("Slab"))) continue;
            var m = r.sharedMaterial;
            string col = "n/a", emi = "n/a", bm = "n/a", sh = "n/a";
            if (m != null)
            {
                if (m.HasProperty("_BaseColor")) { var c = m.GetColor("_BaseColor"); col = $"{c.r:F2}/{c.g:F2}/{c.b:F2}"; }
                if (m.HasProperty("_EmissionColor")) { var c = m.GetColor("_EmissionColor"); emi = $"{c.r:F2}/{c.g:F2}/{c.b:F2}"; }
                if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) bm = m.GetTexture("_BaseMap").name;
                sh = m.shader != null ? m.shader.name : "null";
            }
            Debug.Log($"[FLOORPROBE] {name} mat={(m != null ? m.name : "null")} shader={sh} base={col} emis={emi} map={bm} boundsY={r.bounds.min.y:F2}");
            if (++n >= 14) break;
        }
        Debug.Log($"[FLOORPROBE] total={n}");
    }
}
