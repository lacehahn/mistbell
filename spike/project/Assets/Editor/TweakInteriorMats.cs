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
            m.SetColor("_BaseColor", new Color(.47f, .37f, .27f, 1f));   // 暖石色：0.40 那版在冷环境光下仍显灰
            EditorUtility.SetDirty(m);
            n++;
        }
        var aisle = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_InteriorAisle.mat");
        if (aisle != null)
        {
            aisle.SetColor("_EmissionColor", new Color(.022f, .019f, .016f, 1f));
            // 探针发现：大多数地板块其实挂的是 M_InteriorAisle（底色 0.55/0.50/0.44 偏冷灰），
            // 之前只改了 M_InteriorSlab_*，所以地板一直是灰的。
            aisle.SetColor("_BaseColor", new Color(.44f, .355f, .265f, 1f));
            aisle.SetFloat("_Smoothness", .30f);
            EditorUtility.SetDirty(aisle);
            n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[TWEAK_INT] mats={n}");
    }
}
