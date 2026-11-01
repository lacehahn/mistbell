using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// HD-2D 美术验证场景：URP + Blender 教堂 + 像素 Billboard 角色 + 后期栈（Bloom/DoF/调色/暗角/颗粒）
/// 用法：Unity -batchmode -quit -projectPath . -executeMethod SpikeScene.Run -logFile out.log
/// 机位表写进场景里的 ShotRig；正式证据由播放器 `-shots &lt;dir&gt;` 产出（编辑态的 cam.Render() 不跑 Volume）。
/// </summary>
public static class SpikeScene
{
    const int W = 1920, H = 1080;
    const float Fov = 35f;            // 揭示机位垂直视场
    const float SlopeDeg = 6f;        // 坡地倾角

    public static void Run()
    {
        if (GraphicsSettings.currentRenderPipeline == null)
        {
            Debug.LogError("[SPIKE] 当前不是 URP，先跑 SpikeURP.Run");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---------- 材质（程序化像素贴图 + 法线图；UV 以米计，tiling 按每张贴图覆盖的米数反推）----------
        const string TEX = "Assets/Art/Textures/";
        var stone = ChurchMaterial("stone", "#C9A87C");
        stone.SetTextureScale("_BaseMap", new Vector2(1f / 4.8f, 1f / 4.8f));
        stone.SetTextureScale("_BumpMap", new Vector2(1f / 4.8f, 1f / 4.8f));
        stone.SetFloat("_BumpScale", 1.8f);
        Debug.Log("[SPIKE] stone bump=" + stone.GetFloat("_BumpScale").ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        var stoneTiling = stone.GetTextureScale("_BaseMap");
        Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "[SPIKE] stone tiling=({0:F8}, {1:F8})", (double)stoneTiling.x, (double)stoneTiling.y));
        // 固定曝光补偿：原石材 tint × (目标反照率 0.30 / 原目标 0.36)，重复生成不累乘。
        stone.SetColor("_BaseColor", new Color(0.675f, 0.63f, 0.575f, 1f));
        var roofMat = ChurchMaterial("roof", "#A06B4A");
        // 屋顶：贴图本身已是深石板冷灰（meanRGB 75,81,89）；_BaseColor 是乘性归一系数，
        // 必须写死在代码里——Assets/Settings/M_Roof.mat 被 .gitignore 忽略，写进 .mat 等于不可复现。
        roofMat.SetColor("_BaseColor", new Color(0.50f, 0.47f, 0.43f, 1f));
        var wood = ChurchMaterial("wood", "#6B4A32");
        var ground = MakeLit("Assets/Settings/M_Ground.mat", Hex("#DCE6EE"), 0f, 0.05f);

        EditorUtility.SetDirty(stone);
        EditorUtility.SetDirty(roofMat);
        EditorUtility.SetDirty(ground);

        // ---------- 地形：近景保留 6° 坡面，远景下沉成谷 ----------
        var terrainCollider = BuildTerrain(MakeGroundHaze(ground));

        Physics.SyncTransforms();
        BuildMountains();
        BuildMist();
        BuildTerrainEdgeMist();
        BuildStonePath(terrainCollider, stone);
        BuildProps(terrainCollider, stone, wood);

        // ---------- Blender FBX：保留导出时的米制比例与原点 ----------
        var church = ImportChurch(stone, roofMat);
        BuildWarmDetails();
        var cameraPosition = EyeAt(28f, -100f);
        var cameraRotation = Quaternion.LookRotation(new Vector3(0f, 13f, 0f) - cameraPosition);

        // ---------- 像素角色（Billboard）----------
        var sprite = ImportSprite("Assets/Art/Character/pilgrim.png");
        var bbShader = Shader.Find("Mistbell/PixelBillboard");
        if (bbShader == null)
        {
            Debug.LogError("[SPIKE] 找不到 Mistbell/PixelBillboard 着色器，检查 Assets/Art/Shaders/PixelBillboard.shader 是否编译通过");
            return;
        }

        // 角色在相机视轴前方 20 m 的地面上；落点只查询地形碰撞体。
        var pilgrimPos = cameraPosition + cameraRotation * Vector3.forward * 20f;
        Physics.SyncTransforms();
        float terrainY = 0f;
        if (terrainCollider.Raycast(
                new Ray(new Vector3(pilgrimPos.x, 500f, pilgrimPos.z), Vector3.down),
                out RaycastHit terrainHit, 1000f))
            terrainY = terrainHit.point.y;
        else
            throw new System.InvalidOperationException("Pilgrim ray missed terrain collider");
        pilgrimPos.y = terrainY;
        Debug.Log($"[SPIKE] pilgrim y={pilgrimPos.y:F4} terrain y={terrainY:F4}");

        var bbMat = new Material(bbShader) { name = "M_Pilgrim" };
        bbMat.SetTexture("_MainTex", sprite != null ? sprite.texture : null);
        bbMat.SetFloat("_ScaleX", 1.0f);
        bbMat.SetFloat("_ScaleY", 1.75f);
        bbMat.SetFloat("_PivotY", 0f);
        bbMat.SetFloat("_Cutoff", 0.5f);
        bbMat.mainTextureScale = new Vector2(1f / 3f, 1f / 4f);      // 图集 3 列 × 4 行
        bbMat.mainTextureOffset = new Vector2(0f, 0.75f);            // 顶行 = down
        AssetDatabase.DeleteAsset("Assets/Settings/M_Pilgrim.mat");
        AssetDatabase.CreateAsset(bbMat, "Assets/Settings/M_Pilgrim.mat");

        var pilgrim = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pilgrim.name = "Pilgrim";
        UnityEngine.Object.DestroyImmediate(pilgrim.GetComponent<Collider>());
        pilgrim.transform.position = pilgrimPos;
        pilgrim.GetComponent<MeshRenderer>().sharedMaterial = bbMat;
        pilgrim.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;

        // ---------- 光与雾（曝光整体提亮：HD-2D 要能看清像素细节）----------
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.6f;
        sun.color = Hex("#FFD9A8");
        // Source azimuth -40° from the front (-Z), elevation 20°; light forward is the incoming ray.
        var source = new Vector3(Mathf.Sin(-40f * Mathf.Deg2Rad) * Mathf.Cos(20f * Mathf.Deg2Rad),
            Mathf.Sin(20f * Mathf.Deg2Rad), -Mathf.Cos(-40f * Mathf.Deg2Rad) * Mathf.Cos(20f * Mathf.Deg2Rad));
        sun.transform.rotation = Quaternion.LookRotation(-source);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        sun.shadowBias = 0.025f;
        sun.shadowNormalBias = 0.15f;
        var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        var rendererData=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/MistbellRenderer.asset");
        rendererData.postProcessData=AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
        EditorUtility.SetDirty(rendererData);
        var pipelineSettings = new SerializedObject(pipeline);
        pipelineSettings.FindProperty("m_MainLightShadowsSupported").boolValue = true;
        pipelineSettings.FindProperty("m_SoftShadowsSupported").boolValue = true;
        pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
        pipeline.mainLightShadowmapResolution = 2048;
        pipeline.shadowDistance = 120f;
        pipeline.shadowCascadeCount = 4;
        EditorUtility.SetDirty(pipeline);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Hex("#849DBB");
        RenderSettings.ambientEquatorColor = Hex("#637D99");
        RenderSettings.ambientGroundColor = Hex("#444E64");
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Hex("#DCE2E6");
        RenderSettings.fogStartDistance = 180f;
        RenderSettings.fogEndDistance = 700f;
        Debug.Log("[SCENE] sun sourceAzimuth=-40 elevation=20 intensity=1.6 color=#FFD9A8 shadows=120 resolution=2048 soft=true fog=180..700");

        // ---------- 后期 Volume ----------
        var profile = BuildProfile("Assets/Settings/MistbellPost.asset");
        var volGo = new GameObject("Global Volume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1f;
        vol.sharedProfile = profile;

        // ---------- 相机 ----------
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        cam.fieldOfView = Fov;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1500f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.48f, 0.59f, 0.71f);
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

        // ---------- 机位表：几何算过，保证塔顶不切、基座留出前景地面 ----------
        var rigGo = new GameObject("ShotRig");
        var rig = rigGo.AddComponent<ShotRig>();

        rig.poses.Add(ShotRig.Look("art_runtime", cameraPosition, new Vector3(0f, 13f, 0f), Fov));
        // Ground-level close shot: 8.5m before the terrace, 8 degrees upward.
        // At this 6-degree slope a 3m aim point would require ~12 degrees;
        // preserve the requested eye position/pitch and aim at the lower doorway.
        var closeEye = EyeAt(6f, -21.5f);
        var closeAim = new Vector3(0f, 0f, -7.4f);
        closeAim.y = closeEye.y + Mathf.Tan(8f * Mathf.Deg2Rad) *
            Vector2.Distance(new Vector2(closeEye.x, closeEye.z), new Vector2(closeAim.x, closeAim.z));
        rig.poses.Add(ShotRig.Look("art_close", closeEye, closeAim, 36f));
        var groundPose = ShotRig.Look("art_ground", EyeAt(6f, -23f), new Vector3(0f, GroundY(-20f), -20f), 36f);
        rig.poses.Add(groundPose);

        rig.Apply(0);

        Directory.CreateDirectory("Assets/Scenes");
        const string scenePath = "Assets/Scenes/ArtSpike.unity";

        var shotter = new GameObject("Screenshotter");
        shotter.AddComponent<Screenshotter>();

        // Spike 直接进入场景，避免 macOS 无人值守验收停在启动画面的渲染等待。
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.MTRendering = true;
        PlayerSettings.graphicsJobs = false; // Metal uses a dedicated render thread; no native graphics jobs.
        PlayerSettings.defaultScreenWidth = W;
        PlayerSettings.defaultScreenHeight = H;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = false;
        PlayerSettings.runInBackground = true;

        LogFrame(cam, church, pilgrimPos);
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

        Directory.CreateDirectory("Shots");
        Capture(cam, "Shots/art_reveal_edit.png");
        rig.Apply(1);
        Capture(cam, "Shots/art_close_edit.png");
        rig.Apply(0);

        Debug.Log("[SPIKE] EDIT-TIME CAPTURES DONE（正式证据用播放器 -shots 产出）");
        Debug.Log("[SPIKE] ART DONE");
    }

    static Material MakeGroundHaze(Material ground)
    {
        var shader = Shader.Find("Mistbell/TerrainHaze");
        if (shader == null) throw new System.InvalidOperationException("Missing Mistbell/TerrainHaze");
        var material = new Material(ground) { name = "M_GroundHaze", shader = shader };
        material.SetTexture("_BaseMap", ground.GetTexture("_BaseMap"));
        material.SetTextureScale("_BaseMap", ground.GetTextureScale("_BaseMap"));
        material.SetTextureOffset("_BaseMap", ground.GetTextureOffset("_BaseMap"));
        material.SetColor("_BaseColor", ground.GetColor("_BaseColor"));
        material.SetTexture("_NormalMap", ground.GetTexture("_BumpMap"));
        material.SetFloat("_NormalScale", ground.GetFloat("_BumpScale"));
        if (ground.GetTexture("_BumpMap") != null) material.EnableKeyword("_NORMALMAP");
        else material.DisableKeyword("_NORMALMAP");
        material.SetColor("_HazeColor", Hex("#DCE2E6"));
        material.SetFloat("_HazeStart", 140f);
        material.SetFloat("_HazeEnd", 300f);
        material.SetFloat("_HazePower", 1.4f);
        material.SetFloat("_HazeStrength", 0.45f);
        const string path = "Assets/Settings/M_GroundHaze.mat";
        var saved = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (saved == null) { AssetDatabase.CreateAsset(material, path); saved = material; }
        else { EditorUtility.CopySerialized(material, saved); UnityEngine.Object.DestroyImmediate(material); }
        EditorUtility.SetDirty(saved);
        Debug.Log("[SPIKE] terrain haze shader=TerrainHaze start=140 end=300 strength=0.45 color=#DCE2E6");
        return saved;
    }

    static Material ChurchMaterial(string category, string fallbackColor)
    {
        string path = "Assets/Settings/M_" + char.ToUpperInvariant(category[0]) + category.Substring(1) + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Settings" }))
        {
            var candidate = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (candidate.name.ToLowerInvariant().Contains(category)) return candidate;
        }
        return MakeLit(path, Hex(fallbackColor), 0f, 0.15f);
    }

    static Transform ImportChurch(Material stone, Material roof)
    {
        const string path = "Assets/Art/Church/gergeti_church.fbx";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new System.InvalidOperationException("Cannot import " + path);
        var church = ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).transform;
        church.name = "Church";
        church.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        church.localScale = Vector3.one;
        int stoneCount = 0, roofCount = 0, submeshes = 0;
        long triangles = 0;
        foreach (var filter in church.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            if (mesh == null) continue;
            var renderer = filter.GetComponent<Renderer>();
            if (renderer == null) throw new System.InvalidOperationException("Church mesh has no renderer: " + filter.name);
            var materials = new Material[mesh.subMeshCount];
            var original = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string sourceName = i < original.Length && original[i] != null ? original[i].name : "";
                string names = (filter.name + " " + mesh.name + " " + sourceName).ToLowerInvariant();
                if (names.Contains("roof")) { materials[i] = roof; roofCount++; }
                else { materials[i] = stone; stoneCount++; }
                long submeshTriangles = (long)mesh.GetIndexCount(i) / 3;
                triangles += submeshTriangles;
                submeshes++;
                Debug.Log($"[SPIKE] church submesh object={filter.name} mesh={mesh.name} index={i} source={sourceName} material={materials[i].name} tris={submeshTriangles}");
            }
            renderer.sharedMaterials = materials;
        }
        var bounds = ChurchBounds(church);
        Debug.Log($"[SPIKE] church v2 imported tris={triangles} bounds={bounds.size.x:F2}x{bounds.size.y:F2}x{bounds.size.z:F2} submeshes={submeshes}");
        Debug.Log($"[SPIKE] church bounds min={bounds.min} max={bounds.max} root scale={church.localScale} rotation={church.eulerAngles}");
        Debug.Log($"[SPIKE] church materials stone={stoneCount} roof={roofCount} (submesh assignments; unclassified=stone)");
        if (triangles < 70000 || triangles > 78000 || Mathf.Abs(bounds.size.x - 28f) > 2f || Mathf.Abs(bounds.size.y - 24.5f) > 2f)
            throw new System.InvalidOperationException("Church v2 outside triangle/bounds acceptance limits");
        return church;
    }

    static void BuildWarmDetails()
    {
        var root = new GameObject("Winter Warm Lights").transform;
        var glow = MakeLit("Assets/Settings/M_WindowGlow.mat", Hex("#FFAA42"), 0f, 0f);
        glow.EnableKeyword("_EMISSION");
        glow.SetColor("_EmissionColor", new Color(4.5f, 1.5f, .15f));
        void Window(string name, Vector3 position, float width, float height, float yaw)
        {
            var vs = new System.Collections.Generic.List<Vector3> { Vector3.zero };
            vs.Add(new Vector3(-width/2,0,0)); vs.Add(new Vector3(width/2,0,0));
            for (int i=0;i<=16;i++)
            { float a=i*Mathf.PI/16; vs.Add(new Vector3(Mathf.Cos(a)*width/2,height-width/2+Mathf.Sin(a)*width/2,0)); }
            var ts = new System.Collections.Generic.List<int>();
            for(int i=2;i<vs.Count-1;i++) ts.AddRange(new[]{1,i+1,i});
            var mat = new Material(glow);
            var tr=SaveMountainMesh(root,name,vs.ToArray(),ts.ToArray(),mat);
            tr.position=position; tr.rotation=Quaternion.Euler(0,yaw,0);
            tr.GetComponent<Renderer>().sharedMaterial.SetFloat("_Cull",0);
            EditorUtility.SetDirty(tr.GetComponent<Renderer>().sharedMaterial);
        }
        Window("WarmWindow_FrontL",new Vector3(-5,5.43f,-8.24f),.68f,2.5f,0);
        Window("WarmWindow_FrontR",new Vector3(1,5.43f,-8.24f),.68f,2.5f,0);
        for(int i=0;i<5;i++)
        {
            float a=(210+i*30)*Mathf.Deg2Rad;
            var pos=new Vector3(-2+Mathf.Cos(a)*4.38f,13.13f,Mathf.Sin(a)*4.38f);
            Window("WarmWindow_Drum"+i,pos,.58f,2.38f,90f-(210+i*30));
        }
        var pedestal=MakeLit("Assets/Settings/M_BrazierStone.mat",Hex("#4C5360"),0,0);
        for(int i=0;i<2;i++)
        {
            var p=new Vector3(i==0?-6f:3f,0,-14f); p.y=TerrainHeight(p.x,p.z);
            SpikeArt.Box(root,"BrazierStone_"+i,new Vector3(.85f,1.1f,.85f),p+Vector3.up*.55f,pedestal);
            var fire=GameObject.CreatePrimitive(PrimitiveType.Sphere); fire.name="BrazierEmber_"+i;
            fire.transform.SetParent(root); fire.transform.position=p+Vector3.up*1.25f;
            fire.transform.localScale=new Vector3(.65f,.8f,.65f); fire.GetComponent<Renderer>().sharedMaterial=glow;
            var light=new GameObject("BrazierPoint_"+i).AddComponent<Light>();
            light.transform.position=p+Vector3.up*.65f; light.transform.SetParent(root);
            light.type=LightType.Point; light.color=Hex("#FFB45B"); light.range=24; light.intensity=24;
            light.renderMode=LightRenderMode.ForcePixel;
            var ps=new GameObject("BrazierFlames_"+i).AddComponent<ParticleSystem>();
            ps.transform.SetParent(root); ps.transform.position=p+Vector3.up*1.35f;
            var main=ps.main; main.startLifetime=.8f; main.startSpeed=.9f; main.startSize=.22f;
            main.startColor=Hex("#FFB44D"); main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission; emission.rateOverTime=16;
            var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Cone; shape.radius=.2f; shape.angle=12;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=glow;
        }
        Debug.Log("[SCENE] arched emissive inserts=7 braziers=2 particles=2");
    }

    static Bounds ChurchBounds(Transform church)
    {
        var renderers = church.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new System.InvalidOperationException("Church has no meshes");
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static void LogFrame(Camera cam,Transform church,Vector3 pilgrim)
    {
        cam.aspect=(float)W/H;
        var renderers=church.GetComponentsInChildren<Renderer>();
        var bounds=renderers[0].bounds;
        foreach(var r in renderers) bounds.Encapsulate(r.bounds);
        float left=float.PositiveInfinity,right=float.NegativeInfinity;
        float low=float.PositiveInfinity,high=float.NegativeInfinity;
        for(int i=0;i<8;i++)
        {
            var p=cam.WorldToViewportPoint(bounds.center+Vector3.Scale(bounds.extents,
                new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
            left=Mathf.Min(left,p.x); right=Mathf.Max(right,p.x);
            low=Mathf.Min(low,p.y); high=Mathf.Max(high,p.y);
        }
        Debug.Log($"[SPIKE] proj church_top py={(1-high)*H:F1} church_bottom py={(1-low)*H:F1} (占比 {(high-low)*100:F1}%)");
        Debug.Log($"[SPIKE] frame church top={(1-high)*H:F1} bottom={(1-low)*H:F1} heightPct={(high-low)*100:F1} widthPct={(right-left)*100:F1} centerX={(left+right)*.5f*W:F1}");
        var porchBounds = new Bounds();
        bool hasPorch = false;
        foreach (var r in renderers)
        {
            // v2 结构的 FBX 里门廊叫 Portal_Layer_0..2（旧件叫 Porch）；两者都算门廊。
            var rendererName = r.name.ToLowerInvariant();
            if (!rendererName.Contains("porch") && !rendererName.Contains("portal")) continue;
            if (!hasPorch) { porchBounds = r.bounds; hasPorch = true; }
            else porchBounds.Encapsulate(r.bounds);
        }
        if (hasPorch)
        {
        float porchLeft=float.PositiveInfinity, porchRight=float.NegativeInfinity;
        float porchLow=float.PositiveInfinity, porchHigh=float.NegativeInfinity;
        for (int i=0;i<8;i++)
        {
            var p=cam.WorldToViewportPoint(porchBounds.center+Vector3.Scale(porchBounds.extents,
                new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
            porchLeft=Mathf.Min(porchLeft,p.x); porchRight=Mathf.Max(porchRight,p.x);
            porchLow=Mathf.Min(porchLow,p.y); porchHigh=Mathf.Max(porchHigh,p.y);
        }
        Debug.Log($"[SPIKE] frame porch heightPct={(porchHigh-porchLow)*100:F1} centerX={(porchLeft+porchRight)*.5f*W:F1}");
        }
        else Debug.Log("[SPIKE] frame porch unavailable: church v2 porch geometry merged into Stone");
        var feet=cam.WorldToViewportPoint(pilgrim); var head=cam.WorldToViewportPoint(pilgrim+Vector3.up*1.75f);
        Debug.Log($"[SPIKE] proj pilgrim px_height={(head.y-feet.y)*H:F1}");
        Debug.Log($"[SPIKE] pilgrim viewport feet={feet} head={head}");
        Debug.Log($"[SPIKE] frame pilgrim heightPct={(head.y-feet.y)*100:F1}");
        Debug.Log($"[SPIKE] frame pitch={Mathf.Asin(Mathf.Abs(cam.transform.forward.y))*Mathf.Rad2Deg:F1}");
        for(int layer=1;layer<=3;layer++)
        {
            var ridge=GameObject.Find("Mountain_"+layer).transform;
            var verts=ridge.GetComponent<MeshFilter>().sharedMesh.vertices;
            var peak=ridge.TransformPoint(verts[1]);
            for(int i=3;i<verts.Length;i+=2)
            {
                var p=ridge.TransformPoint(verts[i]); if(p.y>peak.y) peak=p;
            }
            Debug.Log($"[SPIKE] frame ridge{layer} linePct={(1-cam.WorldToViewportPoint(peak).y)*100:F1}");
        }
    }

    static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString(value, out Color color);
        return color;
    }

    static float TerrainHeight(float x, float z)
    {
        float crestZ = 28f + 10f * Mathf.Sin(x / 38f);
        float height = z <= crestZ ? GroundY(z)
            : GroundY(crestZ) - 0.32f * (z - crestZ);
        return height
            + 7f * Mathf.Sin(x / 37f + 0.9f)
                * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 50f, z))
            + 3f * Mathf.Sin(x / 13f - 1.1f)
                * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 40f, z));
    }

    static MeshCollider BuildTerrain(Material ground)
    {
        const int xCells = 150; // x=-150..150，每 2 m 一列。
        var rows = new System.Collections.Generic.List<float>();
        for (int z = -160; z < -100; z += 10) rows.Add(z);
        for (int z = -100; z <= 100; z += 2) rows.Add(z);
        for (int z = 110; z <= 560; z += 10) rows.Add(z);
        int columns = xCells + 1;
        int triangleCount = xCells * (rows.Count - 1) * 2; // 150 * 152 * 2 = 45600 < 60000。
        if (triangleCount >= 60000)
            throw new System.InvalidOperationException("Terrain triangle budget exceeded");
        var vertices = new Vector3[columns * rows.Count];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[triangleCount * 3];
        for (int row = 0; row < rows.Count; row++)
        {
            float z = rows[row];
            for (int col = 0; col < columns; col++)
            {
                float x = -150f + col * 2f;
                int v = row * columns + col;
                vertices[v] = new Vector3(x, TerrainHeight(x, z), z);
                uv[v] = new Vector2(x, z); // 世界米 UV；对象保持单位变换。
                if (row == rows.Count - 1 || col == xCells) continue;
                int t = (row * xCells + col) * 6;
                triangles[t] = v;
                triangles[t + 1] = v + columns;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + columns;
                triangles[t + 5] = v + columns + 1;
            }
        }
        var mesh = new Mesh { name = "Terrain", vertices = vertices, uv = uv, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        // 场景引用持久化网格资产，避免保存/重开场景后丢失程序化网格。
        const string meshPath = "Assets/Settings/Terrain.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        var terrain = new GameObject("Terrain");
        terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
        terrain.AddComponent<MeshRenderer>().sharedMaterial = ground;
        var collider = terrain.AddComponent<MeshCollider>();
        collider.convex = false;
        collider.sharedMesh = mesh;
        Debug.Log($"[SPIKE] terrain mesh verts={vertices.Length} tris={triangleCount} extent=x150 z-160..560");
        // 直接读取 x=0 的网格顶点（含横向起伏），而非简报中的近似参考值。
        float z100 = vertices[rows.IndexOf(100f) * columns + xCells / 2].y;
        float z200 = vertices[rows.IndexOf(200f) * columns + xCells / 2].y;
        float z400 = vertices[rows.IndexOf(400f) * columns + xCells / 2].y;
        float z560 = vertices[rows.IndexOf(560f) * columns + xCells / 2].y;
        Debug.Log($"[SPIKE] terrain edge sample z100={z100:F4} z200={z200:F4} z400={z400:F4} z560={z560:F4}");
        return collider;
    }

    static void BuildTerrainEdgeMist()
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_Mist_TerrainEdge" };
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/mist.png"));
        var tint = Hex("#DCE2E6");
        tint.a = 0.45f;
        mat.SetColor("_BaseColor", tint);
        mat.SetTextureScale("_BaseMap", new Vector2(6f, 1f));
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        // 水平 900×220 m，中心 z=95，落在地形上方 5 m。
        var vertices = new[] { new Vector3(-450f, 0f, -110f), new Vector3(450f, 0f, -110f),
                               new Vector3(-450f, 0f, 110f), new Vector3(450f, 0f, 110f) };
        var band = SaveMountainMesh(null, "Mist_TerrainEdge", vertices, new[] { 0, 2, 1, 1, 2, 3 }, mat);
        var mesh = band.GetComponent<MeshFilter>().sharedMesh;
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        EditorUtility.SetDirty(mesh);
        band.position = new Vector3(0f, TerrainHeight(0f, 95f) + 5f, 95f);
    }

    // 沿揭示机位视轴横向展开；指定色板与远距薄雾共同形成层次。
    static void BuildMountains()
    {
        var root = new GameObject("Mountain Ridges").transform;
        // Closed 3D ridges: front flank, broken crest, rear flank and end caps.
        for (int layer = 0; layer < 3; layer++)
        {
            const int n = 120, rows = 14;
            float distance = 345f + layer * 190f;
            var peakHeights = new float[n];
            var vertices = new Vector3[n * rows];
            var indices = new System.Collections.Generic.List<int>();
            for (int r = 0; r < rows; r++)
            for (int i = 0; i < n; i++)
            {
                float x = -700f + i * 1400f / (n - 1);
                float peak = 130f + layer * 60f + 35f * Mathf.Sin(x * .011f + layer * 2)
                    + 20f * Mathf.Sin(x * .027f + layer) + 10f * Mathf.Cos(x * .069f)
                    + 4f * Mathf.Sin(x * .151f + layer);
                peakHeights[i] = peak;
                float t = r / (float)(rows - 1);
                float h = -10f + (peak + 10f) * Mathf.Pow(1f - Mathf.Abs(t * 2f - 1f), .85f);
                float z = distance + (t - .5f) * 170f + 9f * Mathf.Sin(x * .043f) * Mathf.Sin(t * Mathf.PI);
                vertices[r * n + i] = new Vector3(x, h, z);
                if (r < rows - 1 && i < n - 1)
                {
                    int v = r * n + i;
                    indices.AddRange(new[] { v, v+n, v+1, v+1, v+n, v+n+1 });
                }
            }
            for (int r = 0; r < rows-1; r++)
            {
                indices.AddRange(new[] { 0, (r+1)*n, r*n, n-1, r*n+n-1, (r+1)*n+n-1 });
            }
            indices.AddRange(new[] { 0, n-1, (rows-1)*n, n-1, rows*n-1, (rows-1)*n });
            // Share normals across the dense flank grid, retain a small facet component.
            var smoothMesh = new Mesh { vertices = vertices, triangles = indices.ToArray() };
            smoothMesh.RecalculateNormals();
            var smoothNormals = smoothMesh.normals;
            UnityEngine.Object.DestroyImmediate(smoothMesh);
            var flat = new Vector3[indices.Count];
            var tris = new int[indices.Count];
            for (int i = 0; i < flat.Length; i++) { flat[i] = vertices[indices[i]]; tris[i] = i; }
            var mat = new Material(Shader.Find("Mistbell/RidgeFog"));
            mat.SetColor("_ColorBottom", Hex("#A8A29A"));
            mat.SetColor("_RockShade", Hex("#5B6470"));
            mat.SetColor("_SnowColor", Hex("#EAF1F7"));
            mat.SetColor("_SnowShade", Hex("#B9C8D4"));
            mat.SetColor("_AirColor", Hex("#7B97B4"));
            // Local height fraction follows each irregular crest, measured from the -10m foot.
            mat.SetFloat("_SnowHeight", .72f);
            mat.SetFloat("_SnowSoftness", .025f);
            mat.SetFloat("_FogFloor", 0f);
            mat.SetFloat("_Aerial", new[] { .25f, .50f, .78f }[layer]);
            var ridge = SaveMountainMesh(root, "Mountain_" + (layer+1), flat, tris, mat);
            var ridgeMesh = ridge.GetComponent<MeshFilter>().sharedMesh;
            var normals = ridgeMesh.normals;
            for (int i = 0; i < normals.Length; i++)
                normals[i] = Vector3.Slerp(normals[i], smoothNormals[indices[i]], .88f).normalized;
            ridgeMesh.normals = normals;
            var heights = new Vector2[flat.Length];
            for (int i = 0; i < flat.Length; i++)
            {
                int column = indices[i] % n;
                heights[i] = new Vector2((flat[i].y + 10f) / (peakHeights[column] + 10f), 0f);
            }
            ridge.GetComponent<MeshFilter>().sharedMesh.uv = heights;
            EditorUtility.SetDirty(ridge.GetComponent<MeshFilter>().sharedMesh);
        }
        Debug.Log("[SCENE] mountains=3 grid=120x14 snowline=.72 aerial=.25/.50/.78 rock=#A8A29A/#5B6470 snow=#EAF1F7/#B9C8D4");
    }

    static Transform SaveMountainMesh(Transform parent, string name, Vector3[] vertices,
                                      int[] triangles, Material mat)
    {
        var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        string meshPath = "Assets/Settings/" + name + ".asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        string matPath = "Assets/Settings/M_" + name + ".mat";
        AssetDatabase.DeleteAsset(matPath);
        AssetDatabase.CreateAsset(mat, matPath);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.transform;
    }

    static void BuildMist()
    {
        const string texturePath = "Assets/Art/Textures/mist.png";
        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null) throw new System.InvalidOperationException("Generate mist.png first");
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        var root = new GameObject("Mist Bands").transform;
        var eye = EyeAt(28f, -100f);
        var view = Quaternion.LookRotation(new Vector3(0f,13f,0f)-eye);
        float[] depths = { 300f, 370f };
        float[] screenYs = { .34f, .43f };
        for (int i = 0; i < 2; i++)
        {
            var mat = new Material(Shader.Find("Mistbell/RidgeFog")) { name = "M_Mist_" + i };
            mat.SetTexture("_NoiseTex", texture);
            mat.SetFloat("_CloudBand", 1f);
            mat.SetFloat("_DepthWrite", 0f);
            mat.SetFloat("_StencilComp", (float)CompareFunction.Equal);
            mat.SetFloat("_StencilPass", (float)StencilOp.Keep);
            var tint = Hex("#B5C1CD");
            tint.a = i == 0 ? .92f : .82f;
            mat.SetColor("_CloudColor", tint);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            string name = "Mist_" + (i == 0 ? "Near" : "Far");
            // 正对主视角的水平云带，透明纹理的上下软边避免山天交界硬线。
            var mesh = new Mesh { name = name };
            float halfHeight = i == 0 ? 12f : 14f;
            mesh.vertices = new[] { new Vector3(-800f, -halfHeight, 0f), new Vector3(800f, -halfHeight, 0f),
                                    new Vector3(-800f, halfHeight, 0f), new Vector3(800f, halfHeight, 0f) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateBounds();
            var band = SaveMountainMesh(root, name, mesh.vertices, mesh.triangles, mat);
            band.GetComponent<MeshFilter>().sharedMesh.uv = mesh.uv;
            EditorUtility.SetDirty(band.GetComponent<MeshFilter>().sharedMesh);
            UnityEngine.Object.DestroyImmediate(mesh);
            band.rotation = view;
            band.position = eye + view * new Vector3(0f,
                depths[i] * Mathf.Tan(Fov * .5f * Mathf.Deg2Rad) * (1f-2f*screenYs[i]), depths[i]);
        }
        Debug.Log("[SPIKE] mist screenY=.34/.43 bands=2 texture-alpha stencil=mountains-only");
    }

    static void BuildStonePath(MeshCollider terrainCollider, Material stone)
    {
        // 12 段 S 形石板路，自下坡连接正面台阶。
        var points = new Vector3[13];
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / 12f;
            points[i] = new Vector3(3f * Mathf.Sin(t * 2f * Mathf.PI), 0f, Mathf.Lerp(-70f, -13.25f, t));
        }
        for (int i = 0; i < points.Length; i++)
            if (terrainCollider.Raycast(new Ray(points[i] + Vector3.up * 100f, Vector3.down),
                                out RaycastHit hit, 200f))
                points[i].y = hit.point.y + 0.035f;
        var root = new GameObject("Stone Path").transform;
        for (int i = 0; i < points.Length - 1; i++)
        {
            var delta = points[i + 1] - points[i];
            var segment = SpikeArt.Box(root, "Paving_" + (i + 1),
                new Vector3(3.2f, 0.07f, delta.magnitude + 0.08f),
                (points[i] + points[i + 1]) * 0.5f - Vector3.up * 0.035f, stone);
            segment.transform.rotation = Quaternion.LookRotation(delta, terrainCollider.transform.up);
        }
        Debug.Log($"[SPIKE] stone path segments={points.Length - 1} material=existing M_Stone (stone_ashlar; no Substance material available)");
    }

    static bool ArgFlag(string name) =>
        System.Array.IndexOf(System.Environment.GetCommandLineArgs(), name) >= 0;

    static void BuildProps(MeshCollider terrain, Material stone, Material wood)
    {
        var root = new GameObject("Props").transform;
        var roadside = new GameObject("Roadside Props").transform;
        roadside.SetParent(root, false);
        var rockMat = MakeLit("Assets/Settings/M_WinterRock.mat", Hex("#8E877D"), 0f, .05f);
        // Local rock skylight fill keeps back faces readable under the low sun.
        rockMat.EnableKeyword("_EMISSION");
        rockMat.SetColor("_EmissionColor", new Color(.065f,.075f,.085f));
        EditorUtility.SetDirty(rockMat);
        var snowMat = MakeLit("Assets/Settings/M_RockSnow.mat", Hex("#DCE6EE"), 0f, .05f);
        var fenceWood = new Material(wood) { name = "M_FenceWood" };
        fenceWood.SetColor("_BaseColor", Hex("#827767"));
        AssetDatabase.DeleteAsset("Assets/Settings/M_FenceWood.mat");
        AssetDatabase.CreateAsset(fenceWood, "Assets/Settings/M_FenceWood.mat");
        var rnd = new System.Random(20261102);
        float Range(float a, float b) => Mathf.Lerp(a, b, (float)rnd.NextDouble());
        Vector3 Snap(Vector3 p)
        {
            if (!terrain.Raycast(new Ray(new Vector3(p.x, 300f, p.z), Vector3.down), out var hit, 700f))
                throw new System.InvalidOperationException("Prop ray missed terrain: " + p);
            return hit.point;
        }
        var meshes = new Mesh[6];
        for (int i = 0; i < meshes.Length; i++)
            meshes[i] = SpikeArt.Blob(Vector3.one, .20f, 1, true, 7200 + i);
        var centers = new System.Collections.Generic.List<Vector2>();
        // Two foreground groups, followed by irregularly spaced groups across the slope.
        var eye = EyeAt(28f, -100f);
        var forward = (new Vector3(0f, 13f, 0f) - eye).normalized;
        var right = new Vector3(forward.z, 0f, -forward.x).normalized;
        for (int side = -1; side <= 1; side += 2)
        {
            var p = eye + forward * 23f + right * (side * 7.5f);
            centers.Add(new Vector2(p.x, p.z));
        }
        for (int attempt = 0; attempt < 3000 && centers.Count < 18; attempt++)
        {
            var candidate = new Vector2(Range(-40f, 48f), Range(-90f, 24f));
            float nearest = float.PositiveInfinity;
            foreach (var center in centers) nearest = Mathf.Min(nearest, Vector2.Distance(center, candidate));
            if (nearest < Range(6f, 20f) || nearest > 20f) continue;
            if (Mathf.Abs(candidate.x) < 18f && candidate.y > -18f) continue;
            centers.Add(candidate);
        }
        int rocks = 0;
        foreach (var center in centers)
        {
            float radius = Range(1.2f, 2.0f);
            int count = rnd.Next(5, 8);
            for (int j = 0; j < count && rocks < 108; j++)
            {
                float angle = Range(0f, Mathf.PI * 2f), offset = radius * Mathf.Sqrt(Range(0f, 1f));
                var p = Snap(new Vector3(center.x + Mathf.Cos(angle) * offset, 0f, center.y + Mathf.Sin(angle) * offset));
                float size = Range(.95f, 1.9f);
                var rock = new GameObject("ClusterRock_" + rocks++);
                rock.transform.SetParent(root, false);
                rock.transform.position = p + Vector3.up * size * .18f;
                rock.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                rock.transform.localScale = new Vector3(size, size * Range(.55f, .85f), size * Range(.7f, 1f));
                var mesh = UnityEngine.Object.Instantiate(meshes[rnd.Next(meshes.Length)]);
                // Split the top faces into a snow submesh; no floating duplicate shell.
                var stoneTris = new System.Collections.Generic.List<int>();
                var snowTris = new System.Collections.Generic.List<int>();
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    var a = vertices[triangles[t]]; var b = vertices[triangles[t+1]]; var c = vertices[triangles[t+2]];
                    var target = Vector3.Cross(b-a, c-a).normalized.y > .45f ? snowTris : stoneTris;
                    target.AddRange(new[] {triangles[t], triangles[t+1], triangles[t+2]});
                }
                mesh.subMeshCount = 2;
                mesh.SetTriangles(stoneTris, 0); mesh.SetTriangles(snowTris, 1);
                string path = "Assets/Settings/ClusterRock_" + rocks + ".asset";
                AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
                rock.AddComponent<MeshFilter>().sharedMesh = mesh;
                rock.AddComponent<MeshRenderer>().sharedMaterials = new[] {rockMat, snowMat};
            }
        }
        Debug.Log($"[SPIKE] ground clusters={centers.Count} rocks={rocks} members=5..7 radius=1.2..2 spacing=6..20 size=.95..1.9 color=#8E877D snowCaps=true");
        Vector3 RoadPoint(float t, float offset) => new Vector3(3f * Mathf.Sin(t * 2f * Mathf.PI) + offset, 0f,
            Mathf.Lerp(-70f, -13.25f, t));
        int posts = 0, rails = 0;
        Vector3 previous = Vector3.zero;
        // 包含两端，32 段的 z 间隔 1.5625m，沿弯路实际柱距约 1.6m。
        for (int i = 0; i <= 32; i++)
        {
            float z = Mathf.Lerp(-66f, -16f, i / 32f);
            Vector3 p = Snap(RoadPoint((z + 70f) / 56.75f, -2.6f));
            SpikeArt.Box(roadside, "FencePost_" + posts++, new Vector3(0.12f, 0.95f, 0.12f),
                p + Vector3.up * (0.475f - 0.08f), fenceWood);
            if (i > 0)
                foreach (float height in new[] { 0.42f, 0.74f })
                {
                    Vector3 a = previous + Vector3.up * height, b = p + Vector3.up * height;
                    var delta = b - a;
                    var rail = SpikeArt.Box(roadside, "FenceRail_" + rails++,
                        new Vector3(0.08f, 0.06f, delta.magnitude), (a + b) * 0.5f, fenceWood);
                    rail.transform.rotation = Quaternion.LookRotation(delta);
                }
            previous = p;
        }
        Debug.Log($"[SPIKE] fence posts={posts} rails={rails}");
    }

    /// <summary>坡面解析式：6° 上坡，z=0 处地面 y=0。</summary>
    static float GroundY(float z) => Mathf.Tan(SlopeDeg * Mathf.Deg2Rad) * z;

    static Vector3 EyeAt(float x, float z) => new Vector3(x, GroundY(z) + 1.9f, z);

    static void LogPoint(Camera cam, string label, Vector3 world)
    {
        var vp = cam.WorldToViewportPoint(world);
        Debug.Log(string.Format("[SPIKE] proj {0} world=({1:F1},{2:F1},{3:F1}) viewport=({4:F3},{5:F3}) px=({6:F0},{7:F0}) depth={8:F1}",
            label, world.x, world.y, world.z, vp.x, vp.y, vp.x * W, (1f - vp.y) * H, vp.z));
    }

    static void LogBounds(Camera cam, Transform t, string label)
    {
        var r = t.GetComponent<Renderer>();
        if (r == null) return;
        var b = r.bounds;
        LogPoint(cam, label + ".min", new Vector3(b.min.x, b.min.y, b.min.z));
        LogPoint(cam, label + ".max", new Vector3(b.max.x, b.max.y, b.max.z));
        LogPoint(cam, label + ".center", b.center);
    }

    static void Capture(Camera cam, string path)
    {
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        var png = tex.EncodeToPNG();
        File.WriteAllBytes(Path.GetFullPath(path), png);
        Debug.Log("[SPIKE] shot " + path + " bytes=" + png.Length);
    }

    static Material MakeLit(string path, Color color, float metallic, float smoothness)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Sprite ImportSprite(string path)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ti = AssetImporter.GetAtPath(path) as TextureImporter;
        }
        if (ti == null)
        {
            Debug.LogError("[SPIKE] 无法导入 " + path);
            return null;
        }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 32f;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static VolumeProfile BuildProfile(string path)
    {
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var tonemap = profile.Add<Tonemapping>(true);
        tonemap.mode.Override(TonemappingMode.ACES);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.75f);
        bloom.scatter.Override(0.72f);
        bloom.tint.Override(new Color(1f, 0.95f, 0.88f));

        var dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(26f);
        dof.aperture.Override(2.8f);
        dof.focalLength.Override(50f);
        dof.bladeCount.Override(6);

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(-0.15f); // previous +0.20 minus 0.35 EV
        color.contrast.Override(14f);
        color.saturation.Override(-6f);
        color.colorFilter.Override(Color.white);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.16f);
        vignette.smoothness.Override(0.45f);

        var grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin2);
        grain.intensity.Override(0.18f);
        grain.response.Override(0.8f);

        foreach (var comp in profile.components)
        {
            comp.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(comp, profile);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return profile;
    }
}
