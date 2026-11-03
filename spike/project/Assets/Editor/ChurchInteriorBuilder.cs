using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;

public static class ChurchInteriorBuilder
{
    static Material Lit(string name, Color color, Color emission=default)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name=name };
        m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",.12f);
        if(emission.maxColorComponent>0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",emission); if(name=="M_InteriorWood" || name=="M_InteriorStone" || name=="M_InteriorGold" || name.StartsWith("M_InteriorSlab_") || name.StartsWith("M_InteriorPew") || name=="M_InteriorAisle" || name=="M_InteriorCeiling" || name.StartsWith("M_InteriorDrum") || name=="M_InteriorDome") m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive; }
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
                interior.indoorStone.SetColor("_BaseColor",new Color(.42f,.36f,.29f));   // .65 线性≈0.83 sRGB，太亮
                interior.indoorStone.EnableKeyword("_EMISSION");
                interior.indoorStone.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
                interior.indoorStone.SetColor("_EmissionColor",new Color(.13f,.115f,.10f));   // 自发光 0.32 让墙整体发白
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
        fill.type=LightType.Point; fill.range=16; fill.intensity=.60f;
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
        Vector3 lampCenter=new Vector3(0,3.4f,3.5f);   // 人眼相机在 z≈-4.8 朝 +z，吊灯必须在玩家前方而非头顶
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
            // Vertical suspension: 8.6 - 4.0 = 4.6 metres.
            var top=new Vector3(bottom.x,8.6f,bottom.z);
            var cable=Primitive(detail,"Interior_Suspension_"+i,PrimitiveType.Cylinder,(top+bottom)/2,
                new Vector3(.025f,(top-bottom).magnitude/2,.025f),iron);
            cable.transform.rotation=Quaternion.FromToRotation(Vector3.up,top-bottom);
        }
        OpenIndoorDrum(church,interior);
        BuildCeiling(detail);
        BuildDrum(detail,interior);
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
        ApplyLighting(interior);
        Debug.Log($"[INTERIOR_BUILD] floor_y={fy:F3} lamps={lamps.Count} shafts={shafts.Count} hinge={interior.doorLeaf.position}");
        return interior;
    }
    // Patch the authored scene in place: do not regenerate terrain, sky or exterior assets.
    public static void ApplyLightingAndBuild()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        ApplyLighting(Object.FindFirstObjectByType<ChurchInterior>());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{"Assets/Scenes/ArtSpike.unity"}, locationPathName="Builds/Mac/Mistbell.app",
            target=BuildTarget.StandaloneOSX, options=BuildOptions.None });
        Debug.Log($"[INT_LIGHT_BUILD] result={report.summary.result} errors={report.summary.totalErrors}");
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
    static void ApplyLighting(ChurchInterior interior)
    {
        var lamps=new List<Light>(interior.lamps);
        lamps.RemoveAll(l=>l==null || l.name=="Interior_NaveFill" || l.name.StartsWith("Interior_WindowKey") || l.name.StartsWith("Interior_NaveFill_"));
        foreach(var l in interior.GetComponentsInChildren<Light>(true))
            if(l.name=="Interior_NaveFill" || l.name.StartsWith("Interior_WindowKey") || l.name.StartsWith("Interior_NaveFill_")) Object.DestroyImmediate(l.gameObject);
        var key=new GameObject("Interior_WindowKey").AddComponent<Light>();
        key.transform.SetParent(interior.transform,true);
        key.transform.position=new Vector3(-5.5f,interior.FloorY+6.5f,-3.5f);
        key.transform.rotation=Quaternion.Euler(35,75,0);
        // Indoor-only directional daylight; its transform encodes the west high-window direction.
        key.type=LightType.Directional; key.intensity=.95f; key.range=24;   // 1.9 把中殿打成一间白墙现代厅
        // Project the high-window aperture instead of illuminating the whole floor uniformly.
        // The imported solid roof otherwise occludes a second directional shadow map entirely.
        var cookie=new Texture2D(256,256,TextureFormat.RGB24,false,true) {
            name="Interior_WestWindowCookie",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear };
        for(int y=0;y<256;y++) for(int x=0;x<256;x++)
        {
            float u=(x/255f-.5f)*24, v=(y/255f-.5f)*24;
            float aperture=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-4.6f,-3.4f,v)) *
                (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(5,6,v))) *
                (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(9,10,Mathf.Abs(u))));
            cookie.SetPixel(x,y,new Color(aperture,aperture,aperture));
        }
        cookie.Apply();
        AssetDatabase.DeleteAsset("Assets/Art/Church/Interior_WestWindowCookie.asset");
        AssetDatabase.CreateAsset(cookie,"Assets/Art/Church/Interior_WestWindowCookie.asset");
        key.cookie=cookie; key.gameObject.AddComponent<UniversalAdditionalLightData>().lightCookieSize=new Vector2(24,24);
        key.color=new Color32(232,217,196,255); key.shadows=LightShadows.None;
        key.renderMode=LightRenderMode.ForcePixel; key.enabled=false; lamps.Add(key);
        for(int i=0;i<2;i++)
        {
            var fill=new GameObject("Interior_NaveFill_"+i).AddComponent<Light>();
            fill.transform.SetParent(interior.transform,true);
            fill.transform.position=new Vector3(i==0?-4.1f:.1f,interior.FloorY+3.6f,2.6f);
            fill.type=LightType.Point; fill.intensity=.55f; fill.range=9;
            fill.color=new Color32(201,214,230,255); fill.shadows=LightShadows.None;
            fill.renderMode=LightRenderMode.ForcePixel; fill.enabled=false; lamps.Add(fill);
        }
        foreach(var lamp in lamps) if(lamp.name.StartsWith("Interior_Candle_")) lamp.range=6;
        interior.lamps=lamps.ToArray();
        var foot=Lit("M_InteriorWallFoot",interior.indoorStone.GetColor("_BaseColor")*.9f,
            interior.indoorStone.GetColor("_EmissionColor"));
        foot.CopyPropertiesFromMaterial(interior.indoorStone);
        var c=interior.indoorStone.GetColor("_BaseColor");
        foot.SetColor("_BaseColor",new Color(c.r*1.1f,c.g*1.1f,c.b*1.1f,c.a));
        interior.wallFootStone=foot;
        for(int i=0;i<interior.drumCutFilters.Length;i++)
        {
            var f=interior.drumCutFilters[i]; if(f.GetComponent<Renderer>()!=interior.shell) continue;
            var mesh=interior.drumIndoorMeshes[i]; var upper=new List<int>(); var lower=new List<int>();
            var v=mesh.vertices; var normals=mesh.normals; var uv=mesh.uv; var t=mesh.triangles;
            var resultV=new List<Vector3>(); var resultN=new List<Vector3>(); var resultUV=new List<Vector2>();
            void Emit(List<CutVertex> polygon,List<int> indices)
            {
                int offset=resultV.Count;
                foreach(var q in polygon) { resultV.Add(q.p); resultN.Add(q.n); resultUV.Add(q.uv); }
                for(int k=1;k<polygon.Count-1;k++) indices.AddRange(new[]{offset,offset+k,offset+k+1});
            }
            var normal=f.transform.InverseTransformDirection(Vector3.up).normalized;
            float height=Vector3.Dot(normal,f.transform.InverseTransformPoint(new Vector3(0,interior.FloorY+.7f,0)));
            for(int k=0;k<t.Length;k+=3)
            {
                var polygon=new List<CutVertex>();
                for(int j=0;j<3;j++) { int n=t[k+j]; polygon.Add(new CutVertex {p=v[n],n=normals[n],uv=uv[n]}); }
                Emit(Clip(polygon,normal,height,true),upper);
                Emit(Clip(polygon,normal,height,false),lower);
            }
            mesh.Clear(); mesh.SetVertices(resultV); mesh.SetNormals(resultN); mesh.SetUVs(0,resultUV);
            mesh.subMeshCount=2; mesh.SetTriangles(upper,0); mesh.SetTriangles(lower,1); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            Debug.Log($"[INT_LIGHT_SETUP] wall_foot_tris={lower.Count/3} brighten=1.10");
        }
        interior.pewEnds=Lit("M_InteriorPewEnd",new Color(107/255f*1.1f,74/255f*1.1f,50/255f*1.1f),new Color(.06f,.05f,.04f));
        interior.pewEnds.CopyPropertiesFromMaterial(interior.pewWood);
        c=interior.pewWood.GetColor("_BaseColor");
        interior.pewEnds.SetColor("_BaseColor",new Color(c.r*1.1f,c.g*1.1f,c.b*1.1f,c.a));
        foreach(var r in interior.pews)
        {
            var f=r.GetComponent<MeshFilter>(); var mesh=f.sharedMesh; var body=new List<int>(); var feet=new List<int>(); var ends=new List<int>();
            var v=mesh.vertices; var t=mesh.triangles;
            for(int k=0;k<t.Length;k+=3)
            {
                var a=f.transform.TransformPoint(v[t[k]]); var b=f.transform.TransformPoint(v[t[k+1]]); var d=f.transform.TransformPoint(v[t[k+2]]);
                var n=Vector3.Cross(b-a,d-a).normalized;
                var slot=(a.y+b.y+d.y)/3<interior.FloorY+.38f?feet:Mathf.Abs(n.x)>.8f?ends:body;
                slot.AddRange(new[]{t[k],t[k+1],t[k+2]});
            }
            mesh.subMeshCount=3; mesh.SetTriangles(body,0); mesh.SetTriangles(feet,1); mesh.SetTriangles(ends,2);
            r.sharedMaterials=new[]{interior.pewWood,interior.pewLegs,interior.pewEnds};
            EditorUtility.SetDirty(mesh);
        }
        for(int i=0;i<15;i++)
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_InteriorSlab_"+i+".mat");
            // Preserve subtle per-slab variation around the specified stone palette.
            mat.SetColor("_BaseColor",(Color)new Color32(185,167,142,255)*(1+(i-7)*.01f));
            mat.SetFloat("_Smoothness",.22f); EditorUtility.SetDirty(mat);
        }
        var aisle=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_InteriorAisle.mat");
        aisle.SetFloat("_Smoothness",.22f); EditorUtility.SetDirty(aisle);
        EditorUtility.SetDirty(foot); EditorUtility.SetDirty(interior.pewEnds); EditorUtility.SetDirty(interior);
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
        var wood=Lit("M_InteriorCeiling",new Color32(120,96,72,255),new Color(.24f,.185f,.13f));   // 抬头时天花板几乎没有直射光，靠自发光维持可读   // 原 4A3B30+弱自发光 在室内等于纯黑，读成"没顶"
        // Split the nave around the 1.6 m square opening below the drum (x=-2,z=0).
        for(int side=0;side<2;side++)
        {
            float edge=side==0?-5.9f:1.9f;
            var vertices=new List<Vector3>(); var indices=new List<int>();
            float[] xs=side==0?new[]{edge,-2.8f,-2}:new[]{-2,-1.2f,edge};
            float[] zs={-7.4f,-.8f,.8f,7.4f};
            for(int x=0;x<2;x++) for(int z=0;z<3;z++)
            {
                if(z==1 && (side==0?x==1:x==0)) continue;
                float x0=xs[x],x1=xs[x+1],z0=zs[z],z1=zs[z+1];
                float y0=9-Mathf.Abs(x0+2)/3.9f*.4f,y1=9-Mathf.Abs(x1+2)/3.9f*.4f;
                int n=vertices.Count;
                vertices.AddRange(new[]{new Vector3(x0,y0,z0),new Vector3(x1,y1,z0),new Vector3(x0,y0,z1),new Vector3(x1,y1,z1)});
                indices.AddRange(new[]{n,n+1,n+2,n+1,n+3,n+2});
            }
            Surface(parent,"Interior_Ceiling_Nave_"+side,vertices.ToArray(),indices.ToArray(),wood);
            float left=side==0?-8.4f:1.9f,right=side==0?-5.9f:4.4f;
            for(int half=0;half<2;half++)
            {
                float z=half==0?-2.9f:2.9f;
                var wing=new[]{new Vector3(left,8.6f,z),new Vector3(right,8.6f,z),new Vector3(left,side==0?9:8.6f,0),new Vector3(right,side==0?8.6f:9,0)};
                Surface(parent,"Interior_Ceiling_Transept_"+side+"_"+half,wing,half==0?new[]{0,1,2,1,3,2}:new[]{0,2,1,1,2,3},wood);
            }
        }
        // Keep all five beams clear of the central drum opening.
        float[] beamZ={-4,-2,1,3,5};
        for(int i=0;i<5;i++) Primitive(parent,"Interior_CeilingBeam_"+i,PrimitiveType.Cube,
            new Vector3(-2,8.75f,beamZ[i]),new Vector3(7.8f,.3f,.25f),wood);
        foreach(var r in parent.GetComponentsInChildren<MeshRenderer>())
            if(r.name.StartsWith("Interior_Ceiling"))
            {
                var f=r.GetComponent<MeshFilter>(); f.sharedMesh=OpenMesh(f,r.name+"_DrumOpening");
            }
    }

    // Subtract a convex vertical opening by splitting triangles at its planes.
    // Retain interpolated UVs/normals, every submesh and all geometry outside the cut.
    struct CutVertex
    {
        public Vector3 p,n; public Vector2 uv;
        public static CutVertex Lerp(CutVertex a,CutVertex b,float t) => new CutVertex {
            p=Vector3.Lerp(a.p,b.p,t), n=Vector3.Lerp(a.n,b.n,t).normalized, uv=Vector2.Lerp(a.uv,b.uv,t) };
    }
    static List<CutVertex> Clip(List<CutVertex> polygon,Vector3 normal,float distance,bool outside)
    {
        var result=new List<CutVertex>();
        for(int i=0;i<polygon.Count;i++)
        {
            var a=polygon[i]; var b=polygon[(i+1)%polygon.Count];
            float da=Vector3.Dot(a.p,normal)-distance, db=Vector3.Dot(b.p,normal)-distance;
            bool ka=outside?da>=0:da<=0, kb=outside?db>=0:db<=0;
            if(ka) result.Add(a);
            if(ka!=kb) result.Add(CutVertex.Lerp(a,b,da/(da-db)));
        }
        return result;
    }
    static Mesh OpenMesh(MeshFilter filter,string name)
    {
        var source=filter.sharedMesh; var sv=source.vertices; var sn=source.normals; var su=source.uv;
        var vertices=new List<Vector3>(); var normals=new List<Vector3>(); var uv=new List<Vector2>();
        var slots=new List<int[]>();
        var planes=new List<Plane> {new Plane(Vector3.down,8.599f)};
        for(int i=0;i<12;i++)
        {
            float a=(i+.5f)*Mathf.PI/6; var n=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            planes.Add(new Plane(n,-(3.2f*Mathf.Cos(Mathf.PI/12)+Vector3.Dot(n,new Vector3(-2,0,0)))));
        }
        for(int sub=0;sub<source.subMeshCount;sub++)
        {
            var indices=new List<int>(); var triangles=source.GetTriangles(sub);
            void Emit(List<CutVertex> polygon)
            {
                if(polygon.Count<3) return;
                int start=vertices.Count;
                foreach(var v in polygon) { vertices.Add(filter.transform.InverseTransformPoint(v.p)); normals.Add(filter.transform.InverseTransformDirection(v.n)); uv.Add(v.uv); }
                for(int j=1;j<polygon.Count-1;j++) indices.AddRange(new[]{start,start+j,start+j+1});
            }
            for(int t=0;t<triangles.Length;t+=3)
            {
                var polygon=new List<CutVertex>();
                for(int j=0;j<3;j++)
                {
                    int k=triangles[t+j]; polygon.Add(new CutVertex {p=filter.transform.TransformPoint(sv[k]),
                        n=filter.transform.TransformDirection(sn[k]),uv=su.Length==sv.Length?su[k]:Vector2.zero});
                }
                // Most triangles are wholly outside one clipping plane: preserve them exactly.
                bool untouched=false;
                foreach(var plane in planes) if(polygon.TrueForAll(v=>plane.GetDistanceToPoint(v.p)>=0)) { untouched=true; break; }
                if(untouched) { Emit(polygon); continue; }
                foreach(var plane in planes)
                {
                    Emit(Clip(polygon,plane.normal,-plane.distance,true));
                    polygon=Clip(polygon,plane.normal,-plane.distance,false);
                    if(polygon.Count<3) break;
                }
            }
            slots.Add(indices.ToArray());
        }
        var mesh=new Mesh {name=name,indexFormat=IndexFormat.UInt32};
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0,uv); mesh.subMeshCount=slots.Count;
        for(int i=0;i<slots.Count;i++) mesh.SetTriangles(slots[i],i);
        mesh.RecalculateBounds();
        string path="Assets/Art/Church/"+name+".asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh,path);
        return mesh;
    }
    static void OpenIndoorDrum(Transform church,ChurchInterior interior)
    {
        var filters=new List<MeshFilter>(); var outside=new List<Mesh>(); var inside=new List<Mesh>();
        foreach(var f in church.GetComponentsInChildren<MeshFilter>())
        {
            var r=f.GetComponent<Renderer>();
            if(r==null || r.bounds.max.y<8.6f || r.bounds.min.x>1.2f || r.bounds.max.x< -5.2f || r.bounds.min.z>3.2f || r.bounds.max.z< -3.2f) continue;
            filters.Add(f); outside.Add(f.sharedMesh); inside.Add(OpenMesh(f,f.name+"_IndoorDrumCut"));
            GameObjectUtility.SetStaticEditorFlags(f.gameObject,GameObjectUtility.GetStaticEditorFlags(f.gameObject)&~StaticEditorFlags.BatchingStatic);
        }
        interior.drumCutFilters=filters.ToArray(); interior.drumOutdoorMeshes=outside.ToArray(); interior.drumIndoorMeshes=inside.ToArray();
    }
    static void BuildDrum(Transform parent,ChurchInterior interior)
    {
        var stone=Lit("M_InteriorDrum",new Color32(140,133,121,255),new Color(.34f,.32f,.29f));
        var coneColor=(Color)new Color32(138,129,119,255);
        // Emissive energy is authored in linear light; this spike renders in Gamma.
        // Convert the requested 0.05 floor for that pipeline rather than losing it to gamma/tonemapping.
        var coneEmission=new Color(.05f,.05f,.05f);
        if(PlayerSettings.colorSpace==ColorSpace.Gamma) coneEmission=coneEmission.gamma;
        var dark=Lit("M_InteriorDome",coneColor,coneEmission);
        stone.globalIlluminationFlags=dark.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        var cold=Lit("M_InteriorDrumDaylight",new Color32(132,170,211,255),new Color(.48f,.68f,.95f));
        var shaft=new Material(Shader.Find("Mistbell/InteriorShaft")) {name="M_InteriorDrumShaft"};
        shaft.SetColor("_BaseColor",new Color(.45f,.64f,.9f,.10f)); shaft.SetFloat("_DstBlend",(float)BlendMode.One);
        AssetDatabase.DeleteAsset("Assets/Settings/M_InteriorDrumShaft.mat"); AssetDatabase.CreateAsset(shaft,"Assets/Settings/M_InteriorDrumShaft.mat");
        var windowEmission=(Color)new Color32(207,226,245,255);
        var windowFill=Lit("M_InteriorDrumWindowFill",windowEmission,windowEmission*1.2f);
        var skyEmission=(Color)new Color32(191,215,234,255);
        var skyFill=Lit("M_InteriorOculusSky",skyEmission,skyEmission);
        // Clear EmissiveIsBlack before URP validates/saves this material, or its emission keyword is removed.
        skyFill.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        skyFill.SetFloat("_Cull",0); EditorUtility.SetDirty(skyFill);
        var center=new Vector3(-2,0,0);
        interior.domeUplights=new Light[2];
        for(int i=0;i<2;i++)
        {
            var lamp=new GameObject("Interior_DomeUplight_"+i).AddComponent<Light>();
            lamp.transform.SetParent(parent,true);
            lamp.transform.position=center+new Vector3(i==0?-1.8f:1.8f,10.5f,0);
            lamp.transform.rotation=Quaternion.LookRotation(Vector3.up);
            lamp.type=LightType.Point; lamp.range=8; lamp.intensity=.35f;
            lamp.color=new Color32(255,224,184,255); lamp.shadows=LightShadows.None;
            lamp.renderMode=LightRenderMode.ForcePixel;
            interior.domeUplights[i]=lamp;
        }
        float apothem=3.2f*Mathf.Cos(Mathf.PI/12),outer=4.2f*Mathf.Cos(Mathf.PI/12);
        for(int side=0;side<12;side++)
        {
            float a=(side+.5f)*Mathf.PI/6;
            var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)); var tangent=new Vector3(-radial.z,0,radial.x);
            Vector3 P(float u,float y,float depth) => center+tangent*u+Vector3.up*y+radial*depth;
            var v=new List<Vector3>(); var t=new List<int>();
            void Quad(Vector3 p0,Vector3 p1,Vector3 p2,Vector3 p3,Vector3 normal)
            {
                int n=v.Count; v.AddRange(new[]{p0,p1,p2,p3});
                if(Vector3.Dot(Vector3.Cross(p1-p0,p2-p0),normal)>0) t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
                else t.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
            }
            // Each face is built around a 0.7 x 1.4 m arched void, never overlaid with a dark decal.
            for(int layer=0;layer<2;layer++)
            {
                float d=layer==0?apothem:outer, half=d*Mathf.Tan(Mathf.PI/12); var normal=radial*(layer==0?-1:1);
                Quad(P(-half,8.6f,d),P(half,8.6f,d),P(half,9.7f,d),P(-half,9.7f,d),normal);
                Quad(P(-half,9.7f,d),P(-.35f,9.7f,d),P(-.35f,12.2f,d),P(-half,12.2f,d),normal);
                Quad(P(.35f,9.7f,d),P(half,9.7f,d),P(half,12.2f,d),P(.35f,12.2f,d),normal);
                for(int j=0;j<12;j++)
                {
                    float x0=-.35f*Mathf.Cos(j*Mathf.PI/12), x1=-.35f*Mathf.Cos((j+1)*Mathf.PI/12);
                    float y0=10.75f+.35f*Mathf.Sin(j*Mathf.PI/12),y1=10.75f+.35f*Mathf.Sin((j+1)*Mathf.PI/12);
                    Quad(P(x0,y0,d),P(x1,y1,d),P(x1,12.2f,d),P(x0,12.2f,d),normal);
                    if(layer==0) Quad(P(x0,y0,apothem),P(x1,y1,apothem),P(x1,y1,outer),P(x0,y0,outer),Vector3.down);
                }
            }
            Quad(P(-.35f,9.7f,apothem),P(.35f,9.7f,apothem),P(.35f,9.7f,outer),P(-.35f,9.7f,outer),Vector3.up);
            foreach(float x in new[]{-.35f,.35f}) Quad(P(x,9.7f,apothem),P(x,10.75f,apothem),P(x,10.75f,outer),P(x,9.7f,outer),tangent*-Mathf.Sign(x));
            float ih=3.2f*Mathf.Sin(Mathf.PI/12),oh=4.2f*Mathf.Sin(Mathf.PI/12);
            // Seat the underside 1 cm above the existing 8.6 m wing ceiling to avoid coplanar flicker.
            foreach(float y in new[]{8.61f,12.2f}) Quad(P(-ih,y,apothem),P(ih,y,apothem),P(oh,y,outer),P(-oh,y,outer),y<9?Vector3.down:Vector3.up);
            Surface(parent,"Interior_DrumWall_"+side,v.ToArray(),t.ToArray(),stone);
            float corner=side*Mathf.PI/6;
            var rib=Primitive(parent,"Interior_DrumRib_"+side,PrimitiveType.Cube,center+new Vector3(Mathf.Cos(corner)*3.16f,10.4f,Mathf.Sin(corner)*3.16f),new Vector3(.18f,3.6f,.18f),stone);
            rib.transform.rotation=Quaternion.Euler(0,-corner*Mathf.Rad2Deg,0);
            // Daylight lies beyond the full wall thickness; the passage itself remains empty.
            var glowCenter=P(0,10.4f,outer+.015f);
            Arch(parent,"Interior_DrumWindow_"+side,glowCenter,.7f,1.4f,cold);
            var glow=parent.Find("Interior_DrumWindow_"+side); glow.rotation=Quaternion.LookRotation(radial);
            glow.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            // Inward-facing emissive substitute; no additional real-time window light.
            var window=Primitive(parent,"Interior_DrumWindowFill_"+side,PrimitiveType.Quad,
                P(0,10.4f,apothem+.02f),new Vector3(.4f,.4f,1),windowFill);
            window.transform.rotation=Quaternion.LookRotation(radial);
            if(side%3==0)
            {
                Vector3 top=P(0,10.3f,apothem-.02f),bottom=top-radial*1.8f-Vector3.up*3.7f;
                var beam=Primitive(parent,"Interior_DrumShaft_"+(side/3),PrimitiveType.Quad,(top+bottom)/2,new Vector3(.32f,(top-bottom).magnitude,1),shaft);
                beam.transform.rotation=Quaternion.LookRotation(Vector3.Cross(tangent,top-bottom).normalized,(top-bottom).normalized);
            }
        }
        // Downward/inward winding; the final ring leaves a real 0.8 m diameter oculus.
        var coneV=new List<Vector3>(); var coneT=new List<int>(); const int segments=48;
        for(int i=0;i<segments;i++)
        {
            float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments; int n=coneV.Count;
            Vector3 C(float angle,float radius,float y) => center+new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius);
            coneV.AddRange(new[]{C(a,3.2f,12.2f),C(b,3.2f,12.2f),C(b,.4f,14.6f),C(a,.4f,14.6f)});
            coneT.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        Surface(parent,"Interior_DomeInner",coneV.ToArray(),coneT.ToArray(),dark);
        var oculus=Primitive(parent,"Interior_OculusSky",PrimitiveType.Quad,center+Vector3.up*14.65f,
            new Vector3(.84f,.84f,1),skyFill);
        oculus.transform.rotation=Quaternion.LookRotation(Vector3.up);
        Debug.Log("[DOME] drum_sides=12 drum_h=3.6 windows=12 ribs=12 cone_h=2.4 aperture=0.8 look_target=10.2");
    }

    static void BuildFloor(Transform parent,ChurchInterior interior)
    {
        var mats=new Material[15];
        for(int i=0;i<mats.Length;i++) mats[i]=Lit("M_InteriorSlab_"+i,(Color)new Color32(120,110,98,255)*(1+(i-7)*.01f),new Color(.11f,.10f,.085f));   // 自发光 0.32 让石板变纯白
        var aisle=Lit("M_InteriorAisle",new Color32(110,101,91,255),new Color(.09f,.08f,.07f));
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
        var wood=Lit("M_InteriorPewWood",new Color32(107,74,50,255),new Color(.06f,.05f,.04f));   // 木头上加 0.32 自发光 = 塑料感 wood.name="M_Wood"; EditorUtility.SetDirty(wood);
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
