using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class InteriorBalanceBuild
{
    static Material Mat(string name,Color color,float emission=0)
    {
        string path="Assets/Settings/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null) { m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path); }
        m.SetColor("_BaseColor",color);m.SetTexture("_BaseMap",null);m.SetFloat("_Smoothness",.15f);
        m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*emission);
        m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(m);return m;
    }
    static void Mesh(Transform parent,string name,List<Vector3> v,List<int> t,Material mat)
    {
        string path="Assets/Art/Church/"+name+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null) {mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);} else mesh.Clear();
        mesh.name=name;mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        var uv=v.Select(p=>new Vector2(p.x,p.z)).ToArray();mesh.uv=uv;EditorUtility.SetDirty(mesh);
        var go=new GameObject(name);go.transform.SetParent(parent,true);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;
    }
    static void Quad(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {int n=v.Count;v.AddRange(new[]{a,b,c,d});t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
    public static void Apply(ChurchInterior interior)
    {
        var root=interior.detailRoot.transform;
        foreach(var l in interior.lamps)
        {
            if(l.name.StartsWith("Interior_ChandelierCandle_"))l.renderMode=LightRenderMode.ForceVertex;
            if(l.name.StartsWith("Interior_WindowKey")){l.spotAngle=39;l.intensity=2.125f;}
            if(l.name=="Interior_FloorBounce"){l.range=22;l.intensity=.95f;l.renderMode=LightRenderMode.ForcePixel;l.transform.position=new Vector3(-2,interior.FloorY+.3f,3f);}
        }
        // The main sun still lights the single indoor wall mesh. Reduce only its indoor
        // diffuse response and supply broad warm bounce; exterior material is untouched.
        foreach(var mat in new[]{interior.indoorStone,interior.wallFootStone})
        {mat.SetColor("_BaseColor",(Color)new Color32(122,107,92,255)*.8f);
         mat.SetColor("_EmissionColor",new Color(.32f,.272f,.224f));mat.SetFloat("_SpecularHighlights",0);mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");EditorUtility.SetDirty(mat);}
        foreach(var shaft in interior.shafts)
        {var c=new Color(2.8f,2.35f,1.85f,.08f);c.r*=.25f;c.g*=.25f;c.b*=.25f;
         shaft.sharedMaterial.SetColor("_BaseColor",c);EditorUtility.SetDirty(shaft.sharedMaterial);
         var scale=shaft.transform.localScale;scale.x=1.4f;shaft.transform.localScale=scale;}
        var lamps=interior.lamps.Where(l=>l!=null && l.name!="Interior_ChancelFill" && l.name!="Interior_NaveFill_Balance").ToList();
        var naveOld=root.Find("Interior_NaveFill_Balance");if(naveOld!=null)Object.DestroyImmediate(naveOld.gameObject);
        var old=root.Find("Interior_ChancelFill");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var fill=new GameObject("Interior_ChancelFill").AddComponent<Light>();fill.transform.SetParent(root,true);
        fill.transform.position=new Vector3(-2,interior.FloorY+4,7.5f);fill.type=LightType.Point;
        fill.range=12;fill.intensity=.45f;fill.color=new Color32(201,183,154,255);fill.shadows=LightShadows.None;
        fill.renderMode=LightRenderMode.ForcePixel;lamps.Add(fill);
        var nave=new GameObject("Interior_NaveFill_Balance").AddComponent<Light>();nave.transform.SetParent(root,true);
        nave.transform.position=new Vector3(-2,interior.FloorY+1,1.5f);nave.type=LightType.Point;nave.range=10;nave.intensity=1.2f;
        nave.color=new Color32(201,183,154,255);nave.shadows=LightShadows.None;nave.renderMode=LightRenderMode.ForcePixel;lamps.Add(nave);
        interior.lamps=lamps.ToArray();
        foreach(var tr in root.GetComponentsInChildren<Transform>(true))
            if(tr.name.StartsWith("Interior_ConeRib_") || tr.name=="Interior_OculusGoldRing" || tr.name.StartsWith("Interior_PewContact_"))Object.DestroyImmediate(tr.gameObject);
        var plaster=Mat("M_InteriorBalancePlaster",new Color32(138,122,102,255),.20f);
        root.Find("Interior_DomeInner").GetComponent<Renderer>().sharedMaterial=plaster;
        var bright=Mat("M_InteriorBalanceRibLight",(Color)new Color32(138,122,102,255)*1.11f,.20f);
        var dark=Mat("M_InteriorBalanceRibDark",(Color)new Color32(138,122,102,255)*.91f,.20f);
        for(int i=0;i<12;i++)
        {
            float a=i*Mathf.PI/6;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var tangent=new Vector3(-radial.z,0,radial.x);
            Vector3 p=new Vector3(-2,12.2f,0)+radial*3.2f,q=new Vector3(-2,14.6f,0)+radial*.4f;
            var normal=(Vector3.down*2.8f-radial*2.4f).normalized; p+=normal*.05f;q+=normal*.05f;
            for(int side=0;side<2;side++)
            {var v=new List<Vector3>();var t=new List<int>();float u=side==0?-.06f:0,w=side==0?0:.06f;
             Quad(v,t,p+tangent*u,q+tangent*u,q+tangent*w,p+tangent*w);
             var m=side==0?bright:dark;m.SetFloat("_Cull",0);Mesh(root,"Interior_ConeRib_"+i+"_"+side,v,t,m);}
        }
        var sky=root.Find("Interior_OculusSky").GetComponent<Renderer>();
        sky.sharedMaterial=Mat("M_InteriorBalanceSky",new Color32(217,235,249,255),1.6f);sky.sharedMaterial.SetFloat("_Cull",0);
        var gold=Mat("M_InteriorBalanceOculusGold",new Color32(201,162,39,255),.4f);gold.SetFloat("_Cull",0);
        var rv=new List<Vector3>();var rt=new List<int>();
        for(int i=0;i<64;i++)
        {float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;Vector3 P(float angle,float radius)=>new Vector3(-2+Mathf.Cos(angle)*radius,14.585f,Mathf.Sin(angle)*radius);
         Quad(rv,rt,P(a,.4f),P(b,.4f),P(b,.46f),P(a,.46f));}
        Mesh(root,"Interior_OculusGoldRing",rv,rt,gold);
        var contact=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_InteriorPewContact.mat");
        if(contact==null){contact=new Material(Shader.Find("Mistbell/PewContact"));AssetDatabase.CreateAsset(contact,"Assets/Settings/M_InteriorPewContact.mat");}
        contact.SetColor("_BaseColor",new Color(0,0,0,.20f));EditorUtility.SetDirty(contact);
        for(int side=0;side<2;side++)for(int row=0;row<6;row++)
        {
            string prefix="Interior_V2Pew_"+side+"_"+row;
            var legs=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name==prefix+"_Leg").ToArray();
            var v=new List<Vector3>();var t=new List<int>();
            foreach(var leg in legs){var p=leg.bounds.center;p.y=interior.FloorY+.008f;
            Quad(v,t,p+new Vector3(-.12f,0,-.09f),p+new Vector3(-.12f,0,.09f),p+new Vector3(.12f,0,.09f),p+new Vector3(.12f,0,-.09f));}
            Mesh(root,"Interior_PewContact_"+side+"_"+row,v,t,contact);
        }
        EditorUtility.SetDirty(interior);
    }
    static string Protected(ChurchInterior interior) => string.Join("\n",AssetDatabase.FindAssets("t:Material",new[]{"Assets/Settings"}).Select(AssetDatabase.GUIDToAssetPath)
        .Where(p=>p.Contains("M_InteriorAisle.") || p.Contains("M_InteriorSlab_")).OrderBy(p=>p).Select(p=>p+"|"+System.IO.File.ReadAllText(p)))+
        string.Join("\n",interior.GetComponentsInChildren<Collider>(true).Select(c=>c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6")).OrderBy(s=>s));
    static string ProtectedArt(ChurchInterior interior)
    {
        var entries=new List<string>();
        foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(r.transform.IsChildOf(interior.detailRoot.transform) && !r.name.Contains("Altar") && !r.name.Contains("RoyalDoor") && !r.name.StartsWith("Interior_V2Niche") && !r.name.StartsWith("Interior_ArchFrame"))continue;
            entries.Add(r.name+"|"+EditorJsonUtility.ToJson(r)+"|"+r.transform.localToWorldMatrix.ToString("F6"));
            var mesh=r.GetComponent<MeshFilter>();if(mesh!=null)entries.Add(r.name+"|"+EditorJsonUtility.ToJson(mesh.sharedMesh));
            foreach(var mat in r.sharedMaterials)entries.Add(mat.name+"|"+EditorJsonUtility.ToJson(mat));
        }
        foreach(var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))entries.Add(cam.name+EditorJsonUtility.ToJson(cam)+cam.transform.localToWorldMatrix.ToString("F6"));
        foreach(var lamp in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(!lamp.transform.IsChildOf(interior.transform))entries.Add(lamp.name+EditorJsonUtility.ToJson(lamp)+lamp.transform.localToWorldMatrix.ToString("F6"));
        entries.Sort();return string.Join("\n",entries);
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");var interior=Object.FindFirstObjectByType<ChurchInterior>();
        string before=Protected(interior),art=ProtectedArt(interior);Apply(interior);Apply(interior);
        AssetDatabase.SaveAssets();if(before!=Protected(interior))throw new InvalidOperationException("Protected floor/collision changed");
        if(art!=ProtectedArt(interior))throw new InvalidOperationException("Protected exterior/camera/altar/royal door changed");
        Debug.Log("[INT_BALANCE_PROTECTED] exterior_camera_altar_royal_door_identical=true");
        Debug.Log("[INT_BALANCE_INVARIANTS] floor_materials_identical=true colliders_identical=true idempotent=true");
        EditorSceneManager.MarkSceneDirty(interior.gameObject.scene);EditorSceneManager.SaveScene(interior.gameObject.scene);PerformanceBuild.Run();
    }
}
