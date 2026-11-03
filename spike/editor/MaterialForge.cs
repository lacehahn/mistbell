using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Canonical source: spike/editor/MaterialForge.cs. accept.sh syncs the Unity compile copy before building.
// STYLE3: preserve Snow; church Stone, secondary Stone_Sub, and Door_Leaf Wood only.
public static class MaterialForge
{
    public const int Size = 256;
    public const float SnowTexelsPerMeter = 20, StoneTexelsPerMeter = 28, WoodTexelsPerMeter = 24, SubTexelsPerMeter = 24;
    const string Root = "Assets/Art/MaterialForge/";
    static readonly string[] Names = { "Snow", "Stone", "Wood", "Stone_Sub" };
    static Color32 C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    static int Hash(int a, int b) { unchecked { uint n=(uint)(a*73856093 ^ b*19349663 ^ 731); n^=n>>13; n*=1274126177; return (int)(n&0x7fffffff); } }
    static Color32 Gain(Color32 c, float gain) => new Color32((byte)Mathf.RoundToInt(c.r*gain),(byte)Mathf.RoundToInt(c.g*gain),(byte)Mathf.RoundToInt(c.b*gain),255);
    static Color32 Pixel(int type, int x, int y, out int step)
    {
        // Opposite border samples coincide. The pattern itself is periodic; no blurred border.
        if(x==255) x=0; if(y==255) y=0;
        step=1;
        if(type==1 || type==3)
        {
            bool sub=type==3;
            StoneCell(sub,x,y,out int row,out int col,out int local,out int yy,out int bw,out int bh);
            bool cool=CoolBlocks[sub?1:0][row,col];
            if(yy==0 || local==0) { step=0; return Gain(C("#8C7F6E"),sub?.8f:1); }
            int lowerEdge=bh-2;
            step=(yy==1 || local==1) ? 2 : (yy>=lowerEdge || local==bw-1) ? 0 : 1;
            // Three hard levels per block. Body is the darker family swatch; a narrow
            // bevel uses the middle swatch, with sparse worn bright edges. This retains
            // the specified six colours without making every block a high-contrast stripe.
            Color32 body=C(cool?"#7E848C":"#8C7C66");
            bool worn=Hash(col,row)%31==0;
            Color32 bevel=C(cool?(worn?"#A9AEB4":"#9298A0"):(worn?"#C2B49A":"#A8967C"));
            Color32 color=step==2?bevel:step==0?Gain(body,.94f):body;
            return Gain(color,sub?.8f:1);
        }
        if(type==2)
        {
            int plank=x*12/256, left=(plank*256+11)/12, local=x-left;
            if(local==0) { step=0; return C("#2E2018"); }
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
    // Same bond/bevel algorithm; secondary stone uses twenty flatter courses.
    public static void StoneCell(bool sub,int x,int y,out int row,out int col,out int local,out int yy,out int width,out int height)
    {
        if(x==255)x=0; if(y==255)y=0;
        x=(x+8)%256; y=(y+8)%256;
        int rows=sub?20:16;
        row=y*rows/256; int top=(row*256+rows-1)/rows;
        height=((row+1)*256+rows-1)/rows-top; yy=y-top;
        int xx=(x+(row%2)*12)%256; col=xx*11/256;
        int left=(col*256+10)/11;
        width=((col+1)*256+10)/11-left; local=xx-left;
    }
    static bool[,] MakeCool(bool sub)
    {
        int rows=sub?20:16; var result=new bool[rows,11];
        for(int row=0;row<rows;row++)
        {
            int count=sub?(row%2==0?5:6):4;
            // Random starting order, then prefer warm neighbours. Never horizontal/vertical bands.
            for(int i=0;i<count;i++)
            {
                int best=-1; long score=long.MaxValue;
                for(int col=0;col<11;col++) if(!result[row,col])
                {
                    int adjacent=(result[row,(col+10)%11]?1:0)+(result[row,(col+1)%11]?1:0);
                    long rank=adjacent*3000000000L+Hash(col+31,row+17+(sub?91:0));
                    if(rank<score) {score=rank;best=col;}
                }
                result[row,best]=true;
            }
        }
        return result;
    }
    static readonly bool[][,] CoolBlocks={MakeCool(false),MakeCool(true)};
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
    [MenuItem("Mistbell/Forge STYLE3 material samples")]
    public static void Generate()
    {
        Directory.CreateDirectory(Root);
        for(int type=0;type<Names.Length;type++)
        {
            var albedo=new Color32[Size*Size]; var rough=new Color32[Size*Size];
            for(int y=0;y<Size;y++) for(int x=0;x<Size;x++)
            {
                albedo[y*Size+x]=Pixel(type,x,y,out int s);
                byte r=(byte)(s==0?250:s==1?242:234); rough[y*Size+x]=new Color32(r,r,r,255);
            }
            // Snow v2 is an immutable visual reference, including encoded PNG bytes.
            if(type!=0 || !File.Exists(Root+"Snow_Albedo.png")) Write(Names[type]+"_Albedo",albedo,false);
            Write(Names[type]+"_Roughness",rough,true);
        }
    }
    static void Apply(Material m, int type, bool interior=false)
    {
        m.shader=Shader.Find("Mistbell/TerrainHaze");
        m.SetFloat("_ForgeSurface",type==3?2:type+1);
        m.SetFloat("_ForgeTopGain",1);
        m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+Names[type]+"_Albedo.png"));
        m.SetTexture("_ForgeRoughness",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+Names[type]+"_Roughness.png"));
        float density=interior?24:type==0?SnowTexelsPerMeter:type==1?StoneTexelsPerMeter:type==3?SubTexelsPerMeter:WoodTexelsPerMeter;
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
        // Apply only the approved samples; indoor walls reuse the secondary stone.
        Apply(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_GroundHaze.mat"),0);
        Apply(terrain.GetComponent<Renderer>().sharedMaterial,0);
        Apply(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_ChurchStone.mat"),1);
        var terrace=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_TerraceAshlar.mat");
        var sub=new Material(terrace) {name="Stone_Sub"};
        Apply(sub,3);
        const string subPath="Assets/Settings/Stone_Sub.mat";
        AssetDatabase.DeleteAsset(subPath); AssetDatabase.CreateAsset(sub,subPath);
        // Only existing step renderers receive the top-face polish; all geometry is retained.
        var stepMaterial=new Material(sub) {name="Stone_Sub_Steps"};
        stepMaterial.SetFloat("_ForgeTopGain",1.08f);
        const string stepPath="Assets/Settings/Stone_Sub_Steps.mat";
        AssetDatabase.DeleteAsset(stepPath); AssetDatabase.CreateAsset(stepMaterial,stepPath);
        foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var materials=r.sharedMaterials; bool changed=false;
            for(int i=0;i<materials.Length;i++) if(materials[i]==terrace)
            { materials[i]=r.name.StartsWith("TerraceStep_")?stepMaterial:sub; changed=true; }
            if(changed)r.sharedMaterials=materials;
        }
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
        Apply(indoor.indoorStone,3,true);
        Apply(indoor.wallFootStone,3,true);
        AssetDatabase.SaveAssets();
        ExportManifest(doorWidth);
    }
    [Serializable] class Entry { public string name, albedo, roughness, material, projection="dominant_axis_world_metres"; public float texels_per_meter, tiling_x, tiling_y; public int forge_surface; }
    [Serializable] class Door { public float width_metres, texels_per_meter=WoodTexelsPerMeter; }
    [Serializable] class Manifest { public Door door; public int size=Size; public string phase="style3_four_samples"; public Entry[] materials; }
    public static void ExportManifest(float doorWidth)
    {
        string[] paths={"Assets/Settings/M_Snow2Terrain.mat","Assets/Settings/M_ChurchStone.mat","Assets/Settings/M_InteriorDoorWood.mat","Assets/Settings/Stone_Sub.mat"};
        var data=new Manifest { door=new Door {width_metres=doorWidth}, materials=new Entry[Names.Length] };
        for(int i=0;i<Names.Length;i++)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(paths[i]); var scale=m.GetTextureScale("_BaseMap");
            data.materials[i]=new Entry {name=Names[i], projection=i==2?"dominant_axis_object_metres":"dominant_axis_world_metres", material=paths[i], albedo=Root+Names[i]+"_Albedo.png", roughness=Root+Names[i]+"_Roughness.png", texels_per_meter=Size*scale.x, tiling_x=scale.x, tiling_y=scale.y, forge_surface=(int)m.GetFloat("_ForgeSurface")};
        }
        File.WriteAllText(Root+"manifest.json",JsonUtility.ToJson(data,true));
    }
}
