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
    static Color32 Gain(Color32 c, float gain) => new Color32((byte)Mathf.RoundToInt(c.r*gain),(byte)Mathf.RoundToInt(c.g*gain),(byte)Mathf.RoundToInt(c.b*gain),255);
    static Color32 Pixel(int type, int x, int y, out int step)
    {
        // Opposite border samples coincide. The pattern itself is periodic; no blurred border.
        if(x==255) x=0; if(y==255) y=0;
        step=1;
        if(type==1)
        {
            // Sixteen 16px courses, eleven 23/24px blocks per course, half-block bond.
            x=(x+8)%256; y=(y+8)%256;
            int row=y/16, yy=y%16;
            int xx=(x+(row%2)*12)%256, col=xx*11/256;
            int left=(col*256+10)/11;
            int local=xx-left;
            if(yy==0 || local==0) { step=0; return C("#5E5245"); }
            float gain=1+(Hash(col,row)%3-1)*.08f;
            // Exactly three interior levels: 1px upper bevel, body, 1–2px lower face.
            // Lower contour varies by one texel per block; no random internal steps.
            int lowerEdge=14+Hash(col,row)%2;
            step=yy==1 ? 2 : yy>=lowerEdge ? 0 : 1;
            return Gain(C(step==2?"#8C7A66":step==0?"#6E6050":"#7D6D5B"),gain);
        }
        if(type==2)
        {
            int plank=x*12/256, left=(plank*256+11)/12, local=x-left;
            if(local==0) { step=0; return C("#3A281D"); }
            // Independent long vertical strokes per board. One pixel wobble, no marble swirls.
            int wobble=(Hash(plank,y/16)%3)-1;
            int grain=7+(Hash(plank,23)%3)+wobble;
            if(local>=grain && local<grain+4) { step=0; return C("#3A281D"); }
            step=local>=20 ? 2 : 1;
            return C(step==2?"#6B4A32":"#4C3728");
        }
        // Wind-shaped, hard-edged snow facets and short 3–6px blue shadow clusters.
        int edge=(int)Mathf.Round(3*Mathf.Sin(x*Mathf.PI/32)+Mathf.Sin(x*Mathf.PI/8));
        int band=(y+edge+256)%64;
        step=band<14?0:band<48?1:2;
        Color32 snow=C(step==0?"#DCE8F2":step==1?"#EDF3F8":"#F6FAFD");
        int cx=x/8, cy=y/8, id=Hash(cx,cy);
        int bx=1+id%3, by=1+(id/7)%3;
        int width=3+(id/17)%4, height=3+(id/31)%4;
        if(id%4!=0 && x%8>=bx && x%8<bx+width && y%8>=by && y%8<by+height)
        { step=0; snow=C("#D5E4F0"); }
        // One or two isolated 1px accents per 8x8 cluster, about 2.1% coverage.
        if((x%8==1+id%2 && y%8==1+(id/11)%3) || (id%3==0 && x%8==5+(id/13)%2 && y%8==4+(id/19)%3)) { step=0; snow=C("#C3D3E3"); }
        return snow;
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
            var albedo=new Color32[Size*Size]; var rough=new Color32[Size*Size];
            for(int y=0;y<Size;y++) for(int x=0;x<Size;x++)
            {
                albedo[y*Size+x]=Pixel(type,x,y,out int s);
                byte r=(byte)(s==0?250:s==1?242:234); rough[y*Size+x]=new Color32(r,r,r,255);
            }
            Write(Names[type]+"_Albedo",albedo,false); Write(Names[type]+"_Roughness",rough,true);
        }
    }
    static void Apply(Material m, int type, bool interior=false)
    {
        m.shader=Shader.Find("Mistbell/TerrainHaze");
        m.SetFloat("_ForgeSurface",type+1);
        m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+Names[type]+"_Albedo.png"));
        m.SetTexture("_ForgeRoughness",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+Names[type]+"_Roughness.png"));
        float density=interior?24:type==0?SnowTexelsPerMeter:type==1?StoneTexelsPerMeter:WoodTexelsPerMeter;
        // Shader uses dominant-axis world metres: density = texture width * repeats/metre.
        m.SetTextureScale("_BaseMap",Vector2.one*(density/Size));
        m.SetTextureOffset("_BaseMap",Vector2.zero);
        // Material exposure compensates the quantized diffuse-only sample shading:
        // keep the door readable and snow above the existing frame luminance gate.
        float gain=interior?.82f:type==0?1.30f:type==2?1.20f:1;
        m.SetColor("_BaseColor",new Color(gain,gain,gain,1)); m.SetFloat("_Smoothness",0); m.SetFloat("_Metallic",0);
        m.DisableKeyword("_NORMALMAP"); EditorUtility.SetDirty(m);
    }
    public static void ApplySamples(MeshCollider terrain, Transform church)
    {
        Generate();
        // Apply only the three samples plus the same stone logic on the indoor shell.
        Apply(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_GroundHaze.mat"),0);
        Apply(terrain.GetComponent<Renderer>().sharedMaterial,0);
        Apply(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_ChurchStone.mat"),1);
        Apply(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_TerraceAshlar.mat"),1);
        float doorWidth=0;
        foreach(var r in church.GetComponentsInChildren<Renderer>()) if(r.name=="Door_Leaf")
        {
            Apply(r.sharedMaterial,2);
            // Anchor the board grid to the leaf left edge (61.44 texels across 2.56m).
            var bounds=r.GetComponent<MeshFilter>().sharedMesh.bounds;
            var scale=r.transform.lossyScale;
            doorWidth=bounds.size.x*scale.x;
            r.sharedMaterial.SetTextureOffset("_BaseMap",new Vector2(-bounds.min.x*scale.x*WoodTexelsPerMeter/Size,0));
        }
        var indoor=church.GetComponent<ChurchInterior>();
        Apply(indoor.indoorStone,1,true);
        Apply(indoor.wallFootStone,1,true);
        AssetDatabase.SaveAssets();
        ExportManifest(doorWidth);
    }
    [Serializable] class Entry { public string name, albedo, roughness, material, projection="dominant_axis_world_metres"; public float texels_per_meter, tiling_x, tiling_y; public int forge_surface; }
    [Serializable] class Door { public float width_metres, texels_per_meter=WoodTexelsPerMeter; }
    [Serializable] class Manifest { public Door door; public int size=Size; public string phase="three_samples_pending_user_review"; public Entry[] materials; }
    public static void ExportManifest(float doorWidth)
    {
        string[] paths={"Assets/Settings/M_Snow2Terrain.mat","Assets/Settings/M_ChurchStone.mat","Assets/Settings/M_InteriorDoorWood.mat"};
        var data=new Manifest { door=new Door {width_metres=doorWidth}, materials=new Entry[3] };
        for(int i=0;i<3;i++)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(paths[i]); var scale=m.GetTextureScale("_BaseMap");
            data.materials[i]=new Entry {name=Names[i], projection=i==2?"dominant_axis_object_metres":"dominant_axis_world_metres", material=paths[i], albedo=Root+Names[i]+"_Albedo.png", roughness=Root+Names[i]+"_Roughness.png", texels_per_meter=Size*scale.x, tiling_x=scale.x, tiling_y=scale.y, forge_surface=(int)m.GetFloat("_ForgeSurface")};
        }
        File.WriteAllText(Root+"manifest.json",JsonUtility.ToJson(data,true));
    }
}
