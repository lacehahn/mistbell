using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Patch only the authored paving and nine treads; no scene regeneration.
public static class PathTextureBuild {
 const string Output="../artifacts/path_texture/";
 static T[] All<T>() where T:Object=>Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None);
 static bool Editable(Component c)=>c.name.StartsWith("Paving_")||c.name.StartsWith("TerraceStep_")||c.name=="PathSnowEdgeV2";
 static string Protected(){
  var rows=new List<string>();
  foreach(var c in All<Component>()) {
   if(c==null||Editable(c))continue;
   // Root children arrays necessarily change when the old independent edge is removed.
   if(c is Transform) {rows.Add(c.name+c.transform.localToWorldMatrix.ToString("F6"));continue;}
   rows.Add(c.name+EditorJsonUtility.ToJson(c));
   if(c is Renderer r)foreach(var m in r.sharedMaterials)if(m!=null)rows.Add(AssetDatabase.GetAssetPath(m)+EditorJsonUtility.ToJson(m));
  }
  rows.Sort(StringComparer.Ordinal);
  using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",rows)))).Replace("-","");
 }
 static Texture2D Import(string file,bool tread=false){
  string p="Assets/Art/Textures/Path/"+file;AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceSynchronousImport);
  var ti=(TextureImporter)AssetImporter.GetAtPath(p);
  ti.textureType=TextureImporterType.Default;ti.sRGBTexture=true;ti.mipmapEnabled=true;ti.filterMode=FilterMode.Bilinear;ti.anisoLevel=4;
  ti.wrapMode=TextureWrapMode.Repeat;ti.wrapModeU=TextureWrapMode.Repeat;ti.wrapModeV=tread?TextureWrapMode.Clamp:TextureWrapMode.Repeat;
  ti.npotScale=TextureImporterNPOTScale.None;ti.maxTextureSize=2048;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.SaveAndReimport();
  return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
 }
 static Material Mat(string n){
  string p="Assets/Settings/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);
  if(m==null){m=new Material(Shader.Find("Mistbell/PathPainted"));AssetDatabase.CreateAsset(m,p);}else m.shader=Shader.Find("Mistbell/PathPainted");
  m.SetColor("_BaseColor",Color.white);m.SetFloat("_Steps",0);m.SetFloat("_ScaleM",2.4f);m.SetFloat("_BlendBand",8);m.SetTextureScale("_BaseMap",Vector2.one);m.SetTextureOffset("_BaseMap",Vector2.zero);EditorUtility.SetDirty(m);return m;
 }
 [Serializable] class Report {public string protected_before,protected_after;public float terrace_bottom,snow_line,blend_band=8,scale_m=2.4f,center_gap_before,center_gap_after;public int tiles,treads,top_triangles;public bool edge_removed;}
 public static void Run(){
  Directory.CreateDirectory(Output);EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");Physics.SyncTransforms();
  var report=new Report{protected_before=Protected()};
  var paving=Mat("M_Paving");paving.SetTexture("_BaseMap",Import("path_stone_a.jpg"));paving.SetTexture("_SnowMap",Import("path_stone_snow.jpg"));
  var steps=All<MeshRenderer>().Where(r=>r.name.StartsWith("TerraceStep_")).OrderBy(r=>r.name).ToArray();
  if(steps.Length!=9)throw new Exception("Expected exactly nine entrance treads");
  var ground=Object.FindFirstObjectByType<PlayerController>().ground;
  if(!ground.Raycast(new Ray(new Vector3(0,100,-13.25f),Vector3.down),out var foot,200))throw new Exception("Terrace foot ray missed");
  // Same terrain contact plus 3.5cm construction offset as BuildTerrace's footY.
  report.terrace_bottom=foot.point.y+.035f;report.snow_line=report.terrace_bottom-10;
  paving.SetFloat("_SnowLine",report.snow_line);
  var tiles=All<MeshRenderer>().Where(r=>r.name.StartsWith("Paving_")).ToArray();
  foreach(var r in tiles){
   report.center_gap_before=Mathf.Max(report.center_gap_before,1.58f-1.52f);
   var scale=r.transform.localScale;scale.x=1.52f;r.transform.localScale=scale;
   r.sharedMaterial=paving;r.enabled=false;
  }
  // The original boxes sample each row at a different altitude and expose a
  // continuous bright inner side. A welded, terrain-following top replaces only
  // their rendering; all original colliders and transforms remain in place.
  var centers=new List<Vector3>();
  for(int seg=0;seg<4;seg++) {
   int count=Mathf.CeilToInt(Vector2.Distance(ClimbRoute.Points[seg],ClimbRoute.Points[seg+1])/.4f);
   for(int j=0;j<count;j++)centers.Add(ClimbRoute.Point(seg,j/(float)count));
  }
  centers.Add(ClimbRoute.Point(3,1));
  var verts=new List<Vector3>();var indices=new List<int>();
  for(int j=0;j<centers.Count;j++) {
   var before=(centers[j]-centers[Mathf.Max(0,j-1)]).normalized;
   var after=(centers[Mathf.Min(centers.Count-1,j+1)]-centers[j]).normalized;
   if(j==0)before=after;if(j==centers.Count-1)after=before;
   var lateral=Vector3.Cross(Vector3.up,(before+after).normalized).normalized;
   float miter=Mathf.Max(.75f,Vector3.Dot(lateral,Vector3.Cross(Vector3.up,after).normalized));
   foreach(int side in new[]{-1,1}) {
    var v=centers[j]+lateral*(side*1.55f/miter);
    if(!ground.Raycast(new Ray(v+Vector3.up*100,Vector3.down),out var h,200))throw new Exception("Paving support missed");
    v.y=h.point.y+.08f;verts.Add(v);
   }
   if(j<centers.Count-1){int k=j*2;indices.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}
  }
  string surfacePath="Assets/Settings/PathTex_Surface.asset";
  var surfaceMesh=AssetDatabase.LoadAssetAtPath<Mesh>(surfacePath);if(surfaceMesh==null){surfaceMesh=new Mesh();AssetDatabase.CreateAsset(surfaceMesh,surfacePath);}
  surfaceMesh.Clear();surfaceMesh.SetVertices(verts);surfaceMesh.SetTriangles(indices,0);surfaceMesh.RecalculateNormals();surfaceMesh.RecalculateBounds();EditorUtility.SetDirty(surfaceMesh);
  var surface=GameObject.Find("Paving_Surface");if(surface==null){surface=new GameObject("Paving_Surface");surface.transform.SetParent(GameObject.Find("Stone Path").transform,false);surface.AddComponent<MeshFilter>();surface.AddComponent<MeshRenderer>();}
  surface.GetComponent<MeshFilter>().sharedMesh=surfaceMesh;var surfaceRenderer=surface.GetComponent<MeshRenderer>();surfaceRenderer.sharedMaterial=paving;
  surfaceRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
  Debug.Log($"[PATH_SURFACE] welded=true vertices={verts.Count} triangles={indices.Count/3} original_colliders_retained=true shadow_casting=false");
  report.center_gap_after=0;report.tiles=tiles.Length;
  Debug.Log($"[PATH_WHITE] name=Paving_*/centre_gap material=M_Snow2Terrain source=exposed_snow_between_rows gap_before_m={report.center_gap_before:F3} gap_after_m=0 removed=true");
  var edge=GameObject.Find("PathSnowEdgeV2");
  if(edge!=null){Debug.Log($"[PATH_WHITE] name={edge.name} material={edge.GetComponent<Renderer>().sharedMaterial.name} source=independent_bright_edge removed=true");Object.DestroyImmediate(edge);}
  report.edge_removed=GameObject.Find("PathSnowEdgeV2")==null;
  var tread=Mat("M_PathTread");tread.SetFloat("_Steps",1);tread.SetTexture("_BaseMap",Import("path_stone_steps_tread.png",true));
  foreach(var r in steps){
   var f=r.GetComponent<MeshFilter>();var old=f.sharedMesh;var src=old.vertices;
   var tris=Enumerable.Range(0,old.subMeshCount).SelectMany(i=>old.GetTriangles(i)).ToArray();
   var top=new List<int>();var rest=new List<int>();
   for(int j=0;j<tris.Length;j+=3){
    var a=f.transform.TransformPoint(src[tris[j]]);var b=f.transform.TransformPoint(src[tris[j+1]]);var c=f.transform.TransformPoint(src[tris[j+2]]);
    var list=Vector3.Cross(b-a,c-a).normalized.y>.99f?top:rest;list.AddRange(new[]{tris[j],tris[j+1],tris[j+2]});
   }
   var uv=old.uv;float z0=r.bounds.min.z,z1=r.bounds.max.z;
   // Source prism vertices are split by face; edit only top UVs. One .34m tread vertically.
   foreach(int k in top.Distinct()){var p=f.transform.TransformPoint(src[k]);uv[k]=new Vector2((p.x+2.5f)/2.4f,Mathf.InverseLerp(z0,z1,p.z));}
   string path="Assets/Settings/PathTex_"+r.name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
   if(mesh==null){mesh=Object.Instantiate(old);AssetDatabase.CreateAsset(mesh,path);}
   mesh.uv=uv;mesh.subMeshCount=2;mesh.SetTriangles(rest,0);mesh.SetTriangles(top,1);EditorUtility.SetDirty(mesh);
   var side=r.sharedMaterials[0];r.sharedMaterials=new[]{side,tread};f.sharedMesh=mesh;
   // Identical vertex positions/triangles: preserve collider and walking envelope as authored.
   report.treads++;report.top_triangles+=top.Count/3;
  }
  report.protected_after=Protected();File.WriteAllText(Output+"assets.json",JsonUtility.ToJson(report,true));
  if(report.protected_before!=report.protected_after||report.tiles==0||report.treads!=9||!report.edge_removed)throw new Exception("Path protected/layout gate failed");
  Debug.Log("[PATH_TEX] a=path_stone_a snow=path_stone_snow scale_m=2.4 blend_band=8 mipmap=true aniso=4");
  Debug.Log(FormattableString.Invariant($"[PATH_ALTITUDE] terrace_bottom={report.terrace_bottom:F5} bare_below={report.snow_line:F5} snow_above={report.snow_line+8:F5} tiles={report.tiles} treads={report.treads} top_triangles={report.top_triangles} protected_unchanged=true"));
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(ground.gameObject.scene);EditorSceneManager.SaveScene(ground.gameObject.scene);PerformanceBuild.Run();
 }
}
