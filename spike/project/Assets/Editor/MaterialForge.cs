using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Canonical source: spike/editor/MaterialForge.cs. accept.sh syncs the Unity compile copy before building.
// STYLE10: import four 1024px authored families; Snow/Wood/Rock/Grass PNGs stay frozen.
public static class MaterialForge
{
    public const int Size = 256;
    public const float SnowTexelsPerMeter = 20, StoneTexelsPerMeter = 28, WoodTexelsPerMeter = 24, SubTexelsPerMeter = 22;
    // Material-only exposure calibration for the dark photo-derived ramps under the existing ACES stack.
    // Tone acceptance is measured on rendered fixed ROIs, never on source-texture Lab.
    public const float ChurchExposure = 1.65f, SubExposure = 1.75f;
    const string Root = "Assets/Art/MaterialForge/";
    static readonly string[] Names = { "Snow", "Stone", "Wood", "Stone_Sub" };
    static Color32 C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    static int Hash(int a, int b) { unchecked { uint n=(uint)(a*73856093 ^ b*19349663 ^ 731); n^=n>>13; n*=1274126177; return (int)(n&0x7fffffff); } }
    static Color32 Gain(Color32 c, float gain) => new Color32((byte)Mathf.RoundToInt(c.r*gain),(byte)Mathf.RoundToInt(c.g*gain),(byte)Mathf.RoundToInt(c.b*gain),255);
    static Color32 Pixel(int type, int x, int y, out int step)
    {
        step=1;
        if(type==1 || type==3) return StonePixel(type==3,x,y,out step);
        if(x==255) x=0; if(y==255) y=0;
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
    // Integer texels, deterministic dimensions. Sub's 56px tile contains seven rubble courses.
    [Serializable] public class Block { public int row, col, x, y, w, h, tier; }
    static readonly Color32[] ChurchRamp={C("#6C6C6C"),C("#787878"),C("#848484"),C("#909090")};
    static readonly Color32[] SubRamp={C("#232323"),C("#363636"),C("#484848"),C("#565656")};
    static readonly System.Collections.Generic.List<Block>[] Blocks={MakeBlocks(false),MakeBlocks(true)};
    static int Tile(bool sub) => sub?56:Size;
    static int[] Partition(int length,int count,int min,int max,int seed)
    {
        var result=new int[count]; int rest=length;
        for(int i=0;i<count;i++)
        {
            int lo=Math.Max(min,rest-(count-i-1)*max), hi=Math.Min(max,rest-(count-i-1)*min);
            result[i]=lo+Hash(seed,i)%(hi-lo+1); rest-=result[i];
        }
        return result;
    }
    static System.Collections.Generic.List<Block> MakeBlocks(bool sub)
    {
        int size=Tile(sub); var result=new System.Collections.Generic.List<Block>();
        var heights=sub?new[]{6,9,8,6,9,9,9}:Partition(size,19,9,16,71);
        int y=0;
        for(int row=0;row<heights.Length;row++)
        {
            int count=sub?9:10;
            var widths=Partition(size,count,sub?4:16,sub?8:32,row+137);
            int x=Hash(row,29)%size;
            for(int col=0;col<count;col++)
            {
                // A predominantly dark field is required by the low-HF reference; the four
                // tiers are sampled with explicit weights rather than a uniform checkerboard.
                int roll=Hash(col+37,row+61)%100;
                int tier=sub?(roll<90?0:roll<95?1:roll<98?2:3):(roll<95?0:roll<98?1:roll<99?2:3);
                result.Add(new Block {row=row,col=col,x=x,y=y,w=widths[col],h=heights[row],tier=tier});
                x=(x+widths[col])%size;
            }
            y+=heights[row];
        }
        // Preserve sampled bright blocks, lifting only touching darkest neighbours.
        // This checks both horizontal and vertical adjacency, including texture wrap.
        if(!sub) foreach(var b in result) if(b.tier==3)
            foreach(var other in result) if(other.tier==0)
            {
                bool same=b.row==other.row && ((b.col-other.col+10)%10==1 || (other.col-b.col+10)%10==1);
                bool vertical=false;
                if((b.row-other.row+heights.Length)%heights.Length==1 ||
                   (other.row-b.row+heights.Length)%heights.Length==1)
                    for(int xx=0;xx<b.w;xx++) if((b.x+xx-other.x+size)%size<other.w) vertical=true;
                if(same || vertical) other.tier=1;
            }
        return result;
    }
    static Color32 StonePixel(bool sub,int x,int y,out int step)
    {
        int size=Tile(sub); x%=size; y%=size; step=0;
        var ramp=sub?SubRamp:ChurchRamp;
        foreach(var b in Blocks[sub?1:0]) if(y>=b.y && y<b.y+b.h)
        {
            int yy=y-b.y;
            // Each boundary has an independent 1px jog, shared by the two touching stones.
            // Four-texel stones pin their boundaries so the minimum size stays above .15m.
            int left=0,right=0;
            if(sub && yy>=b.h/2)
            {
                var peers=Blocks[1];
                Block prev=peers.Find(v=>v.row==b.row && v.col==(b.col+8)%9);
                Block next=peers.Find(v=>v.row==b.row && v.col==(b.col+1)%9);
                left=b.w==4 || prev.w==4?0:Hash(b.col+31,b.row+9)%2;
                right=b.w==4 || next.w==4?0:Hash(next.col+31,b.row+9)%2;
            }
            int xx=(x-b.x-left+size)%size;
            if(xx>=b.w+right-left)continue;
            if(yy==0 || xx==0 || (sub && b.w>=8 && xx==1)) return C(sub?"#1A1A1C":"#595959");
            step=1;
            if(!sub && yy==1)
            {
                step=2;
                // Top-facing 1px +12% bevel; no vertical highlight stripe.
                // Narrowing the lit edge retains the four-material sigma=1 HF budget.
                return Gain(ramp[b.tier],1.12f);
            }
            return ramp[b.tier];
        }
        return C(sub?"#1A1A1C":"#595959");
    }
    static Texture2D Write(string name, Color32[] pixels, bool linear)
    {
        string path=Root+name+".png";
        var t=new Texture2D(name.StartsWith("Stone_Sub")?56:Size,name.StartsWith("Stone_Sub")?56:Size,TextureFormat.RGBA32,false,linear);
        t.SetPixels32(pixels); t.Apply(); File.WriteAllBytes(path,t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        var i=(TextureImporter)AssetImporter.GetAtPath(path);
        i.sRGBTexture=!linear; i.filterMode=FilterMode.Point; i.mipmapEnabled=false;
        i.textureCompression=TextureImporterCompression.Uncompressed; i.wrapMode=TextureWrapMode.Repeat;
        i.npotScale=TextureImporterNPOTScale.None; i.isReadable=true; i.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    [MenuItem("Mistbell/Import STYLE10 material samples")]
    public static void Generate()
    {
        // forge_style10.py authors the four stone families before Unity.
        // Importing frozen Rock/Grass does not re-encode their PNGs.
        foreach(string name in new[]{"Stone","Stone_Sub","Roof_Slate","Rock","Grass","Stone_Interior"})
        foreach(string suffix in new[]{"_Albedo","_Roughness"})
        {
            string path=Root+name+suffix+".png";
            if(!File.Exists(path)) throw new FileNotFoundException("Run python3 spike/tools/forge_style10.py first",path);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var i=(TextureImporter)AssetImporter.GetAtPath(path);
            i.sRGBTexture=suffix=="_Albedo"; i.filterMode=FilterMode.Point; i.mipmapEnabled=false;
            i.textureCompression=TextureImporterCompression.Uncompressed; i.wrapMode=TextureWrapMode.Repeat;
            i.npotScale=TextureImporterNPOTScale.None; i.isReadable=true; i.SaveAndReimport();
        }
    }
    static void RolloutMaterial(Material m,string texture,float density,float gain=1)
    {
        // Calibrate authored Lit emission for this diffuse-only shader (0.40 gain);
        // direct 1:1 energy made the dome brighter than the old display baseline.
        // Existing forged walls never used Lit emission. No lights change.
        Color emission=m.name!="M_IndoorShell" && m.name!="M_InteriorWallFoot" &&
            m.shader.name=="Universal Render Pipeline/Lit" && m.IsKeywordEnabled("_EMISSION")
            ?m.GetColor("_EmissionColor")*.40f:Color.black;
        m.shader=Shader.Find("Mistbell/TerrainHaze");
        m.SetColor("_ForgeEmission",emission);
        m.SetFloat("_ForgeSurface",2); m.SetFloat("_ForgeTopGain",1);
        m.SetVector("_ForgeCopingPlane",Vector4.zero);
        m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+texture+"_Albedo.png"));
        m.SetTexture("_ForgeRoughness",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+texture+"_Roughness.png"));
        m.SetTextureScale("_BaseMap",Vector2.one*(density/AssetDatabase.LoadAssetAtPath<Texture2D>(Root+texture+"_Albedo.png").width)); m.SetTextureOffset("_BaseMap",Vector2.zero);
        m.SetColor("_BaseColor",new Color(gain,gain,gain,1));
        m.SetFloat("_Smoothness",0); m.SetFloat("_Metallic",0);
        m.DisableKeyword("_NORMALMAP"); m.DisableKeyword("_EMISSION");
        m.SetColor("_EmissionColor",Color.black); EditorUtility.SetDirty(m);
    }
    static Material ForegroundMaterial(string texture,float density,int mode)
    {
        var m=new Material(Shader.Find("Mistbell/ForegroundSnowfield")){name="M_STYLE6_"+texture};
        m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+texture+"_Albedo.png"));
        m.SetTextureScale("_BaseMap",Vector2.one*(density/AssetDatabase.LoadAssetAtPath<Texture2D>(Root+texture+"_Albedo.png").width));
        m.SetFloat("_Style6Density",density); m.SetFloat("_Style6Mode",mode);
        string path="Assets/Settings/"+m.name+".mat";
        AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m,path); return m;
    }
    static void ApplyRollout(ChurchInterior indoor)
    {
        RolloutMaterial(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_Roof.mat"),"Roof_Slate",112);
        foreach(string name in new[]{"M_WinterRock","M_WinterRockShade"})
            RolloutMaterial(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/"+name+".mat"),"Rock",24);
        RolloutMaterial(indoor.indoorStone,"Stone_Interior",112,1.23f);
        RolloutMaterial(indoor.wallFootStone,"Stone_Interior",112,1.23f);
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Settings"}))
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)); string n=m.name;
            if(n=="M_InteriorStone" || n=="M_InteriorDrum" || n=="M_InteriorDome" ||
               n=="M_InteriorAisle" || n.StartsWith("M_InteriorSlab_"))
                RolloutMaterial(m,"Stone_Interior",112,1.23f);
        }
        var grass=ForegroundMaterial("Grass",100f/3,1); var rock=ForegroundMaterial("Rock",24,2);
        var stems=new Material(Shader.Find("Mistbell/TerrainHaze")){name="M_STYLE6_Stems"};
        RolloutMaterial(stems,"Grass",100f/3);
        const string stemsPath="Assets/Settings/M_STYLE6_Stems.mat";
        AssetDatabase.DeleteAsset(stemsPath); AssetDatabase.CreateAsset(stems,stemsPath);
        var rockMat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_WinterRock.mat");
        foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if(r.name.StartsWith("FGD_Grass")) r.sharedMaterial=grass;
            else if(r.name=="FGD_Bedrock" || r.name=="FGD_Cairns") r.sharedMaterial=rock;
            else if(r.name.StartsWith("DryGrass_") || r.name.StartsWith("TrailShrub_")) r.sharedMaterial=stems;
            else if(r.name.StartsWith("Rock_") && r.transform.parent.name.StartsWith("TerraceRockpile_"))
            {
                var mats=r.sharedMaterials; mats[0]=rockMat; mats[2]=rockMat; r.sharedMaterials=mats;
            }
        }
    }
    static void Apply(Material m, int type, bool interior=false)
    {
        m.shader=Shader.Find("Mistbell/TerrainHaze");
        m.SetFloat("_ForgeSurface",type==3?2:type+1);
        m.SetFloat("_ForgeTopGain",1);
        m.SetVector("_ForgeCopingPlane",Vector4.zero);
        m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+Names[type]+"_Albedo.png"));
        m.SetTexture("_ForgeRoughness",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+Names[type]+"_Roughness.png"));
        float density=type==0?SnowTexelsPerMeter:type==1 || type==3?112:WoodTexelsPerMeter;
        // Shader uses dominant-axis world metres: density = texture width * repeats/metre.
        m.SetTextureScale("_BaseMap",Vector2.one*(density/AssetDatabase.LoadAssetAtPath<Texture2D>(Root+Names[type]+"_Albedo.png").width));
        m.SetTextureOffset("_BaseMap",Vector2.zero);
        // Material exposure compensates the quantized diffuse-only sample shading:
        // keep the door readable and snow above the existing frame luminance gate.
        float gain=interior?ChurchExposure*.82f:type==0?1.30f:type==2?1.20f:type==1?ChurchExposure:SubExposure;
        m.SetColor("_BaseColor",new Color(gain,gain,gain,1)); m.SetFloat("_Smoothness",0); m.SetFloat("_Metallic",0);
        m.DisableKeyword("_NORMALMAP"); EditorUtility.SetDirty(m);
    }
    public static void ApplySamples(MeshCollider terrain, Transform church)
    {
        Generate();
        // Existing four sample bindings remain unchanged; rollout is material-only.
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
        Material coping=null;
        foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if(coping==null && r.name.StartsWith("TerraceWall_"))
            {
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                Vector3 n=r.transform.TransformDirection(mesh.normals[0]); n/=n.y;
                Vector3 point=r.transform.TransformPoint(mesh.vertices[0]);
                // Existing coping rises .16m above the wall plane; material band extends .25m down.
                coping=new Material(sub) {name="Stone_Sub_Coping"};
                coping.SetVector("_ForgeCopingPlane",new Vector4(n.x,n.y,n.z,-Vector3.Dot(n,point)-.16f));
                const string path="Assets/Settings/Stone_Sub_Coping.mat";
                AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(coping,path);
                break;
            }
        }
        foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var materials=r.sharedMaterials; bool changed=false;
            for(int i=0;i<materials.Length;i++) if(materials[i]==terrace)
            { materials[i]=r.name.StartsWith("TerraceStep_")?stepMaterial:
                (r.name.StartsWith("TerraceWall_") || r.name.StartsWith("TerraceCap_"))?coping:sub; changed=true; }
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
        ApplyRollout(indoor);
        Style11Atlas.Apply(indoor);
        AssetDatabase.SaveAssets();
        ExportManifest(doorWidth);
    }
    [Serializable] class Entry { public string name, albedo, roughness, material, projection="dominant_axis_world_metres"; public float texels_per_meter, tiling_x, tiling_y; public int forge_surface; }
    [Serializable] class Door { public float width_metres, texels_per_meter=WoodTexelsPerMeter; }
    [Serializable] class Manifest { public Door door; public int size=1024; public string phase="style10_course_masonry"; public Entry[] materials; }
    public static void ExportManifest(float doorWidth)
    {
        string[] paths={"Assets/Settings/M_Snow2Terrain.mat","Assets/Settings/M_ChurchStone.mat","Assets/Settings/M_InteriorDoorWood.mat","Assets/Settings/Stone_Sub.mat"};
        var data=new Manifest { door=new Door {width_metres=doorWidth}, materials=new Entry[Names.Length] };
        for(int i=0;i<Names.Length;i++)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(paths[i]); var scale=m.GetTextureScale("_BaseMap");
            data.materials[i]=new Entry {name=Names[i], projection=i==2?"dominant_axis_object_metres":"dominant_axis_world_metres", material=paths[i], albedo=Root+Names[i]+"_Albedo.png", roughness=Root+Names[i]+"_Roughness.png", texels_per_meter=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+Names[i]+"_Albedo.png").width*scale.x, tiling_x=scale.x, tiling_y=scale.y, forge_surface=(int)m.GetFloat("_ForgeSurface")};
        }
        var entries=new System.Collections.Generic.List<Entry>(data.materials);
        string[] extraNames={"Roof_Slate","Rock","Grass","Stone_Interior"};
        string[] extraPaths={"M_Roof","M_WinterRock","M_STYLE6_Grass","M_IndoorShell"};
        float[] densities={112,24,100f/3,112};
        for(int i=0;i<extraNames.Length;i++) entries.Add(new Entry {
            name=extraNames[i], material="Assets/Settings/"+extraPaths[i]+".mat",
            albedo=Root+extraNames[i]+"_Albedo.png",roughness=Root+extraNames[i]+"_Roughness.png",
            texels_per_meter=densities[i],tiling_x=densities[i]/AssetDatabase.LoadAssetAtPath<Texture2D>(Root+extraNames[i]+"_Albedo.png").width,tiling_y=densities[i]/AssetDatabase.LoadAssetAtPath<Texture2D>(Root+extraNames[i]+"_Albedo.png").width,forge_surface=2 });
        data.materials=entries.ToArray();
        File.WriteAllText(Root+"manifest.json",JsonUtility.ToJson(data,true));
    }
}
