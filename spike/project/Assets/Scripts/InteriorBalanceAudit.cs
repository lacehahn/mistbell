using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

public static class InteriorBalanceAudit
{
    // Same fixed top-left ROIs and linearization-of-mean as measure_interior.py.
    static float Region(Texture2D frame,float x0,float y0,float x1,float y1)
    {
        Color sum=Color.black;int n=0;
        for(int y=(int)((1-y1)*frame.height);y<(int)((1-y0)*frame.height);y++)for(int x=(int)(x0*frame.width);x<(int)(x1*frame.width);x++){sum+=frame.GetPixel(x,y);n++;}
        return Luma((sum/n).linear);
    }
    static float Luma(Color c)=>.2126f*c.r+.7152f*c.g+.0722f*c.b;
    static float Point(Texture2D f,Camera cam,Vector3 p)
    {
        var v=cam.WorldToViewportPoint(p);if(v.z<=0 || v.x<0 || v.x>=1 || v.y<0 || v.y>=1)return float.NaN;
        return Luma(f.GetPixel((int)(v.x*f.width),(int)(v.y*f.height)).linear);
    }
    public static void Inside(ChurchInterior interior,Camera cam,Texture2D f)
    {
        float l=Region(f,.06f,.35f,.20f,.62f),r=Region(f,.80f,.35f,.94f,.62f),n=Region(f,.36f,.72f,.64f,.92f);
        Debug.Log(FormattableString.Invariant($"[INT_BALANCE] left={l:F5} right={r:F5} nave={n:F5} ratio={l/r:F5}"));
        var details=interior.detailRoot.GetComponentsInChildren<MeshRenderer>();
        float delta=0;int samples=0;var pairs=new List<string>();
        foreach(var leg in details.Where(x=>x.name.StartsWith("Interior_V2Pew_") && x.name.EndsWith("_Leg")))
        {
            var p=leg.bounds.center;p.y=interior.FloorY+.035f;p.z-=.031f;
            var floor=p;floor.y=interior.FloorY+.01f;floor.z-=.035f;
            float a=Point(f,cam,p),b=Point(f,cam,floor);if(float.IsNaN(a)||float.IsNaN(b))continue;
            delta=Mathf.Max(delta,Mathf.Abs(a-b));samples++;
            var u=cam.WorldToViewportPoint(p);var v=cam.WorldToViewportPoint(floor);
            pairs.Add($"{(int)(u.x*f.width)},{(int)(u.y*f.height)},{(int)(v.x*f.width)},{(int)(v.y*f.height)}");
        }
        Debug.Log("[PEW_SAMPLES] "+string.Join(";",pairs));
        int shadows=details.Count(x=>x.name.StartsWith("Interior_PewContact_"));
        Debug.Log(FormattableString.Invariant($"[PEW_GROUND] shadows={shadows} edge_delta={delta:F5} samples={samples} blend=alpha alpha=0.20"));
    }
    public static void Dome(ChurchInterior interior,Camera cam,Texture2D frame)
    {
        const int layer=29;var queries=new List<GameObject>();var ids=new Dictionary<Collider,string>();
        foreach(var r in interior.detailRoot.GetComponentsInChildren<MeshRenderer>())
        {
            if(!r.enabled || !r.gameObject.activeInHierarchy || r.name.StartsWith("StaticArtBatch") || r.sharedMaterials.Any(m=>m==null || m.renderQueue>=2500))continue;
            var go=new GameObject("BalanceQuery");go.layer=layer;go.transform.SetPositionAndRotation(r.transform.position,r.transform.rotation);go.transform.localScale=r.transform.lossyScale;
            var c=go.AddComponent<MeshCollider>();c.sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;queries.Add(go);ids[c]=r.name;
        }
        var mask=new Texture2D(480,270,TextureFormat.RGB24,false);mask.SetPixels(new Color[480*270]);
        Physics.SyncTransforms();var sums=new Dictionary<string,double>();var counts=new Dictionary<string,int>();
        bool back=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
        for(int y=0;y<270;y++)for(int x=0;x<480;x++)
        {
            var v=new Vector3((x+.5f)/480,(y+.5f)/270,0);
            if(!Physics.Raycast(cam.ViewportPointToRay(v),out var hit,cam.farClipPlane,1<<layer))continue;
            string name=ids[hit.collider];
            if(name!="Interior_DomeInner" && name!="Interior_OculusSky" && !name.StartsWith("Interior_ConeRib_"))continue;
            mask.SetPixel(x,y,name.StartsWith("Interior_ConeRib_")?Color.red:name=="Interior_OculusSky"?Color.blue:Color.green);
            string key=name.StartsWith("Interior_ConeRib_")?"rib":name;
            float l=Luma(frame.GetPixel((int)(v.x*frame.width),(int)(v.y*frame.height)));
            if(!sums.ContainsKey(key)){sums[key]=0;counts[key]=0;}sums[key]+=l;counts[key]++;
        }
        mask.Apply();
        var dir=new System.IO.DirectoryInfo(Application.dataPath);while(dir!=null && dir.Name!="Builds")dir=dir.Parent;
        string output=System.IO.Path.Combine(dir!=null?dir.Parent.FullName:System.IO.Directory.GetCurrentDirectory(),"Shots/interior_dome_mask.png");
        System.IO.File.WriteAllBytes(output,mask.EncodeToPNG());Object.Destroy(mask);
        Physics.queriesHitBackfaces=back;foreach(var go in queries){go.SetActive(false);Object.Destroy(go);}
        double Avg(string key)=>counts.ContainsKey(key)?sums[key]/counts[key]:0;
        double plaster=Avg("Interior_DomeInner"),rib=Avg("rib"),sky=Avg("Interior_OculusSky"),contrast=plaster>0?Math.Abs(rib-plaster)/plaster:0;
        var mat=interior.detailRoot.transform.Find("Interior_DomeInner").GetComponent<Renderer>().sharedMaterial;
        bool smooth=mat.GetTexture("_BaseMap")==null && Mathf.Abs(mat.GetFloat("_Smoothness")-.15f)<.001f;
        Debug.Log(FormattableString.Invariant($"[DOME_SKIN] plaster={smooth.ToString().ToLowerInvariant()} ribs_visible={(counts.ContainsKey("rib") && counts["rib"]>=12).ToString().ToLowerInvariant()} rib_contrast={contrast:F5} oculus_luma={sky:F5} plaster_luma={plaster:F5} rib_luma={rib:F5} rib_pixels={(counts.ContainsKey("rib")?counts["rib"]:0)} oculus_pixels={(counts.ContainsKey("Interior_OculusSky")?counts["Interior_OculusSky"]:0)}"));
    }
}
