using System;
using System.IO;
using System.Globalization;
using UnityEditor;
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
    static readonly int[] Seeds = { 15731, 80447, 213397 };
    const float HeightScale = 1.15f;
    // Preserve the previous cards' feet; scaling is anchored here, never at the crest.
    static readonly int[] OriginalCrests = { 444, 460, 448 };
    static float maxPeakStdPct, maxDarkPct;
    static int darkestRgb;
    static readonly float[] LayerLuma = new float[3];
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
    static Texture2D Paint(int layer,int[] ridge,float visibleFoot)
    {
        var pixels=new Color32[W*H];
        Color floor=Hex("#5A6876"), mid=Hex("#6C7D8D"), light=Hex("#91A2B3");
        Color snow=layer==0?Hex("#B7C7D4"):layer==1?Hex("#D1DFEA"):Hex("#EAF1F7");
        Color haze=Hex("#EDF3F8"), air=Hex("#DAE5EF");
        float mix=layer==0?0:layer==1?.48f:.88f;
        double visibleLuma=0; int visibleArea=0;
        int seed=Seeds[layer],area=0,dark=0,minRgb=0xffffff;
        int crest=0;
        foreach(int h in ridge) crest=Math.Max(crest,h);
        // The cards extend underground. Anchor fog at the visible snow contact, so
        // its lower quarter is not lost behind the unchanged foreground terrain.
        float hazeHeight=(crest-visibleFoot)*.12f;
        for(int x=0;x<W;x++)
        {
            int l=Mathf.Max(0,x-4),r=Mathf.Min(W-1,x+4);
            float slope=Mathf.Abs((ridge[r]-ridge[l])/(float)Mathf.Max(1,r-l));
            // Thickness in source pixels. Steep flanks: 2..6; shallow shelves: 10..22.
            float large=Jagged(x,seed+503,6,12),small=Jagged(x,seed+907,2,4);
            float depositNoise=Mathf.Clamp01(.5f+.35f*large+.15f*small);
            float steep=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1.45f,slope));
            int cap=Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(
                Mathf.Lerp(10,22,depositNoise),Mathf.Lerp(2,6,depositNoise),steep)),2,22);
            for(int y=0;y<=ridge[x];y++)
            {
                int depth=ridge[x]-y;
                // Two-dimensional broken rock beds, independent of a single flank direction.
                float grain=Noise2(x/53f,y/41f,seed+42)*.65f
                    +Noise2(x/19f,y/27f,seed+137)*.35f;
                float exposure=Mathf.Clamp01(.60f+grain*.78f);
                Color c=exposure<.5f
                    ?Color.Lerp(floor,mid,Mathf.SmoothStep(0,1,exposure*2))
                    :Color.Lerp(mid,light,Mathf.SmoothStep(0,1,(exposure-.5f)*2));
                float bed=Noise2(x/24f,(y+x*.24f)/7f,seed+211);
                c=Color.Lerp(c,light,Mathf.Max(0,bed)*.10f);
                float deposit=Mathf.Lerp(.35f,1,Mathf.SmoothStep(0,1,
                    Mathf.InverseLerp(-.55f,.2f,Noise2(x/13f,y/11f,seed+301))));
                float coverage=depth<cap?deposit*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(cap-2,cap,depth))):0;
                c=Color.Lerp(c,snow,coverage);
                c=Color.Lerp(c,air,mix);
                float fog=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((y-visibleFoot)/hazeHeight));
                c=Color.Lerp(c,haze,fog);
                // Quantized output is the source of every color/area measurement below.
                Color32 pixel=c;
                pixels[y*W+x]=pixel;
                if(y>=visibleFoot) { visibleLuma+=Luma(pixel); visibleArea++; }
                int rgb=(pixel.r<<16)|(pixel.g<<8)|pixel.b;
                minRgb=Math.Min(minRgb,rgb);
                if(pixel.r<0x5A || pixel.g<0x68 || pixel.b<0x76)
                    throw new InvalidOperationException("MTN7 RGB floor gate");
                area++;
                // Dark tier: lower half of floor-to-mid luminance, measured after snow/air/fog.
                float lum=.2126f*pixel.r+.7152f*pixel.g+.0722f*pixel.b;
                if(lum<119.6f) dark++;
            }
        }
        LayerLuma[layer]=(float)(visibleLuma/Math.Max(1,visibleArea));
        // Inspect the first two visible rows across the full width, not hidden card feet.
        int footRow=Mathf.CeilToInt(visibleFoot);
        float horizontalJump=0,verticalJump=0;
        for(int x=1;x<W;x++)
        {
            horizontalJump=Mathf.Max(horizontalJump,Mathf.Abs(Luma(pixels[footRow*W+x])-Luma(pixels[footRow*W+x-1])));
            verticalJump=Mathf.Max(verticalJump,Mathf.Abs(Luma(pixels[(footRow+1)*W+x])-Luma(pixels[footRow*W+x])));
        }
        maxFootJump=Mathf.Max(maxFootJump,horizontalJump,verticalJump);
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN9_FOOT] L{0} visible_foot_px={1:F3} haze_height_px={2:F3} horizontal_jump={3:F6} vertical_jump={4:F6} luma_scope=visible_mountain_pixels",
            layer+1,visibleFoot,hazeHeight,horizontalJump,verticalJump));
        if(horizontalJump>.04f || verticalJump>.04f) throw new InvalidOperationException("MTN9 foot continuity gate");
        float darkPct=100f*dark/area;
        if(darkPct>25 || minRgb<0x5A6876 || HeightScale!=1.15f)
            throw new InvalidOperationException("MTN7 dark area/color/scale gate");
        maxDarkPct=Mathf.Max(maxDarkPct,darkPct);
        darkestRgb=Math.Min(darkestRgb,minRgb);
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN7_PAINT] L{0} mountain_pixels={1} dark_pixels={2} dark_area_pct={3:F4} darkest=#{4:X6}",
            layer+1,area,dark,darkPct,minRgb));
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
    public static void Build(Camera cam,GameObject oldRidges)
    {
        if(oldRidges==null) throw new InvalidOperationException("MTN4: missing rollback ridges");
        oldRidges.SetActive(false);
        var root=new GameObject("Painted Mountain Layers").transform;
        float aspect=1920f/1080f;
        float[] distances={900,1300,1800},tops={.32f,.28f,.24f};
        var measured=new float[3];
        var widths=new float[3]; var fills=new float[3];
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
        maxPeakStdPct=0; maxDarkPct=0; darkestRgb=0xffffff; maxFootJump=0;
        for(int layer=0;layer<3;layer++)
        {
            int[] ridge=Profile(layer,out int peaks,out float std);
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
                cam.transform.up*((bottom-.5f)*viewH+height*.5f);
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
            width=extent*2;
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
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN9] snowcap_px=2..22 double_jagged=true layer_luma=L1:{0:F4}/L2:{1:F4}/L3:{2:F4} gradient={3:F4} haze_band_pct=12 foot_jump={4:F6}",
            LayerLuma[0],LayerLuma[1],LayerLuma[2],gradient,maxFootJump));
        if(gradient<.18f || LayerLuma[1]<=LayerLuma[0] || LayerLuma[1]>=LayerLuma[2])
            throw new InvalidOperationException("MTN9 layer luminance gate");
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN7] peak_height_std_pct={0:F4} dark_area_pct={1:F4} darkest=#{2:X6} layer_mix=48/88 haze_band_pct=12 snow_cap_px=2..22 height_scale={3:F2}",
            maxPeakStdPct,maxDarkPct,darkestRgb,HeightScale));
    }
}
