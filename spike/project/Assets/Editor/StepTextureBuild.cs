using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object=UnityEngine.Object;

// Incremental material/UV patch. No geometry, collider, gameplay or camera edits.
public static class StepTextureBuild {
 const string Out="../artifacts/step_textures/";
 static string Hash(string value){using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","");}
 static string Geometry(Mesh m)=>Hash(string.Join(";",m.vertices.Select(v=>v.ToString("R")))+"|"+string.Join(",",m.triangles.OrderBy(x=>x)));
 static string Protected(GameObject surface){
  var rows=new List<string>();
  foreach(var c in Object.FindObjectsByType<Component>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
   if(c==null||c.gameObject==surface)continue;
   rows.Add(c.GetInstanceID()+EditorJsonUtility.ToJson(c));
   if(c is Renderer r)foreach(var m in r.sharedMaterials)if(m!=null)rows.Add(AssetDatabase.GetAssetPath(m)+EditorJsonUtility.ToJson(m));
  }
  rows.Sort(StringComparer.Ordinal);return Hash(string.Join("\n",rows));
 }
 static Texture2D Import(string name){
  string p="Assets/Art/Textures/Path/"+name+".jpg";
  File.Copy("../art-prompts/staged/"+name+".jpg",p,true);AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceSynchronousImport);
  var t=(TextureImporter)AssetImporter.GetAtPath(p);t.textureType=TextureImporterType.Default;t.sRGBTexture=true;t.mipmapEnabled=true;t.wrapMode=TextureWrapMode.Repeat;t.wrapModeU=t.wrapModeV=TextureWrapMode.Repeat;t.filterMode=FilterMode.Bilinear;t.anisoLevel=4;t.npotScale=TextureImporterNPOTScale.None;t.maxTextureSize=2048;t.textureCompression=TextureImporterCompression.Uncompressed;t.SaveAndReimport();
  if(!t.mipmapEnabled||t.wrapModeU!=TextureWrapMode.Repeat||t.wrapModeV!=TextureWrapMode.Repeat||t.anisoLevel!=4||t.filterMode!=FilterMode.Bilinear)throw new Exception("Texture import gate");
  return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
 }
 public static void Run(){
  Directory.CreateDirectory(Out);EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
  var surface=GameObject.Find("Paving_Surface");var mf=surface.GetComponent<MeshFilter>();var mesh=mf.sharedMesh;var collider=surface.GetComponent<MeshCollider>();
  string protectedBefore=Protected(surface),geometryBefore=Geometry(mesh),colliderBefore=Geometry(collider.sharedMesh);
  var positions=mesh.vertices;var uv=mesh.uv;var edge=Enumerable.Repeat(Vector4.zero,mesh.vertexCount).ToArray();
  var lists=Enumerable.Range(0,3).Select(i=>new List<int>(mesh.GetTriangles(i))).ToArray();
  int treads=0,risers=0;var distinctSteps=new HashSet<string>();
  // ClimbFeelBuild creates split four-vertex quads. Preserve all positions/normals and triangle winding.
  for(int q=0;q<positions.Length;q+=4){
   var center=(positions[q]+positions[q+1]+positions[q+2]+positions[q+3])*.25f;
   int flight=-1,step=-1;float best=float.PositiveInfinity;
   for(int f=0;f<3;f++)for(int k=0;k<10;k++){
    bool vertical=Mathf.Abs(positions[q].x-positions[q+1].x)+Mathf.Abs(positions[q].z-positions[q+1].z)<.00001f;
    var p=ClimbFeel.Point(ClimbFeel.FlightStart(f)+(k+(vertical?0:.5f))*.32f);float d=Vector2.Distance(new Vector2(center.x,center.z),p);
    if(d<best){best=d;flight=f;step=k;}
   }
   float station=ClimbFeel.FlightStart(flight)+step*.32f;var start=ClimbFeel.Point(station);var forward=(ClimbFeel.Point(station+.32f)-start).normalized;var right=new Vector2(forward.y,-forward.x);
   float depth=Vector2.Dot(new Vector2(center.x,center.z)-start,forward);
   float minY=Enumerable.Range(q,4).Min(i=>positions[i].y),maxY=Enumerable.Range(q,4).Max(i=>positions[i].y);
   bool top=maxY-minY<.0001f&&depth>.001f&&depth<.319f&&best<1.2f;
   bool riser=Mathf.Abs(positions[q].x-positions[q+1].x)+Mathf.Abs(positions[q].z-positions[q+1].z)<.00001f&&maxY-minY>.01f&&Mathf.Abs(depth)<.003f&&best<1.2f;
   if(!top&&!riser)continue;
   int[] indices={q,q+1,q+2,q,q+2,q+3};
   // Reassign only shoulder tops to tread; geometric triangle multiset stays identical.
   if(top){foreach(var list in lists)list.RemoveAll(i=>i>=q&&i<q+4);lists[1].AddRange(indices);treads++;distinctSteps.Add(flight+":"+step);}else risers++;
   for(int j=q;j<q+4;j++){
    var p=new Vector2(positions[j].x,positions[j].z);float across=Vector2.Dot(p-start,right),d=Vector2.Dot(p-start,forward);
    uv[j]=top?new Vector2(across/1.35f,.11f+d/1.35f):new Vector2(across/1.2f,(j==q+1||j==q+2?1f:0f));
    if(top)edge[j]=new Vector4(across/.7f,.8f+d/.7f,Mathf.Max(0,d),1);
   }
  }
  mesh.uv=uv;mesh.SetUVs(1,edge.ToList());for(int i=0;i<3;i++)mesh.SetTriangles(lists[i],i);EditorUtility.SetDirty(mesh);
  var tread=surface.GetComponent<Renderer>().sharedMaterials[1];var riserMat=surface.GetComponent<Renderer>().sharedMaterials[2];
  Color baseBefore=tread.GetColor("_BaseColor");var shader=Shader.Find("Mistbell/StepPainted");if(shader==null)throw new Exception("Step shader missing");
  var treadTex=Import("stone_step_tread");var riserTex=Import("stone_step_riser");var snowTex=Import("stone_step_snow");
  foreach(var m in new[]{tread,riserMat}){m.shader=shader;m.SetFloat("_Smoothness",.08f);m.SetFloat("_Specular",0);m.SetFloat("_SnowAlpha",.65f);m.SetFloat("_SnowWidth",.08f);m.SetTexture("_SnowMap",snowTex);EditorUtility.SetDirty(m);}
  tread.SetTexture("_BaseMap",treadTex);tread.SetColor("_BaseColor",baseBefore);tread.SetFloat("_SnowEnabled",1);
  riserMat.SetTexture("_BaseMap",riserTex);riserMat.SetFloat("_SnowEnabled",0);
  bool protectedSame=protectedBefore==Protected(surface),geometrySame=geometryBefore==Geometry(mesh),colliderSame=colliderBefore==Geometry(collider.sharedMesh);
  Debug.Log("[STEP_TEX] tread=1.35 riser=1.2x1 snow=0.7");
  Debug.Log("[STEP_TEX] riser_smoothness="+riserMat.GetFloat("_Smoothness")+" spec="+riserMat.GetFloat("_Specular"));
  Debug.Log($"[STEP_TEX] steps={distinctSteps.Count} tread_quads={treads} riser_quads={risers} snow_width=0.08 alpha=0.65 repeat=true mipmap=true aniso=4 filter=Bilinear base_color_unchanged={baseBefore==tread.GetColor("_BaseColor")} geometry_unchanged={geometrySame} collider_unchanged={colliderSame} protected_unchanged={protectedSame}");
  File.WriteAllText(Out+"protection.txt",$"geometry_before={geometryBefore}\ngeometry_after={Geometry(mesh)}\ncollider_before={colliderBefore}\ncollider_after={Geometry(collider.sharedMesh)}\nprotected_before={protectedBefore}\nprotected_after={Protected(surface)}\n");
  if(distinctSteps.Count!=30||treads!=30||risers!=90||!geometrySame||!colliderSame||!protectedSame)throw new Exception("Step texture protection gate failed");
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(surface.scene);EditorSceneManager.SaveScene(surface.scene);
  var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/ArtSpike.unity"},locationPathName="Builds/Mac/Mistbell.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
  Debug.Log($"[STEP_BUILD] result={report.summary.result} errors={report.summary.totalErrors}");if(report.summary.result!=BuildResult.Succeeded)EditorApplication.Exit(1);
 }
}
