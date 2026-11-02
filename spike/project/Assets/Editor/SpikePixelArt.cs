using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Deterministic, editor-only painted pixel assets. No external images or packages.
public static class SpikePixelArt
{
    const string Root = "Assets/Art/Textures/";
    static Color32 Hex(string value) { ColorUtility.TryParseHtmlString(value, out var c); return c; }
    static uint Hash(int x, int y, int seed)
    {
        unchecked {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }
    static Texture2D Save(string name, int size, Color32[] pixels)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels); tex.Apply();
        string path = Root + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var result = SpikeArt.ImportTex(path, false);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        // Clear any platform override so the explicit no-compression contract holds in both builds.
        importer.ClearPlatformTextureSettings("Standalone");
        importer.SaveAndReimport();
        if (result.width != size || result.height != size || importer.filterMode != FilterMode.Point
            || !importer.mipmapEnabled || importer.anisoLevel != 0
            || importer.textureCompression != TextureImporterCompression.Uncompressed)
            throw new System.InvalidOperationException("Pixel texture import failed: " + name);
        Debug.Log($"[PIXEL] {name} {size}x{size} Point Uncompressed mipmaps=true aniso=0");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    public static void Generate()
    {
        Directory.CreateDirectory(Root);
        Color32[] snow = { Hex("#F2F7FB"), Hex("#E6EFF7"), Hex("#D3E1EE"), Hex("#C0D2E4"), Hex("#A9C0D8") };
        var pixels = new Color32[64*64];
        for (int y=0;y<64;y++) for (int x=0;x<64;x++)
        {
            int index = (int)(Hash(x/4,y/4,9021)%5);
            // Sparse, broken diagonal wind strokes, never a continuous sinusoidal stripe.
            if (Hash(x/12,y/12,401)%11==0 && (x-y+64)%16<2) index = Mathf.Max(0,index-1);
            pixels[y*64+x] = snow[index];
        }
        Save("Snow_Pixel",64,pixels);
        Color32[] stone = { Hex("#AC8D66"), Hex("#C9A87C"), Hex("#E0BF94") };
        pixels = new Color32[128*128];
        for (int y=0;y<128;y++) for (int x=0;x<128;x++)
        {
            int row=y/10, sx=x+(row%2)*8, block=sx/16;
            uint h=Hash(block,row,3157);
            var c=stone[h%3];
            if (sx%16==0 || y%10==0) c=Hex("#6B5A47");
            else if (h%10==0 && Hash(x/2,y/2,555)%7<2) c=stone[0];
            pixels[y*128+x]=c;
        }
        Save("Stone_Pixel",128,pixels);
        Color32[] rock = { Hex("#514A42"), Hex("#6B6157"), Hex("#8D8172") };
        pixels = new Color32[64*64];
        for (int y=0;y<64;y++) for (int x=0;x<64;x++)
        {
            var c=rock[Hash(x/4,y/4,7159)%3];
            if (y>54 && Hash(x/2,y/2,805)%5==0) c=snow[1];
            pixels[y*64+x]=c;
        }
        Save("Rock_Pixel",64,pixels);
        InstallPalette();
    }
    public static Material RockMaterial(string path, float shade)
    {
        var material = new Material(Shader.Find("Mistbell/TerrainHaze"));
        material.name=Path.GetFileNameWithoutExtension(path);
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Rock_Pixel.png"));
        material.SetTextureScale("_BaseMap",Vector2.one*2f);
        material.SetColor("_BaseColor", new Color(shade,shade,shade,1));
        material.SetFloat("_RockOnly",1);
        material.SetFloat("_HazeStrength",0);
        material.SetFloat("_Smoothness",.02f);
        AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(material,path);
        return material;
    }
    static void InstallPalette()
    {
        // Quantize the result inside the existing tilt-shift pass, after the original blur.
        // This preserves its kernel/parameters and avoids an extra fullscreen copy.
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/MistbellRenderer.asset");
        for(int i=renderer.rendererFeatures.Count-1;i>=0;i--)
            if(renderer.rendererFeatures[i].name=="PixelPaletteFeature") renderer.rendererFeatures.RemoveAt(i);
        renderer.SetDirty(); EditorUtility.SetDirty(renderer);
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_TiltShift.mat");
        if(material==null) throw new System.InvalidOperationException("Missing existing tilt-shift material");
        material.SetFloat("_DitherStrength",.5f); EditorUtility.SetDirty(material);
        AssetDatabase.DeleteAsset("Assets/Settings/PixelPaletteFeature.asset");
        AssetDatabase.DeleteAsset("Assets/Settings/M_PixelPalette.mat");
        AssetDatabase.SaveAssets();
        Debug.Log("[PIXEL] post=after tiltshift in same pass palette=5bit/channel Bayer=4x4 strength=0.5");
    }
}
