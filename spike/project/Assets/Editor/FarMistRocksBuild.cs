using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Local patch only: never regenerate the scene or any protected asset.
public static class FarMistRocksBuild
{
    static bool Rock(MeshRenderer r)=>r.name.StartsWith("ClusterRock_") || (r.name.StartsWith("Rock_") && r.transform.parent.name.StartsWith("TerraceRockpile_"));
    static bool Mutable(MeshRenderer r)=>Rock(r)||r.name.StartsWith("FencePost_")||r.name=="Sky Dome"||r.sharedMaterials.Any(m=>m!=null&&m.shader.name=="Mistbell/PaintedMountain");
    static string Digest(string s){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s))).Replace("-","").ToLowerInvariant();}
    static string Protected()
    {
        var a=new List<string>();
        foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(Mutable(r))continue;
            a.Add(r.name+EditorJsonUtility.ToJson(r)+r.transform.localToWorldMatrix.ToString("F6"));
            var f=r.GetComponent<MeshFilter>();if(f!=null)a.Add(r.name+EditorJsonUtility.ToJson(f.sharedMesh));
            foreach(var m in r.sharedMaterials)if(m!=null)a.Add(r.name+EditorJsonUtility.ToJson(m));
        }
        foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))a.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))a.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var c in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))a.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var c in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include,FindObjectsSortMode.None))a.Add(EditorJsonUtility.ToJson(c));
        foreach(var c in Object.FindObjectsByType<ShotRig>(FindObjectsInactive.Include,FindObjectsSortMode.None))a.Add(EditorJsonUtility.ToJson(c));
        a.Sort(StringComparer.Ordinal);return Digest(string.Join("\n",a));
    }
    static string Geometry(IEnumerable<MeshRenderer> rocks)=>Digest(string.Join("\n",rocks.OrderBy(r=>r.name+r.transform.position).Select(r=>r.name+r.transform.localToWorldMatrix.ToString("F6")+string.Join(";",r.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v=>v.ToString("F6"))))));
    static Material Mat(string name,Material source)
    {
        string p="Assets/Settings/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);
        if(m==null){m=new Material(source);AssetDatabase.CreateAsset(m,p);}return m;
    }
    static Color Hex(string s){ColorUtility.TryParseHtmlString(s,out var c);return c;}
    static float Linear(float x)=>x<=.04045f?x/12.92f:Mathf.Pow((x+.055f)/1.055f,2.4f);
    static float L(Color c){float y=.2126f*Linear(c.r)+.7152f*Linear(c.g)+.0722f*Linear(c.b);return y>.008856f?116*Mathf.Pow(y,1f/3)-16:903.3f*y;}
    static Color Lab(float l,float a,float b)
    {
        float y=(l+16)/116,x=y+a/500,z=y-b/200;
        float F(float v)=>v*v*v>.008856f?v*v*v:(116*v-16)/903.3f;
        x=.95047f*F(x);y=F(y);z=1.08883f*F(z);
        float G(float v)=>v<=.0031308f?12.92f*v:1.055f*Mathf.Pow(v,1/2.4f)-.055f;
        return new Color(G(3.2404542f*x-1.5371385f*y-.4985314f*z),G(-.969266f*x+1.8760108f*y+.041556f*z),G(.0556434f*x-.2040259f*y+1.0572252f*z),1);
    }
    [Serializable] class Report {public string protected_before,protected_after,geometry_before,geometry_after;public int rocks;public float snow_cap_pct,snow_min_pct,snow_max_pct,post_before_L,post_after_L;}
    public static void Run()
    {
        Directory.CreateDirectory("../artifacts/far_mist_rocks");
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        var all=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);var rocks=all.Where(Rock).ToArray();
        var report=new Report {protected_before=Protected(),geometry_before=Geometry(rocks),rocks=rocks.Length,snow_min_pct=100};
        string baseline="../artifacts/far_mist_rocks/baseline.json";
        if(File.Exists(baseline)) {var old=JsonUtility.FromJson<Report>(File.ReadAllText(baseline));report.protected_before=old.protected_before;report.geometry_before=old.geometry_before;report.post_before_L=old.post_before_L;}
        else {report.post_before_L=L(all.First(r=>r.name.StartsWith("FencePost_")).sharedMaterial.GetColor("_BaseColor"));File.WriteAllText(baseline,JsonUtility.ToJson(report,true));}
        var sky=GameObject.Find("Sky Dome").GetComponent<MeshRenderer>().sharedMaterial;
        sky.SetColor("_HorizonColor",Hex("#DCE4EA"));sky.SetColor("_CloudColor",Hex("#DCE4EA"));EditorUtility.SetDirty(sky);
        float[] haze={.04f,.18f,.34f,.50f};
        for(int i=0;i<4;i++)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>($"Assets/Settings/M_MtnLayer_{i+1}.mat");m.SetFloat("_Haze",haze[i]);
            m.SetColor("_HazeColor",Hex("#DCE4EA"));m.SetColor("_HorizonFogColor",Hex("#DCE4EA"));
            m.SetColor("_SkyCloudColor",Hex("#DCE4EA"));EditorUtility.SetDirty(m);
            m.SetFloat("_HorizonFog",.65f);m.SetFloat("_MistBrightness",.78f);
            m.SetFloat("_RidgeSkyRatio",new[]{.72f,.82f,.90f,.97f}[i]);
        }
        var reveal=Object.FindFirstObjectByType<MistReveal>();reveal.cloudShader=Shader.Find("Mistbell/RevealMistDepth");EditorUtility.SetDirty(reveal);
        var lit=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_WinterRock.mat");
        var rock=Mat("M_FarMistRock",lit);var shade=Mat("M_FarMistRockShade",lit);var snow=Mat("M_FarMistRockSnow",lit);
        foreach(var m in new[]{rock,shade,snow}){m.EnableKeyword("_EMISSION");m.SetFloat("_Smoothness",.05f);m.SetFloat("_Metallic",0);EditorUtility.SetDirty(m);}
        rock.SetColor("_EmissionColor",new Color(.23f,.26f,.30f));shade.SetColor("_EmissionColor",new Color(.24f,.27f,.31f));
        snow.SetTexture("_BaseMap",null);snow.SetColor("_BaseColor",Hex("#EDF3F8"));snow.SetColor("_EmissionColor",Hex("#4B5055"));
        float total=0,covered=0;
        foreach(var r in rocks)
        {
            var f=r.GetComponent<MeshFilter>();string path="Assets/Settings/FarMist_"+r.transform.parent.name+"_"+r.name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=Object.Instantiate(f.sharedMesh);AssetDatabase.CreateAsset(mesh,path);}f.sharedMesh=mesh;
            var v=mesh.vertices;var tri=mesh.triangles;
            var faces=new List<(int start,float area,float score,Vector3 n)>();float area=0;
            for(int k=0;k<tri.Length;k+=3){var n=Vector3.Cross(v[tri[k+1]]-v[tri[k]],v[tri[k+2]]-v[tri[k]]);float ar=n.magnitude*.5f;var wn=r.transform.TransformDirection(n.normalized);faces.Add((k,ar,wn.y+.30f*Vector3.Dot(wn,new Vector3(-.6f,0,-.8f)),wn));area+=ar;}
            var caps=new HashSet<int>();float cap=0;
            foreach(var face in faces.OrderByDescending(q=>q.score)){if(Mathf.Abs((cap+face.area)/area-.425f)>Mathf.Abs(cap/area-.425f))break;caps.Add(face.start);cap+=face.area;}
            var groups=new[]{new List<int>(),new List<int>(),new List<int>()};
            foreach(var face in faces){int slot=caps.Contains(face.start)?1:(Vector3.Dot(face.n,new Vector3(-.6f,.34f,-.72f))>0?0:2);groups[slot].AddRange(new[]{tri[face.start],tri[face.start+1],tri[face.start+2]});}
            mesh.subMeshCount=3;for(int j=0;j<3;j++)mesh.SetTriangles(groups[j],j);EditorUtility.SetDirty(mesh);
            r.sharedMaterials=new[]{rock,snow,shade};EditorUtility.SetDirty(r);total+=area;covered+=cap;
            report.snow_min_pct=Mathf.Min(report.snow_min_pct,100*cap/area);report.snow_max_pct=Mathf.Max(report.snow_max_pct,100*cap/area);
        }
        report.snow_cap_pct=100*covered/total;
        var post=Mat("M_FarMistFencePost",all.First(r=>r.name.StartsWith("FencePost_")).sharedMaterial);
        post.SetTexture("_BaseMap",null);post.SetColor("_BaseColor",Lab(report.post_before_L*.90f,-2,-10));post.SetColor("_EmissionColor",Color.black);EditorUtility.SetDirty(post);
        foreach(var r in all.Where(r=>r.name.StartsWith("FencePost_")))r.sharedMaterial=post;
        report.post_after_L=L(post.GetColor("_BaseColor"));
        var audit=Object.FindFirstObjectByType<FarMistAudit>();if(audit==null)audit=new GameObject("Far Mist Audit").AddComponent<FarMistAudit>();
        audit.ridges=new FarMistAudit.Ridge[4];
        for(int i=0;i<4;i++)
        {
            var r=all.First(q=>q.sharedMaterial!=null&&q.sharedMaterial.name=="M_MtnLayer_"+(i+1));var source=(Texture2D)r.sharedMaterial.GetTexture("_MainTex");var tex=new Texture2D(2,2);tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source)));var points=new List<Vector3>();
            for(int x=0;x<tex.width;x+=8){int y=tex.height-1;while(y>0&&tex.GetPixel(x,y).a<.5f)y--;points.Add(r.transform.TransformPoint(new Vector3((x+.5f)/tex.width-.5f,(y+.5f)/tex.height-.5f,0)));}
            audit.ridges[i]=new FarMistAudit.Ridge {points=points.ToArray()};
            Object.DestroyImmediate(tex);
        }
        report.protected_after=Protected();report.geometry_after=Geometry(rocks);
        File.WriteAllText("../artifacts/far_mist_rocks/assets.json",JsonUtility.ToJson(report,true));
        if(report.protected_before!=report.protected_after||report.geometry_before!=report.geometry_after)throw new InvalidOperationException("Protected objects/rock geometry changed");
        if(report.snow_min_pct<35||report.snow_max_pct>50)throw new InvalidOperationException("Snow cap area outside 35–50%");
        Debug.Log(FormattableString.Invariant($"[SNOW_ROCK] snow_cap_pct={report.snow_cap_pct:F6} per_rock_min={report.snow_min_pct:F6} per_rock_max={report.snow_max_pct:F6} count={rocks.Length} count_unchanged=true geometry_unchanged=true metric=triangle_surface_area"));
        Debug.Log(FormattableString.Invariant($"[FENCE_ASSET] before_L={report.post_before_L:F6} after_L={report.post_after_L:F6} L_ratio={report.post_after_L/report.post_before_L:F6}"));
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(reveal.gameObject.scene);EditorSceneManager.SaveScene(reveal.gameObject.scene);PerformanceBuild.Run();
    }
}
