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
        if(emission.maxColorComponent>0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",emission); if(name=="M_InteriorWood" || name=="M_InteriorStone" || name=="M_InteriorGold" || name.StartsWith("M_InteriorSlab_") || name.StartsWith("M_InteriorPew") || name=="M_InteriorAisle" || name=="M_InteriorCeiling") m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive; }
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
                interior.outdoorChandelierPosition=new Vector3(r.bounds.center.x,interior.FloorY+(r.bounds.center.y-interior.FloorY)*.42f-.2f,r.bounds.center.z);
                ScaleWorld(r.transform,anchor,new Vector3(1,.42f,1));
                r.transform.position+=Vector3.up*(7.2f-r.bounds.center.y);
            }
            if(r.name=="Interior_Iconostasis" || r.name.StartsWith("Interior_IconFrame_"))
            {
                ScaleWorld(r.transform,new Vector3(-2,interior.FloorY,7.5f),new Vector3(1,.55f,1));
            }
            if(r.name.StartsWith("Interior_Pew_") || r.name=="Interior_Altar" || r.name=="Interior_Iconostasis")
                if(r.bounds.min.y<interior.FloorY) r.transform.position+=Vector3.up*(interior.FloorY-r.bounds.min.y);
        }
        var chandelier=lamps.Find(l=>l.name.Contains("Chandelier"));
        var oldRing=System.Array.Find(church.GetComponentsInChildren<MeshRenderer>(),r=>r.name=="Interior_Chandelier");
        interior.legacyChandelier=oldRing; oldRing.name="Interior_LegacyChandelier";
        var detail=new GameObject("Interior_Detail").transform; detail.SetParent(church,true);
        interior.detailRoot=detail.gameObject;
        var iron=Lit("M_InteriorIron",new Color32(43,39,34,255));
        Vector3 lampCenter=new Vector3(0,4.6f,-4.5f);
        var ring=Ring(detail,lampCenter,iron);
        chandelier.transform.position=lampCenter+Vector3.down*.2f;
        interior.chandelierLamp=chandelier; interior.indoorChandelierPosition=chandelier.transform.position;
        var wax=Lit("M_InteriorWax",new Color(.88f,.74f,.46f));
        for(int i=0;i<8;i++)
        {
            float angle=i*Mathf.PI/4;
            Vector3 foot=lampCenter+new Vector3(Mathf.Cos(angle)*.9f,.03f,Mathf.Sin(angle)*.9f);
            var candle=Primitive(detail,"Interior_ChandelierCandle_"+i,PrimitiveType.Cylinder,
                foot+Vector3.up*.11f,new Vector3(.1f,.11f,.1f),wax);
            var glow=Primitive(detail,candle.name+"_Flame",PrimitiveType.Sphere,
                foot+Vector3.up*.26f,new Vector3(.045f,.1f,.045f),flame);
            var lamp=new GameObject(candle.name+"_Light").AddComponent<Light>(); lamp.transform.SetParent(detail,true); lamp.transform.position=glow.transform.position;
            lamp.type=LightType.Point; lamp.color=new Color(1,.62f,.25f); lamp.intensity=.35f; lamp.range=1.2f; lamp.shadows=LightShadows.None; lamps.Add(lamp);
        }
        for(int i=0;i<3;i++)
        {
            float angle=i*Mathf.PI*2/3;
            Vector3 bottom=lampCenter+new Vector3(Mathf.Cos(angle)*.9f,0,Mathf.Sin(angle)*.9f);
            // Nave ridge runs along x=-2; the anchor follows its sloped underside.
            var top=new Vector3(bottom.x,12f-Mathf.Abs(bottom.x+2)/3.9f*.4f,bottom.z);
            var cable=Primitive(detail,"Interior_Suspension_"+i,PrimitiveType.Cylinder,(top+bottom)/2,
                new Vector3(.025f,(top-bottom).magnitude/2,.025f),iron);
            cable.transform.rotation=Quaternion.FromToRotation(Vector3.up,top-bottom);
        }
        BuildCeiling(detail);
        BuildFloor(detail,interior);
        DressPews(church,detail,interior);
        interior.lamps=lamps.ToArray();
        var frameMat=Lit("M_InteriorArchFrame",new Color(.09f,.045f,.018f),new Color(.035f,.018f,.008f));
        var iconMat=Lit("M_InteriorIconCore",new Color(.58f,.28f,.075f),new Color(.65f,.30f,.075f));
        var doorMat=Lit("M_InteriorHolyDoor",new Color(.72f,.42f,.14f),new Color(.95f,.53f,.16f));
        foreach(var r in church.GetComponentsInChildren<MeshRenderer>())
            if(r.name.StartsWith("Interior_IconFrame_")) r.enabled=false;
        var screen=System.Array.Find(church.GetComponentsInChildren<MeshRenderer>(),r=>r.name=="Interior_Iconostasis");
        for(int i=0;i<3;i++)
        {
            var center=new Vector3(screen.bounds.center.x+(i-1)*1.02f,screen.bounds.center.y,screen.bounds.min.z-.025f);
            Arch(church,"Interior_ArchFrame_"+i,center,.9f,1.6f,frameMat);
            Arch(church,"Interior_ArchCore_"+i,center+Vector3.back*.015f,.74f,1.42f,i==1?doorMat:iconMat);
        }
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
        // Camera-facing volumes carry a radial cross-section; additive blending never darkens stone.
        var shaftMat=new Material(Shader.Find("Mistbell/InteriorShaft")) { name="M_InteriorShaft" };
        shaftMat.SetColor("_BaseColor",new Color(.45f,.64f,.9f,.08f));
        shaftMat.SetFloat("_DstBlend",(float)BlendMode.One);
        AssetDatabase.DeleteAsset("Assets/Settings/M_InteriorShaft.mat"); AssetDatabase.CreateAsset(shaftMat,"Assets/Settings/M_InteriorShaft.mat");
        var shafts=new List<Renderer>();
        var rng=new System.Random(2170);
        for(int i=0;i<3;i++)
        {
            float x=i%2==0?-5.55f:1.55f, z=i==0?-5.5f:5.6f;
            Vector3 offset=new Vector3((float)rng.NextDouble()*.22f-.11f,0,(float)rng.NextDouble()*.4f-.2f);
            Vector3 top=new Vector3(x,fy+6.5f,z)+offset, bottom=new Vector3(-2+(x+2)*.3f,fy+.08f,z+1.2f)+offset;
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad); go.name="Interior_LightShaft_"+i;
            Object.DestroyImmediate(go.GetComponent<Collider>()); go.transform.SetParent(church,true);
            go.transform.position=(top+bottom)*.5f;
            go.transform.rotation=Quaternion.LookRotation(Vector3.Cross(Vector3.right,top-bottom).normalized,(top-bottom).normalized);
            go.transform.localScale=new Vector3(.9f,(top-bottom).magnitude,1);
            var r=go.GetComponent<MeshRenderer>(); r.sharedMaterial=shaftMat;
            r.shadowCastingMode=ShadowCastingMode.Off; r.receiveShadows=false; shafts.Add(r);
        }
        interior.shafts=shafts.ToArray();
        Debug.Log($"[INTERIOR_BUILD] floor_y={fy:F3} lamps={lamps.Count} shafts={shafts.Count} hinge={interior.doorLeaf.position}");
        return interior;
    }
    static GameObject Primitive(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(type); go.name=name;
        Object.DestroyImmediate(go.GetComponent<Collider>()); go.transform.SetParent(parent,true);
        go.transform.position=position; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=material;
        // Added indoor detail must not cast through the unchanged exterior shell.
        go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        return go;
    }
    static MeshRenderer Surface(Transform parent,string name,Vector3[] vertices,int[] indices,Material material)
    {
        var mesh=new Mesh {name=name}; mesh.vertices=vertices; mesh.triangles=indices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var uv=new Vector2[vertices.Length]; for(int i=0;i<uv.Length;i++) uv[i]=new Vector2(vertices[i].x,vertices[i].z); mesh.uv=uv;
        string path="Assets/Art/Church/"+name+".asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh,path);
        var go=new GameObject(name); go.transform.SetParent(parent,true);
        go.AddComponent<MeshFilter>().sharedMesh=mesh; var r=go.AddComponent<MeshRenderer>(); r.sharedMaterial=material;
        r.shadowCastingMode=ShadowCastingMode.Off; return r;
    }
    static MeshRenderer Ring(Transform parent,Vector3 center,Material material)
    {
        const int segments=64,sides=8;
        var v=new Vector3[segments*sides]; var t=new List<int>();
        for(int i=0;i<segments;i++) for(int j=0;j<sides;j++)
        {
            float a=i*Mathf.PI*2/segments,b=j*Mathf.PI*2/sides;
            v[i*sides+j]=center+new Vector3(Mathf.Cos(a)*(.9f+.03f*Mathf.Cos(b)),.03f*Mathf.Sin(b),Mathf.Sin(a)*(.9f+.03f*Mathf.Cos(b)));
            int k=i*sides+j,n=((i+1)%segments)*sides+j,q=i*sides+(j+1)%sides,nq=((i+1)%segments)*sides+(j+1)%sides;
            t.AddRange(new[]{k,q,n,q,nq,n});
        }
        return Surface(parent,"Interior_Chandelier",v,t.ToArray(),material);
    }
    static void BuildCeiling(Transform parent)
    {
        var wood=Lit("M_InteriorCeiling",new Color32(74,59,48,255),new Color(.045f,.035f,.025f));
        // Downward-facing gables; side wings meet the nave at its eaves without overlapping faces.
        for(int side=0;side<2;side++)
        {
            float edge=side==0?-5.9f:1.9f;
            var v=new[]{new Vector3(edge,11.6f,-7.4f),new Vector3(-2,12,-7.4f),new Vector3(edge,11.6f,7.4f),new Vector3(-2,12,7.4f)};
            Surface(parent,"Interior_Ceiling_Nave_"+side,v,side==0?new[]{0,1,2,1,3,2}:new[]{0,2,1,1,2,3},wood);
            float x0=side==0?-8.4f:1.9f,x1=side==0?-5.9f:4.4f;
            for(int half=0;half<2;half++)
            {
                float z=half==0?-2.9f:2.9f;
                var wing=new[]{new Vector3(x0,11.6f,z),new Vector3(x1,11.6f,z),new Vector3(x0,side==0?12:11.6f,0),new Vector3(x1,side==0?11.6f:12,0)};
                Surface(parent,"Interior_Ceiling_Transept_"+side+"_"+half,wing,half==0?new[]{0,1,2,1,3,2}:new[]{0,2,1,1,2,3},wood);
            }
        }
        for(int i=0;i<5;i++) Primitive(parent,"Interior_CeilingBeam_"+i,PrimitiveType.Cube,
            new Vector3(-2,11.75f,-4+i*2),new Vector3(7.8f,.3f,.25f),wood);
    }
    static void BuildFloor(Transform parent,ChurchInterior interior)
    {
        var mats=new Material[15];
        for(int i=0;i<mats.Length;i++) mats[i]=Lit("M_InteriorSlab_"+i,(Color)new Color32(143,133,121,255)*(1+(i-7)*.01f),new Color(.32f,.29f,.24f));
        var aisle=Lit("M_InteriorAisle",new Color32(110,101,91,255),new Color(.24f,.21f,.18f));
        var grout=Lit("M_InteriorGrout",new Color32(65,59,53,255),new Color(.03f,.025f,.02f));
        var bed=new GameObject("Interior_GroutBed"); bed.transform.SetParent(parent,true);
        bed.transform.SetPositionAndRotation(interior.floor.transform.position+Vector3.down*.008f,interior.floor.transform.rotation);
        bed.transform.localScale=interior.floor.transform.lossyScale;
        bed.AddComponent<MeshFilter>().sharedMesh=interior.floor.sharedMesh;
        var bedRenderer=bed.AddComponent<MeshRenderer>(); bedRenderer.sharedMaterial=grout; bedRenderer.shadowCastingMode=ShadowCastingMode.Off;
        var rng=new System.Random(2170); int count=0;
        // Nominal 1.2 x .8 m slabs; cut pieces only at the plan boundary and the aisle.
        for(int row=0;row<26;row++) for(int col=0;col<12;col++)
        {
            float x=-8.4f+col*1.2f,z=-9.35f+row*.8f;
            float x1=x+1.2f,z1=z+.8f;
            var cuts=new List<float>{x,x1};
            foreach(float c in new[]{-5.9f,-2.8f,-1.2f,1.9f}) if(c>x && c<x1) cuts.Add(c);
            cuts.Sort();
            for(int k=0;k<cuts.Count-1;k++)
            {
                float a=cuts[k],b=cuts[k+1]; var center=new Vector3((a+b)/2,interior.FloorY,(z+z1)/2);
                if(!interior.floor.Raycast(new Ray(center+Vector3.up,Vector3.down),out var hit,2)) continue;
                // Clamp each piece to the actual flat floor using corner samples.
                if(!interior.floor.Raycast(new Ray(new Vector3(a+.02f,center.y+1,z+.02f),Vector3.down),out _,2) ||
                   !interior.floor.Raycast(new Ray(new Vector3(b-.02f,center.y+1,z1-.02f),Vector3.down),out _,2)) continue;
                bool middle=a>=-2.801f && b<=-1.199f;
                Primitive(parent,"Interior_FloorSlab_"+count++,PrimitiveType.Cube,center+Vector3.down*.009f,
                    new Vector3(b-a-.03f,.03f,.77f),middle?aisle:mats[rng.Next(mats.Length)]);
            }
        }
        interior.floorSlabs=count;
        Primitive(parent,"Interior_CenterAisle",PrimitiveType.Cube,new Vector3(-2,interior.FloorY-.012f,-.9f),new Vector3(1.6f,.03f,16.9f),aisle);
    }
    static void DressPews(Transform church,Transform detail,ChurchInterior interior)
    {
        // Separate asset path avoids touching M_Wood used by exterior props.
        var wood=Lit("M_InteriorPewWood",new Color32(107,74,50,255),new Color(.32f,.26f,.19f)); wood.name="M_Wood"; EditorUtility.SetDirty(wood);
        var legs=Lit("M_InteriorPewLeg",new Color32(78,54,36,255),new Color(.19f,.14f,.09f));
        var edge=Lit("M_InteriorPewEdge",new Color32(122,87,60,255),new Color(.36f,.29f,.21f));
        var grain=new Texture2D(128,32,TextureFormat.RGBA32,false) {name="Interior_WoodGrain",wrapMode=TextureWrapMode.Repeat};
        for(int y=0;y<32;y++) for(int x=0;x<128;x++)
        {
            float f=1-.10f*(.5f+.5f*Mathf.Sin(y*2.3f+Mathf.Sin(x*.08f)*.65f)); grain.SetPixel(x,y,new Color(f,f,f));
        }
        grain.Apply(); AssetDatabase.DeleteAsset("Assets/Art/Church/Interior_WoodGrain.asset"); AssetDatabase.CreateAsset(grain,"Assets/Art/Church/Interior_WoodGrain.asset");
        wood.SetTexture("_BaseMap",grain); wood.SetTextureScale("_BaseMap",new Vector2(3,2));
        var pews=new List<Renderer>(); var originals=new List<Material>();
        foreach(var r in church.GetComponentsInChildren<MeshRenderer>()) if(r.name.StartsWith("Interior_Pew_"))
        {
            pews.Add(r); originals.Add(r.sharedMaterial);
            var b=r.bounds; var f=r.GetComponent<MeshFilter>(); var mesh=Object.Instantiate(f.sharedMesh);
            var body=new List<int>(); var feet=new List<int>(); var vertices=mesh.vertices; var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                float y=0; for(int j=0;j<3;j++) y+=r.transform.TransformPoint(vertices[triangles[i+j]]).y/3;
                var target=y<interior.FloorY+.38f?feet:body; target.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});
            }
            // Only split existing triangles into material slots; all imported vertex positions stay intact.
            mesh.subMeshCount=2; mesh.SetTriangles(body,0); mesh.SetTriangles(feet,1);
            string path="Assets/Art/Church/"+r.name+"_Materials.asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh,path); f.sharedMesh=mesh;
            r.sharedMaterials=new[]{wood,legs};
            Primitive(detail,"Interior_PewSeatEdge_"+r.name,PrimitiveType.Cube,new Vector3(b.center.x,interior.FloorY+.46f,b.max.z+.012f),new Vector3(b.size.x,.06f,.035f),edge);
        }
        interior.pews=pews.ToArray(); interior.pewOutdoorMaterials=originals.ToArray(); interior.pewWood=wood; interior.pewLegs=legs;
    }
    static void Arch(Transform parent,string name,Vector3 center,float width,float height,Material material)
    {
        var vertices=new List<Vector3> { new Vector3(0,0,0),new Vector3(-width/2,-height/2,0),new Vector3(width/2,-height/2,0) };
        float radius=width/2, spring=height/2-radius;
        for(int i=0;i<=20;i++) { float a=i*Mathf.PI/20; vertices.Add(new Vector3(Mathf.Cos(a)*radius,spring+Mathf.Sin(a)*radius,0)); }
        var triangles=new List<int>();
        for(int i=1;i<vertices.Count;i++) triangles.AddRange(new[]{0,i==vertices.Count-1?1:i+1,i});
        var mesh=new Mesh {name=name}; mesh.vertices=vertices.ToArray(); mesh.triangles=triangles.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path="Assets/Art/Church/"+name+".asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh,path);
        var go=new GameObject(name); go.transform.SetParent(parent,true); go.transform.position=center;
        go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=material;
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
