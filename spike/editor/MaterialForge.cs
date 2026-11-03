using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Canonical source: spike/editor/MaterialForge.cs. accept.sh syncs the Unity compile copy before building.
// Phase 1 ONLY: Snow / church exterior Stone / Door_Leaf Wood. No other materials are generated.
public static class MaterialForge
{
    public const int Size = 256;
    public const float SnowTexelsPerMeter = 20, StoneTexelsPerMeter = 28, WoodTexelsPerMeter = 24;
    const string Root = "Assets/Art/MaterialForge/";
    static readonly string[] Names = { "Snow", "Stone", "Wood" };
    static Color32 C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    static int Hash(int a, int b) { unchecked { uint n=(uint)(a*73856093 ^ b*19349663 ^ 731); n^=n>>13; n*=1274126177; return (int)(n&0x7fffffff); } }
    static Color32[] Ramp(int type)
    {
        string[][] colors = {
            new[]{"#DCE8F2","#EDF3F8","#F6FAFD"},
            new[]{"#8C7C66","#A8967C","#C2B49A"},
            new[]{"#4A3426","#6B4A32","#8A6D52"} };
        var ramp = new Color32[type==1 ? 9 : 3];
        for(int v=0;v<ramp.Length/3;v++) for(int s=0;s<3;s++)
        {
            var c=C(colors[type][s]); float gain=type==1 ? 1+(v-1)*.08f : 1;
            ramp[v*3+s]=new Color32((byte)Mathf.RoundToInt(c.r*gain),(byte)Mathf.RoundToInt(c.g*gain),(byte)Mathf.RoundToInt(c.b*gain),255);
        }
        return ramp;
    }
    static int Step(int type, int x, int y, out int variant)
    {
        // Duplicate the first boundary sample, making the exact wrap transition continuous.
        if(x==255) x=0; if(y==255) y=0;
        variant=0;
        if(type==0)
        {
            // Wind along world X. Periodic, drawn 1px contour jitter, no per-pixel photo noise.
            int edge=(int)Mathf.Round(4*Mathf.Sin(x*Mathf.PI/32)+2*Mathf.Sin(x*Mathf.PI/8));
            int band=((y+edge+256)%32);
            if(band==2 && x%31==9) return 0; // sparse blue-grey 1px specks
            return band<6 ? 0 : band<24 ? 1 : 2;
        }
        if(type==1)
        {
            int row=y/32, offset=(row%2)*32;
            int jitter=((x/8+row)%3==0)?1:0;
            int yy=(y+jitter)%32, xx=(x+offset+((y/8)%3==0?1:0))%64;
            variant=Hash((x+offset)/64%4,row)%3;
            if(xx==0 || yy==0) return 0; // one pixel mortar
            if((xx<3 || xx>61) && (yy<3 || yy>29)) return 0; // 2px corner bevel
            if(yy==1 || (xx==1 && yy<18)) return 2;
            // Broad hand painted facets; no photograph grain.
            return (xx>40+(yy/8)%2 && yy>15) ? 0 : 1;
        }
        int plank=x/32, local=x%32;
        int wobble=(int)Mathf.Round(3*Mathf.Sin(y*Mathf.PI/64 + plank*1.7f)+Mathf.Sin(y*Mathf.PI/8+plank));
        int line=10+wobble;
        if(local==0 || (plank%2==0 && local>=line && local<line+2)) return 0;
        if(local>=24+wobble) return 2;
        return 1;
    }
    static Texture2D Write(string name, Color32[] pixels, bool linear)
    {
        string path=Root+name+".png";
        var t=new Texture2D(Size,Size,TextureFormat.RGBA32,false,linear);
        t.SetPixels32(pixels); t.Apply(); File.WriteAllBytes(path,t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        var i=(TextureImporter)AssetImporter.GetAtPath(path);
        i.sRGBTexture=!linear; i.filterMode=FilterMode.Point; i.mipmapEnabled=false;
        i.textureCompression=TextureImporterCompression.Uncompressed; i.wrapMode=TextureWrapMode.Repeat;
        i.npotScale=TextureImporterNPOTScale.None; i.isReadable=true; i.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    [MenuItem("Mistbell/Forge three material samples")]
    public static void Generate()
    {
        Directory.CreateDirectory(Root);
        for(int type=0;type<3;type++)
        {
            var ramp=Ramp(type); var albedo=new Color32[Size*Size]; var rough=new Color32[Size*Size];
            for(int y=0;y<Size;y++) for(int x=0;x<Size;x++)
            {
                int s=Step(type,x,y,out int v); albedo[y*Size+x]=ramp[v*3+s];
                byte r=(byte)(s==0?250:s==1?242:234); rough[y*Size+x]=new Color32(r,r,r,255);
            }
            Write(Names[type]+"_Albedo",albedo,false); Write(Names[type]+"_Roughness",rough,true);
        }
    }
    static void Apply(Material m, int type)
    {
        m.shader=Shader.Find("Mistbell/TerrainHaze");
        m.SetFloat("_ForgeSurface",type+1);
        m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+Names[type]+"_Albedo.png"));
        m.SetTexture("_ForgeRoughness",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+Names[type]+"_Roughness.png"));
        float density=type==0?SnowTexelsPerMeter:type==1?StoneTexelsPerMeter:WoodTexelsPerMeter;
        // Shader uses dominant-axis world metres: density = texture width * repeats/metre.
        m.SetTextureScale("_BaseMap",Vector2.one*(density/Size));
        m.SetColor("_BaseColor",Color.white); m.SetFloat("_Smoothness",0); m.SetFloat("_Metallic",0);
        m.DisableKeyword("_NORMALMAP"); EditorUtility.SetDirty(m);
    }
    public static void ApplySamples(MeshCollider terrain, Transform church)
    {
        Generate();
        // Apply after indoor shell cloning: interior stone and all remaining material categories stay unchanged.
        Apply(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_GroundHaze.mat"),0);
        Apply(terrain.GetComponent<Renderer>().sharedMaterial,0);
        Apply(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_ChurchStone.mat"),1);
        Apply(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_TerraceAshlar.mat"),1);
        foreach(var r in church.GetComponentsInChildren<Renderer>()) if(r.name=="Door_Leaf") Apply(r.sharedMaterial,2);
        AssetDatabase.SaveAssets();
        ExportManifest();
    }
    [Serializable] class Entry { public string name, albedo, roughness, material, projection="dominant_axis_world_metres"; public float texels_per_meter, tiling_x, tiling_y; public int forge_surface; }
    [Serializable] class Manifest { public int size=Size; public string phase="three_samples_pending_user_review"; public Entry[] materials; }
    public static void ExportManifest()
    {
        string[] paths={"Assets/Settings/M_Snow2Terrain.mat","Assets/Settings/M_ChurchStone.mat","Assets/Settings/M_InteriorDoorWood.mat"};
        var data=new Manifest { materials=new Entry[3] };
        for(int i=0;i<3;i++)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(paths[i]); var scale=m.GetTextureScale("_BaseMap");
            data.materials[i]=new Entry {name=Names[i], projection=i==2?"dominant_axis_object_metres":"dominant_axis_world_metres", material=paths[i], albedo=Root+Names[i]+"_Albedo.png", roughness=Root+Names[i]+"_Roughness.png", texels_per_meter=Size*scale.x, tiling_x=scale.x, tiling_y=scale.y, forge_surface=(int)m.GetFloat("_ForgeSurface")};
        }
        File.WriteAllText(Root+"manifest.json",JsonUtility.ToJson(data,true));
    }
}
