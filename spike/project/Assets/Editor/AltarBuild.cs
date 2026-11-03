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
            var p=mf.transform.TransformPoint(vertex)-new Vector3(-2,interior.FloorY,8.6f);
            if(p.y>1.0501f)continue;
            if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
        }
        var r=mf.GetComponent<MeshRenderer>();
        var query=new GameObject("Altar verification collider");query.transform.SetPositionAndRotation(mf.transform.position,mf.transform.rotation);
        query.transform.localScale=mf.transform.lossyScale;var collider=query.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
        Physics.SyncTransforms();
        bool recessed=true;float wall=0;var depths=new float[3];
        if(!collider.Raycast(new Ray(new Vector3(-1.7f,interior.FloorY+.45f,7),Vector3.forward),out var front,3))recessed=false;
        else wall=front.point.z;
        for(int i=0;i<3;i++)
        {
            bool hit=collider.Raycast(new Ray(new Vector3(-2+(i-1)*.66f,interior.FloorY+.45f,7),Vector3.forward),out var back,3);
            depths[i]=back.point.z-wall;recessed &= hit && Mathf.Abs(depths[i]-.05f)<.003f;
        }
        Object.DestroyImmediate(query);
        var size=bounds.size;
        bool pass=indices.Length/3<=9000 && r.sharedMaterials.Length==2 && texel>=110 && texel<=150 &&
            Mathf.Abs(size.x-2.2f)<=.1f && Mathf.Abs(size.y-1.05f)<=.08f && Mathf.Abs(size.z-.95f)<=.1f && recessed;
        Debug.Log(System.FormattableString.Invariant($"[ALTAR_MODEL] tris={indices.Length/3} materials={r.sharedMaterials.Length} bbox={size.x:F4}x{size.y:F4}x{size.z:F4} texel_px_per_m={texel:F3} niche_depths={depths[0]:F4}/{depths[1]:F4}/{depths[2]:F4} front_faces_congregation={recessed} pass={pass}"));
        if(!pass)throw new System.InvalidOperationException("Imported altar acceptance failed");
    }
}
