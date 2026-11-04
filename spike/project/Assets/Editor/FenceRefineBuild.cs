using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Only the existing FM4 fence subtree and fence mesh assets are replaced.
public static class FenceRefineBuild {
 const string Out="../artifacts/fence_refine/";
 [Serializable] public class Piece { public string kind; public Color color; public Vector3[] vertices; public Vector2[] uv; public int[] triangles; public int post=-1; public Vector3 start,end; }
 [Serializable] public class Post { public int side,variant; public Vector3 foot,outward,forward; public float lean; }
 [Serializable] public class Span { public int side,variant,a,b,tris; public float station,length,uv_offset; public Piece[] pieces; }
 [Serializable] public class Report { public string protected_before,protected_after;public float embed;public float width=2.1f,offset=1.4f; public Post[] posts; public Span[] spans; public Piece[] terminals; public Vector3[] path_vertices; public int[] path_triangles;public int batches,tris; }
 static Mesh Save(string n,Mesh m){string p="Assets/Settings/"+n+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(p);if(old==null){AssetDatabase.CreateAsset(m,p);return m;}EditorUtility.CopySerialized(m,old);Object.DestroyImmediate(m);EditorUtility.SetDirty(old);return old;}
 static readonly int[][] Faces={new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,1,5,4},new[]{3,7,6,2},new[]{0,4,7,3},new[]{1,2,6,5}};
 static Piece Box(string kind,Vector3 center,Vector3 size,Func<Vector3,Vector3> transform,bool horizontal,float offset){
  var corners=new Vector3[8];for(int i=0;i<8;i++)corners[i]=center+Vector3.Scale(new Vector3((i==1||i==2||i==5||i==6)?.5f:-.5f,i>=4?.5f:-.5f,(i==2||i==3||i==6||i==7)?.5f:-.5f),size);
  var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
  for(int f=0;f<6;f++){int b=v.Count;foreach(int i in Faces[f]){var q=corners[i];v.Add(transform(q));Vector2 u;
    // All six faces have area in UV space. V always follows the member's long axis.
    if(horizontal)u=f==2||f==3?new Vector2(q.x/.06f,q.y/.08f):new Vector2((f<=1?q.x/.06f:q.y/.08f),q.z/size.z);
    else u=f<=1?new Vector2(q.x/.09f,q.z/.09f):new Vector2((f<=3?q.x/.09f:q.z/.09f),q.y);
    uv.Add(u+new Vector2(offset,offset));}t.AddRange(new[]{b,b+2,b+1,b,b+3,b+2});}
  return new Piece{kind=kind,vertices=v.ToArray(),uv=uv.ToArray(),triangles=t.ToArray()};
 }
 static Piece Cap(Post post,int id){
  // Octagonal footprint: 1 cm corner bevel, actual 10x10x3.5 cm snow volume.
  Vector2[] ring={new Vector2(-.04f,-.05f),new Vector2(.04f,-.05f),new Vector2(.05f,-.04f),new Vector2(.05f,.04f),new Vector2(.04f,.05f),new Vector2(-.04f,.05f),new Vector2(-.05f,.04f),new Vector2(-.05f,-.04f)};
  var v=new List<Vector3>();foreach(float y in new[]{1f,1.035f})foreach(var p in ring)v.Add(PostPoint(post,new Vector3(p.x,y,p.y)));
  var t=new List<int>();for(int i=1;i<7;i++){t.AddRange(new[]{0,i+1,i,8,8+i,8+i+1});}for(int i=0;i<8;i++){int j=(i+1)%8;t.AddRange(new[]{i,j,8+i,j,8+j,8+i});}
  // Ring is counterclockwise in XZ: reverse once for outward winding in XYZ.
  for(int i=0;i<t.Count;i+=3){int q=t[i+1];t[i+1]=t[i+2];t[i+2]=q;}
  return new Piece{kind="column_snow",post=id,vertices=v.ToArray(),uv=v.Select(p=>new Vector2(p.x,p.z)).ToArray(),triangles=t.ToArray()};
 }
 static Vector3 PostPoint(Post p,Vector3 q)=>p.foot+Vector3.Cross(Vector3.up,p.forward)*q.x+Vector3.up*q.y+p.forward*q.z+p.outward*(Mathf.Tan(p.lean*Mathf.Deg2Rad)*q.y);
 static Vector3 Center(Post p,float y)=>PostPoint(p,new Vector3(0,y,0));
 static float FaceDistance(Post p,Vector3 direction){var x=Vector3.Cross(Vector3.up,p.forward);return .045f/Mathf.Max(Mathf.Abs(Vector3.Dot(direction,x)),Mathf.Abs(Vector3.Dot(direction,p.forward)));}
 static Mesh MeshOf(IEnumerable<Piece> pieces,string name){var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();var c=new List<Color>();foreach(var p in pieces){int n=v.Count;v.AddRange(p.vertices);uv.AddRange(p.uv);t.AddRange(p.triangles.Select(i=>i+n));Color color=p.kind.EndsWith("snow")?new Color(237/255f,243/255f,248/255f).linear:Color.white;color.a=p.kind.EndsWith("snow")?0:1;p.color=color;c.AddRange(Enumerable.Repeat(color,p.vertices.Length));}var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(v);m.SetUVs(0,uv);m.SetColors(c);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;}
 public static void Run(){
  // Brief dimensions conflict: 2.2 - .09 + 2*.03 = 2.17 m, not 2.14 m.
  // Prioritize the explicit .03 m embed acceptance while preserving spacing and post size.
  const float embed=.03f;
  Directory.CreateDirectory(Out);EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");Physics.SyncTransforms();var r=new Report{embed=embed,protected_before=FenceModelBuild.Protected()};
  var ground=Object.FindFirstObjectByType<PlayerController>().ground;Vector3 Snap(Vector2 p){if(!ground.Raycast(new Ray(new Vector3(p.x,400,p.y),Vector3.down),out var h,1000))throw new Exception("Fence support missing");return h.point;}
  var surface=GameObject.Find("Paving_Surface").GetComponent<MeshFilter>();r.path_vertices=surface.sharedMesh.vertices.Select(surface.transform.TransformPoint).ToArray();r.path_triangles=surface.sharedMesh.triangles;
  var root=GameObject.Find("FM4_Root").transform;var material=GameObject.Find("FM4_Batch").GetComponent<Renderer>().sharedMaterial;foreach(Transform c in root.Cast<Transform>().ToArray())Object.DestroyImmediate(c.gameObject);
  var line=ClimbRoute.SmoothPoints;var posts=new List<Post>();var spans=new List<Span>();var terminals=new List<Piece>();var all=new List<Piece>();var rng=new System.Random(6321);
  Vector2 Normal(int i){var d=(line[Math.Min(i+1,line.Length-1)]-line[Math.Max(0,i-1)]).normalized;return new Vector2(d.y,-d.x);}
  List<Vector2> Samples(int side){
   var route=Enumerable.Range(0,line.Length).Select(i=>line[i]+Normal(i)*(side*1.4f)).ToArray();var samples=new List<Vector2>{route[0]};float total=0,next=2.2f;
   for(int i=0;i<route.Length-1;i++){float length=Vector2.Distance(route[i],route[i+1]);while(next<=total+length){var p=Vector2.Lerp(route[i],route[i+1],(next-total)/length);if(p.y<=-19.25f)samples.Add(p);next+=2.2f;}total+=length;}return samples;
  }
  var plans=new[]{Samples(-1),Samples(1)};int count=plans.Sum(p=>p.Count-1);
  if(count<148)throw new Exception("Route no longer supports the requested 146 intact spans");
  // Keep the specified 146 intact spans; divide the remainder between lean and missing-rail.
  var bag=Enumerable.Repeat(0,146).Concat(Enumerable.Repeat(1,(count-146+1)/2)).Concat(Enumerable.Repeat(2,(count-146)/2)).ToList();
  for(int i=bag.Count-1;i>0;i--){int j=rng.Next(i+1);int x=bag[i];bag[i]=bag[j];bag[j]=x;}
  int Variant(){int v=bag.Last();bag.RemoveAt(bag.Count-1);return v;}
  for(int side=-1;side<=1;side+=2){var samples=plans[(side+1)/2];
   // Final span terminates at a real post, rather than continuing into the plaza.
   int first=posts.Count;for(int i=0;i<samples.Count;i++){var d=(samples[Math.Min(i+1,samples.Count-1)]-samples[Math.Max(0,i-1)]).normalized;int variant=i==samples.Count-1?0:Variant();var forward=new Vector3(d.x,0,d.y);posts.Add(new Post{side=side,variant=variant,foot=Snap(samples[i]),forward=forward,outward=new Vector3(d.y,0,-d.x)*side,lean=variant==1?4+2*(float)rng.NextDouble():0});}
   for(int i=0;i<samples.Count-1;i++){
    int a=first+i,b=a+1;var post=posts[a];float offset=(float)rng.NextDouble();var pieces=new List<Piece>();
    var column=Box("column",new Vector3(0,.5f,0),new Vector3(.09f,1,.09f),q=>PostPoint(post,q),false,offset);column.post=a;pieces.Add(column);pieces.Add(Cap(post,a));
    foreach(float y in post.variant==2?new[]{.88f}:new[]{.38f,.88f}){
     var ca=Center(post,y);var cb=Center(posts[b],y);var dir=(cb-ca).normalized;var planar=new Vector3(dir.x,0,dir.z).normalized;
     // Intersect each actual shared post face; embed a short tenon, never traverse a post.
     float fa=FaceDistance(post,planar),fb=FaceDistance(posts[b],planar);var start=ca+dir*((fa-embed)/new Vector2(dir.x,dir.z).magnitude);var end=cb-dir*((fb-embed)/new Vector2(dir.x,dir.z).magnitude);var x=Vector3.Cross(Vector3.up,planar);float len=Vector3.Distance(start,end);
     Vector3 Transform(Vector3 q)=>start+x*q.x+Vector3.up*q.y+dir*q.z;
     var rail=Box("rail",new Vector3(0,0,len/2),new Vector3(.06f,.08f,len),Transform,true,offset);rail.start=start;rail.end=end;pieces.Add(rail);
     var snow=Box("rail_snow",new Vector3(0,.05f,len/2),new Vector3(.06f,.02f,len),Transform,true,offset);snow.start=start;snow.end=end;pieces.Add(snow);
    }
    var span=new Span{side=side,variant=post.variant,a=a,b=b,station=i*2.2f,length=Vector2.Distance(samples[i],samples[i+1]),uv_offset=offset,pieces=pieces.ToArray(),tris=pieces.Sum(p=>p.triangles.Length/3)};spans.Add(span);all.AddRange(pieces);
    // Inspection metadata only; actual render/collision geometry is the merged mesh.
    var go=new GameObject("FM4_Segment_"+spans.Count);go.transform.SetParent(root,false);go.transform.position=(post.foot+posts[b].foot)/2;go.transform.rotation=Quaternion.LookRotation(posts[b].foot-post.foot);go.AddComponent<MeshFilter>();
   }
   int last=posts.Count-1;var terminal=Box("column",new Vector3(0,.5f,0),new Vector3(.09f,1,.09f),q=>PostPoint(posts[last],q),false,0);terminal.post=last;terminals.Add(terminal);terminals.Add(Cap(posts[last],last));
  }
  // Preserve the existing reveal support audit and one collision proxy per shared column.
  // The merged MeshCollider supplies the exact leaned silhouette; these boxes retain
  // the authored post-support contract used by the unchanged reveal demo.
  for(int i=0;i<posts.Count;i++){var proxy=new GameObject("SF2_Post_FM4_V2_"+i);proxy.transform.SetParent(root,false);proxy.transform.position=posts[i].foot+Vector3.up*.5f;proxy.AddComponent<BoxCollider>().size=new Vector3(.09f,1,.09f);}
  all.AddRange(terminals);var models=new Mesh[3];for(int i=0;i<3;i++)models[i]=Save("Fence_V2_"+(char)('A'+i),MeshOf(spans.First(s=>s.variant==i).pieces,"Fence_V2_"+(char)('A'+i)));
  int index=0;foreach(Transform c in root){var mf=c.GetComponent<MeshFilter>();if(mf!=null)mf.sharedMesh=models[spans[index++].variant];}
  var combined=Save("FM4_Refined",MeshOf(all,"FM4_Refined"));var batch=new GameObject("FM4_Batch");batch.transform.SetParent(root,false);batch.AddComponent<MeshFilter>().sharedMesh=combined;batch.AddComponent<MeshRenderer>().sharedMaterial=material;batch.AddComponent<MeshCollider>().sharedMesh=combined;
  r.posts=posts.ToArray();r.spans=spans.ToArray();r.terminals=terminals.ToArray();r.batches=1;r.tris=combined.triangles.Length/3;Physics.SyncTransforms();r.protected_after=FenceModelBuild.Protected();File.WriteAllText(Out+"assets.json",JsonUtility.ToJson(r,true));
  if(r.protected_before!=r.protected_after||spans.Any(s=>s.tris>200)||all.Any(p=>FenceModelBuild.Crossing(p.vertices,p.triangles,r.path_vertices,r.path_triangles)))throw new Exception("Fence refine protection/geometry gate failed");
  Debug.Log(FormattableString.Invariant($"[FENCE_V2] segments={spans.Count} share_post=true rail_embed={embed:F3} snow_caps=columns+rails uv_all_faces=true"));Debug.Log("[FENCE_V2] variants=3 lean_deg=4–6 broken=rail_missing");
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);EditorSceneManager.SaveScene(surface.gameObject.scene);PerformanceBuild.Run();
 }
}
