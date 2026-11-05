using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Local, repeatable exterior overlay. Original shared materials and all colliders stay intact.
public static class Church3DDetailBuild
{
    const string Dir="Assets/Settings/Church3D/";
    static Transform church;
    static bool Exterior(Component c)=>c.transform.IsChildOf(church) && !(c is Collider) || c.name.StartsWith("WarmWindow_") || c.transform.root.name=="Church3D Detail";
    static string Protected()
    {
        var entries=new List<string>();
        foreach(var root in church.gameObject.scene.GetRootGameObjects())
        foreach(var c in root.GetComponentsInChildren<Component>(true))
        {
            if(c==null || Exterior(c))continue;
            entries.Add(c.GetType().FullName+":"+c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
            if(c is Renderer r)foreach(var m in r.sharedMaterials)if(m!=null)entries.Add(EditorJsonUtility.ToJson(m));
            if(c is MeshFilter f && f.sharedMesh!=null)entries.Add(EditorJsonUtility.ToJson(f.sharedMesh));
        }
        entries.Sort(StringComparer.Ordinal);
        using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",entries)))).Replace("-","");
    }
    static Material Material(string name,Material source)
    {
        string path=Dir+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(source);AssetDatabase.CreateAsset(m,path);}else EditorUtility.CopySerialized(source,m);
        m.name=name;EditorUtility.SetDirty(m);return m;
    }
    static Transform MeshObject(string name,Transform root,List<Vector3> v,List<int> t,Material mat)
    {
        var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        string path=Dir+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);}
        var go=new GameObject(name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=saved;
        var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        return go.transform;
    }
    static void Quad(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {int n=v.Count;v.AddRange(new[]{a,b,c,d});t.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});}
    static string Key(Vector3 v)=>$"{Mathf.RoundToInt(v.x*10000)},{Mathf.RoundToInt(v.y*10000)},{Mathf.RoundToInt(v.z*10000)}";
    class Edge {public Vector3 a,b;public List<(Vector3 normal,Vector3 third)> faces=new List<(Vector3,Vector3)>();}
    static int RoofShadows(Transform root,Material mat)
    {
        var v=new List<Vector3>();var t=new List<int>();int strips=0;
        foreach(var f in church.GetComponentsInChildren<MeshFilter>())
        {
            if(!f.name.Contains("Roof"))continue;
            var edges=new Dictionary<string,Edge>();var vs=f.sharedMesh.vertices;var ts=f.sharedMesh.triangles;
            for(int i=0;i<ts.Length;i+=3)
            {
                var p=new[]{f.transform.TransformPoint(vs[ts[i]]),f.transform.TransformPoint(vs[ts[i+1]]),f.transform.TransformPoint(vs[ts[i+2]])};
                var n=Vector3.Cross(p[1]-p[0],p[2]-p[0]).normalized;
                for(int k=0;k<3;k++)
                {
                    var a=p[k];var b=p[(k+1)%3];string ka=Key(a),kb=Key(b),key=string.CompareOrdinal(ka,kb)<0?ka+":"+kb:kb+":"+ka;
                    if(!edges.TryGetValue(key,out var e)){e=new Edge{a=a,b=b};edges[key]=e;}
                    e.faces.Add((n,p[(k+2)%3]));
                }
            }
            foreach(var e in edges.Values)
            {
                if((e.b-e.a).magnitude<.15f)continue;
                foreach(var face in e.faces)
                {
                    if(face.normal.y<.2f)continue;
                    if(e.faces.Count>1 && !e.faces.Any(q=>Vector3.Dot(q.normal,face.normal)<.75f))continue;
                    var along=(e.b-e.a).normalized;var inward=face.third-e.a;inward=(inward-Vector3.Dot(inward,along)*along).normalized;
                    var lift=face.normal*.004f;float width=8f/112f;
                    Quad(v,t,e.a+lift,e.b+lift,e.b+inward*width+lift,e.a+inward*width+lift);strips++;
                }
            }
        }
        MeshObject("RoofRidgeGableShadows",root,v,t,mat);return strips;
    }
    [Serializable] class WindowReport {public string name;public float projection,glow_ratio,reveal_ratio;public int vertical,horizontal,shadow_quads,glow_vertices;}
    [Serializable] class Report {public string protected_before,protected_after;public int roof_strips;public float roof_alpha,roof_width_texels;public WindowReport[] windows;}
    public static void Run()
    {
        Directory.CreateDirectory(Dir);Directory.CreateDirectory("../artifacts/church3d");AssetDatabase.Refresh();
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");church=GameObject.Find("Church").transform;
        var report=new Report{protected_before=Protected()};
        var old=GameObject.Find("Church3D Detail");if(old!=null)Object.DestroyImmediate(old);
        var root=new GameObject("Church3D Detail").transform;
        var textures=new Texture2D[3];
        for(int i=0;i<3;i++)
        {
            string p=$"Assets/Art/MaterialForge/Church3D_Atlas{i}.png";var imp=(TextureImporter)AssetImporter.GetAtPath(p);
            imp.textureType=TextureImporterType.Default;imp.filterMode=FilterMode.Point;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=4096;imp.isReadable=true;imp.SaveAndReimport();textures[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        var clones=new Dictionary<Material,Material>();
        foreach(var r in church.GetComponentsInChildren<MeshRenderer>())
        {
            var mats=r.sharedMaterials;
            for(int i=0;i<mats.Length;i++)
            {
                var src=mats[i];if(src==null || !src.HasProperty("_Style11UV") || src.GetFloat("_Style11UV")<.5f)continue;
                if(!clones.TryGetValue(src,out var m))
                {
                    // Reruns derive from the original, never accumulate a material tint.
                    string sourceName=src.name.StartsWith("Church3D_")?src.name.Substring(9):src.name.EndsWith("_Church3D")?src.name.Substring(0,src.name.Length-9):src.name;
                    var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/"+sourceName+".mat");
                    if(original==null)throw new Exception("Original church material missing: "+src.name);
                    m=Material(original.name+"_Church3D",original);m.SetTexture("_BaseMap",textures[0]);m.SetTexture("_Style11Atlas1",textures[1]);m.SetTexture("_Style11Atlas2",textures[2]);clones[src]=m;
                }
                mats[i]=m;
            }
            r.sharedMaterials=mats;
        }
        var shadow=new Material(Shader.Find("Universal Render Pipeline/Unlit"));shadow.SetColor("_BaseColor",new Color(0,0,0,.35f));
        shadow.SetFloat("_Surface",1);shadow.SetFloat("_Blend",0);shadow.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);shadow.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);shadow.SetFloat("_ZWrite",0);shadow.SetFloat("_Cull",0);shadow.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");shadow.renderQueue=3000;
        var roofShadow=Material("RoofShadow",shadow);Object.DestroyImmediate(shadow);
        report.roof_strips=RoofShadows(root,roofShadow);report.roof_alpha=roofShadow.GetColor("_BaseColor").a;report.roof_width_texels=8;
        var windows=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(f=>f.name.StartsWith("WarmWindow_")&&!f.name.EndsWith("_Reveal")).ToArray();
        var reports=new List<WindowReport>();
        foreach(var f in windows)
        {
            var tr=f.transform;var bounds=f.sharedMesh.bounds;float w=bounds.size.x,h=bounds.size.y;
            var ring=GameObject.Find(f.name+"_Reveal").GetComponent<MeshRenderer>();var originalFrame=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_WindowInset.mat");
            var inset=Material("WindowInset",originalFrame);inset.SetColor("_BaseColor",originalFrame.GetColor("_BaseColor")*.92f);ring.sharedMaterial=inset;
            var bar=Material("WindowMullion",originalFrame);bar.SetFloat("_Cull",0);
            var baseGlow=f.GetComponent<MeshRenderer>().sharedMaterial;var glow=Material("WindowTrapezoid",baseGlow);glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",baseGlow.GetColor("_EmissionColor")*1.2f);
            var shade=Material("WindowMullionShadow",baseGlow);shade.SetColor("_BaseColor",baseGlow.GetColor("_BaseColor")*.38f);shade.SetColor("_EmissionColor",baseGlow.GetColor("_EmissionColor")*.38f);
            var bv=new List<Vector3>();var bt=new List<int>();var sv=new List<Vector3>();var st=new List<int>();
            var delta=new Vector3(.075f,-Mathf.Sqrt(.15f*.15f-.075f*.075f),0);
            void Rect(float x0,float y0,float x1,float y1)
            {
                var p=new[]{new Vector3(x0,y0,-.10f),new Vector3(x1,y0,-.10f),new Vector3(x1,y1,-.10f),new Vector3(x0,y1,-.10f)};
                Quad(bv,bt,p[0],p[1],p[2],p[3]);
                var q=p.Select(a=>new Vector3(Mathf.Clamp(a.x+delta.x,-w/2+.005f,w/2-.005f),Mathf.Max(.005f,a.y+delta.y),-.016f)).ToArray();Quad(sv,st,q[0],q[1],q[2],q[3]);
            }
            Rect(-.018f,.14f,.018f,h-w*.5f);Rect(-w/2+.01f,h*.34f,w/2-.01f,h*.34f+.035f);Rect(-w/2+.01f,h*.64f,w/2-.01f,h*.64f+.035f);
            var gv=new List<Vector3>();var gt=new List<int>();Quad(gv,gt,new Vector3(-w*.25f,h*.70f,-.008f),new Vector3(w*.30f,h*.70f,-.008f),new Vector3(w*.20f,h*.84f,-.008f),new Vector3(-w*.10f,h*.84f,-.008f));
            foreach(var pair in new[]{(name:"Mullions",v:bv,t:bt,m:bar),(name:"Shadows",v:sv,t:st,m:shade),(name:"Glow",v:gv,t:gt,m:glow)})
            {
                var child=MeshObject(f.name+"_"+pair.name,root,pair.v,pair.t,pair.m);child.SetPositionAndRotation(tr.position,tr.rotation);
            }
            reports.Add(new WindowReport{name=f.name,projection=delta.magnitude,glow_ratio=glow.GetColor("_EmissionColor").r/baseGlow.GetColor("_EmissionColor").r,reveal_ratio=inset.GetColor("_BaseColor").r/originalFrame.GetColor("_BaseColor").r,vertical=1,horizontal=2,shadow_quads=st.Count/6,glow_vertices=gv.Count});
        }
        report.windows=reports.ToArray();report.protected_after=Protected();
        File.WriteAllText("../artifacts/church3d/assets.json",JsonUtility.ToJson(report,true));
        if(report.protected_before!=report.protected_after)throw new Exception("Protected scene data changed");
        if(report.windows.Length!=7 || report.roof_strips==0)throw new Exception("Missing church detail geometry");
        Debug.Log($"[CHURCH3D_ASSETS] windows={report.windows.Length} roof_strips={report.roof_strips} protected=true");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(church.gameObject.scene);EditorSceneManager.SaveScene(church.gameObject.scene);
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/ArtSpike.unity","Assets/Scenes/Climb2D.unity"},locationPathName="Builds/Mac/Mistbell.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
        Debug.Log($"[CHURCH3D_BUILD] result={result.summary.result} errors={result.summary.totalErrors}");
        if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Church detail build failed");
    }
}
