using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Patch the current authored scene. Never regenerate the church or player.
public static class SnowGrassFenceBuild
{
    const string Output="../artifacts/snow_grass_fence/";
    static T[] All<T>() where T:Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None);
    static bool Grass(string n)=>n.StartsWith("FGD_Grass")||n.StartsWith("DryGrass_")||n.StartsWith("TrailShrub_");
    static bool Fence(string n)=>n.StartsWith("FencePost_")||n.StartsWith("FenceRail_")||n.StartsWith("FenceSnow_")||n.StartsWith("PostDrift_")||n.StartsWith("SF2_");
    static string Sha(string s){using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s))).Replace("-","");}
    static string Protected()
    {
        var data=new List<string>();
        foreach(var r in All<Renderer>()) {
            if(Grass(r.name)||Fence(r.name))continue;
            data.Add(r.name+EditorJsonUtility.ToJson(r)+r.transform.localToWorldMatrix.ToString("F6"));
            var f=r.GetComponent<MeshFilter>();if(f!=null)data.Add(EditorJsonUtility.ToJson(f.sharedMesh));
            foreach(var m in r.sharedMaterials) if(m!=null && m.name!="M_Snow2Terrain")data.Add(EditorJsonUtility.ToJson(m));
        }
        foreach(var c in All<Collider>()) if(!Grass(c.name)&&!Fence(c.name))data.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var c in All<Camera>())data.Add(EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var c in All<Light>())data.Add(EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var c in All<PlayerController>())data.Add(EditorJsonUtility.ToJson(c));
        foreach(var c in All<ShotRig>())data.Add(EditorJsonUtility.ToJson(c));
        data.Sort(StringComparer.Ordinal);return Sha(string.Join("\n",data));
    }
    static Material Material(string name,string color,bool wood) {
        string path="Assets/Settings/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Mistbell/SnowFence"));AssetDatabase.CreateAsset(m,path);}
        ColorUtility.TryParseHtmlString(color,out var c);m.SetColor("_BaseColor",c);m.SetFloat("_Wood",wood?1:0);m.SetFloat("_ShadowCoverage",.625f);EditorUtility.SetDirty(m);return m;
    }
    [Serializable] class Report {
        public string protected_before,protected_after,post_color,snow_color;
        public int grass_before,grass_after,posts,rails,snow_caps,batches;
        public float post_width=.1f,post_height=1,post_spacing=2.2f,rail_width=.06f,rail_height=.08f,rail_gap=.45f,snow_depth=.04f,shadow_coverage=.625f;
        public float spacing_min=999,spacing_max;
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");Physics.SyncTransforms();
        var report=new Report {protected_before=Protected(),post_color="#4A3B2E",snow_color="#EDF3F8"};
        if(File.Exists(Output+"assets.json")&&!File.Exists(Output+"initial_assets.json"))File.Copy(Output+"assets.json",Output+"initial_assets.json");
        var grass=All<MeshRenderer>().Where(r=>Grass(r.name)).ToArray();report.grass_before=grass.Length;
        foreach(var r in grass) {
            var m=r.sharedMaterial;var c=m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):Color.white;
            Debug.Log(FormattableString.Invariant($"[GRASS_PROBE] name={r.name} material={m.name} shader={m.shader.name} alpha={c.a:F3} surface={(m.HasProperty("_Surface")?m.GetFloat("_Surface"):0)} bounds={r.bounds.ToString("F3")} reason=opaque_geometry_no_cutout action=delete"));
            Object.DestroyImmediate(r.gameObject);
        }
        foreach(var r in All<MeshRenderer>().Where(r=>Fence(r.name)).ToArray())Object.DestroyImmediate(r.gameObject);
        var old=GameObject.Find("Snow Grass Fence");if(old!=null)Object.DestroyImmediate(old);
        var root=new GameObject("Snow Grass Fence").transform;
        var wood=Material("M_SF2_Wood",report.post_color,true);var snow=Material("M_SF2_Snow",report.snow_color,false);
        var terrain=Object.FindFirstObjectByType<PlayerController>().ground;
        Vector3 Snap(Vector3 p){if(!terrain.Raycast(new Ray(new Vector3(p.x,400,p.z),Vector3.down),out var hit,1000))throw new Exception("Fence ground missed");return hit.point;}
        var batches=new Dictionary<string,List<CombineInstance>>();
        void Box(string name,Vector3 size,Vector3 center,Quaternion rotation,Material mat,int segment) {
            // Source transforms/colliders retained for measurable dimensions; render mesh
            // merged per segment and material, so both capture and play use the same batches.
            var go=SpikeArt.Box(root,name,size,center,mat,name.StartsWith("SF2_Post_")||name.StartsWith("SF2_Rail_"));go.transform.rotation=rotation;
            var f=go.GetComponent<MeshFilter>();var r=go.GetComponent<MeshRenderer>();
            string key=segment+"_"+(mat==wood?"Wood":"Snow");
            if(!batches.TryGetValue(key,out var list))batches[key]=list=new List<CombineInstance>();
            list.Add(new CombineInstance{mesh=f.sharedMesh,transform=go.transform.localToWorldMatrix});
            Object.DestroyImmediate(r);Object.DestroyImmediate(f);
        }
        for(int side=-1;side<=1;side+=2) {
            // Offset/miter the route, then sample 2.2m horizontal chords through bends.
            var route=ClimbRoute.Points;
            var offsetRoute=new Vector2[route.Length];
            Vector2 Normal(int segment){var d=(route[segment+1]-route[segment]).normalized;return new Vector2(d.y,-d.x);}
            for(int j=0;j<route.Length;j++) {
                var n=j==0?Normal(0):j==route.Length-1?Normal(j-1):(Normal(j-1)+Normal(j)).normalized;
                float miter=j==0||j==route.Length-1?1:Vector2.Dot(n,Normal(j));
                offsetRoute[j]=route[j]+n*(side*2.6f/miter);
            }
            float station=0,total=0;Vector3 previous=Vector3.zero;bool havePrevious=false;
            for(int seg=0;seg<4;seg++) {
                float length=Vector2.Distance(offsetRoute[seg],offsetRoute[seg+1]);
                while(station<=total+length) {
                    // Across a bend, arc length is longer than the chord. Advance
                    // the first station to the 2.2m circle/segment intersection.
                    if(havePrevious) {
                        var direction=(offsetRoute[seg+1]-offsetRoute[seg]).normalized;
                        var delta=offsetRoute[seg]-new Vector2(previous.x,previous.z);
                        float dot=Vector2.Dot(delta,direction);
                        float discriminant=dot*dot-delta.sqrMagnitude+2.2f*2.2f;
                        if(discriminant>=0)station=Mathf.Max(station,total-dot+Mathf.Sqrt(discriminant));
                        if(station>total+length)break;
                    }
                    var point=Vector2.Lerp(offsetRoute[seg],offsetRoute[seg+1],(station-total)/length);
                    var p=Snap(new Vector3(point.x,0,point.y));station+=2.2f;
                    if(p.z>-19.25f)continue;
                    float yaw=Mathf.Atan2(ClimbRoute.Points[seg+1].x-ClimbRoute.Points[seg].x,ClimbRoute.Points[seg+1].y-ClimbRoute.Points[seg].y)*Mathf.Rad2Deg;
                    var rot=Quaternion.Euler(0,yaw,0);
                    Box("SF2_Post_"+report.posts,new Vector3(.1f,1,.1f),p+Vector3.up*.5f,rot,wood,seg);report.posts++;
                    Box("SF2_Cap_"+report.snow_caps,new Vector3(.12f,.04f,.12f),p+Vector3.up*1.02f,rot,snow,seg);report.snow_caps++;
                    if(havePrevious) {
                        float spacing=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(previous.x,previous.z));
                        report.spacing_min=Mathf.Min(report.spacing_min,spacing);report.spacing_max=Mathf.Max(report.spacing_max,spacing);
                        foreach(float h in new[]{.30f,.75f}) {
                            var a=previous+Vector3.up*h;var b=p+Vector3.up*h;var d=b-a;
                            Box("SF2_Rail_"+report.rails,new Vector3(.06f,.08f,d.magnitude),(a+b)*.5f,Quaternion.LookRotation(d),wood,seg);report.rails++;
                            Box("SF2_RailSnow_"+report.rails,new Vector3(.07f,.04f,d.magnitude),(a+b)*.5f+Vector3.up*.06f,Quaternion.LookRotation(d),snow,seg);
                        }
                    }
                    previous=p;havePrevious=true;
                }
                total+=length;
            }
        }
        foreach(var entry in batches) {
            var mesh=new Mesh {name="SF2_"+entry.Key,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(entry.Value.ToArray(),true,true);mesh.RecalculateBounds();
            string path="Assets/Settings/SF2_"+entry.Key+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);}
            var go=new GameObject("SF2_Batch_"+entry.Key);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=saved;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=entry.Key.EndsWith("Wood")?wood:snow;r.shadowCastingMode=ShadowCastingMode.On;
            report.batches++;
        }
        // Read the actual authored objects/materials back for acceptance, rather
        // than reporting only the requested construction constants.
        var nodes=root.GetComponentsInChildren<Transform>();
        var postNodes=nodes.Where(t=>t.name.StartsWith("SF2_Post_")).ToArray();
        var railNodes=nodes.Where(t=>t.name.StartsWith("SF2_Rail_")).OrderBy(t=>int.Parse(t.name.Substring(9))).ToArray();
        var caps=nodes.Where(t=>t.name.StartsWith("SF2_Cap_")).ToArray();
        if(postNodes.Any(t=>Vector3.Distance(t.localScale,new Vector3(.1f,1,.1f))>1e-5f)||
           railNodes.Any(t=>Mathf.Abs(t.localScale.x-.06f)>1e-5f||Mathf.Abs(t.localScale.y-.08f)>1e-5f)||
           caps.Any(t=>Mathf.Abs(t.localScale.y-.04f)>1e-5f))throw new Exception("Fence dimensions failed");
        report.posts=postNodes.Length;report.rails=railNodes.Length;report.snow_caps=caps.Length;
        if(report.spacing_min<2.199f||report.spacing_max>2.201f)throw new Exception("Fence post spacing failed");
        report.post_spacing=(report.spacing_min+report.spacing_max)*.5f;
        report.post_width=postNodes[0].localScale.x;report.post_height=postNodes[0].localScale.y;
        report.rail_width=railNodes[0].localScale.x;report.rail_height=railNodes[0].localScale.y;
        report.rail_gap=railNodes[1].position.y-railNodes[0].position.y;report.snow_depth=caps[0].localScale.y;
        report.post_color="#"+ColorUtility.ToHtmlStringRGB(wood.GetColor("_BaseColor"));
        report.snow_color="#"+ColorUtility.ToHtmlStringRGB(snow.GetColor("_BaseColor"));report.shadow_coverage=wood.GetFloat("_ShadowCoverage");
        var ground=terrain.GetComponent<MeshRenderer>().sharedMaterial;ground.SetFloat("_SnowNonrepeat",1);EditorUtility.SetDirty(ground);
        report.grass_after=All<MeshRenderer>().Count(r=>Grass(r.name));report.protected_after=Protected();
        File.WriteAllText(Output+"assets.json",JsonUtility.ToJson(report,true));
        if(report.protected_before!=report.protected_after||report.grass_after!=0||report.posts<2||report.rails!=2*(report.posts-2))throw new Exception("Snow grass fence protected/layout gate failed");
        Debug.Log($"[GRASS] count={report.grass_after} removed_renderers={report.grass_before}");
        Debug.Log(FormattableString.Invariant($"[FENCE2] post_color={report.post_color} rail=2 snow_cap=true shadow_strength={wood.GetFloat("_ShadowCoverage"):F3} shadow_method=bayer_coverage posts={report.posts} rails_total={report.rails} batches={report.batches} spacing=2.2 rail_gap=0.45 protected_unchanged=true"));
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveScene(root.gameObject.scene);PerformanceBuild.Run();
    }
}
