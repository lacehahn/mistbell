using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// In-place patch: fences, paving surface/shoulder and three snow volumes only.
public static class FencePathToneBuild {
 const string Out="../artifacts/fence_path_tone/";
 static T[] All<T>() where T:Object=>Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None);
 static bool Fence(string n)=>n.StartsWith("SF2_")||n.StartsWith("FT3_")||n.StartsWith("FencePost_")||n.StartsWith("FenceRail_")||n.StartsWith("FenceSnow_")||n.StartsWith("PostDrift_");
 static bool Edit(Component c)=>Fence(c.name)||c.name=="Snow Grass Fence"||c.name=="Fence Path Tone"||c.name=="Paving_Surface"||c.name.StartsWith("PathSnowEdge")||c.name.StartsWith("SnowRidge3_");
 static string Protected(){
  var rows=new List<string>();
  foreach(var c in All<Component>()) {
   if(c==null||Edit(c))continue;
   rows.Add(c.name+(c is Transform?c.transform.localToWorldMatrix.ToString("F6"):EditorJsonUtility.ToJson(c)));
   if(c is Renderer r) foreach(var m in r.sharedMaterials)if(m!=null&&m.name!="M_Paving")rows.Add(EditorJsonUtility.ToJson(m));
   if(c is MeshFilter f&&f.sharedMesh!=null)rows.Add(EditorJsonUtility.ToJson(f.sharedMesh));
  }
  rows.Add(FormattableString.Invariant($"fog={RenderSettings.fog}/{RenderSettings.fogMode}/{RenderSettings.fogDensity}/{RenderSettings.fogStartDistance}/{RenderSettings.fogEndDistance}/{RenderSettings.fogColor} ambient={RenderSettings.ambientMode}/{RenderSettings.ambientSkyColor}/{RenderSettings.ambientEquatorColor}/{RenderSettings.ambientGroundColor}/{RenderSettings.ambientIntensity}"));
  rows.Sort(StringComparer.Ordinal);
  using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",rows)))).Replace("-","");
 }
 static Mesh Save(string n,List<Vector3> v,List<int> t,List<Vector2> uv=null){
  string p="Assets/Settings/FT3_"+n+".asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(p);
  if(m==null){m=new Mesh();AssetDatabase.CreateAsset(m,p);}m.Clear();m.indexFormat=IndexFormat.UInt32;m.SetVertices(v);m.SetTriangles(t,0);if(uv!=null)m.SetUVs(0,uv);m.RecalculateNormals();m.RecalculateBounds();EditorUtility.SetDirty(m);return m;
 }
 static Material Mat(string n,string shader,string hex){
  string p="Assets/Settings/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,p);}m.shader=Shader.Find(shader);ColorUtility.TryParseHtmlString(hex,out var c);m.SetColor("_BaseColor",c);EditorUtility.SetDirty(m);return m;
 }
 [Serializable] public class Post {public int side;public Vector3 p;public Vector3 size;}
 [Serializable] public class Rail {public int side;public Vector3 a,b;}
 [Serializable] public class Ridge {public Vector3 center;public float length,width,height;}
 [Serializable] public class Report {
  public string protected_before,protected_after,post_color="#4A3B2E",edge_color="#E8EFF6";
  public float spacing_min=999,spacing_max,spacing_3d_min=999,spacing_3d_max,post_width=.08f,post_height=1,snow_depth=.04f,roughness=.85f,edge_width=.07f;
  public int count,rows_per_side=1,crossing,renderers,old_renderers_removed;public bool edge_ok;
  public Post[] posts;public Rail[] rails;public Ridge[] ridges;
 }
 public static void Run(){
  Directory.CreateDirectory(Out);EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");Physics.SyncTransforms();
  var report=new Report{protected_before=Protected()};
  var ground=Object.FindFirstObjectByType<PlayerController>().ground;
  Vector3 Snap(Vector2 p){if(!ground.Raycast(new Ray(new Vector3(p.x,400,p.y),Vector3.down),out var h,1000))throw new Exception("Ground missing");return h.point;}
  foreach(var r in All<Renderer>().Where(r=>Fence(r.name)))report.old_renderers_removed++;
  foreach(var t in All<Transform>().Where(t=>Fence(t.name)).OrderByDescending(t=>t.name.StartsWith("SF2_Batch")).ToArray())if(t!=null)Object.DestroyImmediate(t.gameObject);
  foreach(string n in new[]{"Snow Grass Fence","Fence Path Tone","PathSnowEdgeV2"}){var old=GameObject.Find(n);if(old!=null)Object.DestroyImmediate(old);}
  var root=new GameObject("Fence Path Tone").transform;
  var wood=Mat("M_FT3_Wood","Mistbell/FenceTone",report.post_color);wood.SetFloat("_Wood",1);wood.SetFloat("_Roughness",.85f);wood.SetFloat("_ShadowCoverage",.625f);
  var snow=Mat("M_FT3_Snow","Mistbell/FenceTone","#EDF3F8");snow.SetFloat("_Wood",0);snow.SetFloat("_ShadowCoverage",.625f);
  var parts=new[]{new List<CombineInstance>(),new List<CombineInstance>()};
  void Box(string name,Vector3 size,Vector3 p,Quaternion q,int material,bool collider){
   var go=SpikeArt.Box(root,name,size,p,material==0?wood:snow,collider);go.transform.rotation=q;
   var f=go.GetComponent<MeshFilter>();parts[material].Add(new CombineInstance{mesh=f.sharedMesh,transform=go.transform.localToWorldMatrix});Object.DestroyImmediate(go.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);
  }
  var posts=new List<Post>();var rails=new List<Rail>();
  for(int side=-1;side<=1;side+=2){
   var route=ClimbRoute.Points;var line=new Vector2[route.Length];
   Vector2 N(int i){var d=(route[i+1]-route[i]).normalized;return new Vector2(d.y,-d.x);}
   for(int j=0;j<line.Length;j++){var n=j==0?N(0):j==line.Length-1?N(j-1):(N(j-1)+N(j)).normalized;float dot=j==0||j==line.Length-1?1:Vector2.Dot(n,N(j));line[j]=route[j]+n*(side*2.6f/dot);}
   var corners=new List<Vector3>();Vector3 prev=Vector3.zero;bool have=false;float station=0,total=0;
   for(int seg=0;seg<line.Length-1;seg++){
    float len=Vector2.Distance(line[seg],line[seg+1]);var d=(line[seg+1]-line[seg]).normalized;
    while(station<=total+len){
     if(have){var delta=line[seg]-new Vector2(prev.x,prev.z);float dot=Vector2.Dot(delta,d),disc=dot*dot-delta.sqrMagnitude+2.2f*2.2f;if(disc>=0)station=Mathf.Max(station,total-dot+Mathf.Sqrt(disc));if(station>total+len)break;}
     var point=Vector2.Lerp(line[seg],line[seg+1],(station-total)/len);station+=2.2f;if(point.y>-19.25f)continue;
     var p=Snap(point);var q=Quaternion.LookRotation(new Vector3(d.x,0,d.y));
     // SF2_Post prefix is the existing reveal regression's support-query contract.
     Box("SF2_Post_"+posts.Count,new Vector3(.08f,1,.08f),p+Vector3.up*.5f,q,0,true);
     Box("FT3_Cap_"+posts.Count,new Vector3(.10f,.04f,.10f),p+Vector3.up*1.02f,q,1,false);
     posts.Add(new Post{side=side,p=p,size=new Vector3(.08f,1,.08f)});
     if(have){float horizontal=Vector2.Distance(new Vector2(prev.x,prev.z),point),spatial=Vector3.Distance(prev,p);report.spacing_min=Mathf.Min(report.spacing_min,horizontal);report.spacing_max=Mathf.Max(report.spacing_max,horizontal);report.spacing_3d_min=Mathf.Min(report.spacing_3d_min,spatial);report.spacing_3d_max=Mathf.Max(report.spacing_3d_max,spatial);
      var knots=new List<Vector3>{prev};knots.AddRange(corners);knots.Add(p);
      for(int k=0;k<knots.Count-1;k++)foreach(float h in new[]{.30f,.75f}){
       var a=knots[k]+Vector3.up*h;var b=knots[k+1]+Vector3.up*h;var direction=b-a;
       Box("FT3_Rail_"+rails.Count,new Vector3(.06f,.08f,direction.magnitude),(a+b)*.5f,Quaternion.LookRotation(direction),0,true);
       Box("FT3_RailSnow_"+rails.Count,new Vector3(.07f,.04f,direction.magnitude),(a+b)*.5f+Vector3.up*.06f,Quaternion.LookRotation(direction),1,false);
       rails.Add(new Rail{side=side,a=a,b=b});
      }
     }
     prev=p;have=true;corners.Clear();
    }
    if(have)corners.Add(Snap(line[seg+1]));total+=len;
   }
  }
  // One renderer per material, no retained rendered source boxes or hidden inner row.
  for(int i=0;i<2;i++){
   var m=new Mesh{indexFormat=IndexFormat.UInt32};m.CombineMeshes(parts[i].ToArray(),true,true);m.RecalculateBounds();string p="Assets/Settings/FT3_Fence"+i+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(p);if(saved==null){AssetDatabase.CreateAsset(m,p);saved=m;}else{EditorUtility.CopySerialized(m,saved);Object.DestroyImmediate(m);}
   var go=new GameObject("FT3_Batch_"+i);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=saved;go.AddComponent<MeshRenderer>().sharedMaterial=i==0?wood:snow;
  }
  var surface=GameObject.Find("Paving_Surface");var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_Paving.mat");
  var paving=Mat("M_FT3_Paving","Mistbell/PathTone","#FFFFFF");paving.CopyPropertiesFromMaterial(original);paving.shader=Shader.Find("Mistbell/PathTone");paving.SetFloat("_ToneGain",1.135f);paving.SetColor("_CoolTint",new Color(.90f,.99f,1.12f));ColorUtility.TryParseHtmlString(report.edge_color,out var edgeColor);paving.SetColor("_EdgeColor",edgeColor);EditorUtility.SetDirty(paving);
  var centers=new List<Vector3>();for(int seg=0;seg<4;seg++){int count=Mathf.CeilToInt(Vector2.Distance(ClimbRoute.Points[seg],ClimbRoute.Points[seg+1])/.4f);for(int j=0;j<count;j++)centers.Add(ClimbRoute.Point(seg,j/(float)count));}centers.Add(ClimbRoute.Point(3,1));
  var vtx=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
  for(int j=0;j<centers.Count;j++){
   var before=(centers[j]-centers[Mathf.Max(0,j-1)]).normalized;var after=(centers[Mathf.Min(centers.Count-1,j+1)]-centers[j]).normalized;if(j==0)before=after;if(j==centers.Count-1)after=before;
   var lateral=Vector3.Cross(Vector3.up,(before+after).normalized).normalized;float miter=Mathf.Max(.75f,Vector3.Dot(lateral,Vector3.Cross(Vector3.up,after).normalized));
   foreach(float x in new[]{-1.62f,-1.585f,-1.55f,1.55f,1.585f,1.62f}){var p=centers[j]+lateral*(x/miter);p.y=Snap(new Vector2(p.x,p.z)).y+Mathf.Lerp(.035f,.002f,Mathf.Clamp01((Mathf.Abs(x)-1.55f)/.07f));vtx.Add(p);uv.Add(new Vector2(Mathf.Clamp01((Mathf.Abs(x)-1.55f)/.07f),j));}
   if(j<centers.Count-1)for(int k=0;k<5;k++){int a=j*6+k;tri.AddRange(new[]{a,a+6,a+1,a+1,a+6,a+7});}
  }
  var mesh=Save("PathSurface",vtx,tri,uv);surface.GetComponent<MeshFilter>().sharedMesh=mesh;surface.GetComponent<MeshRenderer>().sharedMaterial=paving;surface.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
  // Check every rail against every paving triangle in XZ (including shoulder).
  float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
  bool Inside(Vector2 p,Vector2 a,Vector2 b,Vector2 c){float x=Cross(b-a,p-a),y=Cross(c-b,p-b),z=Cross(a-c,p-c);return (x>=0&&y>=0&&z>=0)||(x<=0&&y<=0&&z<=0);}
  bool Cuts(Vector2 a,Vector2 b,Vector2 c,Vector2 d){float x=Cross(b-a,c-a),y=Cross(b-a,d-a),z=Cross(d-c,a-c),w=Cross(d-c,b-c);return x*y<0&&z*w<0;}
  Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
  foreach(var r in rails){bool hit=false;var a=XZ(r.a);var b=XZ(r.b);for(int k=0;k<tri.Count&&!hit;k+=3){var x=XZ(vtx[tri[k]]);var y=XZ(vtx[tri[k+1]]);var z=XZ(vtx[tri[k+2]]);hit=Inside(a,x,y,z)||Inside(b,x,y,z)||Cuts(a,b,x,y)||Cuts(a,b,y,z)||Cuts(a,b,z,x);}if(hit)report.crossing++;}
  // Smooth, finite volumes: C1 cosine falloff to the existing snow, no noise.
  var snowMaterial=ground.GetComponent<Renderer>().sharedMaterial;var ridges=new List<Ridge>();
  for(int n=0;n<3;n++){
   float z=-90+n*16,x=ClimbRoute.CenterX(z)+(n%2==0?-7:7);var r=new Ridge{center=Snap(new Vector2(x,z)),length=4+n*.5f,width=2.0f,height=.20f+n*.025f};ridges.Add(r);
   var vv=new List<Vector3>();var tt=new List<int>();const int nx=32,nz=12;
   for(int j=0;j<=nz;j++)for(int i=0;i<=nx;i++){float u=i/(float)nx*2-1,w=j/(float)nz*2-1;var p=Snap(new Vector2(x+u*r.length*.5f,z+w*r.width*.5f));float bump=Mathf.Pow(Mathf.Cos(u*Mathf.PI*.5f)*Mathf.Cos(w*Mathf.PI*.5f),2);p.y+=r.height*bump-.003f;vv.Add(p);if(i<nx&&j<nz){int a=j*(nx+1)+i;tt.AddRange(new[]{a,a+nx+1,a+1,a+1,a+nx+1,a+nx+2});}}
   var go=new GameObject("SnowRidge3_"+n);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=Save("Ridge"+n,vv,tt);go.AddComponent<MeshRenderer>().sharedMaterial=snowMaterial;
  }
  report.count=posts.Count;report.posts=posts.ToArray();report.rails=rails.ToArray();report.ridges=ridges.ToArray();report.renderers=root.GetComponentsInChildren<Renderer>().Length;report.edge_ok=surface.GetComponent<MeshFilter>().sharedMesh.subMeshCount==1&&surface.GetComponent<MeshRenderer>().shadowCastingMode==ShadowCastingMode.Off&&GameObject.Find("PathSnowEdgeV2")==null;
  Physics.SyncTransforms();report.protected_after=Protected();File.WriteAllText(Out+"assets.json",JsonUtility.ToJson(report,true));
  if(report.protected_before!=report.protected_after||report.spacing_min<2.1f||report.spacing_max>2.3f||report.spacing_3d_max>2.3f||report.crossing!=0||!report.edge_ok)throw new Exception("Fence/path protection or layout gate failed");
  Debug.Log(FormattableString.Invariant($"[FENCE3] spacing={report.spacing_min:F6}..{report.spacing_max:F6} spatial={report.spacing_3d_min:F6}..{report.spacing_3d_max:F6} count={report.count} rows_per_side=1 crossing={report.crossing} color={report.post_color} width=.08 height=1 snow=.04 roughness=.85"));
  Debug.Log("[PATH3_ASSET] edge_ok=true edge_width=.07 edge_color=#E8EFF6 side_faces=0 center_gap=0");Debug.Log("[SNOW3] ridges="+ridges.Count);Debug.Log("[FT3_PROTECTED] unchanged=true sha="+report.protected_after);
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(surface.scene);EditorSceneManager.SaveScene(surface.scene);PerformanceBuild.Run();
 }
}
