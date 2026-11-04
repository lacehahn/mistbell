using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Local patch only: never call scene regeneration or previous-round Apply methods.
public static class InteriorPilastersDomeBuild
{
 const string Out="../artifacts/interior_pilasters_dome/";
 static bool Pillar(string n)=>n=="Interior_Column_03"||n=="Interior_Column_04";
 static bool Mutable(string n)=>Pillar(n)||n.StartsWith("Interior_Pilaster")||n.StartsWith("Interior_ConeRib_")||n.StartsWith("Interior_DrumRib_")||n.StartsWith("Interior_DomeMullion")||n.StartsWith("Interior_DrumWindow")||n=="Interior_DomeInner"||n.StartsWith("Interior_LightShaft_");
 static string Digest(string s){using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s))).Replace("-","").ToLowerInvariant();}
 static string Protected(){
 var lines=new List<string>();
 foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
 if(Mutable(r.name))continue;
 lines.Add(r.name+(r.name=="Interior_RoyalDoor_Frame"?r.enabled.ToString():EditorJsonUtility.ToJson(r))+r.transform.localToWorldMatrix.ToString("F6"));
 lines.Add(r.name+EditorJsonUtility.ToJson(r.GetComponent<MeshFilter>().sharedMesh));
 foreach(var m in r.sharedMaterials)if(r.name!="Interior_RoyalDoor_Frame")lines.Add(r.name+EditorJsonUtility.ToJson(m));
 }
 foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))lines.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
 foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))lines.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
 foreach(var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))lines.Add(l.name+EditorJsonUtility.ToJson(l)+l.transform.localToWorldMatrix.ToString("F6"));
 lines.Sort(StringComparer.Ordinal);return Digest(string.Join("\n",lines));
 }
 static Material Mat(string name,Material source){
 string path="Assets/Settings/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
 if(!m){m=new Material(source);AssetDatabase.CreateAsset(m,path);}else m.CopyPropertiesFromMaterial(source);
 m.name=name;EditorUtility.SetDirty(m);return m;
 }
 static void Tone(Material m,Color c,Color e){m.SetColor("_BaseColor",c);if(m.HasProperty("_Color"))m.SetColor("_Color",c);m.SetColor("_EmissionColor",e);m.EnableKeyword("_EMISSION");m.SetTexture("_BaseMap",null);m.SetTexture("_EmissionMap",null);m.SetFloat("_Smoothness",.15f);m.SetFloat("_Cull",0);m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;EditorUtility.SetDirty(m);}
 static void Quad(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d){int k=v.Count;v.AddRange(new[]{a,b,c,d});t.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}
 static void Mesh(Transform root,string name,List<Vector3> v,List<int> t,Material m){
 string path="Assets/Art/Church/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
 mesh.name=name;mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.uv=v.Select(p=>new Vector2(p.x,p.y)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
 var tr=root.Find(name);if(!tr){tr=new GameObject(name).transform;tr.SetParent(root,true);tr.gameObject.AddComponent<MeshFilter>();tr.gameObject.AddComponent<MeshRenderer>();}
 tr.GetComponent<MeshFilter>().sharedMesh=mesh;var r=tr.GetComponent<MeshRenderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;
 }
 static void Ring(Transform root,string name,Vector3 center,float radius,float height,Material m){
 var v=new List<Vector3>();var t=new List<int>();
 for(int k=0;k<24;k++){float a=k*Mathf.PI/12,b=(k+1)*Mathf.PI/12;Vector3 P(float angle,float y,float r)=>center+new Vector3(Mathf.Cos(angle)*r,y,Mathf.Sin(angle)*r);
 Quad(v,t,P(a,0,radius),P(a,height,radius),P(b,height,radius),P(b,0,radius));
 Quad(v,t,P(a,height,radius),P(a,height,radius-.05f),P(b,height,radius-.05f),P(b,height,radius));}
 Mesh(root,name,v,t,m);
 }
 public static void Apply(ChurchInterior i){
 var root=i.detailRoot.transform;var rs=i.GetComponentsInChildren<MeshRenderer>(true);
 var shaft=Mat("M_InteriorPilasterShaft",i.indoorStone);shaft.shader=Shader.Find("Mistbell/InteriorPilasterStone");
 Tone(shaft,new Color(.50f,.481f,.457f),new Color(.239f,.211f,.197f)*1.19f);
 var trim=Mat("M_InteriorPilasterTrim",shaft);Tone(trim,shaft.GetColor("_BaseColor")*1.09f,shaft.GetColor("_EmissionColor")*1.09f);
 foreach(var r in rs.Where(r=>Pillar(r.name))){r.sharedMaterial=shaft;var b=r.bounds;
 Ring(root,"Interior_PilasterBase_"+r.name,b.center+Vector3.down*(b.size.y/2),.38f,.16f,trim);
 Ring(root,"Interior_PilasterCap_"+r.name,new Vector3(b.center.x,b.max.y-.16f,b.center.z),.38f,.16f,trim);
 }
 var plaster=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_InteriorWindowsPlaster.mat");plaster.shader=Shader.Find("Mistbell/InteriorDomeMottle");Tone(plaster,(Color)new Color32(156,139,116,255)*.945f,(Color)new Color32(156,139,116,255)*(.35f*.945f));
 Color pc=plaster.GetColor("_BaseColor"),pe=plaster.GetColor("_EmissionColor");
 var light=Mat("M_InteriorDome2RibTop",plaster);light.shader=Shader.Find("Universal Render Pipeline/Lit");Tone(light,pc*1.11f,pe*1.11f);
 var dark=Mat("M_InteriorDome2RibSide",plaster);dark.shader=light.shader;Tone(dark,pc*1.11f*.84f,pe*1.11f*.84f);
 // A full 12cm top face with two 5cm side faces, replacing the old two coplanar half-width strips.
 for(int k=0;k<12;k++){
 float a=k*Mathf.PI/6;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var tangent=new Vector3(-radial.z,0,radial.x);var normal=(Vector3.down*2.8f-radial*2.4f).normalized;
 Vector3 p=new Vector3(-2,12.2f,0)+radial*3.2f,q=new Vector3(-2,14.6f,0)+radial*.4f;
 var v=new List<Vector3>();var t=new List<int>();Quad(v,t,p+normal*.05f-tangent*.06f,q+normal*.05f-tangent*.06f,q+normal*.05f+tangent*.06f,p+normal*.05f+tangent*.06f);Mesh(root,"Interior_ConeRib_"+k+"_0",v,t,light);
 v.Clear();t.Clear();foreach(float sign in new[]{-1f,1f})Quad(v,t,p+tangent*(sign*.06f),q+tangent*(sign*.06f),q+tangent*(sign*.06f)+normal*.05f,p+tangent*(sign*.06f)+normal*.05f);Mesh(root,"Interior_ConeRib_"+k+"_1",v,t,dark);
 }
 foreach(var r in rs.Where(r=>r.name.StartsWith("Interior_DrumRib_"))){var s=r.transform.localScale;s.x=.04f;s.z=.04f;r.transform.localScale=s;r.sharedMaterial=rs.First(x=>x.name=="Interior_DrumWall_0").sharedMaterial;}
 var glass=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_InteriorWindowsDrum.mat");glass.SetColor("_EmissionColor",(Color)new Color32(217,230,242,255)*.95f);EditorUtility.SetDirty(glass);
 // One physical 3cm vertical mullion per drum window; no new colliders.
 var stone=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_InteriorWindowsStone.mat");
 for(int k=0;k<12;k++){
 float a=(k+.5f)*Mathf.PI/6;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var tangent=new Vector3(-radial.z,0,radial.x);var c=new Vector3(-2,10.4f,0)+radial*(3.2f*Mathf.Cos(Mathf.PI/12)-.02f);
 var v=new List<Vector3>();var t=new List<int>();
 foreach(float d in new[]{-.015f,.015f})Quad(v,t,c+radial*d-tangent*.015f-Vector3.up*.675f,c+radial*d-tangent*.015f+Vector3.up*.675f,c+radial*d+tangent*.015f+Vector3.up*.675f,c+radial*d+tangent*.015f-Vector3.up*.675f);
 Mesh(root,"Interior_DomeMullion_"+k,v,t,stone);
 }
 // Preserve side arch frames using the shared original: isolate only the authorized royal door.
 var gold=Mat("M_InteriorV2Gold_RoyalDoor",AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_InteriorV2Gold.mat"));
 rs.First(r=>r.name=="Interior_RoyalDoor_Frame").sharedMaterial=gold;
 gold.SetColor("_BaseColor",(Color)new Color32(201,162,39,255)*.93f);gold.SetColor("_Color",gold.GetColor("_BaseColor"));gold.SetColor("_EmissionColor",new Color(.70f,.45f,.10f)*.93f);EditorUtility.SetDirty(gold);
 foreach(var r in rs.Where(r=>r.name.StartsWith("Interior_LightShaft_"))){var m=r.sharedMaterial;var c=m.GetColor("_BaseColor");c.a=.08f;m.SetColor("_BaseColor",c);EditorUtility.SetDirty(m);}
 }
 static string Changed(ChurchInterior i){
 var lines=new List<string>();
 foreach(var r in i.GetComponentsInChildren<MeshRenderer>(true).Where(r=>Mutable(r.name)||r.name=="Interior_RoyalDoor_Frame").OrderBy(r=>r.name)) {
 var text=r.enabled+r.transform.localToWorldMatrix.ToString("F6")+EditorJsonUtility.ToJson(r.GetComponent<MeshFilter>().sharedMesh)+string.Join("|",r.sharedMaterials.Select(m=>EditorJsonUtility.ToJson(m)));
 lines.Add(r.name+" "+Digest(text));
 }
 return string.Join("\n",lines);
 }
 public static void Run(){
 Directory.CreateDirectory(Out);EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");var i=Object.FindFirstObjectByType<ChurchInterior>();
 string before=Protected();Apply(i);AssetDatabase.SaveAssets();string once=Changed(i);Apply(i);AssetDatabase.SaveAssets();if(once!=Changed(i))throw new Exception("Patch not idempotent");string after=Protected();
 File.WriteAllText(Out+"protected_digests.json","{\"before\":\""+before+"\",\"after\":\""+after+"\"}\n");if(before!=after)throw new Exception("Protected content changed");
 var rs=i.GetComponentsInChildren<MeshRenderer>(true);var gold=rs.First(r=>r.name=="Interior_RoyalDoor_Frame").sharedMaterial;Color c=gold.GetColor("_BaseColor");
 Debug.Log(FormattableString.Invariant($"[GOLD_FRAME_MATERIAL] luma={.2126f*c.r+.7152f*c.g+.0722f*c.b:F6} unchanged_geometry=true factor=0.93 metric=material_base_Rec709"));
 var widths=new List<float>();var depths=new List<float>();var bars=new List<float>();
 for(int k=0;k<12;k++){
 float a=k*Mathf.PI/6;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var tangent=new Vector3(-radial.z,0,radial.x);var normal=(Vector3.down*2.8f-radial*2.4f).normalized;
 var v=rs.First(r=>r.name=="Interior_ConeRib_"+k+"_0").GetComponent<MeshFilter>().sharedMesh.vertices;var side=rs.First(r=>r.name=="Interior_ConeRib_"+k+"_1").GetComponent<MeshFilter>().sharedMesh.vertices;
 widths.Add(v.Max(p=>Vector3.Dot(p,tangent))-v.Min(p=>Vector3.Dot(p,tangent)));depths.Add(side.Max(p=>Vector3.Dot(p,normal))-side.Min(p=>Vector3.Dot(p,normal)));
 a=(k+.5f)*Mathf.PI/6;tangent=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a));var mv=rs.First(r=>r.name=="Interior_DomeMullion_"+k).GetComponent<MeshFilter>().sharedMesh.vertices;bars.Add(mv.Max(p=>Vector3.Dot(p,tangent))-mv.Min(p=>Vector3.Dot(p,tangent)));
 }
 var ribs=rs.Where(r=>r.name.StartsWith("Interior_DrumRib_")).ToArray();int black=ribs.Count(r=>Mathf.Max(r.bounds.size.x,r.bounds.size.z)>.06001f||r.sharedMaterial!=rs.First(x=>x.name=="Interior_DrumWall_0").sharedMaterial);
 Debug.Log(FormattableString.Invariant($"[DOME2_GEOMETRY] rib_width_min={widths.Min():F5} rib_width_max={widths.Max():F5} rib_depth_min={depths.Min():F5} rib_depth_max={depths.Max():F5} mullion_width_min={bars.Min():F5} mullion_width_max={bars.Max():F5} mullions={bars.Count} black_bits={black} max_decoration_aabb={ribs.Max(r=>Mathf.Max(r.bounds.size.x,r.bounds.size.z)):F5} origin=old_18cm_drum_rib_endcaps replacement=4cm_wall_stone"));
 Debug.Log("[PILASTERS_PROTECTED] identical=true idempotent=true floor_exterior_camera_altar_icons_pews_colliders=true");
 EditorSceneManager.MarkSceneDirty(i.gameObject.scene);EditorSceneManager.SaveScene(i.gameObject.scene);PerformanceBuild.Run();
 }
}
