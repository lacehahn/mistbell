using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

public static class InteriorPewsAudit
{
    [Serializable] public class Sample {public string name;public int x,y;public float above,below;}
    [Serializable] public class Report {public int width,height;public List<Sample> rails=new List<Sample>();public List<Sample> icons=new List<Sample>();public int[] visibleRows=new int[2];public float[] nicheLuma=new float[2];public float minGap;public float ringY,topY;public int chainSegments;}
    public static string ShotDirectory()
    {
        var dir=new System.IO.DirectoryInfo(Application.dataPath);while(dir!=null && dir.Name!="Builds")dir=dir.Parent;
        return System.IO.Path.Combine(dir!=null?dir.Parent.FullName:System.IO.Directory.GetCurrentDirectory(),"Shots");
    }
    static float Luma(Color c)=>.2126f*c.r+.7152f*c.g+.0722f*c.b;
    public static void Facing(ChurchInterior interior)
    {
        var root=interior.detailRoot.transform;
        var altar=root.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name=="Interior_Altar_Model");
        float altarZ=altar.bounds.center.z,firstZ=root.Find("Interior_V2Pew_0_0_Seat").position.z;
        int checkedPews=0;float minOffset=float.PositiveInfinity,maxOffset=float.NegativeInfinity;
        for(int side=0;side<2;side++)for(int row=0;row<6;row++)
        {
            string prefix="Interior_V2Pew_"+side+"_"+row;
            var seat=root.Find(prefix+"_Seat");var back=root.Find(prefix+"_Back");var rim=root.Find(prefix+"_Rim");
            float offset=back.position.z-seat.position.z;
            if(!(offset<0 && rim.position.z<back.position.z && altarZ>seat.position.z) ||
                Quaternion.Angle(back.rotation,Quaternion.Euler(-6,0,0))>.01f ||
                Quaternion.Angle(rim.rotation,Quaternion.Euler(-6,0,0))>.01f)
                throw new InvalidOperationException("Pew facing gate failed: "+prefix);
            minOffset=Mathf.Min(minOffset,offset);maxOffset=Mathf.Max(maxOffset,offset);checkedPews++;
        }
        Debug.Log(FormattableString.Invariant($"[PEW_FACING] back_z_side=-1 seat_front_z=+1 faces_altar=true altar_z≈{altarZ:F3} pew_z≈{firstZ:F3} checked={checkedPews} back_offset_min={minOffset:F5} back_offset_max={maxOffset:F5}"));
    }
    public static void Inside(ChurchInterior interior,Camera cam,Texture2D frame)
    {
        Facing(interior);
        var report=new Report{width=frame.width,height=frame.height,minGap=100};
        const int layer=28;var queries=new List<GameObject>();var ids=new Dictionary<Collider,string>();
        var renderers=interior.GetComponentsInChildren<MeshRenderer>();
        foreach(var r in renderers)
        {
            if(!r.enabled || !r.gameObject.activeInHierarchy || r.name.StartsWith("StaticArtBatch") || r.sharedMaterials.Any(m=>m==null || m.renderQueue>=2500))continue;
            var go=new GameObject("PewsVisibilityQuery");go.layer=layer;go.transform.SetPositionAndRotation(r.transform.position,r.transform.rotation);go.transform.localScale=r.transform.lossyScale;
            var c=go.AddComponent<MeshCollider>();c.sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;queries.Add(go);ids[c]=r.name;
        }
        Physics.SyncTransforms();bool back=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
        bool Visible(Vector3 p,string name,out Vector3 vp)
        {
            vp=cam.WorldToViewportPoint(p);if(vp.z<=0 || vp.x<0 || vp.x>=1 || vp.y<0 || vp.y>=1)return false;
            return Physics.Raycast(cam.ViewportPointToRay(vp),out var hit,cam.farClipPlane,1<<layer) && ids[hit.collider]==name;
        }
        for(int side=0;side<2;side++)for(int row=0;row<6;row++)
        {
            string prefix="Interior_V2Pew_"+side+"_"+row;
            var rim=renderers.First(r=>r.name==prefix+"_Rim");int samples=0;
            for(int i=0;i<17;i++)
            {
                var p=rim.transform.TransformPoint(new Vector3(-.45f+i*.9f/16,0,-.5f));
                if(!Visible(p,rim.name,out var vp))continue;
                int x=(int)(vp.x*frame.width),y=(int)(vp.y*frame.height);
                if(y<3 || y>=frame.height-3)continue;
                float a=Luma(frame.GetPixel(x,y+3)),b=Luma(frame.GetPixel(x,y-3));
                report.rails.Add(new Sample{name=rim.name,x=x,y=y,above=a,below=b});samples++;
            }
            if(samples>=3)report.visibleRows[side]++;
            if(row<5)
            {
                var bounds=new Bounds();bool first=true;
                foreach(var r in renderers.Where(r=>r.name.StartsWith(prefix+"_"))){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
                string next="Interior_V2Pew_"+side+"_"+(row+1)+"_";
                float nextMin=renderers.Where(r=>r.name.StartsWith(next)).Min(r=>r.bounds.min.z);
                report.minGap=Mathf.Min(report.minGap,nextMin-bounds.max.z);
            }
        }
        for(int i=0;i<2;i++)
        {
            string name="Interior_SideIcon_"+i;var panel=renderers.First(r=>r.name==name);Color sum=Color.black;int count=0;
            for(int y=0;y<15;y++)for(int x=0;x<11;x++)
            {
                var p=panel.bounds.center+new Vector3((x/10f-.5f)*.30f,(y/14f-.5f)*.48f,0);
                if(!Visible(p,name,out var vp))continue;
                int sx=(int)(vp.x*frame.width),sy=(int)(vp.y*frame.height);
                sum+=frame.GetPixel(sx,sy);count++;report.icons.Add(new Sample{name=name,x=sx,y=sy});
            }
            report.nicheLuma[i]=count>0?Luma((sum/count).linear):0;
        }
        report.chainSegments=renderers.Count(r=>r.name.StartsWith("Interior_Chain_Link_"));
        report.ringY=renderers.First(r=>r.name=="Interior_Chandelier").bounds.center.y;
        report.topY=renderers.First(r=>r.name=="Interior_Chain_CeilingStem").bounds.max.y;
        Physics.queriesHitBackfaces=back;foreach(var go in queries){go.SetActive(false);Object.Destroy(go);}
        System.IO.File.WriteAllText(System.IO.Path.Combine(ShotDirectory(),"interior_pews_samples.json"),JsonUtility.ToJson(report,true));
        int legs=renderers.Count(r=>r.name.StartsWith("Interior_V2Pew_") && r.name.EndsWith("_Leg"));
        float width=renderers.First(r=>r.name=="Interior_V2Pew_0_0_Seat").bounds.size.x;
        Debug.Log(FormattableString.Invariant($"[PEW_V2] rows=6 width={width:F2} gap={report.minGap:F2} legs={legs/12} rail=true visible_rows={Mathf.Min(report.visibleRows[0],report.visibleRows[1])} left={report.visibleRows[0]} right={report.visibleRows[1]} measurement=mesh_raycast"));
        Debug.Log(FormattableString.Invariant($"[CHANDELIER] chain_segments={report.chainSegments} top_y={report.topY:F3} ring_y={report.ringY:F3}"));
        Debug.Log(FormattableString.Invariant($"[SIDE_NICHE] panels=2 luma={(report.nicheLuma[0]+report.nicheLuma[1])/2:F5} left={report.nicheLuma[0]:F5} right={report.nicheLuma[1]:F5} space=linear_mean_rgb"));
    }
}
