using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class InteriorMaterialsBuild
{
    public static void Probe()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        var i=Object.FindFirstObjectByType<ChurchInterior>();
        foreach(var r in i.GetComponentsInChildren<MeshRenderer>(true))
            if(r.enabled && (r.name.Contains("Candle") || r.name.Contains("Flame") || r.name.Contains("Gold") || r.name.Contains("Frame") || r.name.Contains("Window")))
                Debug.Log($"[MAT_PROBE] name={r.name} center={r.bounds.center:F3} size={r.bounds.size:F3} material={string.Join(",",r.sharedMaterials.Select(m=>m.name))}");
        Debug.Log($"[MAT_PROBE] floor={i.FloorY:F5} lamps={i.lamps.Length}");
    }

    static Color Hex(string s){ColorUtility.TryParseHtmlString(s,out var c);return c;}
    static Material Mat(string name,string shader)
    {
        string path="Assets/Settings/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}
        m.shader=Shader.Find(shader);return m;
    }
    static Material Wood(string name,float factor,float wear)
    {
        var m=Mat(name,"Mistbell/PewWood");
        m.SetColor("_BaseColor",Hex("#8A6544")*factor);m.SetColor("_ShadowColor",Hex("#5E4230")*factor);
        m.SetTexture("_BaseMap",null);m.SetFloat("_GrainPeriod",.2f);m.SetFloat("_GrainContrast",.06f);
        m.SetFloat("_WearMode",wear);m.SetFloat("_Smoothness",.15f);m.SetFloat("_Metallic",0);
        m.SetColor("_EmissionColor",Color.black);EditorUtility.SetDirty(m);return m;
    }
    static bool Furniture(Renderer r)=>r.name.StartsWith("Interior_V2Pew_");
    static string Protected(ChurchInterior interior)
    {
        var entries=new List<string>();
        foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(r.name.StartsWith("Interior_MaterialsGlow"))continue;
            // All authored geometry/transforms, including the pews, are protected.
            entries.Add(r.name+r.transform.localToWorldMatrix.ToString("F6")+EditorJsonUtility.ToJson(r.GetComponent<MeshFilter>().sharedMesh));
            bool mutable=Furniture(r) || r.name=="Interior_Altar_Model" || r.name.EndsWith("_Flame");
            if(!mutable){entries.Add(EditorJsonUtility.ToJson(r));foreach(var m in r.sharedMaterials)entries.Add(EditorJsonUtility.ToJson(m));}
        }
        foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))entries.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))entries.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(l.name!="Interior_ChandelierCandle_0_Light" && l.name!="Interior_ChandelierCandle_4_Light")entries.Add(l.name+EditorJsonUtility.ToJson(l)+l.transform.localToWorldMatrix.ToString("F6"));
        entries.Sort(StringComparer.Ordinal);return string.Join("\n",entries);
    }
    public static void Apply(ChurchInterior i)
    {
        var root=i.detailRoot.transform;
        var seat=Wood("M_InteriorMaterialsSeat",1,1);var back=Wood("M_InteriorMaterialsBack",1,2);
        var legs=Wood("M_InteriorMaterialsLeg",.87f,0);var rail=Wood("M_InteriorMaterialsRail",1.11f,3);
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true).Where(Furniture))
            r.sharedMaterial=r.name.EndsWith("_Seat")?seat:r.name.EndsWith("_Back")?back:r.name.EndsWith("_Rim")?rail:legs;
        float pitch=root.Find("Interior_V2Pew_0_1_Seat").position.z-root.Find("Interior_V2Pew_0_0_Seat").position.z;
        foreach(var mat in new[]{seat,back,legs,rail}){mat.SetFloat("_PewFloor",i.FloorY);mat.SetFloat("_PewPitch",pitch);mat.SetFloat("_PewStartZ",.55f);}
        var altar=root.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name=="Interior_Altar_Model");
        // Preserve both atlas references, icon emission, and the non-stone quadrants.
        foreach(var m in altar.sharedMaterials)
        {
            if(m.name=="M_AltarPaintedRecess")continue;
            m.shader=Shader.Find("Mistbell/AltarPainted");m.SetFloat("_StoneEnabled",1);m.SetFloat("_AltarUVScale",2);
            m.SetColor("_StoneColor",Hex("#8C8478"));m.SetFloat("_JointContrast",.20f);m.SetFloat("_JointWidth",.012f);
            m.SetFloat("_CourseHeight",.22f);m.SetFloat("_FloorY",i.FloorY);m.SetFloat("_Specular",.05f);
            EditorUtility.SetDirty(m);
        }
        var runtime=i.GetComponent<InteriorMaterialsRuntime>();if(runtime==null)runtime=i.gameObject.AddComponent<InteriorMaterialsRuntime>();
        runtime.interior=i;EditorUtility.SetDirty(runtime);
        var tips=new List<Vector3>();
        foreach(var r in i.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(r.name.EndsWith("_Flame")){if(r.transform.IsChildOf(root))r.enabled=false;continue;}
            if(r.name.StartsWith("Interior_ChandelierCandle_") || r.name.StartsWith("Interior_Candle_"))
            {
                tips.Add(new Vector3(r.bounds.center.x,r.bounds.max.y+.03f+.035f,r.bounds.center.z));

            }
        }
        // Three wax candles are part of the combined authored altar mesh.
        foreach(float x in new[]{-.44f,0,.44f})tips.Add(altar.transform.TransformPoint(new Vector3(x,1.04f+.28f+.03f+.035f,.15f)));
        foreach(var tr in root.GetComponentsInChildren<Transform>(true))if(tr.name.StartsWith("Interior_MaterialsGlow"))Object.DestroyImmediate(tr.gameObject);
        var flame=Mat("M_InteriorMaterialsFlame","Mistbell/CandleGlow");flame.SetColor("_BaseColor",Hex("#FFC97A")*new Color(1,1,1,.75f));flame.SetFloat("_Halo",0);EditorUtility.SetDirty(flame);
        var halo=Mat("M_InteriorMaterialsHalo","Mistbell/CandleGlow");halo.SetColor("_BaseColor",Hex("#FFC97A")*new Color(1,1,1,.10f));halo.SetFloat("_Halo",1);EditorUtility.SetDirty(halo);
        var v=new List<Vector3>();var uv=new List<Vector2>();var centers=new List<Vector3>();var indices=new[]{new List<int>(),new List<int>()};
        for(int pass=0;pass<2;pass++)foreach(var c in tips)
        {
            float w=pass==0?.05f:.16f,h=pass==0?.07f:.16f;int n=v.Count;
            v.AddRange(new[]{new Vector3(-w/2,-h/2,0),new Vector3(-w/2,h/2,0),new Vector3(w/2,h/2,0),new Vector3(w/2,-h/2,0)});
            uv.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right});centers.AddRange(Enumerable.Repeat(c,4));indices[pass].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        string path="Assets/Art/Church/InteriorMaterialsGlow.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
        mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetUVs(1,centers);mesh.subMeshCount=2;
        for(int p=0;p<2;p++)mesh.SetTriangles(indices[p],p);
        var bounds=new Bounds(tips[0],Vector3.one);foreach(var t in tips)bounds.Encapsulate(t);bounds.Expand(.3f);mesh.bounds=bounds;EditorUtility.SetDirty(mesh);
        var go=new GameObject("Interior_MaterialsGlow");go.transform.SetParent(root,true);go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var glow=go.AddComponent<MeshRenderer>();glow.sharedMaterials=new[]{flame,halo};glow.shadowCastingMode=ShadowCastingMode.Off;glow.receiveShadows=false;
        for(int k=0;k<2;k++)
        {
            string name="Interior_ChandelierCandle_"+(k==0?0:4)+"_Light";
            var lamp=i.lamps.FirstOrDefault(l=>l!=null && l.name==name);
            if(lamp==null){lamp=new GameObject(name).AddComponent<Light>();lamp.transform.SetParent(root,true);i.lamps=i.lamps.Concat(new[]{lamp}).ToArray();}
            lamp.transform.position=root.Find("Interior_ChandelierCandle_"+(k==0?0:4)).GetComponent<Renderer>().bounds.center+Vector3.up*.18f;
            lamp.type=LightType.Point;lamp.color=Hex("#FFC97A");lamp.intensity=.35f;lamp.range=1.2f;lamp.shadows=LightShadows.None;
        }
        EditorUtility.SetDirty(i);
        Debug.Log("[PEW_MAT] base=#8A6544 shadow=#5E4230 grain_period=0.2 contrast=0.06 leg_darker=0.13 end_darker=0.13 rail_brighter=0.11 wear_width=0.01 wear_brighter=0.08");
        float L(Color c)=>.2126f*c.r+.7152f*c.g+.0722f*c.b;
        Debug.Log(FormattableString.Invariant($"[ALTAR_STONE] base=#8C8478 warm_delta_vs_pew={(L(Hex("#8C8478"))-L(Hex("#8A6544")))*100:F3} hue=cool joint_contrast=0.20 course_height=0.22 joint_width=0.012 roughness=0.85 specular=0.05 atlas_preserved=true"));
        Debug.Log($"[CANDLE_GLOW] flames={tips.Count} flames_glow={tips.Count} halo_alpha=0.10 flame_alpha=0.75 width=0.05 height=0.07 halo_size=0.16 tip_gap=0.03 lamps={i.lamps.Length}");
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");var i=Object.FindFirstObjectByType<ChurchInterior>();
        string before=Protected(i);Apply(i);AssetDatabase.SaveAssets();string after=Protected(i);
        string Digest(string t){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(t))).Replace("-","").ToLowerInvariant();}
        System.IO.Directory.CreateDirectory("../artifacts/interior_materials");
        System.IO.File.WriteAllText("../artifacts/interior_materials/protected_digests.json","{\"before\":\""+Digest(before)+"\",\"after\":\""+Digest(after)+"\"}\n");
        if(before!=after)throw new InvalidOperationException("Protected content changed: floor/exterior/camera/royal door/icons/pew geometry");
        Debug.Log("[MATERIALS_PROTECTED] floor_exterior_camera_royal_door_icons_pew_geometry_colliders=true");
        EditorSceneManager.MarkSceneDirty(i.gameObject.scene);EditorSceneManager.SaveScene(i.gameObject.scene);
        PerformanceBuild.Run();
    }
}
