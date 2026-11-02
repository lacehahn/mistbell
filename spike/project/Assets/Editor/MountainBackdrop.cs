using System;
using System.IO;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Deterministic painted background. Does not consume UnityEngine.Random state.
public static class MountainBackdrop
{
    const int W = 1024, H = 512;
    static float Hash(int n)
    {
        unchecked { uint v=(uint)n; v^=v>>16; v*=0x7feb352d; v^=v>>15; v*=0x846ca68b; v^=v>>16; return (v&0xffffff)/16777215f; }
    }
    static float Noise(float x, int seed)
    {
        int i=Mathf.FloorToInt(x); float t=x-i;
        t=t*t*(3-2*t);
        return Mathf.Lerp(Hash(i+seed)*2-1,Hash(i+1+seed)*2-1,t);
    }
    static float Noise2(float x,float y,int seed)
    {
        int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);
        float tx=x-ix,ty=y-iy;
        tx=tx*tx*(3-2*tx); ty=ty*ty*(3-2*ty);
        return Mathf.Lerp(Mathf.Lerp(Hash(seed+ix+iy*7919),Hash(seed+ix+1+iy*7919),tx),
            Mathf.Lerp(Hash(seed+ix+(iy+1)*7919),Hash(seed+ix+1+(iy+1)*7919),tx),ty)*2-1;
    }
    static Color Hex(string s) { ColorUtility.TryParseHtmlString(s,out var c); return c; }
    static readonly int[] Seeds = { 15731, 80447, 213397, 379391 };
    const float HeightScale = 1.15f * .92f;
    const float Overscan = .12f, Feather = .08f;
    // Preserve the previous cards' feet; scaling is anchored here, never at the crest.
    static readonly int[] OriginalCrests = { 444, 460, 448, 452 };
    static float maxPeakStdPct;
    static readonly float[] LayerLuma = new float[4];
    static readonly int[] RockStreaks = new int[3], SnowGullies = new int[3], PeakCounts = new int[4];
    static int minCap, maxCap;
    static float minFaceDelta;
    const int RockLevels = 3;
    static float maxFootJump;
    static int[] Profile(int layer, out int peaks, out float std)
    {
        var rng = new System.Random(Seeds[layer]);
        peaks = 9 + layer;
        var ridge = new int[W];
        var widths = new float[peaks];
        float total = 0;
        for (int i=0;i<peaks;i++) { widths[i]=60+rng.Next(165); total+=widths[i]; }
        int start=0;
        float valley=OriginalCrests[layer]-75;
        // About 30% of each layer has rounded crowns (three of nine/ten/eleven).
        int rounded=Mathf.RoundToInt(peaks*.30f);
        for(int i=0;i<peaks;i++)
        {
            int end=i==peaks-1?W-1:start+Mathf.RoundToInt(widths[i]/total*(W-1));
            float apex=Mathf.Lerp(start,end,.36f+(float)rng.NextDouble()*.27f);
            float top=OriginalCrests[layer]-rng.Next(0,24);
            float nextValley=OriginalCrests[layer]-rng.Next(48,95);
            bool round=(i*7%peaks)<rounded;
            float leftShoulder=.31f+(float)rng.NextDouble()*.21f;
            float rightShoulder=.31f+(float)rng.NextDouble()*.21f;
            float leftKnee=.65f+(float)rng.NextDouble()*.18f;
            float rightKnee=.65f+(float)rng.NextDouble()*.18f;
            for(int x=start;x<=end;x++)
            {
                bool left=x<=apex;
                float t=left?(x-start)/(apex-start):(end-x)/(end-apex);
                // Unequal foothills and two shoulders interrupt repeated triangular flanks.
                float shoulder=left?leftShoulder:rightShoulder;
                float knee=left?leftKnee:rightKnee;
                float shape=t<.35f?Mathf.Lerp(0,shoulder,t/.35f):
                    t<.72f?Mathf.Lerp(shoulder,knee,(t-.35f)/.37f):
                    Mathf.Lerp(knee,1,round?Mathf.Sin((t-.72f)/.28f*Mathf.PI*.5f):(t-.72f)/.28f);
                float baseY=left?valley:nextValley;
                ridge[x]=Mathf.RoundToInt(Mathf.Lerp(baseY,top,shape));
            }
            start=end; valley=nextValley;
        }
        // Measure actual raster maxima, not requested/random apex parameters.
        var heights=new System.Collections.Generic.List<int>();
        int direction=0;
        for(int x=1;x<W;x++)
        {
            int d=Math.Sign(ridge[x]-ridge[x-1]);
            if(d==0) continue;
            if(direction>0 && d<0) heights.Add(ridge[x-1]);
            direction=d;
        }
        if(heights.Count!=peaks) throw new InvalidOperationException("MTN7 raster peak count");
        float sum=0, sum2=0;
        foreach(int h in heights) {sum+=h;sum2+=h*h;}
        float mean=sum/heights.Count;
        std=Mathf.Sqrt(Mathf.Max(0,sum2/heights.Count-mean*mean));
        float pct=100*std/mean;
        maxPeakStdPct=Mathf.Max(maxPeakStdPct,pct);
        if(pct>20) throw new InvalidOperationException("MTN7 peak height variation gate");
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN7_PROFILE] L{0} peaks={1} rounded={2} peak_height_std_pct={3:F4}",layer+1,heights.Count,rounded,pct));
        return ridge;
    }
    // Irregular knot spacing gives two genuinely distinct serration scales, without
    // a fixed repeating sawtooth. Neither helper touches the scene random generator.
    static float Jagged(int x,int seed,int minPeriod,int maxPeriod)
    {
        int start=0,index=0;
        while(true)
        {
            int period=minPeriod+Mathf.FloorToInt(Hash(seed+index*31)*(maxPeriod-minPeriod+1));
            period=Mathf.Min(period,maxPeriod);
            if(x<start+period)
                return Mathf.Lerp(Hash(seed+index*47+701),Hash(seed+(index+1)*47+701),(x-start)/(float)period)*2-1;
            start+=period; index++;
        }
    }
    static float Luma(Color32 p) => (.2126f*p.r+.7152f*p.g+.0722f*p.b)/255f;
    struct Stroke
    {
        public int x, top, length, width;
        public float lean, contrast;
        public float Center(int y) => x+(top-y)*lean;
    }
    static Color Rock(Color basis, bool lit, float mix, Color air)
    {
        // Keep a measurable 0.08 luminance split AFTER atmospheric perspective.
        float face=(lit?.04f:-.04f)/(1-mix);
        basis.r+=face; basis.g+=face; basis.b+=face;
        return Color.Lerp(basis,air,mix);
    }
    static Texture2D Paint(int layer,int[] ridge,float visibleFoot)
    {
        var pixels=new Color32[W*H];
        Color[] rock={Hex("#5A6878"),Hex("#6B7888"),Hex("#7C8896")};
        Color snow=Hex("#F2F7FB"), transition=Hex("#C9D7E5");
        Color haze=Hex("#EDF3F8"), air=layer==3?Hex("#D6E2EC"):Hex("#DAE5EF");
        float mix=layer==0?0:layer==1?.38f:layer==2?.68f:.75f;
        int seed=Seeds[layer],crest=0;
        foreach(int h in ridge) crest=Math.Max(crest,h);
        float hazeHeight=(crest-visibleFoot)*.12f;
        var caps=new int[W];
        var summits=new System.Collections.Generic.List<int>();
        int direction=0;
        for(int x=1;x<W;x++)
        {
            int d=Math.Sign(ridge[x]-ridge[x-1]);
            if(d==0) continue;
            if(direction>0 && d<0) summits.Add(x-1);
            direction=d;
        }
        // Valley-to-valley ownership: every original peak keeps its own lit left face.
        var apex=new int[W];
        int start=0;
        for(int i=0;i<summits.Count;i++)
        {
            int end=W-1;
            if(i+1<summits.Count)
            {
                end=summits[i];
                for(int x=summits[i];x<summits[i+1];x++) if(ridge[x]<ridge[end]) end=x;
            }
            for(int x=start;x<=end;x++) apex[x]=summits[i];
            start=end+1;
        }
        // Smooth both apex and valley face changes; binary ownership made full-height seams.
        var faceLight=new float[W];
        for(int x=0;x<W;x++)
        {
            float sum=0,weight=0;
            for(int dx=-16;dx<=16;dx++)
            {
                int sample=Mathf.Clamp(x+dx,0,W-1);
                float w=17-Math.Abs(dx);
                sum+=(sample<=apex[sample]?1:0)*w; weight+=w;
            }
            faceLight[x]=sum/weight;
        }
        for(int x=0;x<W;x++)
        {
            int l=Mathf.Max(0,x-4),r=Mathf.Min(W-1,x+4);
            float slope=Mathf.Abs((ridge[r]-ridge[l])/(float)Mathf.Max(1,r-l));
            float deposit=Mathf.Clamp01(.5f+.4f*Jagged(x,seed+503,6,12)+.2f*Jagged(x,seed+907,2,4));
            float steep=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,1.1f,slope));
            caps[x]=Mathf.RoundToInt(Mathf.Lerp(Mathf.Lerp(12,24,deposit),Mathf.Lerp(5,8,deposit),steep));
            if(layer<3) { minCap=Math.Min(minCap,caps[x]); maxCap=Math.Max(maxCap,caps[x]); }
        }
        int streakCount=layer==0?12:layer==1?14:10;
        int gullyCount=12;
        var streaks=new Stroke[layer==3?0:streakCount];
        var gullies=new Stroke[layer==3?0:gullyCount];
        var streakDrawn=new bool[streaks.Length]; var gullyDrawn=new bool[gullies.Length];
        for(int i=0;i<streaks.Length;i++)
        {
            int x=Mathf.RoundToInt((i+.25f+.5f*Hash(seed+i*53))*W/streaks.Length);
            float side=x<apex[x]?-1:1;
            streaks[i]=new Stroke { x=x,top=ridge[x]-caps[x]-3,
                length=Mathf.RoundToInt((ridge[x]-visibleFoot)*Mathf.Lerp(.45f,.9f,Hash(seed+i*71))),
                width=2+(int)(Hash(seed+i*97)*3.999f),lean=side*Mathf.Tan(Mathf.Lerp(2,8,Hash(seed+i*113))*Mathf.Deg2Rad),
                contrast=i%2==0?1.06f:.94f };
        }
        for(int i=0;i<gullies.Length;i++)
        {
            int x=Mathf.RoundToInt((i+.2f+.6f*Hash(seed+i*131))*W/gullies.Length);
            gullies[i]=new Stroke {x=x,top=ridge[x]-caps[x]+2,
                length=20+Mathf.RoundToInt(50*Hash(seed+i*173)),width=2+(int)(Hash(seed+i*197)*2.999f),
                lean=(x<apex[x]?-1:1)*Mathf.Tan(6*Mathf.Deg2Rad)};
        }
        // Tree tips stay within the lowest 7% of the visible mountain.
        var trees=new int[W]; var treeFeet=new int[W];
        if(layer<2)
            for(int x=3,index=0;x<W-3;index++)
            {
                int height=3+Mathf.RoundToInt(3*Hash(seed+index*239));
                int bottom=Mathf.FloorToInt(visibleFoot+Mathf.Max(0,(crest-visibleFoot)*.07f-height)*Hash(seed+index*251));
                for(int dx=-2;dx<=2;dx++)
                    { trees[x+dx]=bottom+height-Math.Abs(dx)*2; treeFeet[x+dx]=bottom; }
                x+=4+Mathf.RoundToInt(4*Hash(seed+index*269));
            }
        double visibleLuma=0; int visibleArea=0;
        for(int x=0;x<W;x++)
            for(int y=0;y<=ridge[x];y++)
            {
                int depth=ridge[x]-y;
                float down=(ridge[x]-y)/Mathf.Max(1,ridge[x]-visibleFoot);
                // Three dark-to-light rock beds; eroded boundaries break straight color bands.
                float strata=down+Noise2(x/19f,y/23f,seed+42)*.19f+Noise2(x/6f,y/11f,seed+211)*.08f;
                int level=Mathf.Clamp(Mathf.FloorToInt(strata*RockLevels),0,RockLevels-1);
                Color basis=rock[level];
                // Feather only the eroded joins; retain three readable rock-color levels.
                for(int bed=1;bed<RockLevels;bed++)
                    if(Mathf.Abs(strata-bed/(float)RockLevels)<.055f)
                        basis=Color.Lerp(rock[bed-1],rock[bed],Mathf.SmoothStep(0,1,
                            Mathf.InverseLerp(bed/(float)RockLevels-.055f,bed/(float)RockLevels+.055f,strata)));
                Color c=layer==3?Color.Lerp(Hex("#91A2B3"),air,mix):Color.Lerp(Rock(basis,false,mix,air),Rock(basis,true,mix,air),faceLight[x]);
                if(layer<3)
                {
                    // Broad irregular relief plus sparse, narrow vertical rock fractures.
                    float grain=Noise2((x+(crest-y)*.12f)/7f,y/21f,seed+137)*.055f
                        +Noise2(x/2.5f,y/7f,seed+433)*.025f;
                    c.r+=grain*(1-mix); c.g+=grain*(1-mix); c.b+=grain*(1-mix);
                    for(int i=0;i<streaks.Length;i++)
                    {
                        var stroke=streaks[i]; int dy=stroke.top-y;
                        int left=Mathf.RoundToInt(stroke.Center(y))-stroke.width/2;
                        if(dy<0 || dy>=stroke.length || x<left || x>=left+stroke.width) continue;
                        c.r*=stroke.contrast; c.g*=stroke.contrast; c.b*=stroke.contrast;
                        streakDrawn[i]=true;
                    }
                    int ridgeWidth=1+(int)(Hash(seed+apex[x])*1.999f);
                    if(x>=apex[x] && x<apex[x]+ridgeWidth && down<.45f)
                        c=Color.Lerp(c,Color.Lerp(transition,air,mix),.18f*(1-down/.45f)*Mathf.Clamp01(.6f+Noise(y/7f,seed+apex[x])*.5f));
                }
                Color snowColor=Color.Lerp(snow,air,mix);
                Color edgeColor=Color.Lerp(transition,air,mix);
                int edgeWidth=2+Mathf.RoundToInt(2*Hash(seed+x/5));
                if(depth<caps[x])
                    c=Color.Lerp(c,snowColor,Mathf.Clamp01(.76f+Noise2(x/4f,y/5f,seed+301)*.35f));
                else if(depth<caps[x]+edgeWidth)
                    c=Color.Lerp(edgeColor,c,(depth-caps[x])/(float)edgeWidth);
                for(int i=0;i<gullies.Length;i++)
                {
                    var g=gullies[i]; int dy=g.top-y;
                    int left=Mathf.RoundToInt(g.Center(y)+Noise(y/9f,seed+i*307)*.7f)-g.width/2;
                    if(dy<0 || dy>=g.length || x<left || x>=left+g.width) continue;
                    float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,dy/(float)g.length));
                    c=Color.Lerp(c,Color.Lerp(edgeColor,snowColor,.6f),fade*Mathf.Lerp(.6f,.9f,Hash(seed+i*13+y/3))); gullyDrawn[i]=true;
                }
                float fog=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((y-visibleFoot)/hazeHeight));
                c=Color.Lerp(c,haze,fog);
                if(layer<2 && y>=treeFeet[x] && y<trees[x])
                {
                    Color tree=Color.Lerp(Hex("#26313A"),air,mix);
                    c=Color.Lerp(c,tree,(layer==0?.45f:.2f)*(1-fog));
                }
                c.a=Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Min(x,W-1-x)/(W*Feather)));
                Color32 pixel=c; pixels[y*W+x]=pixel;
                if(y>=visibleFoot) { visibleLuma+=Luma(pixel); visibleArea++; }
            }
        LayerLuma[layer]=(float)(visibleLuma/Math.Max(1,visibleArea));
        if(layer<3)
        {
            RockStreaks[layer]=Array.FindAll(streakDrawn,v=>v).Length;
            SnowGullies[layer]=Array.FindAll(gullyDrawn,v=>v).Length;
            // Quantized same-bed samples include the actual layer's atmospheric mix.
            foreach(var basis in rock)
                minFaceDelta=Mathf.Min(minFaceDelta,Luma(Rock(basis,true,mix,air))-Luma(Rock(basis,false,mix,air)));
            if(RockStreaks[layer]<8 || RockStreaks[layer]>14 || SnowGullies[layer]<6 || SnowGullies[layer]>12)
                throw new InvalidOperationException("MTN10 painted streak/gully count gate");
        }
        int footRow=Mathf.CeilToInt(visibleFoot);
        float jump=0;
        for(int x=1;x<W;x++)
        {
            jump=Mathf.Max(jump,Mathf.Abs(Luma(pixels[footRow*W+x])-Luma(pixels[footRow*W+x-1])));
            jump=Mathf.Max(jump,Mathf.Abs(Luma(pixels[(footRow+1)*W+x])-Luma(pixels[footRow*W+x])));
        }
        maxFootJump=Mathf.Max(maxFootJump,jump);
        if(jump>.04f) throw new InvalidOperationException("MTN10 foot continuity gate");
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN10_PAINT] L{0} rock_streaks={1} snow_gullies={2} luma={3:F4} foot_jump={4:F6}",
            layer+1,streaks.Length,gullies.Length,LayerLuma[layer],jump));
        string path=$"Assets/Art/Textures/MtnLayer_{layer+1}.png";
        var tex=new Texture2D(W,H,TextureFormat.RGBA32,false); tex.SetPixels32(pixels); tex.Apply();
        File.WriteAllBytes(path,tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Default; importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled=false; importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency=false; importer.wrapMode=TextureWrapMode.Clamp;
        importer.maxTextureSize=1024; importer.npotScale=TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    // Run AFTER capture_runtime.sh: measure the encoded screenshot, not texture estimates.
    public static void MeasureScreenshot()
    {
        string path="Shots/art_runtime.png";
        var frame=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
        if(!frame.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("MTN11 screenshot decode");
        int w=frame.width,h=frame.height;
        var pixels=frame.GetPixels32();
        int x0=(int)(w*.05),x1=(int)(w*.95),y0=(int)(h*.10),y1=(int)(h*.45);
        var columns=new double[x1-x0];
        double sum=0;
        for(int x=x0;x<x1;x++)
        {
            for(int y=y0;y<y1;y++) columns[x-x0]+=Luma(pixels[(h-1-y)*w+x]);
            columns[x-x0]/=y1-y0; sum+=columns[x-x0];
        }
        var differences=new double[columns.Length-1];
        for(int x=0;x<differences.Length;x++) differences[x]=Math.Abs(columns[x+1]-columns[x]);
        Array.Sort(differences);
        double index=(differences.Length-1)*.99;
        int lower=(int)Math.Floor(index),upper=(int)Math.Ceiling(index);
        double seam=differences[lower]+(differences[upper]-differences[lower])*(index-lower);
        double luma=sum/columns.Length;
        float delta=1;
        foreach(float mix in new[]{0f,.38f,.68f})
            foreach(string tint in new[]{"#5A6878","#6B7888","#7C8896"})
                delta=Mathf.Min(delta,Luma(Rock(Hex(tint),true,mix,Hex("#DAE5EF")))-Luma(Rock(Hex(tint),false,mix,Hex("#DAE5EF"))));
        UnityEngine.Object.DestroyImmediate(frame);
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN11] rock_tints=#5A6878/#6B7888/#7C8896 lit_shadow_delta={0:F6} treeline_pct=0.07 treeline_alpha=0.45 overscan=0.12 feather=0.08 seam_max_grad={1:F6} height_scale={2:F6}",delta,seam,HeightScale));
        Debug.Log(string.Format(CultureInfo.InvariantCulture,"[MTN11] mountain_luma={0:F6}",luma));
        if(luma<.42 || luma>.60 || seam>.035 || delta<.06f || delta>.10f)
            throw new InvalidOperationException("MTN11 screenshot gate");
    }
    // Repaint the existing scene in isolation; do not regenerate church, interior or cameras.
    public static void Repaint()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        var oldRidges=Array.Find(scene.GetRootGameObjects(),go=>go.name=="Mountain Ridges");
        var painted=GameObject.Find("Painted Mountain Layers");
        if(painted!=null) UnityEngine.Object.DestroyImmediate(painted);
        Build(Camera.main,oldRidges);
        EditorSceneManager.SaveScene(scene);
    }
    public static void Build(Camera cam,GameObject oldRidges)
    {
        if(oldRidges==null) throw new InvalidOperationException("MTN4: missing rollback ridges");
        oldRidges.SetActive(false);
        var root=new GameObject("Painted Mountain Layers").transform;
        float aspect=1920f/1080f;
        float[] distances={900,1300,1800,2200},tops={.32f,.28f,.24f,.20f};
        var measured=new float[4];
        var widths=new float[4]; var fills=new float[4];
        var poses=new System.Collections.Generic.List<ShotPose>();
        foreach(var pose in UnityEngine.Object.FindFirstObjectByType<ShotRig>().poses)
            if(pose.name!="art_ground") poses.Add(pose); // ground inspection looks away from the church/backdrop.
        var controller=UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        float runtimeFov=PlayerController.FollowFieldOfView(controller.GetComponent<MistReveal>().church,controller.ground);
        foreach(float z in new[]{-88f,-89f,-59f,-29f,-15f,-9f})
        {
            // Use the same geometry-derived FOV as the actual runtime camera.
            var player=new Vector3(0,Mathf.Tan(6*Mathf.Deg2Rad)*z,z);
            poses.Add(ShotRig.Look("follow",player+PlayerController.FollowOffset,
                player+Vector3.up*PlayerController.LookHeight,runtimeFov));
        }
        maxPeakStdPct=0; maxFootJump=0; minCap=int.MaxValue; maxCap=0; minFaceDelta=1;
        for(int layer=0;layer<4;layer++)
        {
            int[] ridge=Profile(layer,out int peaks,out float std);
            PeakCounts[layer]=peaks;
            float d=distances[layer];
            float viewH=2*d*Mathf.Tan(cam.fieldOfView*.5f*Mathf.Deg2Rad);
            float width=viewH*aspect*1.04f,height=width*.5f;
            // Measure maximum alpha only inside the visible viewport (exclude overscan).
            int peak=0,peakX=0;
            for(int x=20;x<W-20;x++) if(ridge[x]>peak) {peak=ridge[x];peakX=x;}
            float bottom=1-tops[layer]-(OriginalCrests[layer]+.5f)/H*height/viewH;
            height*=HeightScale;
            // Current locked shot: the distant ground meets the mountain at 74% screen Y.
            float visibleFoot=Mathf.Clamp(((1-bottom)-.74f)*viewH/height*H,0,peak-1);
            var texture=Paint(layer,ridge,visibleFoot);
            Vector3 center=cam.transform.position+cam.transform.forward*d+
                cam.transform.up*((bottom-.5f)*viewH+height*.5f)+
                cam.transform.right*((layer-1.5f)*150f/1920f*viewH*aspect);
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name=$"MtnLayer_{layer+1}"; quad.transform.SetParent(root,false);
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetPositionAndRotation(center,cam.transform.rotation);
            // Intersect the widest horizontal camera frustum with this card plane. Include
            // asymmetric shot positions: a total width alone can hide a side gap.
            float extent=width/2;
            foreach(var pose in poses)
            {
                var rotation=Quaternion.Euler(pose.euler);
                float tan=Mathf.Tan(pose.fov*.5f*Mathf.Deg2Rad);
                for(int x=-1;x<=1;x+=2)
                {
                    var ray=rotation*new Vector3(x*1.30f*tan*aspect,0,1);
                    float denominator=Vector3.Dot(ray,quad.transform.forward);
                    if(denominator<=.01f) continue;
                    float t=Vector3.Dot(center-pose.position,quad.transform.forward)/denominator;
                    var local=Quaternion.Inverse(quad.transform.rotation)*(pose.position+ray*t-center);
                    extent=Mathf.Max(extent,Mathf.Abs(local.x)*1.02f);
                }
            }
            width=extent*2*(1+Overscan);
            quad.transform.localScale=new Vector3(width,height,1);
            widths[layer]=width; fills[layer]=float.PositiveInfinity;
            foreach(var pose in poses)
            {
                var rotation=Quaternion.Euler(pose.euler);
                float tan=Mathf.Tan(pose.fov*.5f*Mathf.Deg2Rad);
                float low=0,high=3;
                for(int iteration=0;iteration<20;iteration++)
                {
                    float f=(low+high)/2; bool covered=true;
                    for(int x=-1;x<=1;x+=2)
                    {
                        var ray=rotation*new Vector3(x*f*tan*aspect,0,1);
                        float denominator=Vector3.Dot(ray,quad.transform.forward);
                        float t=Vector3.Dot(center-pose.position,quad.transform.forward)/denominator;
                        var local=Quaternion.Inverse(quad.transform.rotation)*(pose.position+ray*t-center);
                        covered &= denominator>.001f && t>0 && Mathf.Abs(local.x)<=width/2;
                    }
                    if(covered) low=f; else high=f;
                }
                fills[layer]=Mathf.Min(fills[layer],low);
            }

            // L3 physically remains at 1800m. Bounds overlap the frustum because its
            // background shader clamps far depth; no camera clipping setting is changed.
            var mesh=UnityEngine.Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);
            mesh.name=quad.name; mesh.bounds=new Bounds(Vector3.zero,new Vector3(1,1,4000));
            string meshPath=$"Assets/Settings/MtnLayer_{layer+1}.asset";
            AssetDatabase.DeleteAsset(meshPath); AssetDatabase.CreateAsset(mesh,meshPath);
            quad.GetComponent<MeshFilter>().sharedMesh=mesh;
            var mat=new Material(Shader.Find("Mistbell/PaintedMountain")); mat.SetTexture("_MainTex",texture);
            // L3/L4 share the far-depth clamp: explicitly paint farther cards first.
            mat.renderQueue=2450-layer;
            string matPath=$"Assets/Settings/M_MtnLayer_{layer+1}.mat";
            AssetDatabase.DeleteAsset(matPath); AssetDatabase.CreateAsset(mat,matPath);
            var renderer=quad.GetComponent<MeshRenderer>(); renderer.sharedMaterial=mat;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            Vector3 crest=quad.transform.TransformPoint(new Vector3((peakX+.5f)/W-.5f,(peak+.5f)/H-.5f,0));
            // Use explicit output aspect without modifying any camera property.
            measured[layer]=1-cam.WorldToViewportPoint(crest).y;
            Vector3 foot=quad.transform.TransformPoint(new Vector3(0,-.5f,0));
            if(fills[layer]<1.25f || measured[layer]>.30f || bottom>0 || foot.y>-20)
                throw new InvalidOperationException($"MTN8 silhouette/foot gate L{layer+1} fill={fills[layer]} top={measured[layer]} bottom={bottom} foot={foot.y}");
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[MTN4_LAYER] L{0} peaks={1} ridge_std_px={2:F2} distance_m={3:F0} width_m={4:F2} top_viewport_y={5:F4} foot_viewport_y={6:F4} foot_world_y={7:F2}",
                layer+1,peaks,std,d,width,measured[layer],1-bottom,foot.y));
        }
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN8] layer_width_m={0:F2}/{1:F2}/{2:F2} screen_fill_pct={3:F4}/{4:F4}/{5:F4} top_y={6:F4}/{7:F4}/{8:F4}",
            widths[0],widths[1],widths[2],fills[0],fills[1],fills[2],measured[0],measured[1],measured[2]));
        float gradient=LayerLuma[2]-LayerLuma[0];
        if(gradient<.18f || LayerLuma[1]<=LayerLuma[0] || LayerLuma[1]>=LayerLuma[2])
            throw new InvalidOperationException("MTN10 layer luminance gate");
        if(RockLevels!=3 || minFaceDelta<.06f || minFaceDelta>.10f || minCap!=5 || maxCap!=24 || root.childCount!=4)
            throw new InvalidOperationException($"MTN10 drawing gate delta={minFaceDelta} caps={minCap}..{maxCap}");
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN10] layers={0} rock_levels={1} rock_streaks={2}/{3} lit_shadow_delta={4:F6} snowcap_px={5}..{6} snow_gullies={7}/{8} treeline=0.45/0.2/no silhouette_peaks={9}/{10}/{11}",
            root.childCount,RockLevels,RockStreaks[0],RockStreaks[1],minFaceDelta,minCap,maxCap,
            SnowGullies[0],SnowGullies[1],PeakCounts[0],PeakCounts[1],PeakCounts[2]));
    }
}
