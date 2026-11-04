using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

// Only enabled by the task acceptance flag; export geometry-selected PNG pixels.
public static class FencePathToneAudit {
 public static void Capture(Camera camera,string output,string name){
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-fence-path-tone-audit")<0||name!="cam_play_1080"&&name!="path_closeup")return;
  const int layer=30;var temporary=new List<GameObject>();var kinds=new Dictionary<Collider,string>();
  var player=Object.FindFirstObjectByType<PlayerController>();var posts=Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(c=>c.name.StartsWith("SF2_Post_")).ToArray();
  var sprites=Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Where(r=>r.enabled).ToArray();
  var sun=RenderSettings.sun;var lightDirection=sun!=null?-sun.transform.forward:Vector3.up;
  var csv=new StringBuilder("x,y,kind\n");
  try{
   foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)){
    string kind=r.name=="Paving_Surface"?"path":r.name=="FT3_Batch_0"?"wood":r.name=="FT3_Batch_1"?"snow":null;if(kind==null)continue;
    var f=r.GetComponent<MeshFilter>();var go=new GameObject("FT3_PixelQuery");temporary.Add(go);go.layer=layer;go.transform.SetPositionAndRotation(r.transform.position,r.transform.rotation);go.transform.localScale=r.transform.lossyScale;var col=go.AddComponent<MeshCollider>();col.sharedMesh=f.sharedMesh;kinds[col]=kind;
   }
   Physics.SyncTransforms();
   for(int y=4;y<Screen.height-4;y+=4)for(int x=4;x<Screen.width-4;x+=4){
    var ray=camera.ScreenPointToRay(new Vector3(x+.5f,y+.5f));if(!Physics.Raycast(ray,out var hit,camera.farClipPlane,1<<layer)||!kinds.TryGetValue(hit.collider,out var kind)||kind=="snow")continue;
    if(player.ground.Raycast(ray,out var ground,camera.farClipPlane)&&ground.distance+.02f<hit.distance)continue;
    bool sprite=false;foreach(var r in sprites)if(r.bounds.IntersectRay(ray,out var d)&&d<hit.distance){sprite=true;break;}if(sprite)continue;
    if(kind=="wood"){
     if(Vector3.Dot(hit.normal,lightDirection)<.2f)continue;
     var post=posts.FirstOrDefault(p=>(p.ClosestPoint(hit.point)-hit.point).sqrMagnitude<.000025f);
     if(post==null||hit.distance>18||hit.point.y<post.bounds.min.y+.30f||hit.point.y>post.bounds.max.y-.30f)continue;
     bool interior=true;
     foreach(var offset in new[]{Vector2.left*4,Vector2.right*4,Vector2.up*4,Vector2.down*4}){
      var probe=camera.ScreenPointToRay(new Vector3(x+.5f+offset.x,y+.5f+offset.y));
      if(!post.Raycast(probe,out var ph,camera.farClipPlane)||Vector3.Dot(ph.normal,hit.normal)<.99f){interior=false;break;}
     }
     if(!interior)continue;
     kind="sunlit_post";
    }else if(hit.textureCoord.x>.001f)kind="edge";
    // Erode silhouettes by two pixels; avoids background/DOF mixed edge samples.
    bool same=true;foreach(var o in new[]{Vector2.left*2,Vector2.right*2,Vector2.up*2,Vector2.down*2}){
     if(!Physics.Raycast(camera.ScreenPointToRay(new Vector3(x+.5f+o.x,y+.5f+o.y)),out var neighbor,camera.farClipPlane,1<<layer)||neighbor.collider!=hit.collider){same=false;break;}
    }if(!same)continue;
    csv.Append(x).Append(',').Append(Screen.height-1-y).Append(',').Append(kind).Append('\n');
   }
   File.WriteAllText(Path.Combine(output,name+"_tone_pixels.csv"),csv.ToString());Debug.Log("[FT3_PIXELS] view="+name+" geometry_mask=true pixels="+(csv.ToString().Count(c=>c=='\n')-1));
  }finally{foreach(var go in temporary){go.SetActive(false);Object.Destroy(go);}}
 }
}
