using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Patch the authored scene in place. Never call SpikeScene.Run.
public static class FarMistV2Build
{
    const string Output="../artifacts/far_mist_v2/";
    static bool Rock(MeshRenderer r)=>r.name.StartsWith("ClusterRock_")||(r.name.StartsWith("Rock_")&&r.transform.parent.name.StartsWith("TerraceRockpile_"));
    static string Sha(string s){using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s))).Replace("-","");}
    static string Protected()
    {
        var data=new List<string>();
        foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(r is MeshRenderer mr && Rock(mr)||r.name.StartsWith("Paving_")||r.name.StartsWith("PathSnowEdge"))continue;
            data.Add(r.name+EditorJsonUtility.ToJson(r)+r.transform.localToWorldMatrix.ToString("F6"));
            var f=r.GetComponent<MeshFilter>();if(f!=null)data.Add(EditorJsonUtility.ToJson(f.sharedMesh));
            foreach(var m in r.sharedMaterials)if(m!=null)data.Add(EditorJsonUtility.ToJson(m));
        }
        foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))data.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))data.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var c in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))data.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var c in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include,FindObjectsSortMode.None))data.Add(EditorJsonUtility.ToJson(c));
        foreach(var c in Object.FindObjectsByType<ShotRig>(FindObjectsInactive.Include,FindObjectsSortMode.None))data.Add(EditorJsonUtility.ToJson(c));
        data.Sort(StringComparer.Ordinal);return Sha(string.Join("\n",data));
    }
    [Serializable] class Stone {public string name;public Vector3 position; public float aspect,buried,snow_y;}
    [Serializable] class Report {public string protected_before,protected_after,layout_before,layout_after;public int count,edge_segments;public float aspect_max,buried_min=1,buried_max,edge_width=.1f,edge_albedo_gain;public Stone[] stones;}
    static string Layout(MeshRenderer[] rocks)=>Sha(string.Join("\n",rocks.OrderBy(r=>r.transform.parent.name+"/"+r.name).Select(r=>r.transform.parent.name+"/"+r.name+r.transform.localToWorldMatrix.ToString("F6"))));
    static Material Mat(string n,Material source){string p="Assets/Settings/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(m==null){m=new Material(source);AssetDatabase.CreateAsset(m,p);}return m;}
    static float SnowY(Vector3 p)
    {
        // Highest authored walkable snow collider; props/roof/blockers excluded.
        float y=float.NegativeInfinity;
        foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if(c.name!="Snow Terrain"&&!c.name.StartsWith("TerraceFill_")&&c!=Object.FindFirstObjectByType<PlayerController>().ground)continue;
            if(c.Raycast(new Ray(new Vector3(p.x,400,p.z),Vector3.down),out var hit,1000))y=Mathf.Max(y,hit.point.y);
        }
        if(float.IsNegativeInfinity(y))throw new Exception("Snow surface missing at "+p);return y;
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        Physics.SyncTransforms();
        var all=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var rocks=all.Where(Rock).OrderBy(r=>r.transform.parent.name+"/"+r.name).ToArray();
        var report=new Report {protected_before=Protected(),layout_before=Layout(rocks),count=rocks.Length};
        if(rocks.Length!=88)throw new Exception("Expected 88 rocks");
        var stones=new List<Stone>();
        foreach(var r in rocks)
        {
            var f=r.GetComponent<MeshFilter>();string p="Assets/Settings/FarMistV2_"+r.transform.parent.name+"_"+r.name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(p);
            if(mesh==null){mesh=Object.Instantiate(f.sharedMesh);AssetDatabase.CreateAsset(mesh,p);} f.sharedMesh=mesh;
            var vertices=mesh.vertices;var world=vertices.Select(v=>r.transform.TransformPoint(v)).ToArray();
            float minY=world.Min(v=>v.y),maxY=world.Max(v=>v.y),height=maxY-minY;
            float width=Mathf.Max(world.Max(v=>v.x)-world.Min(v=>v.x),world.Max(v=>v.z)-world.Min(v=>v.z));
            float h=Mathf.Min(height,width*.42f),ground=SnowY(r.transform.position),bottom=ground-.4f*h;
            for(int i=0;i<vertices.Length;i++){var v=world[i];v.y=bottom+(v.y-minY)/height*h;vertices[i]=r.transform.InverseTransformPoint(v);}
            mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();EditorUtility.SetDirty(mesh);
            report.aspect_max=Mathf.Max(report.aspect_max,h/width);report.buried_min=Mathf.Min(report.buried_min,(ground-bottom)/h);report.buried_max=Mathf.Max(report.buried_max,(ground-bottom)/h);
            stones.Add(new Stone {name=r.transform.parent.name+"/"+r.name,position=r.transform.position,aspect=h/width,buried=(ground-bottom)/h,snow_y=ground});
        }
        foreach(string name in new[]{"M_FarMistRock","M_FarMistRockShade"})
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/"+name+".mat");
            // Existing diffuse-only shader supports an explicit ambient floor.
            m.SetFloat("_ForgeSurface",2);m.SetColor("_BaseColor",new Color(.68f,.73f,.80f));m.SetColor("_ForgeEmission",new Color(.10f,.12f,.15f));EditorUtility.SetDirty(m);
        }
        var paving=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_Paving.mat");
        paving.SetFloat("_ForgeSurface",2);paving.SetTexture("_BaseMap",null);paving.SetColor("_BaseColor",new Color(.61f,.63f,.66f));paving.SetColor("_ForgeEmission",Color.black);EditorUtility.SetDirty(paving);
        var snow=Mat("M_PathSnowEdge",paving);var roadLinear=paving.GetColor("_BaseColor").linear;
        snow.SetColor("_BaseColor",(roadLinear*1.15f).gamma);
        report.edge_albedo_gain=snow.GetColor("_BaseColor").linear.g/roadLinear.g;EditorUtility.SetDirty(snow);
        // Independent strips on the OUTER side of the two tile rows, 0.10m inward.
        // Combine all strips into a single mesh/renderer to keep draw cost bounded.
        var verts=new List<Vector3>();var tris=new List<int>();
        bool modernPath=all.Any(r=>r.enabled && r.GetComponent<MeshCollider>()!=null && r.sharedMaterial!=null && (r.sharedMaterial.shader.name=="Mistbell/PathTone" || r.sharedMaterial.shader.name=="Mistbell/PathPainted"));
        if(modernPath)Debug.Log("[FAR_MIST_V2] legacy_snow_edges=skipped reason=current_path_surface_preserved");
        foreach(var tile in all.Where(r=>!modernPath && r.name.StartsWith("Paving_")))
        {
            // Tile local X is path lateral axis. Determine outer row from sibling pair.
            if(!int.TryParse(tile.name.Substring(7),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int id)) {
                Debug.Log("[FAR_MIST_V2] legacy_edge_tile=skipped name="+tile.name+" raw=\""+tile.name.Substring(7)+"\" reason=non_numeric_paving_mesh");continue;
            }
            var f=tile.GetComponent<MeshFilter>();
            if(f==null || f.sharedMesh==null) {Debug.LogWarning("[FAR_MIST_V2] legacy_edge_tile=skipped name="+tile.name+" reason=missing_mesh");continue;}
            var b=f.sharedMesh.bounds;
            int side=id%6<3?-1:1;
            float x=side<0?b.min.x:b.max.x;
            float dx=.1f/tile.transform.lossyScale.x;
            float y=b.max.y+.012f/tile.transform.lossyScale.y;
            int k=verts.Count;
            foreach(var v in new[]{new Vector3(x,y,b.min.z),new Vector3(x-side*dx,y,b.min.z),new Vector3(x-side*dx,y,b.max.z),new Vector3(x,y,b.max.z)})verts.Add(tile.transform.TransformPoint(v));
            tris.AddRange(side<0?new[]{k,k+3,k+1,k+1,k+3,k+2}:new[]{k,k+1,k+3,k+1,k+2,k+3});report.edge_segments++;
        }
        GameObject edge=null;
        if(!modernPath) {
        string edgePath="Assets/Settings/PathSnowEdgeV2.asset";var edgeMesh=AssetDatabase.LoadAssetAtPath<Mesh>(edgePath);
        if(edgeMesh==null){edgeMesh=new Mesh();AssetDatabase.CreateAsset(edgeMesh,edgePath);}edgeMesh.Clear();edgeMesh.SetVertices(verts);edgeMesh.SetTriangles(tris,0);edgeMesh.RecalculateBounds();edgeMesh.RecalculateNormals();EditorUtility.SetDirty(edgeMesh);
        edge=GameObject.Find("PathSnowEdgeV2");if(edge==null){edge=new GameObject("PathSnowEdgeV2");edge.AddComponent<MeshFilter>();edge.AddComponent<MeshRenderer>();}
        edge.GetComponent<MeshFilter>().sharedMesh=edgeMesh;edge.GetComponent<MeshRenderer>().sharedMaterial=snow;
        edge.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        report.layout_after=Layout(rocks);report.protected_after=Protected();report.stones=stones.ToArray();
        File.WriteAllText(Output+"assets.json",JsonUtility.ToJson(report,true));
        if(report.layout_before!=report.layout_after||report.protected_before!=report.protected_after||report.aspect_max>.45001f||report.buried_min<.35f||report.buried_max>.45f)throw new Exception("Far mist v2 protected/shape gate failed");
        Debug.Log(FormattableString.Invariant($"[ROCK_SHAPE_ASSET] count={report.count} aspect={report.aspect_max:F6} buried={report.buried_min:F6}..{report.buried_max:F6} layout_unchanged=true protected_unchanged=true"));
        Debug.Log($"[PATH_ASSET] snow_edge=true width_m=0.10 segments={report.edge_segments} albedo_gain=1.15");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(rocks[0].gameObject.scene);EditorSceneManager.SaveScene(rocks[0].gameObject.scene);PerformanceBuild.Run();
    }
}
