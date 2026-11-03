using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Isolated asset update: no exterior scene generation or lighting changes.
public static class AltarBuild
{
    public static void Run()
    {
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        var interior=Object.FindFirstObjectByType<ChurchInterior>();
        ChurchInteriorBuilder.InstallAltar(interior.transform,interior.detailRoot.transform,interior.FloorY);
        EditorUtility.SetDirty(interior);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(interior.gameObject.scene);
        EditorSceneManager.SaveScene(interior.gameObject.scene);
        Verify();
        PerformanceBuild.Run();
    }
    public static void Verify()
    {
        var interior=Object.FindFirstObjectByType<ChurchInterior>();
        var mf=System.Array.Find(interior.detailRoot.GetComponentsInChildren<MeshFilter>(true),f=>f.name=="Interior_Altar_Model");
        var mesh=mf.sharedMesh;var vertices=mesh.vertices;var uv=mesh.uv;var indices=mesh.triangles;
        double area=0,uvArea=0;
        for(int i=0;i<indices.Length;i+=3)
        {
            int a=indices[i],b=indices[i+1],c=indices[i+2];
            var va=mf.transform.TransformPoint(vertices[a]);var vb=mf.transform.TransformPoint(vertices[b]);var vc=mf.transform.TransformPoint(vertices[c]);
            area+=Vector3.Cross(vb-va,vc-va).magnitude*.5;
            var x=uv[b]-uv[a];var y=uv[c]-uv[a];uvArea+=System.Math.Abs(x.x*y.y-x.y*y.x)*.5;
        }
        double texel=1024*System.Math.Sqrt(uvArea/area);
        // Bounds below mensa include linen; candles/cross lie strictly above y=1.05.
        var bounds=new Bounds();bool first=true;
        foreach(var vertex in vertices)
        {
            var p=mf.transform.TransformPoint(vertex)-new Vector3(-2,interior.FloorY,8.72f);
            if(p.y>1.0501f)continue;
            if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
        }
        var r=mf.GetComponent<MeshRenderer>();
        var query=new GameObject("Altar verification collider");query.transform.SetPositionAndRotation(mf.transform.position,mf.transform.rotation);
        query.transform.localScale=mf.transform.lossyScale;var collider=query.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
        Physics.SyncTransforms();
        // Probe above the added icon boards to measure the original 5cm recess itself.
        bool recessed=true;float wall=0;var depths=new float[3];
        if(!collider.Raycast(new Ray(new Vector3(-1.7f,interior.FloorY+.695f,7),Vector3.forward),out var front,3))recessed=false;
        else wall=front.point.z;
        for(int i=0;i<3;i++)
        {
            bool hit=collider.Raycast(new Ray(new Vector3(-2+(i-1)*.66f,interior.FloorY+.695f,7),Vector3.forward),out var back,3);
            depths[i]=back.point.z-wall;recessed &= hit && Mathf.Abs(depths[i]-.05f)<.003f;
        }
        for(int i=0;i<2;i++)
        {
            if(!collider.Raycast(new Ray(new Vector3(-2+(i==0?-.78f:-.73f),interior.FloorY+.85f,7),Vector3.forward),out var clothHit,3))continue;
            int t=clothHit.triangleIndex*3;var bary=clothHit.barycentricCoordinate;
            var normal=mesh.normals[indices[t]]*bary.x+mesh.normals[indices[t+1]]*bary.y+mesh.normals[indices[t+2]]*bary.z;
            Debug.Log($"[ALTAR_CLOTH_NORMAL] peak={i==0} normal={mf.transform.TransformDirection(normal)} uv={clothHit.textureCoord} point={clothHit.point}");
        }
        Object.DestroyImmediate(query);
        var size=bounds.size;
        bool pass=indices.Length/3<=9000 && r.sharedMaterials.Length==2 && texel>=110 && texel<=150 &&
            Mathf.Abs(size.x-2.2f)<=.1f && Mathf.Abs(size.y-1.05f)<=.08f && Mathf.Abs(size.z-.95f)<=.1f && recessed;
        Debug.Log(System.FormattableString.Invariant($"[ALTAR_MODEL] tris={indices.Length/3} materials={r.sharedMaterials.Length} bbox={size.x:F4}x{size.y:F4}x{size.z:F4} texel_px_per_m={texel:F3} niche_depths={depths[0]:F4}/{depths[1]:F4}/{depths[2]:F4} front_faces_congregation={recessed} pass={pass}"));
        var eye=new Vector3(-2,interior.FloorY+1.75f,-4.78f);
        var tip=new Vector3(-2,interior.FloorY+2.132f,9.01f);
        string occluder="none";
        foreach(var panel in interior.detailRoot.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!panel.name.StartsWith("Interior_V2NicheBack_") && !panel.name.StartsWith("Interior_ArchFrame_"))continue;
            var probe=new GameObject("Altar cross sightline probe");
            probe.transform.SetPositionAndRotation(panel.transform.position,panel.transform.rotation);
            probe.transform.localScale=panel.transform.lossyScale;
            var pc=probe.AddComponent<MeshCollider>();pc.sharedMesh=panel.sharedMesh;
            if(pc.Raycast(new Ray(eye,(tip-eye).normalized),out var obstruction,Vector3.Distance(eye,tip)))occluder=panel.name;
            Object.DestroyImmediate(probe);
        }
        Debug.Log($"[ALTAR_CROSS] tip={tip} original_camera={eye} occluder={occluder} visible={occluder=="none"} altar_shift_z=0.12 icon_wall_lower_edge_raise=0.05");
        if(!pass)throw new System.InvalidOperationException("Imported altar acceptance failed");
    }
}
