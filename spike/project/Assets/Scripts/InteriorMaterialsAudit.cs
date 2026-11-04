using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

public static class InteriorMaterialsAudit
{
    [Serializable] public class Pixel {public string name,material;public int x,y;}
    [Serializable] public class Report {public int width,height;public List<Pixel> yellow=new List<Pixel>();}
    public static void Pews(ChurchInterior interior,Camera cam,Texture2D frame)
    {
        var report=new Report{width=frame.width,height=frame.height};
        const int layer=27;var queries=new List<GameObject>();var ids=new Dictionary<Collider,MeshRenderer>();
        foreach(var r in interior.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(!r.enabled || !r.gameObject.activeInHierarchy || r.name.StartsWith("StaticArtBatch") || r.name.StartsWith("Interior_MaterialsGlow") || r.sharedMaterials.Any(m=>m==null || m.renderQueue>=2500))continue;
            var go=new GameObject("MaterialsVisibilityQuery");go.layer=layer;go.transform.SetPositionAndRotation(r.transform.position,r.transform.rotation);go.transform.localScale=r.transform.lossyScale;
            var c=go.AddComponent<MeshCollider>();c.sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;queries.Add(go);ids[c]=r;
        }
        Physics.SyncTransforms();bool back=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
        for(int y=0;y<270;y++)for(int x=0;x<480;x++)
        {
            var vp=new Vector3((x+.5f)/480,(y+.5f)/270,0);
            if(!Physics.Raycast(cam.ViewportPointToRay(vp),out var hit,cam.farClipPlane,1<<layer))continue;
            var r=ids[hit.collider];
            if((!r.name.StartsWith("Interior_Candle_") || r.name.EndsWith("_Flame")) && r.name!="Interior_RoyalDoor_Frame")continue;
            report.yellow.Add(new Pixel{name=r.name,material=r.sharedMaterial.name,x=(int)(vp.x*frame.width),y=(int)(vp.y*frame.height)});
        }
        Physics.queriesHitBackfaces=back;foreach(var go in queries){go.SetActive(false);Object.Destroy(go);}
        foreach(var group in report.yellow.GroupBy(p=>p.name))
        {
            var selected=group.AsEnumerable();
            if(group.Key=="Interior_RoyalDoor_Frame")
            {
                float midX=(group.Min(p=>p.x)+group.Max(p=>p.x))*.5f,midY=(group.Min(p=>p.y)+group.Max(p=>p.y))*.5f;
                selected=group.Where(p=>p.x<midX && p.y<midY);
            }
            float sum=0,max=0;foreach(var p in selected){var c=frame.GetPixel(p.x,p.y);float l=.2126f*c.r+.7152f*c.g+.0722f*c.b;sum+=l;max=Mathf.Max(max,l);}
            string tag=group.Key=="Interior_RoyalDoor_Frame"?"YELLOW_STRIP":"YELLOW_STRIP_CANDIDATE";
            string identity=group.Key=="Interior_RoyalDoor_Frame"?"royal_door_gilt_frame":"standing_gilt_candle_holder";
            Debug.Log(FormattableString.Invariant($"[{tag}] name={group.Key} material={group.First().material} luma={sum/selected.Count():F5} max_luma={max:F5} pixels={selected.Count()} roi=left_vertical_lower_half identity={identity} space=display_rec709"));
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(InteriorPewsAudit.ShotDirectory(),"interior_materials_samples.json"),JsonUtility.ToJson(report,true));
    }
}
