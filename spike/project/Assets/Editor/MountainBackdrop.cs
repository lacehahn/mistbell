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
    static float Ridged(float x, int seed)
    {
        float product=1;
        for(int o=0;o<4;o++)
            product*=1-Mathf.Abs(Noise(x*(1<<o)+o*17.391f,seed+o*113));
        return product;
    }
    static Color Hex(string s) { ColorUtility.TryParseHtmlString(s,out var c); return c; }
    static readonly int[] Seeds = { 15731, 80447, 213397 };
    static int symmetricPeaks;
    static int[] Profile(int layer, out int peaks, out float std)
    {
        // Independent streams, and no UnityEngine.Random calls (other scene assets stay identical).
        var rng = new System.Random(Seeds[layer]);
        peaks = 9 + layer;
        var widths = new int[peaks];
        for (int i=0;i<peaks;i++) widths[i]=60;
        // Bounded random composition: exact 1023px coverage, each base 60..260px.
        for (int remaining=W-1-60*peaks;remaining>0;)
        {
            int i=rng.Next(peaks);
            if(widths[i]>=260) continue;
            int add=Math.Min(remaining,Math.Min(260-widths[i],rng.Next(1,24)));
            widths[i]+=add; remaining-=add;
        }
        // Make the range visibly broad, rather than concentrating every width around 100px.
        for(int i=1;i<peaks && widths[0]<260;i++)
        {
            int transfer=Math.Min(260-widths[0],widths[i]-60);
            widths[0]+=transfer; widths[i]-=transfer;
        }
        // Shuffle the broad massif among the smaller peaks independently in each layer.
        for(int i=peaks-1;i>0;i--)
        { int j=rng.Next(i+1),w=widths[i]; widths[i]=widths[j]; widths[j]=w; }
        var ridge=new int[W];
        int start=0, valley=280;
        float sum=0,sum2=0,min=H,max=0,minRatio=3,maxRatio=0;
        int minWidth=260,maxWidth=0,spurCount=0;
        for(int i=0;i<peaks;i++)
        {
            int width=widths[i], apex=0, top=0, end=0;
            float ratio=0;
            // Different valley elevations let both flanks carry shoulders >= .3 base widths
            // away from the apex while their actual rise/run remains asymmetric.
            for(int attempt=0;attempt<10000;attempt++)
            {
                apex=Mathf.RoundToInt(width*(.34f+(float)rng.NextDouble()*.32f));
                // Couple relief to width: a 60px ridge must not become a 250px needle.
                top=Math.Min(460,Mathf.RoundToInt(285+width*.65f)+rng.Next(-20,31));
                end=rng.Next(260,301);
                float left=(top-valley)/(float)apex;
                float right=(top-end)/(float)(width-apex);
                ratio=Mathf.Max(left,right)/Mathf.Min(left,right);
                if(ratio>=1.65f && ratio<=2.75f) break;
            }
            if(ratio<1.6f || ratio>2.8f) throw new InvalidOperationException("MTN5 asymmetry gate");
            if(Mathf.Abs(ratio-1)<.0001f) symmetricPeaks++;
            minRatio=Mathf.Min(minRatio,ratio); maxRatio=Mathf.Max(maxRatio,ratio);
            minWidth=Math.Min(minWidth,width); maxWidth=Math.Max(maxWidth,width);
            // Ordered, monotonic shoulder knots produce stepped flanks, not extra tent peaks.
            var knots=new System.Collections.Generic.List<Vector2>();
            knots.Add(new Vector2(start,valley));
            int leftCount=apex-.3f*width<6?1:rng.Next(1,3);
            int rightCount=width-apex-.3f*width<6?1:rng.Next(1,3);
            for(int j=leftCount-1;j>=0;j--)
            {
                float t=leftCount==1?.5f:j;
                float offset=Mathf.Lerp(.3f*width,Mathf.Min(.6f*width,apex-2),t);
                float drop=Mathf.Lerp(.25f,.45f,t);
                knots.Add(new Vector2(start+apex-offset,top-(top-valley)*drop));
            }
            knots.Add(new Vector2(start+apex,top));
            for(int j=0;j<rightCount;j++)
            {
                float t=rightCount==1?.5f:j;
                float offset=Mathf.Lerp(.3f*width,Mathf.Min(.6f*width,width-apex-2),t);
                float drop=Mathf.Lerp(.25f,.45f,t);
                knots.Add(new Vector2(start+apex+offset,top-(top-end)*drop));
            }
            knots.Add(new Vector2(start+width,end));
            for(int k=0;k<knots.Count-1;k++)
            {
                Vector2 a=knots[k],b=knots[k+1];
                for(int x=Mathf.CeilToInt(a.x);x<=Mathf.FloorToInt(b.x);x++)
                    ridge[x]=Mathf.RoundToInt(Mathf.Lerp(a.y,b.y,(x-a.x)/(b.x-a.x))/2)*2;
            }
            float rasterLeft=(ridge[start+apex]-ridge[start])/(float)apex;
            float rasterRight=(ridge[start+apex]-ridge[start+width])/(float)(width-apex);
            float rasterRatio=Mathf.Max(rasterLeft,rasterRight)/Mathf.Min(rasterLeft,rasterRight);
            if(rasterRatio<1.6f || rasterRatio>2.8f)
                throw new InvalidOperationException("MTN5 raster asymmetry gate");
            minRatio=Mathf.Min(minRatio,rasterRatio); maxRatio=Mathf.Max(maxRatio,rasterRatio);
            spurCount+=leftCount+rightCount;
            sum+=top;sum2+=top*top;min=Mathf.Min(min,top);max=Mathf.Max(max,top);
            start+=width; valley=end;
        }
        // Measure maxima on the final raster, ignoring flat 2px crest plateaus.
        int rasterPeaks=0,prev=0;
        for(int x=1;x<W;x++)
        {
            int d=Math.Sign(ridge[x]-ridge[x-1]); if(d==0) continue;
            if(prev>0 && d<0) rasterPeaks++; prev=d;
        }
        std=Mathf.Sqrt(Mathf.Max(0,sum2/peaks-Mathf.Pow(sum/peaks,2)));
        if(rasterPeaks!=peaks || peaks<9 || peaks>14 || symmetricPeaks!=0)
            throw new InvalidOperationException($"MTN5 raster gate peaks={rasterPeaks} symmetric_peaks={symmetricPeaks}");
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN5_LAYER] L{0} seed={1} peaks={2} symmetric_peaks=0 asymmetry_min={3:F6} asymmetry_max={4:F6} width_min_px={5} width_max_px={6} shoulders={7}",
            layer+1,Seeds[layer],rasterPeaks,minRatio,maxRatio,minWidth,maxWidth,spurCount));
        return ridge;
    }
    static Texture2D Paint(int layer,int[] ridge)
    {
        var pixels=new Color32[W*H];
        var rock=new[]{Hex("#4A5666"),Hex("#5E6B7C"),Hex("#74828F")};
        Color snow=Hex("#EAF1F7"), haze=Hex("#C9D6E3");
        Color air=layer==1?Hex("#8FA0B4"):Hex("#C3D2E0");
        float mix=layer==0?0:layer==1?.35f:.60f;
        int seed=Seeds[layer], bands=5-layer, gullies=20-layer*4;
        var snowMask=new float[W*H];
        // Separate deterministic stream: painting cannot alter props, snow or characters.
        var rng=new System.Random(seed+600);
        for(int g=0;g<gullies;g++)
        {
            int startX=Mathf.Clamp(Mathf.RoundToInt((g+.25f+(float)rng.NextDouble()*.5f)*W/gullies),4,W-5);
            int length=rng.Next(20,91), width=rng.Next(2,7);
            int startY=ridge[startX]-rng.Next(0,4);
            float slope=(ridge[startX+4]-ridge[startX-4])/8f;
            float drift=-Mathf.Clamp(slope,-1.5f,1.5f)*.42f;
            for(int d=0;d<length;d++)
            {
                float t=d/(float)(length-1);
                int cx=Mathf.RoundToInt(startX+drift*d+Noise(d/22f,seed+g*37)*2);
                int cy=startY-d;
                // Retain a 2..6px channel, fade snow into rock at its tapered end.
                int w=Mathf.Clamp(Mathf.RoundToInt(width*(1-.5f*t)),2,6);
                float opacity=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,t));
                for(int dx=0;dx<w;dx++)
                {
                    int x=cx+dx-w/2;
                    if(x<0 || x>=W || cy<0 || cy>ridge[x]) continue;
                    int index=cy*W+x;
                    snowMask[index]=Mathf.Max(snowMask[index],opacity);
                }
            }
        }
        for(int x=0;x<W;x++)
        {
            int l=Mathf.Max(0,x-4),r=Mathf.Min(W-1,x+4);
            float slope=(ridge[r]-ridge[l])/(float)Mathf.Max(1,r-l);
            float up=1/Mathf.Sqrt(1+slope*slope);
            // Patchy crest deposits replace the former continuous 8..36px white border.
            float deposit=Mathf.Clamp01(.45f+Noise(x/19f,seed+13)*.85f);
            int cap=Mathf.RoundToInt(Mathf.Lerp(2,12,up)*deposit);
            for(int y=0;y<=ridge[x];y++)
            {
                int depth=ridge[x]-y;
                float facet=Noise((x+depth*.63f)/47f,seed+42)
                    +.5f*Noise((x-depth*.35f)/83f,seed+109);
                float exposure=Mathf.Clamp01(up+facet*.45f-depth/(float)H*.1f);
                // Broad planes with softer transitions keep texture from reading as cut paper.
                Color c=Color.Lerp(rock[0],rock[1],Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.65f,exposure)));
                c=Color.Lerp(c,rock[2],Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,exposure))*.75f);
                for(int band=0;band<bands;band++)
                {
                    int bandSeed=seed+band*193;
                    int height=6+Mathf.FloorToInt(Hash(bandSeed)*13);
                    // Bed edges track the local ridge angle at 65% dip, never horizontal.
                    float edge=ridge[x]*.65f+90-band*34
                        +Noise(x/55f,bandSeed+51)*4;
                    int gapWidth=2+Mathf.FloorToInt(Hash(x/29+bandSeed+71)*3);
                    int gapStart=5+Mathf.FloorToInt(Hash(x/29+bandSeed+97)*17);
                    bool gap=x%29>=gapStart && x%29<gapStart+gapWidth;
                    bool segment=Hash(x/67+bandSeed+29)>.22f;
                    if(y>=edge && y<edge+height && segment && !gap)
                        c*=1+(band%2==0?-.08f:.08f);
                }
                float coverage=snowMask[y*W+x];
                // Broken leeward snowfields feed the thin gullies, rather than a white outline.
                // Coherent oblique pockets leave exposed rock ribs; no per-pixel white noise.
                float pocket=Noise((x+depth*.42f)/23f,seed+301)
                    +.30f*Noise((x-depth*.6f)/9f,seed+331);
                float snowReach=105+65*up+Noise(x/37f,seed+367)*36;
                float field=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.12f,.10f,pocket))
                    *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(snowReach-20,snowReach,depth)));
                coverage=Mathf.Max(coverage,field);
                if(depth<cap) coverage=Mathf.Max(coverage,.72f+.28f*deposit);
                // Slight cool shadow in the channels, all snow stays below white clipping.
                c=Color.Lerp(c,Color.Lerp(snow,Hex("#B9C8D4"),.16f*(1-up)),coverage);
                pixels[y*W+x]=Color.Lerp(c,air,mix);
            }
        }
        if(layer<2)
        {
            Color forest=Hex("#26313A");
            for(int x=2;x<W-2;)
            {
                int height=rng.Next(3,8), baseY=rng.Next(0,Mathf.FloorToInt(H*.12f)-7);
                int halfWidth=rng.Next(1,3);
                for(int dy=0;dy<height;dy++)
                {
                    int radius=Mathf.RoundToInt(halfWidth*(1-dy/(float)height));
                    for(int dx=-radius;dx<=radius;dx++)
                    {
                        int px=x+dx, py=baseY+dy;
                        if(px<0 || px>=W || py>ridge[px]) continue;
                        int index=py*W+px;
                        pixels[index]=Color.Lerp(pixels[index],Color.Lerp(forest,air,mix),layer==0?.9f:.35f);
                    }
                }
                x+=rng.Next(4,10);
            }
        }
        // Horizontal, continuous 0..45% foot haze; far layers also retain more aerial mix.
        for(int y=0;y<Mathf.CeilToInt(H*.30f);y++)
        {
            float fog=.45f*(1-Mathf.SmoothStep(0,1,y/(H*.30f)));
            for(int x=0;x<W;x++)
                if(y<=ridge[x]) pixels[y*W+x]=Color.Lerp(pixels[y*W+x],haze,fog);
        }
        Debug.Log($"[MTN6_LAYER] L{layer+1} strata_bands={bands} snow_gullies={gullies} treeline={(layer<2?"yes":"no")} aerial_mix={mix:F2}");
        if(layer==2)
            Debug.Log("[MTN6] strata_bands=3..5 per_layer snow_gullies=20/16/12 treeline=yes/yes/no haze_bottom_pct=30");
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
        var widths=new float[3]; var measured=new float[3];
        var peakCounts=new int[3]; symmetricPeaks=0;
        for(int layer=0;layer<3;layer++)
        {
            int[] ridge=Profile(layer,out int peaks,out float std);
            peakCounts[layer]=peaks;
            var texture=Paint(layer,ridge);
            float d=distances[layer];
            float viewH=2*d*Mathf.Tan(cam.fieldOfView*.5f*Mathf.Deg2Rad);
            float width=viewH*aspect*1.04f,height=width*.5f;
            widths[layer]=width;
            // Measure maximum alpha only inside the visible viewport (exclude overscan).
            int peak=0,peakX=0;
            for(int x=20;x<W-20;x++) if(ridge[x]>peak) {peak=ridge[x];peakX=x;}
            float bottom=1-tops[layer]-(peak+.5f)/H*height/viewH;
            Vector3 center=cam.transform.position+cam.transform.forward*d+
                cam.transform.up*((bottom-.5f)*viewH+height*.5f);
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name=$"MtnLayer_{layer+1}"; quad.transform.SetParent(root,false);
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetPositionAndRotation(center,cam.transform.rotation);
            quad.transform.localScale=new Vector3(width,height,1);
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
            if(Mathf.Abs(measured[layer]-tops[layer])>.019f || bottom>0 || foot.y>-20)
                throw new InvalidOperationException("MTN4 silhouette/foot gate failed");
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[MTN4_LAYER] L{0} peaks={1} ridge_std_px={2:F2} distance_m={3:F0} width_m={4:F2} top_viewport_y={5:F4} foot_viewport_y={6:F4} foot_world_y={7:F2}",
                layer+1,peaks,std,d,width,measured[layer],1-bottom,foot.y));
        }
        Debug.Log($"[MTN5] peaks_per_layer={peakCounts[0]}/{peakCounts[1]}/{peakCounts[2]} asymmetry_ratio=1.6..2.8 width_px=60..260 spurs=1..2 shading_levels=3 seed_per_layer=distinct symmetric_peaks={symmetricPeaks}");
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN4] layers=3 tex=1024x512 peaks=9..14 ridge_std_px>=18 top_viewport_y=L1:{0:F4}/L2:{1:F4}/L3:{2:F4} quads_width_m={3:F2}/{4:F2}/{5:F2} 3d_ridges_disabled=true",
            measured[0],measured[1],measured[2],widths[0],widths[1],widths[2]));
    }
}
