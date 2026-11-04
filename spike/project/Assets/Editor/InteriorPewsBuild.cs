using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Local furnishing update: never regenerate the scene or its floor.
public static class InteriorPewsBuild
{
    static Material Material(string name, Color color, Color emission, Texture texture=null)
    {
        string path="Assets/Settings/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetTexture("_BaseMap",texture);m.SetFloat("_Smoothness",.12f);
        m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission);
        m.SetTexture("_EmissionMap",texture);EditorUtility.SetDirty(m);return m;
    }
    static GameObject Box(Transform parent,string name,Vector3 position,Vector3 scale,Material mat)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;
        Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.SetParent(parent,true);
        go.transform.position=position;go.transform.localScale=scale;var r=go.GetComponent<Renderer>();
        r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;return go;
    }
    static void Mesh(Transform parent,string name,List<Vector3> v,List<int> t,Material mat,Vector2[] uv=null)
    {
        string path="Assets/Art/Church/"+name+".asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(m==null){m=new Mesh();AssetDatabase.CreateAsset(m,path);}else m.Clear();
        m.name=name;m.SetVertices(v);m.SetTriangles(t,0);m.uv=uv??v.Select(p=>new Vector2(p.x,p.z)).ToArray();
        m.RecalculateNormals();m.RecalculateBounds();EditorUtility.SetDirty(m);
        var go=new GameObject(name);go.transform.SetParent(parent,true);go.AddComponent<MeshFilter>().sharedMesh=m;
        var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;
    }
    static void Quad(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {int n=v.Count;v.AddRange(new[]{a,b,c,d});t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
    static void Rod(Transform parent,string name,Vector3 a,Vector3 b,float diameter,Material mat)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());
     go.transform.SetParent(parent,true);go.transform.position=(a+b)/2;go.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);
     go.transform.localScale=new Vector3(diameter,(b-a).magnitude/2,diameter);go.GetComponent<Renderer>().sharedMaterial=mat;go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;}
    public static void Apply(ChurchInterior interior)
    {
        var root=interior.detailRoot.transform;
        foreach(var tr in root.GetComponentsInChildren<Transform>(true))
            if(tr.name.StartsWith("Interior_V2Pew_") || tr.name.StartsWith("Interior_PewContact_") || tr.name.StartsWith("Interior_PewsV2_") || tr.name.StartsWith("Interior_Chain_") || tr.name.StartsWith("Interior_Suspension_") || tr.name.StartsWith("Interior_SideIcon_"))Object.DestroyImmediate(tr.gameObject);
        // The old roof-cut table also referenced one obsolete suspension cylinder.
        // Remove its row together with the removed cylinder, keeping parallel tables aligned.
        var keep=Enumerable.Range(0,interior.drumCutFilters.Length).Where(i=>interior.drumCutFilters[i]!=null).ToArray();
        interior.drumCutFilters=keep.Select(i=>interior.drumCutFilters[i]).ToArray();
        interior.drumOutdoorMeshes=keep.Select(i=>interior.drumOutdoorMeshes[i]).ToArray();
        interior.drumIndoorMeshes=keep.Select(i=>interior.drumIndoorMeshes[i]).ToArray();
        var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_InteriorPewWood.mat");
        var legs=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_InteriorPewLeg.mat");
        // Match every colour term, including emission, for a genuine 11% rail increment.
        var rail=Material("M_InteriorPewsV2Rail",wood.GetColor("_BaseColor")*1.11f,wood.GetColor("_EmissionColor")*1.11f,wood.GetTexture("_BaseMap"));
        rail.SetTexture("_EmissionMap",null);
        float fy=interior.FloorY,angle=6*Mathf.Deg2Rad;
        float backZ=.21f+.21f*Mathf.Sin(angle), halfZ=.21f*Mathf.Sin(angle)+.025f*Mathf.Cos(angle);
        float minZ=-.24f,maxZ=Mathf.Max(backZ+halfZ,.21f+.43f*Mathf.Sin(angle)+.0275f*Mathf.Cos(angle)),pitch=maxZ-minZ+.28f;
        var contact=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_InteriorPewContact.mat");
        for(int side=0;side<2;side++)for(int row=0;row<6;row++)
        {
            float x=side==0?-4.15f:.15f,z=.55f+row*pitch;string n="Interior_V2Pew_"+side+"_"+row;
            Box(root,n+"_Seat",new Vector3(x,fy+.455f,z),new Vector3(2.35f,.05f,.42f),wood);
            var back=Box(root,n+"_Back",new Vector3(x,fy+.48f+.21f*Mathf.Cos(angle),z-backZ),new Vector3(2.35f,.42f,.05f),wood);
            back.transform.rotation=Quaternion.Euler(-6,0,0);
            var rim=Box(root,n+"_Rim",new Vector3(x,fy+.48f+.42f*Mathf.Cos(angle),z-(.21f+.42f*Mathf.Sin(angle))),new Vector3(2.35f,.02f,.055f),rail);
            rim.transform.rotation=Quaternion.Euler(-6,0,0);
            var v=new List<Vector3>();var t=new List<int>();
            foreach(float dx in new[]{-1.145f,1.145f})
            {
                Box(root,n+"_End",new Vector3(x+dx,fy+.66f,z),new Vector3(.06f,.36f,.48f),wood);
                foreach(float dz in new[]{-.16f,.16f})
                {
                    Box(root,n+"_Leg",new Vector3(x+dx,fy+.215f,z+dz),new Vector3(.06f,.43f,.06f),legs);
                    var p=new Vector3(x+dx,fy+.008f,z+dz);
                    Quad(v,t,p+new Vector3(-.12f,0,-.09f),p+new Vector3(-.12f,0,.09f),p+new Vector3(.12f,0,.09f),p+new Vector3(.12f,0,-.09f));
                }
            }
            Mesh(root,"Interior_PewContact_"+side+"_"+row,v,t,contact);
        }
        var ring=root.Find("Interior_Chandelier").GetComponent<Renderer>();
        float shift=-2-ring.bounds.center.x;ring.transform.position+=Vector3.right*shift;
        foreach(var tr in root.GetComponentsInChildren<Transform>(true))
            if(tr.name.StartsWith("Interior_ChandelierCandle_"))tr.position+=Vector3.right*shift;
        interior.indoorChandelierPosition+=Vector3.right*shift;
        var center=ring.bounds.center;var iron=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_InteriorIron.mat");
        // Three 0.16m links cannot bridge the existing 5.2m gap: use a ceiling stem
        // and three radial braces, keeping the established ring height/framing.
        float linkBottom=center.y+.18f,linkTop=linkBottom+.42f;
        Rod(root,"Interior_Chain_CeilingStem",new Vector3(-2,linkTop-.015f,center.z),new Vector3(-2,8.6f,center.z),.03f,iron);
        for(int i=0;i<3;i++)
        {
            var v=new List<Vector3>();var t=new List<int>();
            // Outer length .16m; wire diameter .03m; alternate vertical link planes.
            var c=new Vector3(-2,linkBottom+.08f+i*.13f,center.z);
            for(int a=0;a<32;a++)for(int b=0;b<8;b++)
            {
                float u=a*Mathf.PI/16,w=b*Mathf.PI/4;
                var p=new Vector3((.035f+.015f*Mathf.Cos(w))*Mathf.Cos(u),(.065f+.015f*Mathf.Cos(w))*Mathf.Sin(u),.015f*Mathf.Sin(w));
                if(i%2==1)p=new Vector3(p.z,p.y,p.x);v.Add(c+p);
                int k=a*8+b,q=a*8+(b+1)%8,j=((a+1)%32)*8+b,l=((a+1)%32)*8+(b+1)%8;t.AddRange(new[]{k,q,j,q,l,j});
            }
            Mesh(root,"Interior_Chain_Link_"+i,v,t,iron);
            float a0=i*Mathf.PI*2/3;
            Rod(root,"Interior_Chain_Brace_"+i,new Vector3(-2,linkBottom+.01f,center.z),center+new Vector3(Mathf.Cos(a0)*.9f,0,Mathf.Sin(a0)*.9f),.018f,iron);
        }
        var gold=Material("M_InteriorSideIconGold",new Color32(126,100,36,255),new Color(.12f,.08f,.015f));
        for(int i=0;i<2;i++)
        {
            int index=i==0?0:2;var niche=root.Find("Interior_V2NicheBack_"+index).GetComponent<Renderer>();
            var c=niche.bounds.center;c.z=niche.bounds.min.z-.025f;
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Interior/Altar_Icons/"+(i==0?"icon_left":"icon_mid")+".png");
            var mat=Material("M_InteriorSideIcon_"+i,new Color(.5f,.5f,.5f),Color.white*(i==0?.96f:1.23f),texture);
            // Quad faces the nave (-Z), preserving Point filtering of original icons.
            var v=new List<Vector3>();var t=new List<int>();Quad(v,t,c+new Vector3(-.17f,-.26f,0),c+new Vector3(-.17f,.26f,0),c+new Vector3(.17f,.26f,0),c+new Vector3(.17f,-.26f,0));
            Mesh(root,"Interior_SideIcon_"+i,v,t,mat,new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)});
            foreach(float dx in new[]{-.18f,.18f})Box(root,"Interior_SideIcon_"+i+"_Frame",c+new Vector3(dx,0,-.005f),new Vector3(.02f,.56f,.02f),gold);
            foreach(float dy in new[]{-.27f,.27f})Box(root,"Interior_SideIcon_"+i+"_Frame",c+new Vector3(0,dy,-.005f),new Vector3(.34f,.02f,.02f),gold);
        }
        EditorUtility.SetDirty(interior);
        Debug.Log(FormattableString.Invariant($"[PEWS_BUILD] rows=6 width=2.35 gap=0.28 pitch={pitch:F5} legs=4 rail=true"));
        Debug.Log(FormattableString.Invariant($"[CHANDELIER] chain_segments=3 top_y=8.600 ring_y={center.y:F3} axis_x={center.x:F3} link_length=0.16 wire_diameter=0.03 stem=true"));
    }
    static string Protected(ChurchInterior interior, bool facingOnly=false)
    {
        var entries=new List<string>();
        foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            // Only the explicit furniture being replaced is excluded.
            if(facingOnly ? r.name.StartsWith("Interior_V2Pew_") && (r.name.EndsWith("_Back") || r.name.EndsWith("_Rim")) : r.name.StartsWith("Interior_V2Pew_") || r.name.StartsWith("Interior_PewContact_") || r.name.StartsWith("Interior_Chain_") || r.name.StartsWith("Interior_Suspension_") || r.name.StartsWith("Interior_SideIcon_") || r.name=="Interior_Chandelier" || r.name.StartsWith("Interior_ChandelierCandle_"))continue;
            entries.Add(r.name+EditorJsonUtility.ToJson(r)+r.transform.localToWorldMatrix.ToString("F6"));
            var mf=r.GetComponent<MeshFilter>();if(mf!=null)entries.Add(EditorJsonUtility.ToJson(mf.sharedMesh));
            foreach(var m in r.sharedMaterials)entries.Add(EditorJsonUtility.ToJson(m));
        }
        foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))entries.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))entries.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(facingOnly || !l.name.Contains("Chandelier"))entries.Add(l.name+EditorJsonUtility.ToJson(l)+l.transform.localToWorldMatrix.ToString("F6"));
        entries.Sort(StringComparer.Ordinal);return string.Join("\n",entries);
    }
    // Update only existing backboards and rims; leave every other scene object intact.
    public static void RunFacingFix()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        var interior=Object.FindFirstObjectByType<ChurchInterior>();
        var root=interior.detailRoot.transform;string before=Protected(interior,true);
        float angle=6*Mathf.Deg2Rad;
        for(int side=0;side<2;side++)for(int row=0;row<6;row++)
        {
            string prefix="Interior_V2Pew_"+side+"_"+row;
            float z=root.Find(prefix+"_Seat").position.z;
            var back=root.Find(prefix+"_Back");var rim=root.Find(prefix+"_Rim");
            var p=back.position;p.z=z-(.21f+.21f*Mathf.Sin(angle));back.position=p;
            p=rim.position;p.z=z-(.21f+.42f*Mathf.Sin(angle));rim.position=p;
            back.rotation=rim.rotation=Quaternion.Euler(-6,0,0);
        }
        if(before!=Protected(interior,true))throw new InvalidOperationException("Facing fix changed protected scene content");
        InteriorPewsAudit.Facing(interior);
        Debug.Log("[PEW_FACING_PROTECTED] all_except_back_rim_identical=true");
        EditorSceneManager.MarkSceneDirty(interior.gameObject.scene);EditorSceneManager.SaveScene(interior.gameObject.scene);
        PerformanceBuild.Run();
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");var interior=Object.FindFirstObjectByType<ChurchInterior>();
        string before=Protected(interior);Apply(interior);Apply(interior);AssetDatabase.SaveAssets();
        if(before!=Protected(interior))throw new InvalidOperationException("Protected scene content changed");
        Debug.Log("[PEWS_PROTECTED] floor_exterior_camera_altar_royal_door_balance_colliders_identical=true");
        EditorSceneManager.MarkSceneDirty(interior.gameObject.scene);EditorSceneManager.SaveScene(interior.gameObject.scene);PerformanceBuild.Run();
    }
}
