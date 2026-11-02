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
    static int[] Profile(int layer, out int peaks, out float std)
    {
        // 2px raster steps; irregular sparse control points give pointed, painted silhouettes.
        // Search only deterministic noise seeds; acceptance is measured on the final raster.
        for(int attempt=0;attempt<2000;attempt++)
        {
            int seed=731+layer*2341+attempt*97;
            const int knots=37;
            var values=new float[knots]; float lo=1,hi=0;
            for(int i=0;i<knots;i++)
            { values[i]=Ridged(i*7.31f/(knots-1),seed); lo=Mathf.Min(lo,values[i]); hi=Mathf.Max(hi,values[i]); }
            var ridge=new int[W];
            for(int x=0;x<W;x+=2)
            {
                float t=x*(knots-1f)/(W-1); int i=Mathf.Min(knots-2,(int)t);
                float v=Mathf.Lerp(values[i],values[i+1],t-i);
                ridge[x]=ridge[x+1]=Mathf.RoundToInt((300+160*(v-lo)/(hi-lo))/2)*2;
            }
            peaks=0; float sum=0,sum2=0,min=H,max=0; int prev=0;
            for(int x=2;x<W;x+=2)
            {
                int d=Math.Sign(ridge[x]-ridge[x-2]);
                if(d==0) continue;
                if(prev>0 && d<0) { float h=ridge[x-2]; peaks++; sum+=h; sum2+=h*h; min=Mathf.Min(min,h); max=Mathf.Max(max,h); }
                prev=d;
            }
            std=peaks>0?Mathf.Sqrt(Mathf.Max(0,sum2/peaks-Mathf.Pow(sum/peaks,2))):0;
            if(peaks>=9 && peaks<=14 && std>=18 && (max-min)/max>=.15f) return ridge;
        }
        throw new InvalidOperationException("MTN4: no valid ridged multifractal silhouette");
    }
    static Texture2D Paint(int layer,int[] ridge)
    {
        var pixels=new Color32[W*H];
        var rock=new[]{Hex("#4A5666"),Hex("#5E6B7C"),Hex("#74828F")};
        Color snow=Hex("#EAF1F7");
        Color air=layer==1?Hex("#8FA0B4"):Hex("#C3D2E0");
        float mix=layer==0?0:layer==1?.35f:.60f;
        for(int x=0;x<W;x++)
        {
            int l=Mathf.Max(0,x-4),r=Mathf.Min(W-1,x+4);
            // Upward component of the outward normal (-dy/dx,1).
            float slope=(ridge[r]-ridge[l])/(float)Mathf.Max(1,r-l);
            float up=1/Mathf.Sqrt(1+slope*slope);
            int snowDepth=2*Mathf.RoundToInt(Mathf.Lerp(4,40,Mathf.Clamp01((up-.22f)/.78f))/2);
            int jag=Hash(x/6+layer*83)>.83f?2:0;
            for(int y=0;y<=ridge[x];y++)
            {
                int depth=ridge[x]-y;
                // Broken diagonal facets and drainage marks, quantized to the three rock inks.
                float facet=Noise((x+depth*.63f)/47f,layer*97+42);
                float paint=1.7f-depth/235f+facet*.85f;
                int ink=paint>1.22f?2:paint>.43f?1:0;
                Color c=rock[ink];
                if(up>.25f && depth<Mathf.Min(40,snowDepth+jag)) c=snow;
                pixels[y*W+x]=Color.Lerp(c,air,mix);
            }
        }
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
        for(int layer=0;layer<3;layer++)
        {
            int[] ridge=Profile(layer,out int peaks,out float std);
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
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN4] layers=3 tex=1024x512 peaks=9..14 ridge_std_px>=18 top_viewport_y=L1:{0:F4}/L2:{1:F4}/L3:{2:F4} quads_width_m={3:F2}/{4:F2}/{5:F2} 3d_ridges_disabled=true",
            measured[0],measured[1],measured[2],widths[0],widths[1],widths[2]));
    }
}
