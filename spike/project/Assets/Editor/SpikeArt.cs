using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HD-2D 场景构件工具：按世界尺寸（米）铺 UV 的盒体 + 像素材质导入 + 反照率归一。
///
/// 为什么不用 PrimitiveType.Cube：内置 Cube 每个面都是 0..1 UV，同一材质铺到
/// 6.5×6.4×9 m 的盒体上，正立面和侧立面的纹理密度差 1.4 倍，法线也跟着歪。
/// 这里把 UV 直接写成「米」，材质 tiling 取 1/每张贴图的米数，所有物体密度一致。
/// </summary>
public static class SpikeArt
{
    static Mesh _cubeMesh;

    /// <summary>内置 Cube 网格（正确的法线/绕序），只用来取几何骨架。</summary>
    static Mesh CubeMesh()
    {
        if (_cubeMesh == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
        }
        return _cubeMesh;
    }

    /// <summary>UV 以「米」为单位的盒体网格：U/V 取面内两个世界轴分量 × 对应边长。</summary>
    public static Mesh MeterUVBox(Vector3 size)
    {
        var m = Object.Instantiate(CubeMesh());
        m.name = "BoxMeterUV";
        var verts = m.vertices;
        var normals = m.normals;
        var uv = new Vector2[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            var p = verts[i];
            var n = normals[i];
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ax >= ay && ax >= az)        // 侧立面（±X）：U 沿 Z，V 沿 Y
                uv[i] = new Vector2(p.z * size.z, p.y * size.y);
            else if (ay >= ax && ay >= az)   // 顶/底面（±Y）：U 沿 X，V 沿 Z
                uv[i] = new Vector2(p.x * size.x, p.z * size.z);
            else                             // 正/背面（±Z）：U 沿 X，V 沿 Y
                uv[i] = new Vector2(p.x * size.x, p.y * size.y);
        }
        m.uv = uv;
        m.RecalculateTangents();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>建一个盒体（网格 UV 以米计），可选碰撞体。</summary>
    public static GameObject Box(Transform parent, string name, Vector3 size, Vector3 localPos,
                                 Material mat, bool collider = true, float yaw = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = size;        // 网格是单位立方体，尺寸靠缩放给（UV 已按米写好，缩放不会改纹理密度）
        go.AddComponent<MeshFilter>().sharedMesh = MeterUVBox(size);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        if (collider) go.AddComponent<BoxCollider>().size = Vector3.one;
        return go;
    }

    /// <summary>导入材质贴图：Point 过滤（保留像素感）+ 无压缩 + Repeat + mipmap（远处不闪）。</summary>
    public static Texture2D ImportTex(string path, bool isNormal)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null)
        {
            AssetDatabase.Refresh();                     // 让新落盘的贴图先被资产库看见
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ti = AssetImporter.GetAtPath(path) as TextureImporter;
        }
        if (ti == null)
        {
            Debug.LogError("[SPIKE] 贴图导入失败 " + path);
            return null;
        }
        ti.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = !isNormal;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = true;
        ti.anisoLevel = 0;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.isReadable = true;
        ti.maxTextureSize = 512;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float SrgbToLinear(float c)
    {
        return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
    }

    /// <summary>
    /// 把贴图平均亮度归一到目标反照率：贴图是 sRGB，进光照前会被解码成线性（暗一大截），
    /// 直接当颜色用会让墙面比原来的纯色块暗 2~3 倍。
    ///
    /// 两个坑（都踩过）：
    /// ① 按通道各自补偿会把低值通道（草的蓝）放大 6 倍，色噪被放大成斑点；
    /// ② 补偿过量时最亮 texel × tint 会 &gt; 1，反照率被削顶，画面发白一片。
    /// 所以：按「平均亮度」统一缩放（保留贴图本来色相），再按峰值压一次，保证 max ≤ 0.95。
    /// </summary>
    public static Color TintFor(Texture2D tex, Color targetAlbedo)
    {
        if (tex == null) return targetAlbedo;
        var px = tex.GetPixels();
        double r = 0, g = 0, b = 0;
        for (int i = 0; i < px.Length; i++)
        {
            r += SrgbToLinear(px[i].r);
            g += SrgbToLinear(px[i].g);
            b += SrgbToLinear(px[i].b);
        }
        float n = Mathf.Max(1, px.Length);
        float mr = (float)(r / n), mg = (float)(g / n), mb = (float)(b / n);
        float meanLum = 0.2126f * mr + 0.7152f * mg + 0.0722f * mb;

        var tint = new Color(targetAlbedo.r / Mathf.Max(mr, 0.004f),
                             targetAlbedo.g / Mathf.Max(mg, 0.004f),
                             targetAlbedo.b / Mathf.Max(mb, 0.004f));

        // 削顶守卫用 p99 而不是最大值：草叶/花朵这种孤立高光本来就会超 1，
        // 让 1% 的 texel 轻微削顶（看起来像露水高光）远比把整张贴图压暗对。
        var scaled = new float[px.Length];
        for (int i = 0; i < px.Length; i++)
        {
            float lr = SrgbToLinear(px[i].r) * tint.r;
            float lg = SrgbToLinear(px[i].g) * tint.g;
            float lb = SrgbToLinear(px[i].b) * tint.b;
            scaled[i] = Mathf.Max(lr, Mathf.Max(lg, lb));
        }
        System.Array.Sort(scaled);
        float p99 = scaled[Mathf.Clamp((int)(0.99f * scaled.Length), 0, scaled.Length - 1)];
        float guard = p99 > 1.0f ? 1.0f / p99 : 1f;
        tint *= guard;

        Debug.Log(string.Format("[SPIKE] tint {0} meanLin=({1:F3},{2:F3},{3:F3}) lum={4:F3} max|tint={5:F2} p99={6:F2} tint=({7:F2},{8:F2},{9:F2}) guard={10:F2} → albedo≈({11:F2},{12:F2},{13:F2})",
            tex.name, mr, mg, mb, meanLum, scaled[scaled.Length - 1], p99,
            tint.r, tint.g, tint.b, guard,
            mr * tint.r, mg * tint.g, mb * tint.b));
        return tint;
    }

    /// <summary>
    /// 建一个带颜色贴图 + 法线图的 URP/Lit 材质。
    /// metersPerTile：一张贴图覆盖的世界尺寸（米），材质 tiling 取 1/该值。
    /// </summary>
    public static Material MakeTextured(string matPath, string baseTexPath, string normalTexPath,
                                        float metersPerTile, Color targetAlbedo, float smoothness,
                                        float bumpScale = 1f, float metallic = 0f)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(matPath) };

        var baseTex = ImportTex(baseTexPath, false);
        var normalTex = string.IsNullOrEmpty(normalTexPath) ? null : ImportTex(normalTexPath, true);

        mat.SetTexture("_BaseMap", baseTex);
        mat.SetColor("_BaseColor", TintFor(baseTex, targetAlbedo));
        float tile = 1f / Mathf.Max(0.01f, metersPerTile);
        mat.SetTextureScale("_BaseMap", new Vector2(tile, tile));

        if (normalTex != null)
        {
            mat.SetTexture("_BumpMap", normalTex);
            mat.SetFloat("_BumpScale", bumpScale);
            mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);

        AssetDatabase.DeleteAsset(matPath);
        AssetDatabase.CreateAsset(mat, matPath);
        return mat;
    }
}
