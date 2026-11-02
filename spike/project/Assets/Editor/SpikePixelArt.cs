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
        importer.maxTextureSize = Mathf.Max(512,size);
        importer.SaveAndReimport();
        result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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

            pixels[y*64+x] = snow[index];
        }
        Save("Snow_Pixel",64,pixels);
        // Independent non-directional fields: 64 detail, 256 middle, 1024 low frequency.
        foreach (int size in new[] {256,1024})
        {
            pixels = new Color32[size*size];
            float cell = size == 256 ? 16f : 128f;
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float v = Mathf.PerlinNoise(x/cell+17.31f,y/cell+81.73f);
                byte c = (byte)Mathf.RoundToInt(v*255);
                pixels[y*size+x] = new Color32(c,c,c,255);
            }
            Save(size==256 ? "Snow_Medium" : "Snow_Low",size,pixels);
        }
        pixels = new Color32[128*128];
        Color baseStone = Hex("#B9A78E"), weather = Hex("#9C8B74");
        int weatherCount=0;
        for (int y=0;y<128;y++) for (int x=0;x<128;x++)
        {
            int row=y/16, sx=x+(row%2)*16, block=sx/32;
            float shade = .84f + (Hash(block,row,3157)%1001)/1000f*.32f;
            if(sx%32==0 || y%16==0) shade=.65f;
            Color c=baseStone*shade; c.a=1;
            // Exactly ~15% of texels, in irregular small patches.
            if(Hash(x/3,y/3,555)%1000<150) { c=Color.Lerp(c,weather,.4f); weatherCount++; }
            pixels[y*128+x]=c;
        }
        Save("Stone_Pixel",128,pixels);
        Debug.Log($"[SURFACE] stone=#B9A78E blocks=+-16% mortar=-35% block_width_px=32 weather={weatherCount/16384f:F3} snowBase=0.8m");
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
