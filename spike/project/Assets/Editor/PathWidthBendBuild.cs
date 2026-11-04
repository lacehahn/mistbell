using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Local, repeatable path-only patch. Existing materials, terrain and cameras are reused.
public static class PathWidthBendBuild {
 const string Out="../artifacts/path_width_bend/";
 static T[] All<T>() where T:Object=>Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None);
 static bool Editable(Component c)=>c.name.StartsWith("Paving_")||c.name.StartsWith("FM4_")||c.name.StartsWith("SF2_Post_FM4_")||c.name.StartsWith("Footprint_");
 static string Protected(){var rows=new List<string>();foreach(var c in All<Component>()){
  if(c==null||Editable(c))continue;
  rows.Add(c.name+(c is Transform?c.transform.localToWorldMatrix.ToString("F6"):EditorJsonUtility.ToJson(c)));
  if(c is Renderer r)foreach(var m in r.sharedMaterials)if(m!=null)rows.Add(EditorJsonUtility.ToJson(m));
 }rows.Sort(StringComparer.Ordinal);using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",rows))));}
 [Serializable] public class Fence {public int side,variant;public Vector3 p;public float station,offset;public Vector3[] vertices;public int[] triangles;}
 [Serializable] public class Wall {public string name;public Vector3[] vertices;public int[] triangles;}
 [Serializable] public class Report {
  public string protected_before,protected_after;public Vector2[] centers;public Vector3[] path_vertices;public int[] path_triangles;public Fence[] fences;public Wall[] walls;public Vector3[] footprints;
  public float width=2.1f,edge=.07f,scale_m,climb_before,climb_after,endpoint_before,endpoint_after,min_radius=99999,max_angle,spacing_min=999,spacing_max;
  public int samples,fence_batches,fence_crossings,wall_overlaps;public bool self_intersect;
 }
 static Mesh Save(string name,List<Vector3> v,List<int> t,List<Vector2> uv=null){string p="Assets/Settings/"+name+".asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(p);if(m==null){m=new Mesh();AssetDatabase.CreateAsset(m,p);}m.Clear();m.indexFormat=IndexFormat.UInt32;m.SetVertices(v);m.SetTriangles(t,0);if(uv!=null)m.SetUVs(0,uv);m.RecalculateNormals();m.RecalculateBounds();EditorUtility.SetDirty(m);return m;}
 public static void Run(){
  Directory.CreateDirectory(Out);EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");Physics.SyncTransforms();var report=new Report{protected_before=Protected()};
  var ground=Object.FindFirstObjectByType<PlayerController>().ground;
  Vector3 Snap(Vector2 p){if(!ground.Raycast(new Ray(new Vector3(p.x,400,p.y),Vector3.down),out var h,1000))throw new Exception("Path support missing");return h.point;}
  report.climb_before=Snap(ClimbRoute.Points.Last()).y-Snap(new Vector2(ClimbRoute.Spawn.x,ClimbRoute.Spawn.z)).y;report.endpoint_before=Snap(ClimbRoute.Points.Last()).y;
  var line=ClimbRoute.SmoothPoints;report.centers=line;report.samples=line.Length;
  Vector2 Normal(int i){var d=line[Math.Min(i+1,line.Length-1)]-line[Math.Max(0,i-1)];d.Normalize();return new Vector2(d.y,-d.x);}
  var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
  for(int i=0;i<line.Length;i++){
   if(i>0&&i<line.Length-1){var a=line[i]-line[i-1];var b=line[i+1]-line[i];float cross=Mathf.Abs(a.x*b.y-a.y*b.x);if(cross>1e-7f)report.min_radius=Mathf.Min(report.min_radius,a.magnitude*b.magnitude*(a+b).magnitude/(2*cross));report.max_angle=Mathf.Max(report.max_angle,Vector2.Angle(a,b));}
   foreach(float x in new[]{-1.05f,-1.015f,-.98f,.98f,1.015f,1.05f}){float edge=Mathf.Clamp01((Mathf.Abs(x)-.98f)/.07f);var p=Snap(line[i]+Normal(i)*x);p.y+=Mathf.Lerp(.035f,.002f,edge);v.Add(p);uv.Add(new Vector2(edge,i));}
   if(i<line.Length-1)for(int j=0;j<5;j++){int k=i*6+j;t.AddRange(new[]{k,k+6,k+1,k+1,k+6,k+7});}
  }
  var surface=GameObject.Find("Paving_Surface");var mesh=Save("PWB_Surface",v,t,uv);surface.GetComponent<MeshFilter>().sharedMesh=mesh;
  report.scale_m=surface.GetComponent<Renderer>().sharedMaterial.GetFloat("_ScaleM");
  foreach(var c in All<Collider>().Where(c=>c.name.StartsWith("Paving_")))Object.DestroyImmediate(c);
  surface.AddComponent<MeshCollider>().sharedMesh=mesh;report.path_vertices=v.ToArray();report.path_triangles=t.ToArray();
  // Recenter existing static footprints, including the old off-road final footprints.
  var footprints=new List<Vector3>();foreach(var f in All<MeshFilter>().Where(f=>f.name.StartsWith("Footprint_"))){var source=f.sharedMesh;var vv=source.vertices;var center=f.transform.TransformPoint(source.bounds.center);float z=center.z;int.TryParse(f.name.Substring(10),out int index);float x=ClimbRoute.CenterX(z)+(index%2==0?-.18f:.18f);
   for(int j=0;j<vv.Length;j++){var p=f.transform.TransformPoint(vv[j]);p.x+=x-center.x;var hit=Snap(new Vector2(p.x,p.z));p.y=hit.y+.047f;vv[j]=f.transform.InverseTransformPoint(p);}var saved=Save("PWB_"+f.name,vv.ToList(),source.triangles.ToList(),source.uv.ToList());f.sharedMesh=saved;footprints.Add(new Vector3(x,Snap(new Vector2(x,z)).y,z));}
  report.footprints=footprints.ToArray();
  // The recentered footprints enter the terrace view. Merge eight disjoint decals
  // per cell, retaining their original materials and source renderers as templates.
  foreach(var old in All<Transform>().Where(tr=>tr.name.StartsWith("Paving_FootprintsBatch")).ToArray())Object.DestroyImmediate(old.gameObject);
  var sources=All<MeshFilter>().Where(f=>f.name.StartsWith("Footprint_")).OrderBy(f=>f.transform.TransformPoint(f.sharedMesh.bounds.center).z).ToArray();
  for(int start=0;start<sources.Length;start+=8){var group=sources.Skip(start).Take(8).ToArray();var material0=group[0].GetComponent<MeshRenderer>().sharedMaterial;
   // These footprints share the same authored paint. Do not silently merge unlike materials.
   foreach(var f in group){var m=f.GetComponent<MeshRenderer>().sharedMaterial;if(m.shader!=material0.shader||m.GetColor("_BaseColor")!=material0.GetColor("_BaseColor")||m.GetFloat("_Footprint")!=material0.GetFloat("_Footprint"))throw new Exception("Unlike footprint materials");}
   var merged=new Mesh{indexFormat=IndexFormat.UInt32};merged.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray(),true,true);
   var saved=Save("PWB_FootprintsBatch"+start,merged.vertices.ToList(),merged.triangles.ToList(),merged.uv.ToList());saved.colors=merged.colors;Object.DestroyImmediate(merged);
   var go=new GameObject("Paving_FootprintsBatch"+start);go.transform.SetParent(group[0].transform.root,false);go.AddComponent<MeshFilter>().sharedMesh=saved;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material0;renderer.shadowCastingMode=group[0].GetComponent<MeshRenderer>().shadowCastingMode;
   foreach(var f in group)f.GetComponent<MeshRenderer>().enabled=false;
  }
  var root=GameObject.Find("FM4_Root").transform;var material=GameObject.Find("FM4_Batch").GetComponent<Renderer>().sharedMaterial;
  foreach(Transform child in root.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
  var models=Enumerable.Range(0,3).Select(i=>AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Settings/Fence_"+(char)('A'+i)+".asset")).ToArray();
  var parts=new List<CombineInstance>();var fences=new List<Fence>();var rng=new System.Random(6321);var bag=new List<int>();
  int Variant(){if(bag.Count==0){bag.AddRange(new[]{0,0,0,0,0,0,0,0,1,2});for(int i=9;i>0;i--){int j=rng.Next(i+1);int q=bag[i];bag[i]=bag[j];bag[j]=q;}}int r=bag.Last();bag.RemoveAt(bag.Count-1);return r;}
  for(int side=-1;side<=1;side+=2){
   var offset=Enumerable.Range(0,line.Length).Select(i=>line[i]+Normal(i)*(side*1.4f)).ToArray();float total=0,station=0;Vector2 previous=Vector2.zero;bool have=false;
   for(int i=0;i<offset.Length-1;i++){
    var d=(offset[i+1]-offset[i]).normalized;float length=Vector2.Distance(offset[i],offset[i+1]);
    while(station<=total+length){var p2=offset[i]+d*(station-total);if(p2.y>-19.25f){station+=2.2f;continue;}
     var p=Snap(p2);var direction=d;var q=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));var end=Snap(p2+d*2.1f);
     var matrix=Matrix4x4.TRS(p,q,Vector3.one);matrix.SetColumn(2,new Vector4(d.x,(end.y-p.y)/2.1f,d.y,0));
     int variant=Variant();var model=models[variant];if(variant==1&&side<0)matrix=matrix*Matrix4x4.TRS(new Vector3(0,0,2.1f),Quaternion.Euler(0,180,0),Vector3.one);
     var vertices=model.vertices.Select(matrix.MultiplyPoint3x4).ToArray();if(FenceModelBuild.Crossing(vertices,model.triangles,v.ToArray(),t.ToArray()))report.fence_crossings++;
     fences.Add(new Fence{side=side,variant=variant,p=p,station=station,offset=1.4f,vertices=vertices,triangles=model.triangles});parts.Add(new CombineInstance{mesh=model,transform=matrix});
     var go=new GameObject("FM4_Segment_"+fences.Count);go.transform.SetParent(root,false);go.transform.position=p;go.AddComponent<MeshFilter>().sharedMesh=model;
     foreach(float z in new[]{0f,2.1f}){var foot=matrix.MultiplyPoint3x4(new Vector3(0,0,z));var proxy=new GameObject("SF2_Post_FM4_"+fences.Count+"_"+z);proxy.transform.SetParent(go.transform,false);proxy.transform.position=foot+Vector3.up*.5f;proxy.AddComponent<BoxCollider>().size=new Vector3(.1f,1,.1f);}
     if(have){float distance=Vector2.Distance(previous,p2);report.spacing_min=Mathf.Min(report.spacing_min,distance);report.spacing_max=Mathf.Max(report.spacing_max,distance);}previous=p2;have=true;station+=2.2f;
    }total+=length;
   }
  }
  var combined=new Mesh{indexFormat=IndexFormat.UInt32};combined.CombineMeshes(parts.ToArray(),true,true);var batchMesh=Save("PWB_Fence",combined.vertices.ToList(),combined.triangles.ToList(),combined.uv.ToList());batchMesh.colors=combined.colors;batchMesh.normals=combined.normals;Object.DestroyImmediate(combined);
  var batch=new GameObject("FM4_Batch");batch.transform.SetParent(root,false);batch.AddComponent<MeshFilter>().sharedMesh=batchMesh;batch.AddComponent<MeshRenderer>().sharedMaterial=material;batch.AddComponent<MeshCollider>().sharedMesh=batchMesh;
  report.fence_batches=1;report.fences=fences.ToArray();
  report.walls=All<MeshFilter>().Where(f=>f.name.StartsWith("TerraceWall_")).Select(f=>new Wall{name=f.name,vertices=f.sharedMesh.vertices.Select(f.transform.TransformPoint).ToArray(),triangles=f.sharedMesh.triangles}).ToArray();
  // Nonadjacent ribbon cells must not overlap, including the snow shoulders.
  int[] cellTriangles={0,2,1,1,2,3};
  Vector3[] Cell(int i)=>new[]{v[i*6],v[i*6+5],v[(i+1)*6],v[(i+1)*6+5]};
  for(int i=0;i<line.Length-1;i++)for(int j=i+2;j<line.Length-1;j++)
   if(FenceModelBuild.Crossing(Cell(i),cellTriangles,Cell(j),cellTriangles))report.self_intersect=true;
  foreach(var wall in report.walls)for(int i=0;i<line.Length-1;i++)
   if(FenceModelBuild.Crossing(Cell(i),cellTriangles,wall.vertices,wall.triangles)){report.wall_overlaps++;break;}
  report.endpoint_after=Snap(ClimbRoute.Points.Last()).y;report.climb_after=Snap(ClimbRoute.Points.Last()).y-Snap(new Vector2(ClimbRoute.Spawn.x,ClimbRoute.Spawn.z)).y;
  Physics.SyncTransforms();report.protected_after=Protected();File.WriteAllText(Out+"assets.json",JsonUtility.ToJson(report,true));
  if(report.min_radius<6||report.max_angle>12||report.fence_crossings!=0||report.self_intersect||report.wall_overlaps!=0||report.protected_before!=report.protected_after||report.scale_m!=2.4f)throw new Exception("Path geometry/protection gate failed");
  Debug.Log(FormattableString.Invariant($"[PATH_W] width=2.100 min_turn_radius={report.min_radius:F6} samples={report.samples} self_intersect={report.self_intersect.ToString().ToLowerInvariant()}"));
  Debug.Log(FormattableString.Invariant($"[PATH_SMOOTH] max_angle_step={report.max_angle:F6} edge=.07 fence_spacing={report.spacing_min:F6}..{report.spacing_max:F6} fence_crossings={report.fence_crossings} protected_unchanged=true"));
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(surface.scene);EditorSceneManager.SaveScene(surface.scene);PerformanceBuild.Run();
 }
}
