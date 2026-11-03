using UnityEditor;
using UnityEngine;

// 室内地板/走道材质的自发光收口：这些 .mat 是被场景直接引用的资产，
// ChurchInteriorBuilder 里的 Lit() 只是"首次创建"，之后改动不会回写资产，
// 所以地板一直保持着旧的 0.16 自发光 → 整片发白。这里直接改资产。
public static class TweakInteriorMats
{
    public static void Run()
    {
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("M_InteriorSlab_ t:Material"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null) continue;
            m.SetColor("_EmissionColor", new Color(.028f, .024f, .020f, 1f));
            m.SetFloat("_Smoothness", .30f);
            m.SetColor("_BaseColor", new Color(.40f, .345f, .28f, 1f));   // 暖石色，别偏冷灰
            EditorUtility.SetDirty(m);
            n++;
        }
        var aisle = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_InteriorAisle.mat");
        if (aisle != null)
        {
            aisle.SetColor("_EmissionColor", new Color(.022f, .019f, .016f, 1f));
            EditorUtility.SetDirty(aisle);
            n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[TWEAK_INT] mats={n}");
    }
}
