using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

// Diagnostic geometry queries only; no render/material/camera changes.
public sealed class FarMistAudit:MonoBehaviour
{
    [Serializable] public class Ridge {public Vector3[] points;}
    [Serializable] public class Projected {public Vector2[] points;}
    [Serializable] public class Projection {public int width,height;public Projected[] ridges;}
    public Ridge[] ridges;
    const int QueryLayer=30;
    static bool Rock(string n)=>n.StartsWith("ClusterRock_")||n.StartsWith("Rock_");
    static float Lin(float v)=>v<=.04045f?v/12.92f:Mathf.Pow((v+.055f)/1.055f,2.4f);
    static Vector2 Lab(Color c)
    {
        float r=Lin(c.r),g=Lin(c.g),b=Lin(c.b);float y=.2126729f*r+.7151522f*g+.072175f*b,z=(.0193339f*r+.119192f*g+.9503041f*b)/1.08883f;
        float F(float v)=>v>.008856f?Mathf.Pow(v,1f/3):(903.3f*v+16)/116;
        return new Vector2(116*F(y)-16,200*(F(y)-F(z)));
    }
    public static void Capture(Camera cam,Texture2D frame,string output)
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"-far-mist-audit")<0)return;
        var audit=Object.FindFirstObjectByType<FarMistAudit>();
        var projection=new Projection {width=frame.width,height=frame.height,ridges=audit.ridges.Select(r=>new Projected {points=r.points.Select(p=>{var v=cam.WorldToViewportPoint(p);return new Vector2(v.x*frame.width,(1-v.y)*frame.height);}).ToArray()}).ToArray()};
        File.WriteAllText(Path.Combine(output,"far_mist_contours.json"),JsonUtility.ToJson(projection,true));
        Surface(cam,frame,output,"reveal_80");
    }
    public static void Surface(Camera cam,Texture2D frame,string output,string name)
    {
        var queries=new List<GameObject>();var targets=new Dictionary<Collider,(string name,int shadeStart)>();
        bool back=Physics.queriesHitBackfaces;float minL=100;double postB=0,postL=0;int darkN=0,postN=0,rockN=0;
        const int w=960,h=540;var mask=new Texture2D(w,h,TextureFormat.RGB24,false);var pixels=new Color32[w*h];
        try
        {
            var frustum=GeometryUtility.CalculateFrustumPlanes(cam);
            foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if(!r.enabled||!r.gameObject.activeInHierarchy||r.name.StartsWith("Static")||r.name=="Sky Dome"||r.sharedMaterials.Any(m=>m==null||m.renderQueue>=2500||m.shader.name=="Mistbell/PaintedMountain")||!GeometryUtility.TestPlanesAABB(frustum,r.bounds))continue;
                var f=r.GetComponent<MeshFilter>();if(f==null||f.sharedMesh==null||f.sharedMesh.vertexCount<3)continue;
                var go=new GameObject("FarMistQuery");go.layer=QueryLayer;queries.Add(go);go.transform.SetPositionAndRotation(r.transform.position,r.transform.rotation);go.transform.localScale=r.transform.lossyScale;
                var c=go.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;
                int shade=f.sharedMesh.subMeshCount>=3?(int)((f.sharedMesh.GetIndexCount(0)+f.sharedMesh.GetIndexCount(1))/3):int.MaxValue;
                targets[c]=(r.name,shade);
            }
            Physics.SyncTransforms();Physics.queriesHitBackfaces=true;
            for(int y=0;y<h*.45f;y++)for(int x=0;x<w;x++)
            {
                if(!Physics.Raycast(cam.ViewportPointToRay(new Vector3((x+.5f)/w,(y+.5f)/h,0)),out var hit,cam.farClipPlane,1<<QueryLayer,QueryTriggerInteraction.Ignore)||!targets.TryGetValue(hit.collider,out var t))continue;
                int px=(2*x+1)*frame.width/(2*w),py=(2*y+1)*frame.height/(2*h);var lab=Lab(frame.GetPixel(px,py));
                if(Rock(t.name)){rockN++;pixels[y*w+x]=new Color32(255,0,0,255);if(hit.triangleIndex>=t.shadeStart){darkN++;minL=Mathf.Min(minL,lab.x);pixels[y*w+x]=new Color32(0,0,255,255);}}
                if(t.name.StartsWith("FencePost_")){postN++;postB+=lab.y;postL+=lab.x;pixels[y*w+x]=new Color32(0,255,0,255);}
            }
            mask.SetPixels32(pixels);mask.Apply();File.WriteAllBytes(Path.Combine(output,"far_mist_"+name+"_mask.png"),mask.EncodeToPNG());
            Debug.Log(FormattableString.Invariant($"[SNOW_ROCK_PIXEL] view={name} dark_face_L={(darkN>0?minL:float.NaN):F6} dark_samples={darkN} rock_samples={rockN} metric=min_sampled_PNG_CIELAB_D65"));
            Debug.Log(FormattableString.Invariant($"[FENCE] view={name} post_b_star={(postN>0?postB/postN:double.NaN):F6} post_L={(postN>0?postL/postN:double.NaN):F6} samples={postN} metric=mean_sampled_PNG_CIELAB_D65"));
        }
        finally {Physics.queriesHitBackfaces=back;foreach(var go in queries){go.SetActive(false);Object.Destroy(go);}Object.Destroy(mask);}
    }
}
