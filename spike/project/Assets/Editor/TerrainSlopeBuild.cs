using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object=UnityEngine.Object;

// Patch only the existing terrain mesh, retaining snow detail and every other scene asset.
public static class TerrainSlopeBuild
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        var terrain=GameObject.Find("Terrain").GetComponent<MeshCollider>();
        var mesh=terrain.sharedMesh;
        string Protected() {
            var rows=Object.FindObjectsByType<Component>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(c=>c!=null).Select(c=>EditorJsonUtility.ToJson(c)).ToList();
            foreach(var path in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/Settings/"))) {
                var asset=AssetDatabase.LoadMainAssetAtPath(path);
                if(asset!=null&&asset!=mesh)rows.Add(path+EditorJsonUtility.ToJson(asset));
            }
            rows.Sort(StringComparer.Ordinal);return string.Join("\n",rows);
        }
        string before=Protected();
        var height=typeof(SpikeScene).GetMethod("TerrainHeight",BindingFlags.Static|BindingFlags.NonPublic);
        float New(float x,float z)=>(float)height.Invoke(null,new object[]{x,z});
        // Terrain.asset already includes wind-shaped snow. Apply the formula delta once,
        // using a saved baseline so rerunning is idempotent.
        const string baseline="../artifacts/terrain_slope/terrain_baseline.json.gz";
        Directory.CreateDirectory("../artifacts/terrain_slope");
        if(!File.Exists(baseline)) {
            using(var file=File.Create(baseline))using(var gzip=new GZipStream(file,CompressionMode.Compress))
            using(var writer=new StreamWriter(gzip))writer.Write(EditorJsonUtility.ToJson(mesh));
        }
        using(var file=File.OpenRead(baseline))using(var gzip=new GZipStream(file,CompressionMode.Decompress))
        using(var reader=new StreamReader(gzip))EditorJsonUtility.FromJsonOverwrite(reader.ReadToEnd(),mesh);
        var vertices=mesh.vertices;var oldVertices=(Vector3[])vertices.Clone();
        var oldNormals=mesh.normals;var oldTangents=mesh.tangents;
        int changed=0,protectedChanged=0;
        for(int i=0;i<vertices.Length;i++) {
            var v=vertices[i];if(v.z>=-40)continue;
            float dx=Mathf.Abs(v.x-ClimbRoute.CenterX(v.z));
            float oldBlend=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(25,65,dx)))
                *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-46,-40,v.z)));
            // Recover original and the shared bank/drift from the exact source formula.
            float oldShoulder=1.1f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.5f,15,dx));
            float newValue=New(v.x,v.z);
            float original;
            float crestZ=28+10*Mathf.Sin(v.x/38);
            original=v.z<=crestZ?Mathf.Tan(6*Mathf.Deg2Rad)*v.z:Mathf.Tan(6*Mathf.Deg2Rad)*crestZ-.32f*(v.z-crestZ);
            float mask=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-140,-110,v.z))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-30,-18,v.z)))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(65,90,Mathf.Abs(v.x))));
            float amp=Mathf.Lerp(.1f,.3f,Mathf.PerlinNoise(v.x/48+7,v.z/48+11));
            float drift=amp*(.65f*Mathf.Sin(v.x*.15f+v.z*.12f)+.35f*Mathf.Cos(v.z*.19f-v.x*.08f));
            original+=mask*drift; // hillMask=0 for z<-40
            float route=ClimbRoute.Height(v.z)+ClimbRoute.Depth*Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.5f,4.5f,dx))+oldShoulder+drift*Mathf.Clamp01((dx-4.5f)/10);
            float delta=newValue-Mathf.Lerp(original,route,oldBlend);
            if(dx<=4.5f)delta=0; // exact unchanged route bed
            if(Mathf.Abs(delta)>.000001f){vertices[i].y+=delta;changed++;}
        }
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
        var normals=mesh.normals;var tangents=mesh.tangents;
        for(int i=0;i<vertices.Length;i++) {
            var v=oldVertices[i];
            if(v.z>=-40 || Mathf.Abs(v.x-ClimbRoute.CenterX(v.z))<=4.5f) {
                if(vertices[i]!=v)protectedChanged++;
                normals[i]=oldNormals[i];tangents[i]=oldTangents[i];
            }
        }
        mesh.normals=normals;mesh.tangents=tangents;EditorUtility.SetDirty(mesh);
        terrain.sharedMesh=null;terrain.sharedMesh=mesh;Physics.SyncTransforms();
        float Sample(float z,float offset) {
            float x=ClimbRoute.CenterX(z)+offset;
            if(!terrain.Raycast(new Ray(new Vector3(x,500,z),Vector3.down),out var hit,1000))throw new Exception("Slope ray missed");return hit.point.y;
        }
        bool pass=true;
        foreach(float z in new[]{-150f,-95f})foreach(float side in new[]{-1f,1f}) {
            float rise=Sample(z,side*10)-Sample(z,0);pass&=rise>=.8f&&rise<=1.5f;
            Debug.Log(FormattableString.Invariant($"[SLOPE] at_z={z} side={side} rise_lateral10m={rise:F6}"));
        }
        foreach(float side in new[]{-1f,1f}) {
            float rise=Sample(-40,side*30)-Sample(-205,side*30);pass&=rise>=18;
            Debug.Log(FormattableString.Invariant($"[SLOPE] field_rise={rise:F6} side={side} start_z=-205 end_z=-40 lateral=30"));
        }
        bool same=before==Protected();
        Debug.Log($"[SLOPE] vertices_changed={changed} protected_vertices_changed={protectedChanged} protected_unchanged={same}");
        if(!pass||!same||protectedChanged!=0)throw new Exception("Slope gate failed");
        AssetDatabase.SaveAssets();
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/ArtSpike.unity"},locationPathName="Builds/Mac/Mistbell.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
        Debug.Log($"[SLOPE_BUILD] result={report.summary.result} errors={report.summary.totalErrors}");
        if(report.summary.result!=BuildResult.Succeeded)EditorApplication.Exit(1);
    }
}
