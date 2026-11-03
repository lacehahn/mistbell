using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

// Editor-only UV reprojection. Geometry, normals, material slots and colliders are preserved.
public static class Style11Atlas
{
    const string Root="Assets/Art/MaterialForge/";
    [Serializable] public class Chart { public int id, atlas, x,y,w,h,side; public string family,mesh; public float width,height,area,minx,miny,plane; public bool wall; }
    [Serializable] public class Layout { public Chart[] charts; }
    class Job { public MeshFilter filter; public Mesh source; public Mesh result; public Action<Mesh> assign; public List<int[]> slots=new List<int[]>(); public List<Vector3> v=new List<Vector3>(),n=new List<Vector3>(); public List<Vector2> uv=new List<Vector2>(); public List<(int index,Chart chart,Vector2 p)> mapped=new List<(int,Chart,Vector2)>(); }
    static string Family(Material m) {
        if(m==null || m.GetTexture("_BaseMap")==null) return null;
        string n=m.GetTexture("_BaseMap").name;
        return new[]{"Stone","Stone_Sub","Roof_Slate","Stone_Interior"}.FirstOrDefault(s=>n==s+"_Albedo");
    }
    public static void Apply(ChurchInterior indoor) {
        var charts=new List<Chart>(); var jobs=new List<Job>(); var mats=new Dictionary<Material,string>();
        void Add(MeshFilter f,Mesh source,Action<Mesh> assign) {
            var renderer=f.GetComponent<Renderer>(); if(renderer==null || source==null) return;
            var materials=renderer.sharedMaterials;
            if(!materials.Any(m=>Family(m)!=null))return;
            var j=new Job{filter=f,source=source,assign=assign}; var vs=source.vertices;var ns=source.normals;var olduv=source.uv;
            for(int slot=0;slot<source.subMeshCount;slot++) {
                string family=slot<materials.Length?Family(materials[slot]):null;
                if(family!=null)mats[materials[slot]]=family;
                var ts=source.GetTriangles(slot);var output=new List<int>();
                // Connected structural pieces are projected along their principal face direction.
                // Bevels stay with their parent face instead of becoming thousands of charts.
                var groups=new Dictionary<string,List<int>>();
                // Weld positions for topology only; imported FBX splits vertex indices at normals/UV seams.
                var parent=new int[ts.Length/3];for(int z=0;z<parent.Length;z++)parent[z]=z;
                int Find(int z){while(parent[z]!=z){parent[z]=parent[parent[z]];z=parent[z];}return z;}
                var owners=new Dictionary<Vector3Int,int>();
                for(int t=0;t<ts.Length;t+=3)for(int k=0;k<3;k++) {
                    var v=vs[ts[t+k]];var key=new Vector3Int(Mathf.RoundToInt(v.x*10000),Mathf.RoundToInt(v.y*10000),Mathf.RoundToInt(v.z*10000));
                    if(owners.TryGetValue(key,out int other))parent[Find(t/3)]=Find(other);else owners[key]=t/3;
                }
                for(int t=0;t<ts.Length;t+=3) {
                    Vector3 a=f.transform.TransformPoint(vs[ts[t]]),b=f.transform.TransformPoint(vs[ts[t+1]]),c=f.transform.TransformPoint(vs[ts[t+2]]);
                    Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
                    var an=new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z));
                    int side=an.y>Mathf.Max(an.x,an.z)?(normal.y>0?4:5):an.x>an.z?(normal.x>0?0:1):(normal.z>0?2:3);
                    string key=family==null?"unchanged":$"{Find(t/3)}_{side}";
                    if(!groups.ContainsKey(key))groups[key]=new List<int>();groups[key].Add(t);
                }
                foreach(var group in groups.Values) {
                    var first=group[0];Vector3 a=f.transform.TransformPoint(vs[ts[first]]),b=f.transform.TransformPoint(vs[ts[first+1]]),c=f.transform.TransformPoint(vs[ts[first+2]]);
                    Vector3 normal=Vector3.zero;
                    foreach(int t in group)normal+=Vector3.Cross(f.transform.TransformVector(vs[ts[t+1]]-vs[ts[t]]),f.transform.TransformVector(vs[ts[t+2]]-vs[ts[t]]));
                    normal.Normalize();
                    var direction=new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z));
                    Vector3 principal=direction.y>Mathf.Max(direction.x,direction.z)?Vector3.up*Mathf.Sign(normal.y):direction.x>direction.z?Vector3.right*Mathf.Sign(normal.x):Vector3.forward*Mathf.Sign(normal.z);
                    Vector3 up=Mathf.Abs(principal.y)>.5f?Vector3.forward:Vector3.up;
                    Vector3 right=Vector3.Cross(up,principal).normalized;
                    Vector2 Project(int idx) {var p=f.transform.TransformPoint(vs[idx]);return new Vector2(Vector3.Dot(p,right),Vector3.Dot(p,up));}
                    var points=group.SelectMany(t=>new[]{Project(ts[t]),Project(ts[t+1]),Project(ts[t+2])}).ToArray();
                    Vector2 lo=new Vector2(points.Min(p=>p.x),points.Min(p=>p.y)),hi=new Vector2(points.Max(p=>p.x),points.Max(p=>p.y));
                    float area=group.Sum(t=>Vector3.Cross(f.transform.TransformVector(vs[ts[t+1]]-vs[ts[t]]),f.transform.TransformVector(vs[ts[t+2]]-vs[ts[t]])).magnitude*.5f);
                    Chart chart=null;
                    var absn=new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z));
                    int face=absn.y>Mathf.Max(absn.x,absn.z)?(normal.y>0?4:5):absn.x>absn.z?(normal.x>0?0:1):(normal.z>0?2:3);
                    if(family!=null) {chart=new Chart{id=charts.Count,family=family,atlas=family=="Stone_Sub"?1:family=="Stone_Interior"?2:0,mesh=f.name,side=face,plane=Vector3.Dot((a+b+c)/3,principal),minx=lo.x,miny=lo.y,width=Mathf.Max(.001f,hi.x-lo.x),height=Mathf.Max(.001f,hi.y-lo.y),area=area,wall=Mathf.Abs(normal.y)<.3f && area>2}; if(source!=f.sharedMesh) {
                        var candidate=charts.Where(q=>q.mesh==f.name && q.family==family && q.side==face && q.minx<=lo.x+.001f && q.miny<=lo.y+.001f && q.minx+q.width>=hi.x-.001f && q.miny+q.height>=hi.y-.001f).OrderBy(q=>Mathf.Abs(q.minx-lo.x)+Mathf.Abs(q.miny-lo.y)+Mathf.Abs(q.width-chart.width)+Mathf.Abs(q.height-chart.height)).FirstOrDefault();
                        if(candidate!=null)chart=candidate;else charts.Add(chart);
                    }else charts.Add(chart);}
                    var vertexMap=new Dictionary<int,int>();
                    foreach(int t in group)for(int k=0;k<3;k++) {
                        int idx=ts[t+k];if(vertexMap.TryGetValue(idx,out int reused)){output.Add(reused);continue;}
                        int dest=j.v.Count;vertexMap[idx]=dest;output.Add(dest);j.v.Add(vs[idx]);j.n.Add(ns.Length>idx?ns[idx]:Vector3.up);j.uv.Add(olduv.Length>idx?olduv[idx]:Vector2.zero);
                        if(chart!=null) {Vector2 p=Project(idx)-new Vector2(chart.minx,chart.miny);p=new Vector2(p.x/chart.width,p.y/chart.height);j.mapped.Add((dest,chart,p));}
                    }
                }
                j.slots.Add(output.ToArray());
            }
            jobs.Add(j);
        }
        var filters=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).OrderBy(f=>f.name).ToArray();
        foreach(var f in filters) { var capture=f;Add(f,f.sharedMesh,m=>capture.sharedMesh=m); }
        // The runtime swaps these existing clipped meshes on entering the church.
        if(indoor.drumCutFilters!=null)for(int i=0;i<indoor.drumCutFilters.Length;i++) {
            int index=i;var f=indoor.drumCutFilters[i];
            Add(f,indoor.drumIndoorMeshes[i],m=>indoor.drumIndoorMeshes[index]=m);
        }
        // Adjacent terrace panels and coplanar architectural islands form one structural
        // facade painting, with a shared world-space origin (not one tile per panel).
        var replacement=new Dictionary<Chart,Chart>();
        foreach(var cluster in charts.Where(c=>c.side<4 && c.width>1.5f && c.height>1.2f && c.area>2).GroupBy(c=>$"{c.family}_{c.side}_{Mathf.RoundToInt(c.plane*2)}")) {
            var members=cluster.ToArray();if(members.Length<2)continue;
            var first=members[0];float minx=members.Min(c=>c.minx),miny=members.Min(c=>c.miny),maxx=members.Max(c=>c.minx+c.width),maxy=members.Max(c=>c.miny+c.height);
            var merged=new Chart{family=first.family,mesh=string.Join("+",members.Select(c=>c.mesh).Distinct()),side=first.side,plane=first.plane,minx=minx,miny=miny,width=maxx-minx,height=maxy-miny,area=members.Sum(c=>c.area),wall=true};
            foreach(var member in members)replacement[member]=merged;
        }
        foreach(var j in jobs)for(int i=0;i<j.mapped.Count;i++) {
            var item=j.mapped[i];if(!replacement.TryGetValue(item.chart,out var merged))continue;
            var old=item.chart;var p=new Vector2((old.minx+item.p.x*old.width-merged.minx)/merged.width,(old.miny+item.p.y*old.height-merged.miny)/merged.height);
            j.mapped[i]=(item.index,merged,p);
        }
        charts=charts.Where(c=>!replacement.ContainsKey(c)).Concat(replacement.Values.Distinct()).ToList();
        for(int i=0;i<charts.Count;i++){var c=charts[i];c.id=i;c.wall=c.side<4 && c.width>1.5f && c.height>1.2f && c.area>2 && c.family!="Roof_Slate";}
        File.WriteAllText(Root+"style11_layout.json",JsonUtility.ToJson(new Layout{charts=charts.ToArray()},true));
        var process=new System.Diagnostics.Process();process.StartInfo=new System.Diagnostics.ProcessStartInfo("/usr/bin/env","python3 ../tools/forge_style11.py"){WorkingDirectory=Directory.GetCurrentDirectory(),UseShellExecute=false};process.Start();process.WaitForExit();
        if(process.ExitCode!=0)throw new Exception("STYLE11 atlas authoring failed");
        var layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Root+"style11_layout.json"));
        for(int i=0;i<charts.Count;i++){var r=layout.charts[i];charts[i].atlas=r.atlas;charts[i].x=r.x;charts[i].y=r.y;charts[i].w=r.w;charts[i].h=r.h;}
        AssetDatabase.Refresh();
        for(int i=0;i<3;i++) {
            string path=Root+"Style11_Atlas"+i+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture=true;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
        }
        foreach(var entry in mats) {
            int atlas=entry.Value=="Stone_Sub"?1:entry.Value=="Stone_Interior"?2:0;var m=entry.Key;
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Style11_Atlas"+atlas+".png"));m.SetTextureScale("_BaseMap",Vector2.one);m.SetTextureOffset("_BaseMap",Vector2.zero);m.SetFloat("_Style11UV",1);
            m.SetTexture("_Style11Atlas1",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Style11_Atlas1.png"));m.SetTexture("_Style11Atlas2",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Style11_Atlas2.png"));m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Style11_Atlas0.png"));EditorUtility.SetDirty(m);
        }
        // Shell material is swapped at runtime, using the same geometric charts as its exterior.
        foreach(var m in new[]{indoor.indoorStone,indoor.wallFootStone}) {
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Style11_Atlas0.png"));m.SetTextureScale("_BaseMap",Vector2.one);m.SetTextureOffset("_BaseMap",Vector2.zero);m.SetFloat("_Style11UV",1);
            m.SetTexture("_Style11Atlas1",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Style11_Atlas1.png"));m.SetTexture("_Style11Atlas2",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Style11_Atlas2.png"));
            m.SetColor("_BaseColor",new Color(1.02f,1.02f,1.02f,1));EditorUtility.SetDirty(m);
        }
        string directory=Root+"Style11Meshes";Directory.CreateDirectory(directory);
        for(int ji=0;ji<jobs.Count;ji++) {
            var j=jobs[ji];var atlasIndex=Enumerable.Repeat(Vector2.zero,j.v.Count).ToList();foreach(var item in j.mapped) {var r=item.chart;atlasIndex[item.index]=new Vector2(r.atlas,0); j.uv[item.index]=new Vector2((r.x+2+item.p.x*(r.w-4))/4096f,1-(r.y+2+(1-item.p.y)*(r.h-4))/4096f);}
            var mesh=new Mesh{name=j.source.name+"_Style11",indexFormat=IndexFormat.UInt32};mesh.SetVertices(j.v);mesh.SetNormals(j.n);mesh.SetUVs(0,j.uv);mesh.SetUVs(2,atlasIndex);mesh.subMeshCount=j.slots.Count;for(int s=0;s<j.slots.Count;s++)mesh.SetTriangles(j.slots[s],s);mesh.RecalculateBounds();
            string path=directory+"/Mesh_"+ji+".asset";AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(mesh,path);j.assign(mesh);
        }
        if(indoor.drumCutFilters!=null)for(int i=0;i<indoor.drumCutFilters.Length;i++)indoor.drumOutdoorMeshes[i]=indoor.drumCutFilters[i].sharedMesh;
        AssetDatabase.SaveAssets();Debug.Log($"[STYLE11_UV] charts={charts.Count} renderers={jobs.Count} atlases=3 material_slots_preserved=True");
    }
    public static void CalibrateAndVerify() {
        // Reapply canonical exposure after texture import, including incremental rebuilds.
        var church=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_ChurchStone.mat");
        church.SetColor("_BaseColor",Color.white*MaterialForge.ChurchExposure);EditorUtility.SetDirty(church);
        foreach(string name in new[]{"Stone_Sub","Stone_Sub_Steps","Stone_Sub_Coping"}) {
            var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/"+name+".mat");if(m==null)continue;
            m.SetColor("_BaseColor",Color.white*MaterialForge.SubExposure);EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();Verify();
    }
    public static void VerifyAndBuild() { Verify(); PerformanceBuild.Run(); }
    public static void Verify() {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        var layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Root+"style11_layout.json"));
        var cells=new Dictionary<(int,int,int),Chart>();
        foreach(var c in layout.charts)for(int y=c.y/16;y<(c.y+c.h)/16;y++)for(int x=c.x/16;x<(c.x+c.w)/16;x++)cells[(c.atlas,x,y)]=c;
        int triangles=0,meshes=0,slots=0;
        var checks=new List<(Mesh,Material[])>();
        foreach(var f in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None)) {
            var renderer=f.GetComponent<Renderer>();if(renderer==null)continue;checks.Add((f.sharedMesh,renderer.sharedMaterials));
        }
        var indoor=UnityEngine.Object.FindFirstObjectByType<ChurchInterior>();
        for(int i=0;i<indoor.drumIndoorMeshes.Length;i++)checks.Add((indoor.drumIndoorMeshes[i],indoor.drumCutFilters[i].GetComponent<Renderer>().sharedMaterials));
        foreach(var check in checks) {
            var mesh=check.Item1;if(mesh==null)continue;var materials=check.Item2;bool counted=false;
            var uv=mesh.uv;var layers=new List<Vector2>();mesh.GetUVs(2,layers);
            for(int slot=0;slot<Math.Min(mesh.subMeshCount,materials.Length);slot++) {
                var m=materials[slot];if(m==null || !m.HasProperty("_Style11UV") || m.GetFloat("_Style11UV")<.5f)continue;
                slots++;if(!counted){meshes++;counted=true;}
                if(m.GetTextureScale("_BaseMap")!=Vector2.one || m.GetTextureOffset("_BaseMap")!=Vector2.zero || m.GetTexture("_BaseMap").name!="Style11_Atlas0" || m.GetTexture("_Style11Atlas1").name!="Style11_Atlas1" || m.GetTexture("_Style11Atlas2").name!="Style11_Atlas2")throw new Exception("STYLE11 material binding invalid: "+m.name);
                if(layers.Count!=mesh.vertexCount || uv.Length!=mesh.vertexCount)throw new Exception("STYLE11 missing UV channel: "+mesh.name);
                var indices=mesh.GetTriangles(slot);
                for(int t=0;t<indices.Length;t+=3) {
                    triangles++;int ia=indices[t],ib=indices[t+1],ic=indices[t+2];int atlas=Mathf.RoundToInt(layers[ia].x);
                    if(layers[ia].x!=atlas || layers[ib].x!=atlas || layers[ic].x!=atlas)throw new Exception("STYLE11 interpolated atlas index");
                    var center=(uv[ia]+uv[ib]+uv[ic])/3;int xx=Mathf.Clamp((int)(center.x*4096),0,4095),yy=Mathf.Clamp((int)((1-center.y)*4096),0,4095);
                    if(!cells.TryGetValue((atlas,xx/16,yy/16),out var c))throw new Exception("STYLE11 UV outside atlas chart: "+mesh.name);
                    foreach(int vi in new[]{ia,ib,ic}) {
                        float x=uv[vi].x*4096,y=(1-uv[vi].y)*4096;
                        if(x<c.x+1 || x>c.x+c.w-1 || y<c.y+1 || y>c.y+c.h-1)throw new Exception("STYLE11 triangle crosses chart: "+mesh.name+" chart="+c.id);
                    }
                }
            }
        }
        if(triangles==0)throw new Exception("STYLE11 no atlas triangles");
        string layoutSha;using(var hash=System.Security.Cryptography.SHA256.Create())layoutSha=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Root+"style11_layout.json"))).Replace("-", "").ToLowerInvariant();
        File.WriteAllText(Root+"style11_uv_proof.json",$"{{\"meshes\":{meshes},\"material_slots\":{slots},\"triangles\":{triangles},\"uv_ok\":true,\"binding_ok\":true,\"layout_sha\":\"{layoutSha}\"}}");
        Debug.Log($"[STYLE11_UV_VERIFY] meshes={meshes} triangles={triangles} uv_ok=True binding_ok=True");
    }

}
