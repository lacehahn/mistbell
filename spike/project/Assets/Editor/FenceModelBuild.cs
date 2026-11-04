using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Local, idempotent fence replacement. No scene regeneration or unrelated material edits.
public static class FenceModelBuild {
 const string Out="../artifacts/fence_model/", Dir="Assets/Settings/";
 static T[] All<T>() where T:Object=>Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None);
 static bool Fence(Component c)=>c.name.StartsWith("SF2_")||c.name.StartsWith("FT3_Cap")||c.name.StartsWith("FT3_Rail")||c.name.StartsWith("FT3_Batch")||c.name.StartsWith("FencePost_")||c.name.StartsWith("FenceRail_")||c.name.StartsWith("FenceSnow_")||c.name.StartsWith("PostDrift_")||c.name.StartsWith("FM4_");
 static string Protected(){
  var rows=new List<string>();foreach(var c in All<Component>()){
   if(c==null||Fence(c))continue;
   rows.Add(c.name+(c is Transform?c.transform.localToWorldMatrix.ToString("F6"):EditorJsonUtility.ToJson(c)));
   if(c is Renderer r)foreach(var m in r.sharedMaterials)if(m!=null)rows.Add(EditorJsonUtility.ToJson(m));
   if(c is MeshFilter f&&f.sharedMesh!=null)rows.Add(EditorJsonUtility.ToJson(f.sharedMesh));
  }
  rows.Add(FormattableString.Invariant($"fog={RenderSettings.fog}/{RenderSettings.fogMode}/{RenderSettings.fogDensity}/{RenderSettings.fogStartDistance}/{RenderSettings.fogEndDistance}/{RenderSettings.fogColor} ambient={RenderSettings.ambientMode}/{RenderSettings.ambientSkyColor}/{RenderSettings.ambientEquatorColor}/{RenderSettings.ambientGroundColor}/{RenderSettings.ambientIntensity}"));rows.Sort(StringComparer.Ordinal);
  using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",rows)))).Replace("-","");
 }
 [Serializable] public class Segment {public int side,leg,variant;public float station,spacing,yaw,jitter;public Vector3 p; public Vector3[] vertices;public int[] triangles;}
 [Serializable] public class Variant {public string name;public int tris;public Vector3 size;}
 [Serializable] public class Report {public string protected_before,protected_after,texture_source="../art-prompts/staged/fence_wood.jpg";public int segments,tris,batches,variants,crossing,old_renderers,rows_per_side=1;public float spacing_min=999,spacing_max;public Variant[] meshes;public Segment[] instances;public Vector3[] path_vertices;public int[] path_triangles;public int[] counts;public int texture_width,texture_height,seam_mismatches;}
 static Mesh Save(string name,Mesh mesh){string path=Dir+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
 static Mesh Model(int variant){
  var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();var colors=new List<Color>();
  // Six irregular-prism faces; snow is selected by vertex alpha, so there is one material/texture.
  void Prism(Vector3[] p,bool snow,bool horizontal){int[][] faces={new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,1,5,4},new[]{3,7,6,2},new[]{0,4,7,3},new[]{1,2,6,5}};
   foreach(var face in faces){int b=v.Count;foreach(int k in face){var q=p[k];v.Add(q);uv.Add(horizontal?new Vector2((q.y-.2f)/.8f,q.z/2.1f):new Vector2((q.x+.05f)*7,q.y));colors.Add(snow?new Color(.93f,.96f,.98f,0):Color.white);}t.AddRange(new[]{b,b+2,b+1,b,b+3,b+2});}}
  void Box(Vector3 center,Vector3 size,bool snow=false,bool horizontal=false,float lean=0,bool broken=false){var p=new Vector3[8];for(int i=0;i<8;i++){float x=(i==1||i==2||i==5||i==6)?.5f:-.5f,y=i>=4?.5f:-.5f,z=(i==2||i==3||i==6||i==7)?.5f:-.5f;var q=center+Vector3.Scale(new Vector3(x,y,z),size);if(broken&&(center.z<1?z>0:z<0))q.z+=(center.z<1?1:-1)*new[]{-.05f,.035f,-.025f,.055f}[i%4];q.x+=Mathf.Tan(lean*Mathf.Deg2Rad)*q.y;p[i]=q;}Prism(p,snow,horizontal);}
  foreach(float z in new[]{0f,2.1f}){float tilt=variant==1&&z==0?7:0;Box(new Vector3(0,.5f,z),new Vector3(.08f,1,.08f),lean:tilt);Box(new Vector3(0,.05f,z),new Vector3(.10f,.10f,.10f),lean:tilt);Box(new Vector3(0,1.0175f,z),new Vector3(.10f,.035f,.10f),true,lean:tilt);}
  foreach(float y in variant==2?new[]{.75f}:new[]{.30f,.75f}){
   if(variant==2){Box(new Vector3(0,y,.40f),new Vector3(.06f,.08f,.8f),horizontal:true,broken:true);Box(new Vector3(0,y+.0575f,.37f),new Vector3(.075f,.035f,.74f),true,true,broken:true);Box(new Vector3(0,y,1.88f),new Vector3(.06f,.08f,.44f),horizontal:true,broken:true);Box(new Vector3(0,y+.0575f,1.88f),new Vector3(.075f,.035f,.44f),true,true,broken:true);}
   else{Box(new Vector3(0,y,1.05f),new Vector3(.06f,.08f,2.1f),horizontal:true);Box(new Vector3(0,y+.0575f,1.05f),new Vector3(.075f,.035f,2.1f),true,true);}}
  var mesh=new Mesh{name="Fence_"+(char)('A'+variant)};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.RecalculateNormals();mesh.RecalculateBounds();return Save(mesh.name,mesh);
 }
 static Texture2D Texture(){
  // Import the existing artist-painted source; point resample, three walnut levels + sparse lichen.
  var src=new Texture2D(2,2);src.LoadImage(File.ReadAllBytes("../art-prompts/staged/fence_wood.jpg"));var dst=new Texture2D(1024,1024,TextureFormat.RGB24,false);
  Color32[] palette={new Color32(56,44,34,255),new Color32(74,59,46,255),new Color32(92,74,56,255)};var pixels=new Color32[1024*1024];
  for(int y=0;y<1024;y++)for(int x=0;x<1024;x++){var c=src.GetPixel(x*src.width/1024,y*src.height/1024);int r=Mathf.RoundToInt(c.r*255),g=Mathf.RoundToInt(c.g*255);pixels[y*1024+x]=g>90&&Mathf.Abs(r-g)<25?new Color32(126,121,106,255):palette[r<62?0:r<91?1:2];}
  // Make both opposing edges exactly equal by mirrored overlap from the source interior.
  for(int y=0;y<1024;y++)for(int x=0;x<16;x++)pixels[y*1024+1023-x]=pixels[y*1024+x];
  dst.SetPixels32(pixels);dst.Apply();Directory.CreateDirectory("Assets/Art/Textures");const string p="Assets/Art/Textures/Fence_Wood_1024.png";File.WriteAllBytes(p,dst.EncodeToPNG());Object.DestroyImmediate(src);Object.DestroyImmediate(dst);AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);
  var importer=(TextureImporter)AssetImporter.GetAtPath(p);importer.isReadable=true;importer.sRGBTexture=true;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=true;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
 }
 static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
 static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
 static bool Inside(Vector2 p,Vector2 a,Vector2 b,Vector2 c){float x=Cross(b-a,p-a),y=Cross(c-b,p-b),z=Cross(a-c,p-c);return (x>=0&&y>=0&&z>=0)||(x<=0&&y<=0&&z<=0);}
 static bool Cuts(Vector2 a,Vector2 b,Vector2 c,Vector2 d)=>Cross(b-a,c-a)*Cross(b-a,d-a)<0&&Cross(d-c,a-c)*Cross(d-c,b-c)<0;
 public static bool Crossing(Vector3[] v,int[] t,Vector3[] pv,int[] pt){
  for(int j=0;j<t.Length;j+=3){Vector2 a=XZ(v[t[j]]),b=XZ(v[t[j+1]]),c=XZ(v[t[j+2]]);if(Mathf.Abs(Cross(b-a,c-a))<1e-9f)continue;
   float x0=Mathf.Min(a.x,Mathf.Min(b.x,c.x)),x1=Mathf.Max(a.x,Mathf.Max(b.x,c.x)),z0=Mathf.Min(a.y,Mathf.Min(b.y,c.y)),z1=Mathf.Max(a.y,Mathf.Max(b.y,c.y));
   for(int k=0;k<pt.Length;k+=3){var d=XZ(pv[pt[k]]);var e=XZ(pv[pt[k+1]]);var f=XZ(pv[pt[k+2]]);if(Mathf.Abs(Cross(e-d,f-d))<1e-9f||Mathf.Min(d.x,Mathf.Min(e.x,f.x))>x1||Mathf.Max(d.x,Mathf.Max(e.x,f.x))<x0||Mathf.Min(d.y,Mathf.Min(e.y,f.y))>z1||Mathf.Max(d.y,Mathf.Max(e.y,f.y))<z0)continue;
    if(Inside(a,d,e,f)||Inside(b,d,e,f)||Inside(c,d,e,f)||Inside(d,a,b,c)||Cuts(a,b,d,e)||Cuts(a,b,e,f)||Cuts(a,b,f,d)||Cuts(b,c,d,e)||Cuts(b,c,e,f)||Cuts(b,c,f,d)||Cuts(c,a,d,e)||Cuts(c,a,e,f)||Cuts(c,a,f,d))return true;}}
  return false;
 }
 public static void Run(){
  Directory.CreateDirectory(Out);EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");Physics.SyncTransforms();var report=new Report{protected_before=Protected()};
  var ground=Object.FindFirstObjectByType<PlayerController>().ground;
  Vector3 Snap(Vector2 p){if(!ground.Raycast(new Ray(new Vector3(p.x,400,p.y),Vector3.down),out var hit,1000))throw new Exception("Missing support");return hit.point;}
  var surface=GameObject.Find("Paving_Surface").GetComponent<MeshFilter>();report.path_vertices=surface.sharedMesh.vertices.Select(surface.transform.TransformPoint).ToArray();report.path_triangles=surface.sharedMesh.triangles;
  report.old_renderers=All<Renderer>().Count(Fence);
  foreach(var tr in All<Transform>().Where(Fence).OrderByDescending(t=>t.name=="FM4_Root").ToArray())if(tr!=null)Object.DestroyImmediate(tr.gameObject);
  var meshes=Enumerable.Range(0,3).Select(Model).ToArray();var tex=Texture();string mp=Dir+"M_Fence_Model.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(mp);if(mat==null){mat=new Material(Shader.Find("Mistbell/FenceModel"));AssetDatabase.CreateAsset(mat,mp);}mat.shader=Shader.Find("Mistbell/FenceModel");mat.SetColor("_BaseColor",Color.white);mat.SetTexture("_BaseMap",tex);mat.SetFloat("_ShadowCoverage",.625f);EditorUtility.SetDirty(mat);
  var root=new GameObject("FM4_Root").transform;var instances=new List<Segment>();var parts=new List<CombineInstance>();var rng=new System.Random(6321);float Rand(float lo,float hi)=>lo+(hi-lo)*(float)rng.NextDouble();
  report.counts=new int[3];var bag=new List<int>();
  int NextVariant(){if(bag.Count==0){bag.AddRange(new[]{0,0,0,0,0,0,0,0,1,2});for(int i=9;i>0;i--){int j=rng.Next(i+1);int temp=bag[i];bag[i]=bag[j];bag[j]=temp;}}int result=bag[bag.Count-1];bag.RemoveAt(bag.Count-1);return result;}
  Vector2 Offset(int index,int side){var route=ClimbRoute.Points;Vector2 Normal(int k){var q=(route[k+1]-route[k]).normalized;return new Vector2(q.y,-q.x);}var normal=index==0?Normal(0):index==route.Length-1?Normal(index-1):(Normal(index-1)+Normal(index)).normalized;float dot=index==0||index==route.Length-1?1:Vector2.Dot(normal,Normal(index));return route[index]+normal*(side*2.65f/dot);}
  for(int side=-1;side<=1;side+=2)for(int leg=0;leg<ClimbRoute.Points.Length-1;leg++){
   var a=Offset(leg,side);var b=Offset(leg+1,side);var d=(b-a).normalized;var n=new Vector2(d.y,-d.x);float length=(b-a).magnitude;
   // Separate straight runs at bends: never bridge the road with a corner chord.
   float first=leg==0?0:.25f,last=length-(leg==3?5.9f:.25f)-2.2f,previous=-1;
   int intervals=Mathf.RoundToInt((last-first)/2.2f);float stride=(last-first)/intervals;float variation=Mathf.Min(.025f,Mathf.Min(2.35f-stride,stride-2.05f)*.45f);
   for(int slot=0;slot<=intervals;slot++){
    float station=Mathf.Lerp(first,last,slot/(float)intervals)+(slot==0||slot==intervals?0:Rand(-variation,variation));
    float yaw=Rand(-3,3),jitter=Rand(-.02f,.02f);var forward=Quaternion.Euler(0,yaw,0)*new Vector3(d.x,0,d.y);var lateral=Vector3.Cross(Vector3.up,forward);
    var p=Snap(a+d*station);p.y+=jitter;
    var end=Snap(XZ(p+forward*2.1f));var matrix=Matrix4x4.identity;matrix.SetColumn(0,new Vector4(lateral.x,0,lateral.z,0));matrix.SetColumn(1,new Vector4(0,1,0,0));matrix.SetColumn(2,new Vector4(forward.x,(end.y-(p.y-jitter))/2.1f,forward.z,0));matrix.SetColumn(3,new Vector4(p.x,p.y,p.z,1));
    int variant=NextVariant();var mesh=meshes[variant];if(variant==1&&side<0)matrix=matrix*Matrix4x4.TRS(new Vector3(0,0,2.1f),Quaternion.Euler(0,180,0),Vector3.one);var vertices=mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
    if(Crossing(vertices,mesh.triangles,report.path_vertices,report.path_triangles))throw new Exception("Fence intersects paving");
    var segment=new Segment{side=side,leg=leg,variant=variant,station=station,spacing=previous<0?0:station-previous,yaw=yaw,jitter=jitter,p=p,vertices=vertices,triangles=mesh.triangles};instances.Add(segment);report.counts[variant]++;
    parts.Add(new CombineInstance{mesh=mesh,transform=matrix});report.tris+=mesh.triangles.Length/3;
    var go=new GameObject("FM4_Segment_"+instances.Count);go.transform.SetParent(root,false);go.transform.position=p;go.AddComponent<MeshFilter>().sharedMesh=mesh;
    // Source models remain references, no per-plank renderers or primitive source objects.
    foreach(float z in new[]{0f,2.1f}){var foot=matrix.MultiplyPoint3x4(new Vector3(0,0,z));var proxy=new GameObject("SF2_Post_FM4_"+instances.Count+"_"+z);proxy.transform.SetParent(go.transform,false);proxy.transform.position=foot+Vector3.up*.5f;var collider=proxy.AddComponent<BoxCollider>();collider.size=new Vector3(.1f,1,.1f);}
    if(previous>=0){report.spacing_min=Mathf.Min(report.spacing_min,segment.spacing);report.spacing_max=Mathf.Max(report.spacing_max,segment.spacing);}previous=station;
   }
  }
  var combined=new Mesh{name="FM4_Combined",indexFormat=IndexFormat.UInt32};combined.CombineMeshes(parts.ToArray(),true,true);combined.RecalculateBounds();combined=Save("FM4_Combined",combined);
  var batch=new GameObject("FM4_Batch");batch.transform.SetParent(root,false);batch.AddComponent<MeshFilter>().sharedMesh=combined;batch.AddComponent<MeshRenderer>().sharedMaterial=mat;batch.AddComponent<MeshCollider>().sharedMesh=combined;
  report.segments=instances.Count;report.instances=instances.ToArray();report.batches=root.GetComponentsInChildren<Renderer>().Length;report.variants=meshes.Length;report.meshes=meshes.Select(m=>new Variant{name=m.name,tris=m.triangles.Length/3,size=m.bounds.size}).ToArray();report.texture_width=tex.width;report.texture_height=tex.height;
  for(int y=0;y<tex.height;y++)if(tex.GetPixel(0,y)!=tex.GetPixel(tex.width-1,y))report.seam_mismatches++;
  Physics.SyncTransforms();report.protected_after=Protected();File.WriteAllText(Out+"assets.json",JsonUtility.ToJson(report,true));
  Debug.Log(FormattableString.Invariant($"[FENCE_MESH] segments={report.segments} tris={report.tris} batches={report.batches} spacing={report.spacing_min:F6}..{report.spacing_max:F6} crossing={report.crossing} variants={report.variants}"));
  Debug.Log("[FM4_PROTECTED] unchanged="+(report.protected_before==report.protected_after));
  if(report.protected_before!=report.protected_after||report.crossing!=0||report.spacing_min<2.05f||report.spacing_max>2.35f||report.batches>6||report.meshes.Any(m=>m.tris>600)||report.seam_mismatches!=0)throw new Exception("Fence model gate failed");
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);EditorSceneManager.SaveScene(surface.gameObject.scene);PerformanceBuild.Run();
 }
}
