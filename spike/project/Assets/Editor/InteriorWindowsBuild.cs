using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
public static class InteriorWindowsBuild
{
    public static void Probe()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        var i=Object.FindFirstObjectByType<ChurchInterior>();
        foreach(var r in i.GetComponentsInChildren<MeshRenderer>(true))
            if(r.name.Contains("Window") || r.name.Contains("Banner") || r.name.Contains("Painting") || r.name=="Main_Shell" || r.name.Contains("Wall"))
              Debug.Log($"[WIN_PROBE] {r.name} center={r.bounds.center:F3} size={r.bounds.size:F3} mat={string.Join(",",r.sharedMaterials.Select(m=>m.name))} detail={r.transform.IsChildOf(i.detailRoot.transform)}");
        foreach(var l in i.lamps)Debug.Log($"[WIN_LAMP] {l.name} pos={l.transform.position} intensity={l.intensity} range={l.range}");
        Debug.Log($"[WIN_FLOOR] y={i.FloorY}");
    }

    static Material Mat(string name,Color color,float emission)
    {
        string path="Assets/Settings/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetTexture("_BaseMap",null);m.SetTexture("_EmissionMap",null);
        m.SetFloat("_Smoothness",.20f);m.SetFloat("_Cull",0);m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        m.SetColor("_EmissionColor",color*emission);m.SetFloat("_SpecularHighlights",0);m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        EditorUtility.SetDirty(m);return m;
    }
    static void Quad(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {int k=v.Count;v.AddRange(new[]{a,b,c,d});t.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}
    static void Box(List<Vector3> v,List<int> t,Vector3 c,Vector3 size,Quaternion rotation)
    {
        Vector3 P(float x,float y,float z)=>c+rotation*Vector3.Scale(size,new Vector3(x,y,z))*.5f;
        Quad(v,t,P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1));
        Quad(v,t,P(1,-1,1),P(1,1,1),P(-1,1,1),P(-1,-1,1));
        Quad(v,t,P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1));
        Quad(v,t,P(1,-1,-1),P(1,1,-1),P(1,1,1),P(1,-1,1));
        Quad(v,t,P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1));
        Quad(v,t,P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1),P(1,-1,1));
    }
    static void Mesh(Transform root,string name,List<Vector3> v,List<int> t,Material mat)
    {
        string path="Assets/Art/Church/"+name+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
        mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.uv=v.Select(p=>new Vector2(p.x,p.y)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        var go=new GameObject(name);go.transform.SetParent(root,true);go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;
    }
    public static void Apply(ChurchInterior interior)
    {
        var root=interior.detailRoot.transform;
        foreach(var tr in root.GetComponentsInChildren<Transform>(true))if(tr.name.StartsWith("Interior_Windows"))Object.DestroyImmediate(tr.gameObject);
        var glass=Mat("M_InteriorWindowsNave",new Color32(207,226,245,255),.90f);
        // A neutral transmission map controls exposure without changing the requested HDR tint/intensity.
        string transPath="Assets/Art/Church/InteriorWindowsTransmission.asset";
        var trans=AssetDatabase.LoadAssetAtPath<Texture2D>(transPath);
        if(trans==null){trans=new Texture2D(1,1,TextureFormat.RGBA32,false,true);AssetDatabase.CreateAsset(trans,transPath);}
        trans.SetPixel(0,0,new Color(.95f,.95f,.95f,1));trans.Apply();EditorUtility.SetDirty(trans);
        glass.SetTexture("_EmissionMap",trans);glass.SetColor("_BaseColor",glass.GetColor("_BaseColor")*.035f);
        var wallColor=interior.indoorStone.GetColor("_BaseColor");
        var stone=Mat("M_InteriorWindowsStone",wallColor*.90f,0);
        stone.SetColor("_EmissionColor",interior.indoorStone.GetColor("_EmissionColor")*.90f);
        var v=new List<Vector3>();var t=new List<int>();
        for(int side=0;side<2;side++)
        {
            // The two screen-space black rectangles are legacy banner placeholders, not the side-wall panes.
            // Replace only their indoor appearance with a recessed clerestory module, preserving the outer shell.
            var old=interior.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name=="Interior_Banner_"+(side+1));
            old.enabled=false;
            var trimOld=interior.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name=="Interior_BannerTrim_"+(side+1));trimOld.enabled=false;
            float x=side==0?-5.6f:1.6f,y0=5.27402f,y1=8.27402f,face=3.84f,depth=4.02f;
            var gv=new List<Vector3>();var gt=new List<int>();
            Quad(gv,gt,new Vector3(x-.55f,y0+.05f,depth),new Vector3(x-.55f,y1-.05f,depth),new Vector3(x+.55f,y1-.05f,depth),new Vector3(x+.55f,y0+.05f,depth));
            Mesh(root,"Interior_WindowsNaveGlass_"+side,gv,gt,glass);
            foreach(float dx in new[]{-.575f,.575f})Box(v,t,new Vector3(x+dx,(y0+y1)/2,(face+depth)/2),new Vector3(.05f,y1-y0,.18f),Quaternion.identity);
            foreach(float y in new[]{y0+.025f,y1-.025f})Box(v,t,new Vector3(x,y,(face+depth)/2),new Vector3(1.2f,.05f,.18f),Quaternion.identity);
            for(int bar=0;bar<3;bar++)
            {
                var mv=new List<Vector3>();var mt=new List<int>();
                var c=new Vector3(x,bar==0?(y0+y1)/2:Mathf.Lerp(y0,y1,bar/3f),depth-.035f);
                Box(mv,mt,c,bar==0?new Vector3(.05f,y1-y0-.10f,.05f):new Vector3(1.10f,.05f,.05f),Quaternion.identity);
                Mesh(root,"Interior_WindowsNaveMullion_"+side+"_"+bar,mv,mt,stone);
            }
        }
        Mesh(root,"Interior_WindowsNaveReveal_All",v,t,stone);
        var drum=Mat("M_InteriorWindowsDrum",new Color32(217,230,242,255),1.02f);
        drum.SetColor("_BaseColor",Color.black);
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
            if(r.name.StartsWith("Interior_DrumWindow_") || r.name.StartsWith("Interior_DrumWindowFill_"))r.sharedMaterial=drum;
        v.Clear();t.Clear();
        for(int side=0;side<12;side++)
        {
            float angle=(side+.5f)*Mathf.PI/6;var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var tangent=new Vector3(-radial.z,0,radial.x);
            var c=new Vector3(-2,10.4f,0)+radial*(3.2f*Mathf.Cos(Mathf.PI/12)-.005f);
            var rot=Quaternion.LookRotation(radial);
            foreach(float dx in new[]{-.345f,.345f})Box(v,t,c+tangent*dx+Vector3.down*.17f,new Vector3(.03f,1.08f,.03f),rot);
            Box(v,t,c+Vector3.down*.69f,new Vector3(.72f,.03f,.03f),rot);
            for(int a=0;a<20;a++)
            {float theta=(a+.5f)*Mathf.PI/20;
             var p=c+tangent*(.35f*Mathf.Cos(theta))+Vector3.up*(.35f+.35f*Mathf.Sin(theta));
             var q=Quaternion.LookRotation(radial)*Quaternion.Euler(0,0,theta*Mathf.Rad2Deg);
             Box(v,t,p,new Vector3(.03f,.065f,.03f),q);}
        }
        Mesh(root,"Interior_WindowsDrumFrame_All",v,t,stone);
        var plaster=Mat("M_InteriorWindowsPlaster",new Color32(156,139,116,255),.35f);
        root.Find("Interior_DomeInner").GetComponent<Renderer>().sharedMaterial=plaster;
        var bright=Mat("M_InteriorWindowsRibLight",plaster.GetColor("_BaseColor")*1.11f,.35f);
        var dark=Mat("M_InteriorWindowsRibDark",plaster.GetColor("_BaseColor")*.91f,.35f);
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))if(r.name.StartsWith("Interior_ConeRib_"))r.sharedMaterial=r.name.EndsWith("_0")?bright:dark;
        var gold=Mat("M_InteriorWindowsOculusGold",new Color32(223,193,113,255),1.4f);
        var goldRing=root.Find("Interior_OculusGoldRing");goldRing.GetComponent<Renderer>().sharedMaterial=gold;
        // Seat the complete 6cm annulus just in front of the conical plaster, rather than burying its outer edge.
        var goldMesh=goldRing.GetComponent<MeshFilter>().sharedMesh;var goldVertices=goldMesh.vertices;
        for(int k=0;k<goldVertices.Length;k++)
        {var p=goldVertices[k];float radius=new Vector2(p.x+2,p.z).magnitude;p.y=14.6f-(radius-.4f)*2.4f/2.8f-.025f;goldVertices[k]=p;}
        goldMesh.vertices=goldVertices;goldMesh.RecalculateNormals();goldMesh.RecalculateBounds();EditorUtility.SetDirty(goldMesh);
        foreach(var m in new[]{interior.indoorStone,interior.wallFootStone})
        {m.shader=Shader.Find("Mistbell/InteriorWallBlocks");m.SetTexture("_BaseMap",null);m.SetTexture("_EmissionMap",null);EditorUtility.SetDirty(m);}
        var drummat=Mat("M_InteriorWindowsDrumStone",wallColor,.0f);
        drummat.SetColor("_EmissionColor",interior.indoorStone.GetColor("_EmissionColor"));drummat.shader=Shader.Find("Mistbell/InteriorWallBlocks");drummat.EnableKeyword("_EMISSION");drummat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;EditorUtility.SetDirty(drummat);
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))if(r.name.StartsWith("Interior_DrumWall_"))r.sharedMaterial=drummat;
        // The right-middle red rectangle is the authored wall-painting placeholder.
        var banner=interior.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name=="Interior_WallPainting");
        var red=Mat("M_InteriorWindowsOchrePanel",new Color32(163,90,74,255),.15f);
        var b=banner.bounds;var scale=banner.transform.localScale;
        // Imported mesh local axes are Unity world axes; this panel lies in the YZ plane.
        scale.z*=.9f/b.size.z;scale.y*=1.6f/b.size.y;banner.transform.localScale=scale;
        banner.transform.position+=new Vector3(1.88f,2.77402f,5.2f)-banner.bounds.center;banner.sharedMaterial=red;
        // Preserve both established broad fills. Consolidate two low-power candle lights into the central chandelier light;
        // visible candle meshes remain intact. No counterfeit lamp counting.
        foreach(var lamp in interior.lamps.Where(l=>l.name=="Interior_ChandelierCandle_0_Light" || l.name=="Interior_ChandelierCandle_4_Light").ToArray())Object.DestroyImmediate(lamp);
        interior.lamps=interior.lamps.Where(l=>l!=null).ToArray();
        EditorUtility.SetDirty(interior);
        Debug.Log("[WALL_TONE] block_variants=3 contrast=0.08 hue_shift=0 grout_drop=0.20 grout_width=0.04 block_width=1.10 block_height=0.48 space=albedo_and_emission_scalar");
        Debug.Log($"[RED_PANEL] what=authored_wall_painting_placeholder object=Interior_WallPainting color=#A35A4A width={banner.bounds.size.z:F3} height={banner.bounds.size.y:F3}");
        Debug.Log($"[WINDOWS_BUILD] nave=2 dome=12 mullions=6 reveal_depth=0.18 frame_width=0.03 lamps={interior.lamps.Length} removed_lights=chandelier_candle_0_and_4 broad_fills_preserved=true");
    }
    static bool Mutable(MeshRenderer r)=>r.name.StartsWith("Interior_Windows") || r.name.StartsWith("Interior_DrumWindow") || r.name.StartsWith("Interior_ConeRib_") || r.name.StartsWith("Interior_DrumWall_") || r.name=="Interior_DomeInner" || r.name=="Interior_OculusGoldRing" || r.name.StartsWith("Interior_Banner_") || r.name.StartsWith("Interior_BannerTrim_") || r.name=="Interior_WallPainting";
    static string Protected(ChurchInterior i)
    {
        var lines=new List<string>();
        foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(Mutable(r))continue;
            lines.Add(r.name+EditorJsonUtility.ToJson(r)+r.transform.localToWorldMatrix.ToString("F6"));
            var mf=r.GetComponent<MeshFilter>();if(mf!=null)lines.Add(r.name+EditorJsonUtility.ToJson(mf.sharedMesh));
            foreach(var m in r.sharedMaterials)lines.Add(r.name+EditorJsonUtility.ToJson(m));
        }
        foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))lines.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))lines.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(l.name!="Interior_ChandelierCandle_0_Light" && l.name!="Interior_ChandelierCandle_4_Light")lines.Add(l.name+EditorJsonUtility.ToJson(l)+l.transform.localToWorldMatrix.ToString("F6"));
        lines.Sort(StringComparer.Ordinal);return string.Join("\n",lines);
    }
    static string Changed(ChurchInterior i)=>string.Join("\n",i.GetComponentsInChildren<MeshRenderer>(true).Where(Mutable).OrderBy(r=>r.name).Select(r=>r.name+r.enabled+r.transform.localToWorldMatrix.ToString("F6")+EditorJsonUtility.ToJson(r.GetComponent<MeshFilter>().sharedMesh)+string.Join("|",r.sharedMaterials.Select(m=>EditorJsonUtility.ToJson(m)))))+string.Join(",",i.lamps.Select(l=>l.name));
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");var interior=Object.FindFirstObjectByType<ChurchInterior>();
        string before=Protected(interior);
        Apply(interior);string once=Changed(interior);Apply(interior);AssetDatabase.SaveAssets();
        string after=Protected(interior);
        string Digest(string text){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();}
        System.IO.File.WriteAllText("../artifacts/interior_windows/protected_digests.json","{\"protected_before\":\""+Digest(before)+"\",\"protected_after\":\""+Digest(after)+"\"}\n");
        if(before!=after)throw new InvalidOperationException("Protected content changed");
        if(once!=Changed(interior))throw new InvalidOperationException("Patch not idempotent");
        var rs=interior.detailRoot.GetComponentsInChildren<MeshRenderer>(true);
        var reveal=rs.First(r=>r.name=="Interior_WindowsNaveReveal_All");
        var goldv=rs.First(r=>r.name=="Interior_OculusGoldRing").GetComponent<MeshFilter>().sharedMesh.vertices;
        float[] radii=goldv.Select(p=>new Vector2(p.x+2,p.z).magnitude).ToArray();
        var bars=rs.Where(r=>r.name.StartsWith("Interior_WindowsNaveMullion_")).ToArray();
        float minBar=bars.Min(r=>r.name.EndsWith("_0")?r.bounds.size.x:r.bounds.size.y);
        float maxBar=bars.Max(r=>r.name.EndsWith("_0")?r.bounds.size.x:r.bounds.size.y);
        if(bars.Length!=6 || Mathf.Abs(minBar-.05f)>.001f || Mathf.Abs(maxBar-.05f)>.001f || Mathf.Abs(reveal.bounds.size.z-.18f)>.001f || Mathf.Abs(radii.Max()-radii.Min()-.06f)>.001f)throw new InvalidOperationException("Window geometry dimensions failed");
        Debug.Log(FormattableString.Invariant($"[WINDOWS_GEOMETRY] pane_count={rs.Count(r=>r.name.StartsWith("Interior_WindowsNaveGlass_"))} mullions={bars.Length} mullion_min_width={minBar:F5} mullion_max_width={maxBar:F5} reveal_depth={reveal.bounds.size.z:F5} gold_ring_width={radii.Max()-radii.Min():F5}"));
        Debug.Log("[WINDOWS_PROTECTED] identical=true idempotent=true floor_exterior_camera_altar_royal_door_colliders=true");
        EditorSceneManager.MarkSceneDirty(interior.gameObject.scene);EditorSceneManager.SaveScene(interior.gameObject.scene);PerformanceBuild.Run();
    }
}
