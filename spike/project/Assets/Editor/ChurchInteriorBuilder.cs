using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class ChurchInteriorBuilder
{
    static Material Lit(string name, Color color, Color emission=default)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name=name };
        m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",.12f);
        if(emission.maxColorComponent>0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",emission); if(name=="M_InteriorWood" || name=="M_InteriorStone" || name=="M_InteriorGold") m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive; }
        string path="Assets/Settings/"+name+".mat";
        AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m,path); return m;
    }
    static void ScaleWorld(Transform item, Vector3 anchor, Vector3 scale)
    {
        var pivot=new GameObject(item.name+"_Scale").transform;
        pivot.SetParent(item.root,true); pivot.SetPositionAndRotation(anchor,Quaternion.identity);
        item.SetParent(pivot,true); pivot.localScale=scale;
    }
    public static ChurchInterior Build(Transform church, MeshCollider terrain)
    {
        // Keep the FBX asset intact; scene instance transforms need independent scale pivots.
        PrefabUtility.UnpackPrefabInstance(church.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        var interior=church.gameObject.AddComponent<ChurchInterior>();
        // Low warm material emission supplies the missing unbaked indoor bounce; lamp powers stay fixed.
        // Door leaf and exterior threshold retain their exact previous material values.
        var doorWood=Lit("M_InteriorDoorWood",new Color(.22f,.105f,.045f));
        var thresholdStone=Lit("M_InteriorThreshold",new Color(.36f,.29f,.20f));
        var wood=Lit("M_InteriorWood",new Color(.32f,.21f,.12f),new Color(.32f,.26f,.19f));
        var stone=Lit("M_InteriorStone",new Color(.46f,.39f,.30f),new Color(.32f,.29f,.24f));
        var gold=Lit("M_InteriorGold",new Color(.66f,.43f,.12f),new Color(.32f,.26f,.15f));
        var blue=Lit("M_InteriorBlue",new Color(.045f,.12f,.32f));
        var red=Lit("M_InteriorRed",new Color(.32f,.045f,.025f));
        var glass=Lit("M_InteriorGlass",new Color(.21f,.34f,.48f),new Color(.18f,.26f,.38f));
        glass.SetFloat("_Surface",1); glass.SetFloat("_Blend",0); glass.SetFloat("_ZWrite",0);
        glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        glass.SetFloat("_Cull",0); glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.renderQueue=3000;
        glass.SetColor("_BaseColor",new Color(.21f,.34f,.48f,.18f)); EditorUtility.SetDirty(glass);
        var flame=Lit("M_InteriorFlame",new Color(1,.58f,.16f),new Color(3,1.1f,.12f));
        var lamps=new List<Light>();
        foreach(var f in church.GetComponentsInChildren<MeshFilter>())
        {
            string n=f.name;
            if(n=="Main_Shell")
            {
                interior.shell=f.GetComponent<Renderer>();
                // Runtime indoor instance: exterior/capture material is restored on exit.
                interior.indoorStone=new Material(interior.shell.sharedMaterial) { name="M_IndoorShell" };
                interior.indoorStone.shader=Shader.Find("Universal Render Pipeline/Lit");
                interior.indoorStone.SetColor("_BaseColor",new Color(.65f,.58f,.48f));
                interior.indoorStone.EnableKeyword("_EMISSION");
                interior.indoorStone.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
                interior.indoorStone.SetColor("_EmissionColor",new Color(.32f,.29f,.24f));
                interior.indoorStone.SetTextureScale("_BaseMap",Vector2.one/.9f);
                AssetDatabase.DeleteAsset("Assets/Settings/M_IndoorShell.mat");
                AssetDatabase.CreateAsset(interior.indoorStone,"Assets/Settings/M_IndoorShell.mat");
            }
            if(n=="Door_Leaf")
            {
                interior.doorLeaf=f.transform;
                var c=f.gameObject.AddComponent<BoxCollider>(); c.center=f.sharedMesh.bounds.center; c.size=f.sharedMesh.bounds.size;
                f.gameObject.layer=PlayerController.BlockerLayer; interior.doorCollider=c;
                f.GetComponent<Renderer>().sharedMaterial=doorWood;
            }
            else if(n.StartsWith("Steps_"))
            {
                // The former ornamental five treads end 1m above the new local-zero floor.
                // The playable terrace already supplies nine real steps; replace only this
                // overlapping ornament with the stone threshold, keeping the exterior stair route.
                f.GetComponent<Renderer>().enabled=false;
            }
            else if(n.StartsWith("Window_Glass_"))
            {
                var r=f.GetComponent<Renderer>(); r.sharedMaterial=glass;
                r.shadowCastingMode=ShadowCastingMode.Off; r.receiveShadows=false;
            }
            else if(n.StartsWith("Interior_"))
            {
                var r=f.GetComponent<Renderer>();
                r.sharedMaterial=n.Contains("Pew") || n.Contains("Iconostasis") ? wood :
                    n.Contains("Gold") || n.Contains("Candle") || n.Contains("Chandelier") || n.Contains("Trim") || n.Contains("Frame") ? gold :
                    n.Contains("Banner_1") ? blue : n.Contains("Banner_2") || n.Contains("WallPainting") ? red : stone;
                if(n.StartsWith("Interior_Candle_"))
                {
                    // Measure the imported mesh (its radius is actually .125 m), anchor at its foot.
                    var bottom=new Vector3(r.bounds.center.x,r.bounds.min.y,r.bounds.center.z);
                    var size=r.bounds.size;
                    ScaleWorld(f.transform,bottom,new Vector3(.24f/size.x,1.1f/size.y,.24f/size.z));
                }
                if(n=="Interior_Floor")
                {
                    f.gameObject.layer=PlayerController.TraversalLayer;
                    interior.floor=f.gameObject.AddComponent<MeshCollider>(); interior.floor.sharedMesh=f.sharedMesh;
                }
                else if(n.Contains("Column") || n.Contains("Pew") || n=="Interior_Altar" || n=="Interior_Iconostasis")
                {
                    f.gameObject.layer=PlayerController.BlockerLayer;
                    f.gameObject.AddComponent<MeshCollider>().sharedMesh=f.sharedMesh;
                }
                if(n.StartsWith("Interior_Candle_") || n=="Interior_Chandelier")
                {
                    var b=r.bounds; bool hanging=n.Contains("Chandelier");
                    var light=new GameObject(n+"_Light").AddComponent<Light>(); light.transform.SetParent(church,true);
                    light.transform.position=hanging?b.center+Vector3.down*.2f:new Vector3(b.center.x,b.max.y+.12f,b.center.z);
                    light.type=LightType.Point; light.range=hanging?9:2.6f; light.intensity=hanging?1f:1.2f; light.shadows=LightShadows.None;
                    light.color=new Color(1,.62f,.25f); light.renderMode=LightRenderMode.ForcePixel; lamps.Add(light);
                    if(!hanging)
                    {
                        var glow=GameObject.CreatePrimitive(PrimitiveType.Sphere); glow.name=n+"_Flame";
                        Object.DestroyImmediate(glow.GetComponent<Collider>()); glow.transform.SetParent(light.transform,false);
                        glow.transform.localScale=new Vector3(.09f,.22f,.09f); glow.GetComponent<Renderer>().sharedMaterial=flame;
                    }
                }
            }
        }
        if(interior.doorLeaf==null || interior.floor==null || lamps.Count!=5) throw new System.InvalidOperationException("Interior import incomplete");
        var fill=new GameObject("Interior_NaveFill").AddComponent<Light>();
        fill.transform.SetParent(church,true); fill.transform.position=new Vector3(0,6.5f,-2);
        fill.type=LightType.Point; fill.range=16; fill.intensity=.85f;
        fill.color=new Color32(255,217,168,255); fill.shadows=LightShadows.None;
        fill.renderMode=LightRenderMode.ForcePixel; fill.enabled=false; lamps.Add(fill);
        interior.lamps=lamps.ToArray();
        // Terrain/terrace triangles under the indoor volume cannot protrude through its flat floor.
        // The exterior vertices, heights and materials stay exactly as generated.
        CutBelowFloor(terrain.GetComponent<MeshFilter>());
        foreach(var f in GameObject.Find("Church Terrace").GetComponentsInChildren<MeshFilter>())
            if(f.name.StartsWith("TerraceFill_") || f.name=="TerraceLanding") CutBelowFloor(f);
        // Unity-only furnishing scale, around the sanctuary end of the nave.
        // Keeps all six oversized pews in the middle distance of the narrow indoor rig.
        var pewGroup=new GameObject("Interior_PewScale").transform;
        pewGroup.SetParent(church,true); pewGroup.position=new Vector3(-2,interior.FloorY,6);
        foreach(var r in church.GetComponentsInChildren<MeshRenderer>())
            if(r.name.StartsWith("Interior_Pew_")) r.transform.SetParent(pewGroup,true);
        pewGroup.localScale=new Vector3(1,1,.4f);
        foreach(var r in church.GetComponentsInChildren<MeshRenderer>())
        {
            if(r.name.StartsWith("Interior_Pew_"))
                ScaleWorld(r.transform,r.bounds.center,new Vector3(.65f,1,2.5f));
            if(r.name=="Interior_Chandelier")
            {
                var anchor=new Vector3(r.bounds.center.x,interior.FloorY,r.bounds.center.z);
                ScaleWorld(r.transform,anchor,new Vector3(1,.42f,1));
            }
            if(r.name=="Interior_Iconostasis" || r.name.StartsWith("Interior_IconFrame_"))
            {
                ScaleWorld(r.transform,new Vector3(-2,interior.FloorY,7.5f),new Vector3(.5f,1,1));
            }
            if(r.name.StartsWith("Interior_Pew_") || r.name=="Interior_Altar" || r.name=="Interior_Iconostasis")
                if(r.bounds.min.y<interior.FloorY) r.transform.position+=Vector3.up*(interior.FloorY-r.bounds.min.y);
        }
        var chandelier=lamps.Find(l=>l.name.Contains("Chandelier"));
        var ring=System.Array.Find(church.GetComponentsInChildren<MeshRenderer>(),r=>r.name=="Interior_Chandelier");
        chandelier.transform.position=ring.bounds.center+Vector3.down*.2f;
        Physics.SyncTransforms();
        // Short stone threshold joins the graded courtyard to the flat local-zero floor.
        float fy=interior.FloorY;
        var vs=new[]{new Vector3(-3.28f,fy-.1f,-10.4f),new Vector3(-.72f,fy-.1f,-10.4f),
            new Vector3(-3.28f,fy,-9.15f),new Vector3(-.72f,fy,-9.15f)};
        var threshold=new GameObject("Interior_ThresholdRamp"); threshold.transform.SetParent(church,true);
        var mesh=new Mesh { name="Interior_ThresholdRamp" }; mesh.vertices=vs; mesh.triangles=new[]{0,2,1,1,2,3}; mesh.RecalculateNormals();
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};
        AssetDatabase.DeleteAsset("Assets/Art/Church/Interior_ThresholdRamp.asset"); AssetDatabase.CreateAsset(mesh,"Assets/Art/Church/Interior_ThresholdRamp.asset");
        threshold.AddComponent<MeshFilter>().sharedMesh=mesh; threshold.AddComponent<MeshRenderer>().sharedMaterial=thresholdStone;
        var ramp=threshold.AddComponent<MeshCollider>(); ramp.sharedMesh=mesh; threshold.layer=PlayerController.TraversalLayer;
        interior.threshold=ramp;
        // Cone sections use an existing transparent shader, lit cool against candle warmth.
        var shaftMat=new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name="M_InteriorShaft" };
        shaftMat.SetFloat("_Surface",1); shaftMat.SetFloat("_Blend",0); shaftMat.SetFloat("_ZWrite",0);
        shaftMat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); shaftMat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        shaftMat.SetFloat("_Cull",0); shaftMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); shaftMat.renderQueue=3000;
        shaftMat.SetColor("_BaseColor",new Color(.45f,.64f,.9f,.22f));
        AssetDatabase.DeleteAsset("Assets/Settings/M_InteriorShaft.mat"); AssetDatabase.CreateAsset(shaftMat,"Assets/Settings/M_InteriorShaft.mat");
        var shafts=new List<Renderer>();
        for(int i=0;i<4;i++)
        {
            float x=i%2==0?-5.55f:1.55f, z=i<2?-5.5f:5.6f;
            Vector3 top=new Vector3(x,fy+6.5f,z), bottom=new Vector3(-2+(x+2)*.3f,fy+.08f,z+1.2f);
            var points=new List<Vector3>(); var ts=new List<int>();
            var axis=(bottom-top).normalized; var a=Vector3.Cross(axis,Vector3.forward).normalized; var b=Vector3.Cross(axis,a);
            for(int j=0;j<12;j++)
            {
                float t=j*Mathf.PI*2/12; var radial=a*Mathf.Cos(t)+b*Mathf.Sin(t);
                points.Add(top+radial*.2f); points.Add(bottom+radial*.9f);
                int k=j*2, next=(j+1)%12*2; ts.AddRange(new[]{k,next,next+1,k,next+1,k+1});
            }
            var m=new Mesh { name="Interior_LightShaft_"+i }; m.vertices=points.ToArray(); m.triangles=ts.ToArray(); m.RecalculateNormals();
            string path="Assets/Art/Church/"+m.name+".asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m,path);
            var go=new GameObject(m.name); go.transform.SetParent(church,true);
            go.AddComponent<MeshFilter>().sharedMesh=m; var r=go.AddComponent<MeshRenderer>(); r.sharedMaterial=shaftMat;
            r.shadowCastingMode=ShadowCastingMode.Off; r.receiveShadows=false; shafts.Add(r);
        }
        interior.shafts=shafts.ToArray();
        Debug.Log($"[INTERIOR_BUILD] floor_y={fy:F3} lamps={lamps.Count} shafts={shafts.Count} hinge={interior.doorLeaf.position}");
        return interior;
    }
    static void CutBelowFloor(MeshFilter f)
    {
        var mesh=Object.Instantiate(f.sharedMesh); mesh.name=f.sharedMesh.name+"_InteriorCut";
        var v=mesh.vertices; var keep=new List<int>(); var t=mesh.triangles;
        for(int i=0;i<t.Length;i+=3)
        {
            var p=f.transform.TransformPoint((v[t[i]]+v[t[i+1]]+v[t[i+2]])/3);
            bool portal=p.x>-3.3f && p.x<-.7f && p.z>-9.3f && p.z< -7.3f;
            // The terrain has coarser triangles than the terrace: also test corners and edge midpoints.
            bool under=ChurchInterior.InPlan(p,.15f);
            if(f.sharedMesh.vertexCount>60000)
                for(int j=0;j<3;j++)
                {
                    var a=f.transform.TransformPoint(v[t[i+j]]); var b=f.transform.TransformPoint(v[t[i+(j+1)%3]]);
                    under |= ChurchInterior.InPlan(a,.2f) || ChurchInterior.InPlan((a+b)/2,.2f);
                }
            if(under || portal) continue;
            keep.Add(t[i]); keep.Add(t[i+1]); keep.Add(t[i+2]);
        }
        mesh.triangles=keep.ToArray(); mesh.RecalculateBounds();
        string path="Assets/Art/Church/"+f.name+"_InteriorCut.asset";
        AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh,path); f.sharedMesh=mesh;
        // Keep the original support mesh: outdoor traversal and terrain sampling retain their prior geometry.
        // SurfaceAt explicitly selects the indoor floor before these supports.
    }
}
