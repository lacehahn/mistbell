using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

// Read-only physical snapshots: no transient Unity instance IDs in protected comparisons.
public static class RebuildSpawnAudit {
 const string Out="../artifacts/rebuild_fix_spawn/";
 [Serializable] class Row {public string key,value;}
 [Serializable] class Snapshot {public Row[] rows;}
 static string Sha(string s){using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s))).Replace("-","");}
 static string MeshData(Mesh m) => m==null?"none":JsonUtility.ToJson(new MeshValues {v=m.vertices,n=m.normals,uv=m.uv,c=m.colors,t=m.triangles});
 [Serializable] class MeshValues {public Vector3[] v,n; public Vector2[] uv;public Color[] c;public int[] t;}
 static string MaterialData(Material m){if(m==null)return "none";var s=new System.Text.StringBuilder(m.shader.name);for(int i=0;i<m.shader.GetPropertyCount();i++){
  string p=m.shader.GetPropertyName(i);s.Append('|').Append(p);switch(m.shader.GetPropertyType(i)) {
   case UnityEngine.Rendering.ShaderPropertyType.Color:s.Append(m.GetColor(p).ToString("F6"));break;
   case UnityEngine.Rendering.ShaderPropertyType.Vector:s.Append(m.GetVector(p).ToString("F6"));break;
   case UnityEngine.Rendering.ShaderPropertyType.Texture:var t=m.GetTexture(p);s.Append(AssetDatabase.GetAssetPath(t)).Append(m.GetTextureScale(p).ToString("F6")).Append(m.GetTextureOffset(p).ToString("F6"));break;
   default:s.Append(m.GetFloat(p).ToString("F6",System.Globalization.CultureInfo.InvariantCulture));break;
  }}return s.ToString();}
 static void Capture(string file){
  Directory.CreateDirectory(Out);EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");var rows=new List<Row>();
  void Add(string k,string v){rows.Add(new Row{key=k,value=Sha(v)});}
  foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
   bool fence=r.name=="FM4_Batch",pilgrim=r.name=="Pilgrim",mountain=r.name=="Sky Dome"||r.sharedMaterials.Any(m=>m!=null&&m.shader.name=="Mistbell/PaintedMountain");
   bool inside=r.GetComponentInParent<ChurchInterior>()!=null;
   if(!fence&&!pilgrim&&!mountain&&!inside)continue;
   string category=fence?"fence":pilgrim?"pilgrim":mountain?"mountain":"interior";
   var f=r.GetComponent<MeshFilter>();Add(category+"/"+r.name,"enabled="+r.enabled+(pilgrim?"":r.transform.localToWorldMatrix.ToString("F6"))+MeshData(f!=null?f.sharedMesh:null)+string.Join(";",r.sharedMaterials.Select(MaterialData)));
  }
  foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))Add("camera/"+c.name,c.transform.localToWorldMatrix.ToString("F6")+c.fieldOfView.ToString("F6")+c.nearClipPlane+"/"+c.farClipPlane);
  foreach(var rig in Object.FindObjectsByType<ShotRig>(FindObjectsInactive.Include,FindObjectsSortMode.None))Add("shotrig/"+rig.name,JsonUtility.ToJson(rig));
  foreach(var m in Object.FindObjectsByType<MistReveal>(FindObjectsInactive.Include,FindObjectsSortMode.None))Add("reveal/"+m.name,System.Text.RegularExpressions.Regex.Replace(EditorJsonUtility.ToJson(m),@"""instanceID"":-?\d+","\"instanceID\":0"));
  Add("fog",RenderSettings.fog+"/"+RenderSettings.fogColor.ToString("F6")+"/"+RenderSettings.fogDensity+"/"+RenderSettings.fogStartDistance+"/"+RenderSettings.fogEndDistance);
  File.WriteAllText(Out+file,JsonUtility.ToJson(new Snapshot{rows=rows.OrderBy(r=>r.key).ThenBy(r=>r.value).ToArray()},true));Debug.Log("[PROTECTED] snapshot="+file+" rows="+rows.Count);
 }
 public static void Before()=>Capture("protected_before.json");
 public static void After()=>Capture("protected_after.json");
}
