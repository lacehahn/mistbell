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

    static int contactDrifts, contactStrips, contactShadows, contactPosts, contactFloatErrors;
    static float contactMaxError;
    static bool entryCleared;
    static float churchSeatY;
    static Material generatedRockContact;
    static float foregroundSurfaceClear;

    public static void Run()
    {
        contactDrifts = contactStrips = contactShadows = contactPosts = contactFloatErrors = 0;
        contactMaxError = 0;
        generatedRockContact = null;
        entryCleared = false;
        if (GraphicsSettings.currentRenderPipeline == null)
        {
            Debug.LogError("[SPIKE] 当前不是 URP，先跑 SpikeURP.Run");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---------- 材质（程序化像素贴图 + 法线图；UV 以米计，tiling 按每张贴图覆盖的米数反推）----------
        const string TEX = "Assets/Art/Textures/";
        SpikePixelArt.Generate();
        var stone = ChurchMaterial("stone", "#B9A78E");
        stone.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TEX + "Stone_Pixel.png"));
        stone.SetTexture("_BumpMap", null);
        stone.DisableKeyword("_NORMALMAP");
        stone.SetTextureScale("_BaseMap", new Vector2(1f / 1.8f, 1f / 1.8f));
        stone.SetTextureScale("_BumpMap", new Vector2(1f / 1.8f, 1f / 1.8f));
        stone.SetFloat("_BumpScale", 1.8f);
        Debug.Log("[SPIKE] stone bump=" + stone.GetFloat("_BumpScale").ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        var stoneTiling = stone.GetTextureScale("_BaseMap");
        Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "[SPIKE] stone tiling=({0:F8}, {1:F8})", (double)stoneTiling.x, (double)stoneTiling.y));
        // 石材沿用主光/PBR；中性乘色校准截图受光面，积雪在 shader 世界高度中混合。
        stone.shader = Shader.Find("Mistbell/TerrainHaze");
        stone.SetColor("_BaseColor", new Color(.75f,.75f,.75f,1));
        stone.SetFloat("_StoneSurface",1);
        stone.SetFloat("_HazeStrength",0);
        stone.SetFloat("_Smoothness",.02f);
        var roofMat = ChurchMaterial("roof", "#A06B4A");
        // 屋顶：贴图本身已是深石板冷灰（meanRGB 75,81,89）；_BaseColor 是乘性归一系数，
        // 必须写死在代码里——Assets/Settings/M_Roof.mat 被 .gitignore 忽略，写进 .mat 等于不可复现。
        roofMat.SetColor("_BaseColor", new Color(0.86f, 0.84f, 0.80f, 1f));   // 原来 0.50 太暗，屋顶读成黑洞
        var wood = ChurchMaterial("wood", "#6B4A32");
        var ground = MakeLit("Assets/Settings/M_Ground.mat", Hex("#EDF3F8"), 0f, 0.05f);

        EditorUtility.SetDirty(stone);
        EditorUtility.SetDirty(roofMat);
        EditorUtility.SetDirty(ground);

        // ---------- 地形：近景保留 6° 坡面，远景下沉成谷 ----------
        var terrainCollider = BuildTerrain(MakeGroundHaze(ground));

        Physics.SyncTransforms();
        BuildMountains();
        BuildMist();
        // Ground fog is evaluated by world height in the terrain/ridge shaders.
        BuildStonePath(terrainCollider, stone);
        BuildFootprints(terrainCollider);
        BuildProps(terrainCollider, stone, wood);
        BuildSnowSurface(terrainCollider);
        BuildSky();
        BuildSnowfall();

        // ---------- Blender FBX：保留导出时的米制比例与原点 ----------
        // Wall accumulation belongs only to the church; the existing path keeps exposed stone.
        var churchStone = new Material(stone) { name = "M_ChurchStone" };
        churchStone.SetFloat("_StoneSurface",2);
        AssetDatabase.DeleteAsset("Assets/Settings/M_ChurchStone.mat");
        AssetDatabase.CreateAsset(churchStone,"Assets/Settings/M_ChurchStone.mat");
        var church = ImportChurch(churchStone, roofMat, terrainCollider);
        BuildChurchContact(church, terrainCollider);
        ValidateContact(church, terrainCollider);
        var stairRamp = BuildTerrace(church, terrainCollider, stone);
        BuildWarmDetails();
        ChurchInteriorBuilder.Build(church, terrainCollider);
        MaterialForge.ApplySamples(terrainCollider, church);
        var cameraPosition = EyeAt(5f, -82f);   // 走近到 ~74m：参考图里教堂占画面 55%+，96m 时只占 30%
        var cameraRotation = Quaternion.LookRotation(new Vector3(0f, 13f, 0f) - cameraPosition);

        // ---------- 像素角色（Billboard）----------
        SlicePilgrim.Run();
        var pilgrimAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(SlicePilgrim.AtlasPath);
        var bbShader = Shader.Find("Mistbell/PixelBillboard");
        if (bbShader == null)
        {
            Debug.LogError("[SPIKE] 找不到 Mistbell/PixelBillboard 着色器，检查 Assets/Art/Shaders/PixelBillboard.shader 是否编译通过");
            return;
        }

        // 只移动角色：教堂门廊左前方，两固定机位共同可见；落点只查询地形碰撞体。
        var pilgrimPos = new Vector3(8.5f, 0f, -66f);   // New route lowers the cut: stand on its snow shoulder, keeping the fixed capture cameras.
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
        bbMat.SetTexture("_MainTex", pilgrimAtlas);
        bbMat.SetFloat("_ScaleX", 1.45f * 64f / 80f);
        bbMat.SetFloat("_ScaleY", 1.45f);
        bbMat.SetFloat("_PivotY", 0f);
        bbMat.SetFloat("_Cutoff", 0.5f);
        bbMat.SetFloat("_Cols", 6f);
        bbMat.SetFloat("_Rows", 4f);
        bbMat.SetFloat("_Frame", 0f);
        bbMat.SetFloat("_Row", 3f);
        // 角色是 unlit 像素贴图：给一点日照暖色增益，免得在亮雪地上变成一团黑
        if (bbMat.HasProperty("_Color")) bbMat.SetColor("_Color", new Color(1.14f, 1.11f, 1.06f, 1f));
        AssetDatabase.DeleteAsset("Assets/Settings/M_Pilgrim.mat");
        AssetDatabase.CreateAsset(bbMat, "Assets/Settings/M_Pilgrim.mat");

        var pilgrim = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pilgrim.name = "Pilgrim";
        UnityEngine.Object.DestroyImmediate(pilgrim.GetComponent<Collider>());
        pilgrim.transform.position = pilgrimPos;
        pilgrim.GetComponent<MeshRenderer>().sharedMaterial = bbMat;
        pilgrim.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
        pilgrim.AddComponent<PilgrimAnimator>();

        // Runtime-only playable slice; screenshot flags disable all three components in Awake.
        var player = pilgrim.AddComponent<PlayerController>();
        player.ground = terrainCollider;
        player.stairRamp = stairRamp;
        player.stairDeck = stairRamp.transform.parent.Find("TerraceLanding").GetComponent<Collider>();
        var panel = new GameObject("Church Info Canvas").AddComponent<InfoPanel>();
        var entrance = new GameObject("Church Entrance Interaction").AddComponent<Interactable>();
        entrance.transform.SetParent(church, true);
        entrance.transform.position = new Vector3(0f, churchSeatY + .4f, -13f);
        entrance.player = player;
        entrance.panel = panel;
        player.panel = panel;
        player.entrance = entrance;

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

        var reveal = pilgrim.AddComponent<MistReveal>();
        reveal.contactMaterial = RockContactMaterial();
        reveal.player = player;
        reveal.church = church;
        reveal.volume = vol;
        reveal.cloudShader = Shader.Find("UI/Default");

        // ---------- 相机 ----------
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        cam.fieldOfView = Fov;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1500f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.69f, 0.77f, 0.86f);
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
        MountainBackdrop.Build(cam, GameObject.Find("Mountain Ridges"));
        HorizonHazeSky.Apply();

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
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/Snow_Pixel.png"));
        // Retained texture transform; snow grain uses TerrainHaze's fixed world XZ density.
        material.SetTextureScale("_BaseMap", new Vector2(1f / 1.2f, 1f / 6f));
        material.SetTexture("_SnowMedium",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/" + "Snow_Medium.png"));
        material.SetTexture("_SnowLow",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/" + "Snow_Low.png"));
        material.SetFloat("_SnowAlbedo", .88f);
        Debug.Log("[SNOW] texels_per_meter=40 detail_scale=0.0575 palette_steps=3 cluster_texels=2.3 mound_scale_m=0.5..1 mound_albedo_amplitude=0.04");
        material.SetColor("_SnowShadowColor", Hex("#A9C0D8"));
        material.SetColor("_HazeColor", Hex("#DCE2E6"));
        material.SetFloat("_HazeStart", 110f);
        material.SetFloat("_HazeEnd", 430f);
        material.SetFloat("_HazePower", 1.4f);
        material.SetFloat("_HazeStrength", 0.72f);
        const string path = "Assets/Settings/M_GroundHaze.mat";
        var saved = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (saved == null) { AssetDatabase.CreateAsset(material, path); saved = material; }
        else { EditorUtility.CopySerialized(material, saved); UnityEngine.Object.DestroyImmediate(material); }
        EditorUtility.SetDirty(saved);
        Debug.Log("[SPIKE] terrain haze shader=TerrainHaze start=110 end=430 strength=0.72 color=#DCE2E6");
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

    static Transform ImportChurch(Material stone, Material roof, MeshCollider terrain)
    {
        const string path = "Assets/Art/Church/gergeti_church.fbx";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        // Runtime closeup framing projects actual roof/cross vertices, not an AABB.
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
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
        if (triangles < 70000 || triangles > 95000 || Mathf.Abs(bounds.size.x - 28f) > 2f || Mathf.Abs(bounds.size.y - 24.5f) > 2f)
            throw new System.InvalidOperationException("Church v2 outside triangle/bounds acceptance limits");
        // The imported floor was at y=0, over a metre above the sloping entrance.
        // Seat the existing building at the first tread; retain its scale and rotation.
        var entry = ContactGround(terrain, new Vector3(-2f, 0f, -10.65f));
        churchSeatY = entry.y - .02f - bounds.min.y;
        church.position += Vector3.up * churchSeatY;
        Debug.Log($"[CONTACT_SEAT] church_y={churchSeatY:F4} first_tread_ground={entry.y:F4}");
        // Solid proxies deliberately close the open FBX portal for this exterior slice.
        // Root-local metres; no renderer, mesh, material or imported asset changes.
        void Block(string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(church, false);
            go.layer = PlayerController.BlockerLayer;
            go.isStatic = true;
            var box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }
        // Use the actual hollow shell and recessed portal, preserving every through opening.
        foreach(var f in church.GetComponentsInChildren<MeshFilter>())
        {
            if(f.name!="Main_Shell" && !f.name.StartsWith("Portal_Layer_")) continue;
            var go=new GameObject("ChurchCollision_"+f.name);
            go.transform.SetParent(f.transform,false); go.layer=PlayerController.BlockerLayer;
            go.AddComponent<MeshCollider>().sharedMesh=f.sharedMesh;
        }
        Block("ChurchCollision_Annex", new Vector3(-12.25f, 3.1f, 2.5f), new Vector3(6.5f, 6.2f, 6));
        var tower = new GameObject("ChurchCollision_BellTower");
        tower.transform.SetParent(church, false);
        tower.layer = PlayerController.BlockerLayer;
        tower.isStatic = true;
        var capsule = tower.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(10.5f, 6.5f, -2);
        capsule.radius = 2.2f;
        capsule.height = 13;
        return church;
    }

    // Terrace follows the existing 6-degree site grade: never deform the snow terrain.
    // Its front is the ninth tread's back edge, not the centre of the old path tile.
    static MeshCollider BuildTerrace(Transform church, MeshCollider terrain, Material sourceStone)
    {
        const int steps = 9;
        const float rise = .18f, run = .34f, width = 5f, wallHeight = 1.62f;
        const float thickness = .45f, copingWidth = .70f, capHeight = .12f, gap = .025f;
        const float startZ = -13.25f, halfX = 18.5f;
        const float baseFrontZ = startZ - 1.5f;
        float frontZ = startZ + steps * run, backZ = baseFrontZ + 27f;
        float footY = ContactGround(terrain, new Vector3(0, 0, startZ)).y + .035f;
        float landingY = footY + steps * rise;
        // Leave a shallow coping above the graded courtyard; retain the original slope.
        float deckLift = landingY - GroundY(frontZ);
        // Continuous perimeter drifts are part of the deck mesh and collision surface.
        // Keep the central door/stair route and the tower approach at their old level.
        var moundCenters = new System.Collections.Generic.List<Vector2>();
        foreach(float x in new[]{-15.5f,-11f,-6.5f,6.5f,11f,15.5f})
            moundCenters.Add(new Vector2(x,-12f));
        foreach(float x in new[]{-17f,17f}) foreach(float z in new[]{-7f,-2f,4f,9f})
            moundCenters.Add(new Vector2(x,z));
        foreach(float x in new[]{-12f,-5f,3f,11f}) moundCenters.Add(new Vector2(x,10.8f));
        float Drift(float x,float z)
        {
            float height=0;
            foreach(var q in moundCenters)
            {
                float r=new Vector2((x-q.x)/2.1f,(z-q.y)/1.5f).magnitude;
                if(r>=1) continue;
                float amplitude=Mathf.Lerp(.15f,.35f,Mathf.PerlinNoise(q.x*.37f+40,q.y*.41f+60));
                height=Mathf.Max(height,amplitude*.5f*(1+Mathf.Cos(r*Mathf.PI)));
            }
            float edge=Mathf.Min(halfX-thickness-Mathf.Abs(x),backZ-thickness-z);
            float front= z<frontZ && Mathf.Abs(x)>3.5f ? z-baseFrontZ-thickness : 100;
            edge=Mathf.Min(edge,front);
            float wallBank=Mathf.Clamp01(1-edge/1.4f);
            float noise=Mathf.PerlinNoise(x*.63f+30,z*.58f+50);
            return Mathf.Max(height,wallBank*wallBank*Mathf.Lerp(.25f,.35f,noise));
        }
        float Deck(float x, float z) => GroundY(z) + deckLift + Drift(x,z);
        float Ground(float x, float z) => ContactGround(terrain, new Vector3(x, 0, z)).y;
        float WallTop(float x, float z) => GroundY(z) + wallHeight;
        var root = new GameObject("Church Terrace").transform;
        var stone = new Material(sourceStone) { name = "M_TerraceAshlar" };
        stone.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/stone_ashlar.png"));
        stone.SetTextureScale("_BaseMap", Vector2.one / 2f);
        stone.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/stone_ashlar_n.png"));
        stone.SetTextureScale("_BumpMap", Vector2.one / 2f);
        stone.SetFloat("_StoneSurface", 1);
        stone.SetFloat("_HazeStrength", 0);
        stone.SetColor("_BaseColor", new Color(.75f,.75f,.75f,1));
        const string materialPath = "Assets/Settings/M_TerraceAshlar.mat";
        AssetDatabase.DeleteAsset(materialPath); AssetDatabase.CreateAsset(stone, materialPath);
        var snow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_GroundHaze.mat");
        var grounded = new System.Collections.Generic.Dictionary<MeshFilter,int[]>();
        var caps = new System.Collections.Generic.List<MeshFilter>();

        // Closed, hard-normal prisms with a sampled foundation, never flat boxes
        // balanced on a centre ray. UVs are metres on each face.
        MeshFilter Prism(string name, float x0, float x1, float z0, float z1,
            System.Func<float,float,float> bottom, System.Func<float,float,float> top,
            Material material, bool checkFoot)
        {
            int nx = Mathf.Max(1, Mathf.CeilToInt((x1-x0)/.25f));
            int nz = Mathf.Max(1, Mathf.CeilToInt((z1-z0)/.25f));
            var vs = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var ts = new System.Collections.Generic.List<int>();
            var feet = new System.Collections.Generic.List<int>();
            Vector3 P(float x,float z,bool up) => new Vector3(x,up?top(x,z):bottom(x,z),z);
            void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d, bool horizontal, bool xFace=false)
            {
                int k=vs.Count; vs.AddRange(new[]{a,b,c,d});
                foreach(var p in new[]{a,b,c,d}) uv.Add(horizontal?new Vector2(p.x,p.z):new Vector2(xFace?p.z:p.x,p.y));
                ts.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
            }
            for(int i=0;i<nx;i++) for(int j=0;j<nz;j++)
            {
                float a=Mathf.Lerp(x0,x1,i/(float)nx), b=Mathf.Lerp(x0,x1,(i+1f)/nx);
                float c=Mathf.Lerp(z0,z1,j/(float)nz), d=Mathf.Lerp(z0,z1,(j+1f)/nz);
                Face(P(a,c,true),P(a,d,true),P(b,d,true),P(b,c,true),true);
                int footIndex=vs.Count;
                Face(P(a,c,false),P(b,c,false),P(b,d,false),P(a,d,false),true);
                for(int k=0;k<4;k++) feet.Add(footIndex+k);
                if(j==0) Face(P(a,c,false),P(a,c,true),P(b,c,true),P(b,c,false),false);
                if(j==nz-1) Face(P(a,d,false),P(b,d,false),P(b,d,true),P(a,d,true),false);
                if(i==0) Face(P(a,c,false),P(a,d,false),P(a,d,true),P(a,c,true),false,true);
                if(i==nx-1) Face(P(b,c,false),P(b,c,true),P(b,d,true),P(b,d,false),false,true);
            }
            var tr=ContactMesh(root,name,vs.ToArray(),ts.ToArray(),uv.ToArray(),material);
            var mr=tr.GetComponent<MeshRenderer>(); mr.shadowCastingMode=ShadowCastingMode.On;
            var collider=tr.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh=tr.GetComponent<MeshFilter>().sharedMesh;
            var filter=tr.GetComponent<MeshFilter>();
            if(checkFoot) grounded.Add(filter,feet.ToArray());
            return filter;
        }
        float Base(float x,float z) => Ground(x,z)-.01f;
        int segments=0;
        void Wall(float x0,float x1,float z0,float z1,bool alongX)
        {
            float length=alongX?x1-x0:z1-z0;
            int count=Mathf.CeilToInt(length/4.5f);
            for(int i=0;i<count;i++)
            {
                float a=i/(float)count,b=(i+1f)/count;
                float xa=alongX?Mathf.Lerp(x0,x1,a)+gap/2:x0;
                float xb=alongX?Mathf.Lerp(x0,x1,b)-gap/2:x1;
                float za=alongX?z0:Mathf.Lerp(z0,z1,a)+gap/2;
                float zb=alongX?z1:Mathf.Lerp(z0,z1,b)-gap/2;
                var wall = Prism("TerraceWall_"+segments,xa,xb,za,zb,Base,WallTop,stone,true);
                // Preserve the visible sloped prism; use its enclosing static box as a barrier.
                Object.DestroyImmediate(wall.GetComponent<MeshCollider>());
                wall.gameObject.layer = PlayerController.BlockerLayer;
                wall.gameObject.isStatic = true;
                var box = wall.gameObject.AddComponent<BoxCollider>();
                box.center = wall.sharedMesh.bounds.center;
                box.size = wall.sharedMesh.bounds.size;
                // Collision only: centreline .25m outside the terrace outer edge.
                var outward = alongX ? (z0 < (frontZ+backZ)/2 ? Vector3.back : Vector3.forward)
                    : (x0 < 0 ? Vector3.left : Vector3.right);
                box.center += outward * (thickness / 2 + .25f);
                float overhang=(copingWidth-thickness)/2;
                float cx=(xa+xb)/2,cz=(za+zb)/2;
                float CapBase(float x,float z) => WallTop(x,alongX?cz:z)+.04f-
                    Vector3.Dot(new Vector3(x-cx,0,z-cz),outward)*Mathf.Tan(5*Mathf.Deg2Rad);
                var cap=Prism("TerraceCap_"+segments,
                    xa-(alongX?0:overhang),xb+(alongX?0:overhang),
                    za-(alongX?overhang:0),zb+(alongX?overhang:0),CapBase,
                    (x,z)=>CapBase(x,z)+capHeight,stone,false);
                caps.Add(cap);
                float actualWidth=alongX?cap.sharedMesh.bounds.size.z:cap.sharedMesh.bounds.size.x;
                var topNormal=cap.sharedMesh.normals[0];
                float tilt=Mathf.Atan2(Vector3.Dot(topNormal,outward),topNormal.y)*Mathf.Rad2Deg;
                if(Mathf.Abs(actualWidth-copingWidth)>.001f || Mathf.Abs(tilt-5)>.01f)
                    throw new System.InvalidOperationException("TERRACE2 coping width/tilt gate");
                segments++;
            }
        }
        // 37 x 27m envelope; preserve the stair and church, extend a real apron ahead.
        Wall(-halfX,-width/2,baseFrontZ,baseFrontZ+thickness,true);
        Wall(width/2,halfX,baseFrontZ,baseFrontZ+thickness,true);
        Wall(-halfX,halfX,backZ-thickness,backZ,true);
        Wall(-halfX,-halfX+thickness,baseFrontZ+thickness,backZ-thickness,false);
        Wall(halfX-thickness,halfX,baseFrontZ+thickness,backZ-thickness,false);
        // Tile the terrace fill so each persistent mesh remains below the 16-bit index limit.
        for(int x=0;x<8;x++) for(int z=0;z<6;z++)
        {
            float xa=Mathf.Lerp(-halfX+thickness,halfX-thickness,x/8f);
            float xb=Mathf.Lerp(-halfX+thickness,halfX-thickness,(x+1)/8f);
            float za=Mathf.Lerp(frontZ+thickness,backZ-thickness,z/6f);
            float zb=Mathf.Lerp(frontZ+thickness,backZ-thickness,(z+1)/6f);
            Prism("TerraceFill_"+x+"_"+z,xa,xb,za,zb,Base,Deck,snow,true);
        }
        // Side shoulders enclose all nine treads. Front apron follows the approach
        // ground, so it cannot introduce a new riser before the player-only ramp.
        Prism("TerraceFill_FrontLeft",-halfX+thickness,-width/2-.5f,baseFrontZ+thickness,frontZ+thickness,Base,Deck,snow,true);
        Prism("TerraceFill_FrontRight",width/2+.5f,halfX-thickness,baseFrontZ+thickness,frontZ+thickness,Base,Deck,snow,true);
        var apron = Prism("TerraceFill_Apron",-width/2,width/2,baseFrontZ,startZ,Base,
            (x,z)=>Ground(x,z)+.005f,stone,true);
        Prism("TerraceLanding",-width/2,width/2,frontZ,frontZ+thickness,Base,Deck,snow,true);
        var treads=new System.Collections.Generic.List<MeshFilter>();
        for(int i=0;i<steps;i++)
        {
            float top=footY+(i+1)*rise;
            treads.Add(Prism("TerraceStep_"+i,-width/2,width/2,startZ+i*run,startZ+(i+1)*run,
                Base,(x,z)=>top,stone,true));
        }
        // Player-only upper envelope of the treads. The central incline has exactly
        // atan((9 * .18) / (9 * .34)) grade; the approach joins the road at z=-16.
        // One extra tread of lead-in keeps the sloped surface above every stone top.
        float[] rampZ = { -16f, startZ - run, frontZ - run, frontZ };
        float[] rampY = { Ground(0, -16f), footY, landingY, landingY };
        var rampVertices = new Vector3[8];
        var rampUV = new Vector2[8];
        var rampTriangles = new int[18];
        for (int i = 0; i < rampZ.Length; i++)
        {
            for (int side = 0; side < 2; side++)
            {
                int k = i * 2 + side;
                rampVertices[k] = new Vector3((side - .5f) * width, rampY[i] + .005f, rampZ[i]);
                rampUV[k] = new Vector2(side, i);
            }
            if (i == rampZ.Length - 1) continue;
            int v = i * 2, t = i * 6;
            rampTriangles[t] = v; rampTriangles[t+1] = v+2; rampTriangles[t+2] = v+1;
            rampTriangles[t+3] = v+1; rampTriangles[t+4] = v+2; rampTriangles[t+5] = v+3;
        }
        var ramp = ContactMesh(root, "PlayerStairRamp", rampVertices, rampTriangles, rampUV, snow);
        ramp.gameObject.layer = PlayerController.TraversalLayer;
        ramp.GetComponent<Renderer>().enabled = false;
        var rampCollider = ramp.gameObject.AddComponent<MeshCollider>();
        rampCollider.sharedMesh = ramp.GetComponent<MeshFilter>().sharedMesh;
        rampCollider.excludeLayers = ~(1 << PlayerController.PlayerLayer);
        // Stepped cheek walls are 0.9m above each tread and 0.5m wide.
        for(int side=-1;side<=1;side+=2) for(int i=0;i<steps;i++)
        {
            float xa=side<0?-width/2-.5f:width/2;
            float top=footY+(i+1)*rise+.9f;
            var cheek = Prism("TerraceCheek_"+side+"_"+i,xa,xa+.5f,startZ+i*run,startZ+(i+1)*run,
                Base,(x,z)=>top,stone,true);
            cheek.gameObject.layer = PlayerController.BlockerLayer;
        }
        // Raise only the imported church with the new site fill. Its Blender scale,
        // original entry steps and window offsets remain intact.
        church.position+=Vector3.up*deckLift; churchSeatY+=deckLift;

        // Continuous 40..70cm snow skirts follow the church/annex/tower exterior
        // wall footprints as well as the retaining wall below. Keep the portal clear.
        int skirtSegments=0;
        void Bank(Vector2 a,Vector2 b)
        {
            var normal=new Vector2(-(b-a).y,(b-a).x).normalized;
            int n=Mathf.CeilToInt(Vector2.Distance(a,b)/.25f);
            var vs=new System.Collections.Generic.List<Vector3>(); var uv=new System.Collections.Generic.List<Vector2>();
            var ts=new System.Collections.Generic.List<int>();
            for(int i=0;i<=n;i++) for(int j=0;j<=8;j++)
            {
                float t=i/(float)n, w=j/8f;
                var q=Vector2.Lerp(a,b,t)+normal*(w*.65f-.02f);
                float height=Mathf.Lerp(.4f,.7f,Mathf.PerlinNoise(q.x*.3f+80,q.y*.3f+90));
                vs.Add(new Vector3(q.x,Deck(q.x,q.y)+height*Mathf.Pow(1-w,2),q.y)); uv.Add(q);
                if(i==n||j==8) continue;
                int k=i*9+j; ts.AddRange(new[]{k,k+1,k+10,k,k+10,k+9});
            }
            ContactMesh(root,"TerraceBank_"+root.childCount,vs.ToArray(),ts.ToArray(),uv.ToArray(),snow);
            skirtSegments++;
        }
        void BankLoop(Vector2[] points)
        { for(int i=0;i<points.Length-1;i++) Bank(points[i],points[i+1]); }
        BankLoop(new[]{new Vector2(-4.5f,-8),new Vector2(-6.5f,-8),new Vector2(-6.5f,-3.5f),
            new Vector2(-9,-3.5f),new Vector2(-9,3.5f),new Vector2(-6.5f,3.5f),new Vector2(-6.5f,8),
            new Vector2(-5.2f,8.6f)});
        var apse=new System.Collections.Generic.List<Vector2>();
        for(int i=0;i<=16;i++) {float a=Mathf.PI-i*Mathf.PI/16;apse.Add(new Vector2(-2+Mathf.Cos(a)*3.2f,8.6f+Mathf.Sin(a)*3.2f));}
        BankLoop(apse.ToArray());
        BankLoop(new[]{new Vector2(1.2f,8.6f),new Vector2(2.5f,8),new Vector2(2.5f,3.5f),
            new Vector2(5,3.5f),new Vector2(5,-3.5f),new Vector2(2.5f,-3.5f),new Vector2(2.5f,-8),new Vector2(.5f,-8)});
        BankLoop(new[]{new Vector2(-15.5f,-.5f),new Vector2(-15.5f,5.5f),new Vector2(-9,5.5f),new Vector2(-9,-.5f),new Vector2(-15.5f,-.5f)});
        var tower=new Vector2[9];
        for(int i=0;i<=8;i++) {float a=-Mathf.PI/2-i*Mathf.PI/4;tower[i]=new Vector2(10.5f+2.6f*Mathf.Cos(a),-2+2.6f*Mathf.Sin(a));}
        BankLoop(tower);

        // Four snow-capped boundary stones, entirely outside the five-metre stairs.
        int bollards=0,walls=0,rockpiles=0;
        foreach(float side in new[]{-1f,1f}) foreach(float xAbs in new[]{5.2f,12.3f})
        {
            float x=side*xAbs,z=-11.8f;
            float y=Deck(x,z);
            Prism("TerraceBollard_"+bollards,x-.35f,x+.35f,z-.35f,z+.35f,
                (a,b)=>Deck(a,b)-.04f,(a,b)=>y+.9f,stone,false);
            Prism("TerraceBollardSnow_"+bollards,x-.37f,x+.37f,z-.37f,z+.37f,
                (a,b)=>y+.89f,(a,b)=>y+.96f+.025f*Mathf.PerlinNoise(a*4+40,b*4+40),snow,false);
            bollards++;
        }
        // Low wall remnants divide the lateral empty spaces without crossing any route.
        foreach(float x in new[]{-16f,16f})
        {
            Prism("TerraceLowWall_"+walls,x-.28f,x+.28f,-10.2f,-4.2f,
                (a,b)=>Deck(a,b)-.06f,(a,b)=>Deck(x,b)+.5f,stone,false);
            Prism("TerraceLowWallSnow_"+walls,x-.33f,x+.33f,-10.25f,-4.15f,
                (a,b)=>Deck(x,b)+.49f,(a,b)=>Deck(x,b)+.57f+.03f*Mathf.PerlinNoise(a*3+70,b*3+70),snow,false);
            walls++;
        }
        var rockRng=new System.Random(26791);
        foreach(float x in new[]{-16.8f,16.8f}) foreach(float z in new[]{-13f,10.5f})
        {
            var pile=new GameObject("TerraceRockpile_"+rockpiles).transform; pile.SetParent(root,false);
            int count=3+rockpiles%3;
            for(int i=0;i<count;i++)
            {
                var mesh=BuildWinterRock(rockRng);
                float size=.4f+(float)rockRng.NextDouble()*.4f;
                var verts=mesh.vertices; var bounds=mesh.bounds;
                for(int k=0;k<verts.Length;k++) verts[k]=Vector3.Scale(verts[k]-bounds.center,
                    new Vector3(size/bounds.size.x,size/bounds.size.y,size/bounds.size.z));
                mesh.vertices=verts; mesh.RecalculateBounds();
                string asset="Assets/Settings/TerracePile_"+rockpiles+"_"+i+".asset";
                AssetDatabase.DeleteAsset(asset); AssetDatabase.CreateAsset(mesh,asset);
                float angle=i*2.4f,px=x+Mathf.Cos(angle)*.38f,pz=z+Mathf.Sin(angle)*.38f;
                var rock=new GameObject("Rock_"+i); rock.transform.SetParent(pile,false);
                rock.transform.position=new Vector3(px,Deck(px,pz)+size*.28f,pz);
                rock.AddComponent<MeshFilter>().sharedMesh=mesh;
                rock.AddComponent<MeshRenderer>().sharedMaterials=new[]{stone,snow,stone};
            }
            rockpiles++;
        }
        // A continuous, mitred outer skirt at the retaining-wall foot; omit only the stair opening.
        var skirtCorners=new[]{new Vector2(-halfX,baseFrontZ),new Vector2(halfX,baseFrontZ),
            new Vector2(halfX,backZ),new Vector2(-halfX,backZ)};
        for(int side=0;side<4;side++)
        {
            Vector2 a=skirtCorners[side],b=skirtCorners[(side+1)%4];
            int n=Mathf.CeilToInt(Vector2.Distance(a,b)/1.5f);
            for(int i=0;i<n;i++)
            {
                Vector2 p=Vector2.Lerp(a,b,i/(float)n),q=Vector2.Lerp(a,b,(i+1f)/n);
                if(side==0 && p.x<width/2+.5f && q.x>-width/2-.5f) continue;
                Vector2 Out(Vector2 v) => new Vector2(Mathf.Abs(v.x+halfX)<.001f?-1:Mathf.Abs(v.x-halfX)<.001f?1:0,
                    Mathf.Abs(v.y-baseFrontZ)<.001f?-1:Mathf.Abs(v.y-backZ)<.001f?1:0);
                var vs=new System.Collections.Generic.List<Vector3>(); var uv=new System.Collections.Generic.List<Vector2>();
                var ts=new System.Collections.Generic.List<int>();
                for(int k=0;k<=6;k++) for(int j=0;j<=8;j++)
                {
                    float t=k/6f,w=j/8f;
                    Vector2 v=Vector2.Lerp(p,q,t),normal=Vector2.Lerp(Out(p),Out(q),t);
                    // Interior points on a side share its normal; corner vectors miter the seam.
                    if(side==0) normal.y=-1; if(side==1) normal.x=1;
                    if(side==2) normal.y=1; if(side==3) normal.x=-1;
                    var at=v+normal*(w*1.15f-.015f);
                    float h=Mathf.Lerp(.4f,.7f,Mathf.PerlinNoise(v.x*.3f+80,v.y*.3f+90));
                    vs.Add(new Vector3(at.x,Ground(at.x,at.y)+h*Mathf.Pow(1-w,2)+.006f,at.y)); uv.Add(at);
                    if(k==6||j==8) continue;
                    int id=k*9+j; ts.AddRange(new[]{id,id+10,id+1,id,id+9,id+10});
                }
                var skirt=ContactMesh(root,"TerraceSkirt_"+skirtSegments++,vs.ToArray(),ts.ToArray(),uv.ToArray(),snow);
                foreach(var normal in skirt.GetComponent<MeshFilter>().sharedMesh.normals)
                    if(normal.y<=0) throw new System.InvalidOperationException("TERRACE2 skirt winding gate");
            }
        }
        // Keep named segments/colliders for inspection, but render the dressing in
        // two material batches. Dozens of tiny snow strips must not add dozens of draws.
        var dressingStone=new System.Collections.Generic.List<CombineInstance>();
        var dressingSnow=new System.Collections.Generic.List<CombineInstance>();
        int dressingRenderers=0;
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            string name=renderer.name;
            bool dressing=name.StartsWith("TerraceSkirt_") || name.StartsWith("TerraceBank_") || name.StartsWith("TerraceBollard") ||
                name.StartsWith("TerraceLowWall") || renderer.transform.parent.name.StartsWith("TerraceRockpile_");
            if(!dressing) continue;
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                var material=renderer.sharedMaterials[sub];
                var batch=material==snow?dressingSnow:dressingStone;
                batch.Add(new CombineInstance { mesh=mesh,subMeshIndex=sub,
                    transform=root.worldToLocalMatrix*renderer.transform.localToWorldMatrix });
            }
            renderer.enabled=false; dressingRenderers++;
        }
        void DressingBatch(string name,System.Collections.Generic.List<CombineInstance> parts,Material material)
        {
            var mesh=new Mesh { name=name,indexFormat=IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(),true,true); mesh.RecalculateBounds();
            string path="Assets/Settings/"+name+".asset";
            AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh,path);
            var go=new GameObject(name); go.transform.SetParent(root,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.On; renderer.receiveShadows=true;
        }
        DressingBatch("TerraceDressingStone",dressingStone,stone);
        DressingBatch("TerraceDressingSnow",dressingSnow,snow);
        Debug.Log($"[TERRACE2_BATCH] source_renderers={dressingRenderers} active_renderers=2");

        Debug.Log(FormattableStringInvariant($"[TERRACE2] mounds={moundCenters.Count} bollards={bollards} walls={walls} rockpiles={rockpiles} skirt_segments={skirtSegments} coping_m={copingWidth:F2} tilt_deg=5"));
        if(moundCenters.Count<12||bollards!=4||walls!=2||rockpiles!=4||skirtSegments<20)
            throw new System.InvalidOperationException("TERRACE2 count gate");

        // Measure actual generated mesh bounds rather than requested dimensions.
        var apronBounds = apron.GetComponent<Renderer>().bounds;
        float sideMargin = halfX - width / 2 - .5f;
        float frontMargin = startZ - apronBounds.min.z;
        Debug.Log(FormattableStringInvariant($"[BASE] plinth={halfX*2:F1}x{backZ-baseFrontZ:F1} stair_side_margin={sideMargin:F3} stair_front_margin={frontMargin:F3}"));
        if(sideMargin<1.2f || frontMargin<1f) throw new System.InvalidOperationException("Base margin gate failed");
        Physics.SyncTransforms();
        int errors=0, checkedFeet=0; float maxError=0;
        // Independently inspect saved mesh bottom faces (second quad of each cell),
        // then raycast every final tread at five positions across its width.
        foreach(var pair in grounded)
        {
            var f=pair.Key; var v=f.sharedMesh.vertices;
            foreach(int index in pair.Value)
            {
                var p=f.transform.TransformPoint(v[index]);
                float error=Mathf.Abs(p.y-(Ground(p.x,p.z)-.01f));
                maxError=Mathf.Max(maxError,error);
                if(error>.03f) errors++;
                checkedFeet++;
            }
        }
        for(int i=0;i<steps;i++) for(int j=0;j<=4;j++)
        {
            float x=Mathf.Lerp(-width/2+.02f,width/2-.02f,j/4f);
            var ray=new Ray(new Vector3(x,500,startZ+(i+.5f)*run),Vector3.down);
            if(!treads[i].GetComponent<MeshCollider>().Raycast(ray,out var hit,1000)) {errors++;continue;}
            float error=Mathf.Abs(hit.point.y-(footY+(i+1)*rise));
            if(error>.03f) errors++;
            maxError=Mathf.Max(maxError,error);
        }
        float landingError=Mathf.Abs(Deck(0,frontZ)-(footY+steps*rise));
        if(landingError>.03f) errors++;
        Debug.Log(FormattableStringInvariant($"[TERRACE] wall_segments={segments} wall_h={wallHeight:F2} steps={steps} step_rise={rise:F2} step_run={run:F2} stair_w={width:F2} snow_cap=true float_errors={errors}"));
        Debug.Log(FormattableStringInvariant($"[TERRACE_CHECK] grounded_vertices={checkedFeet} max_error_m={maxError:F6} landing_error_m={landingError:F6} deck_lift_m={deckLift:F4} front_z={frontZ:F2} back_z={backZ:F2} grade_deg={SlopeDeg:F1}"));
        if(segments<12||wallHeight<1.4f||wallHeight>1.8f||steps<8||steps>10||Mathf.Abs(width-5)>.1f||checkedFeet==0||errors!=0)
            throw new System.InvalidOperationException("Terrace hard gate failed");
        return rampCollider;
    }

    static string FormattableStringInvariant(System.FormattableString text) =>
        text.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // All contact geometry queries the terrain collider explicitly; church/prop colliders
    // must never become accidental grounding surfaces. Validate saved perimeter vertices too.
    static Vector3 ContactGround(MeshCollider terrain, Vector3 p)
    {
        if (!terrain.Raycast(new Ray(new Vector3(p.x, 500f, p.z), Vector3.down), out var hit, 1000f))
            throw new System.InvalidOperationException("Contact ray missed terrain: " + p);
        return hit.point;
    }

    static void CheckContact(MeshCollider terrain, Vector3 p, float intendedOffset)
    {
        float error = Mathf.Abs(p.y - ContactGround(terrain, p).y - intendedOffset);
        contactMaxError = Mathf.Max(contactMaxError, error);
        if (error > .03f) contactFloatErrors++;
    }

    static Transform ContactMesh(Transform parent, string name, Vector3[] vertices, int[] triangles,
                                 Vector2[] uv, Material material)
    {
        var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles, uv = uv };
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        string path = "Assets/Settings/" + name + ".asset";
        AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        return go.transform;
    }

    static Material RockContactMaterial()
    {
        if (generatedRockContact != null) return generatedRockContact;
        const string path = "Assets/Settings/M_RockContact.mat";
        var saved = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (saved != null) AssetDatabase.DeleteAsset(path);
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "RockContactSoft" };
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float r = new Vector2((x + .5f) / 32f - 1, (y + .5f) / 32f - 1).magnitude;
            float alpha = 1 - Mathf.SmoothStep(.28f, 1f, r);
            texture.SetPixel(x, y, new Color(.30f, .36f, .44f, alpha));
        }
        texture.Apply();
        AssetDatabase.DeleteAsset("Assets/Settings/RockContactSoft.asset");
        AssetDatabase.CreateAsset(texture, "Assets/Settings/RockContactSoft.asset");
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_RockContact" };
        mat.SetTexture("_BaseMap", texture); mat.SetColor("_BaseColor", new Color(1,1,1,.05f));
        mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0); mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent"); mat.renderQueue = 3000;
        AssetDatabase.CreateAsset(mat, path);
        generatedRockContact = mat;
        return mat;
    }

    static void ContactMound(Transform root, string name, MeshCollider terrain, Vector3 center,
                             Vector2 radius, float height, Material mat, float lift = 0f)
    {
        const int sides = 24, rings = 8;
        var vertices = new Vector3[1 + sides * rings];
        var uv = new Vector2[vertices.Length];
        var tris = new System.Collections.Generic.List<int>();
        vertices[0] = ContactGround(terrain, center) + Vector3.up * (height + lift);
        uv[0] = Vector2.one * .5f;
        for (int ring = 1; ring <= rings; ring++) for (int j = 0; j < sides; j++)
        {
            float r = ring / (float)rings, a = j * 2 * Mathf.PI / sides;
            var direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var p = center + new Vector3(direction.x * radius.x * r, 0, direction.y * radius.y * r);
            // Cosine dome has a broad foot, a narrow crown and zero derivative at the rim.
            float h = height * .5f * (1 + Mathf.Cos(r * Mathf.PI));
            p = ContactGround(terrain, p) + Vector3.up * (h + lift);
            int index = 1 + (ring - 1) * sides + j;
            vertices[index] = p; uv[index] = Vector2.one * .5f + direction * r * .5f;
            if (ring == rings) CheckContact(terrain, p, lift);
            int next = 1 + (ring - 1) * sides + (j + 1) % sides;
            if (ring == 1) tris.AddRange(new[] { 0, next, index });
            else
            {
                int inner = index - sides, innerNext = next - sides;
                tris.AddRange(new[] {inner, innerNext, next, inner, next, index});
            }
        }
        ContactMesh(root, name, vertices, tris.ToArray(), uv, mat);
    }

    // Only subdivide existing triangles intersecting the 3 x 4 m entrance patch.
    // Barycentric interpolation preserves every pre-existing surface outside that patch.
    static void ClearEntrySnow(Mesh mesh)
    {
        var source = mesh.vertices; var indices = mesh.triangles;
        var vertices = new System.Collections.Generic.List<Vector3>();
        var uv = new System.Collections.Generic.List<Vector2>();
        var triangles = new System.Collections.Generic.List<int>();
        void Add(Vector3 a, Vector3 b, Vector3 c)
        {
            foreach (var p in new[] { a, b, c })
            {
                float dx = Mathf.Min(p.x + 3.5f, -.5f - p.x);
                float dz = Mathf.Min(p.z + 14.8f, -10.8f - p.z);
                float mask = Mathf.SmoothStep(0, 1, Mathf.Min(dx, dz) / .25f);
                var v = p; v.y -= .06f * mask;
                triangles.Add(vertices.Count); vertices.Add(v); uv.Add(new Vector2(v.x, v.z));
            }
        }
        for (int i = 0; i < indices.Length; i += 3)
        {
            var a = source[indices[i]]; var b = source[indices[i+1]]; var c = source[indices[i+2]];
            float minX = Mathf.Min(a.x, Mathf.Min(b.x,c.x)), maxX = Mathf.Max(a.x,Mathf.Max(b.x,c.x));
            float minZ = Mathf.Min(a.z, Mathf.Min(b.z,c.z)), maxZ = Mathf.Max(a.z,Mathf.Max(b.z,c.z));
            if (maxX <= -3.5f || minX >= -.5f || maxZ <= -14.8f || minZ >= -10.8f)
            {
                // Preserve shared vertices for smooth terrain normals.
                continue;
            }
            const int n = 16;
            Vector3 Point(int x, int y) => a + (b-a) * (x/(float)n) + (c-a) * (y/(float)n);
            for (int x = 0; x < n; x++) for (int y = 0; y < n-x; y++)
            {
                Add(Point(x,y), Point(x+1,y), Point(x,y+1));
                if (x+y < n-1) Add(Point(x+1,y), Point(x+1,y+1), Point(x,y+1));
            }
        }
        // Keep the original shared mesh everywhere except the intersecting triangles.
        var finalVertices = new System.Collections.Generic.List<Vector3>(source);
        var finalUV = new System.Collections.Generic.List<Vector2>(mesh.uv);
        var finalTris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < indices.Length; i += 3)
        {
            var a=source[indices[i]]; var b=source[indices[i+1]]; var c=source[indices[i+2]];
            if (Mathf.Max(a.x,Mathf.Max(b.x,c.x)) <= -3.5f || Mathf.Min(a.x,Mathf.Min(b.x,c.x)) >= -.5f ||
                Mathf.Max(a.z,Mathf.Max(b.z,c.z)) <= -14.8f || Mathf.Min(a.z,Mathf.Min(b.z,c.z)) >= -10.8f)
                finalTris.AddRange(new[] {indices[i],indices[i+1],indices[i+2]});
        }
        int offset = finalVertices.Count;
        finalVertices.AddRange(vertices); finalUV.AddRange(uv);
        foreach (int i in triangles) finalTris.Add(offset+i);
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.Clear(); mesh.SetVertices(finalVertices); mesh.SetUVs(0, finalUV); mesh.SetTriangles(finalTris,0);
    }

    static void WallSnow(Transform root, string name, MeshCollider terrain, Vector2 a, Vector2 b, Material snow)
    {
        // Clockwise footprint: the left normal points outwards. A thin, upward-facing
        // shelf climbs into the wall; its outer edge is seated 2 cm into the snow.
        var outward = new Vector2(-(b-a).y, (b-a).x).normalized;
        int steps = Mathf.CeilToInt(Vector2.Distance(a,b)/.35f);
        const int across = 8;
        var vs = new System.Collections.Generic.List<Vector3>();
        var uv = new System.Collections.Generic.List<Vector2>();
        var ts = new System.Collections.Generic.List<int>();
        for (int i=0; i<=steps; i++) for (int j=0; j<=across; j++)
        {
            float t = i/(float)steps, w = j/(float)across;
            var xz = Vector2.Lerp(a,b,t) + outward * Mathf.Lerp(-.04f,.6f,w);
            var p = ContactGround(terrain,new Vector3(xz.x,0,xz.y));
            float height = .42f * Mathf.Pow(1-w,2);
            vs.Add(p+Vector3.up*height); uv.Add(xz);
        }
        int topCount=vs.Count;
        for(int i=0;i<topCount;i++) {vs.Add(vs[i]-Vector3.up*.02f);uv.Add(uv[i]);}
        int stride=across+1;
        void Quad(int a0,int b0,int c0,int d0) {ts.AddRange(new[]{a0,b0,c0,a0,c0,d0});}
        for(int i=0;i<steps;i++) for(int j=0;j<across;j++)
        {
            int k=i*stride+j;
            Quad(k,k+1,k+stride+1,k+stride);
            Quad(k+topCount,k+topCount+stride,k+topCount+stride+1,k+topCount+1);
        }
        for(int i=0;i<steps;i++)
        {
            int k=i*stride;
            Quad(k,k+topCount,k+topCount+stride,k+stride);
            k+=across;
            Quad(k,k+stride,k+stride+topCount,k+topCount);
        }
        for(int j=0;j<across;j++)
        {
            Quad(j,j+1,j+1+topCount,j+topCount);
            int k=steps*stride+j; Quad(k,k+topCount,k+topCount+1,k+1);
        }
        ContactMesh(root,name,vs.ToArray(),ts.ToArray(),uv.ToArray(),snow);
        contactStrips++;
    }

    static void ChurchDrift(Transform root, MeshCollider terrain, Vector2[] corners, int side,
                            float from, float to, Material snow)
    {
        const int along=4, across=16;
        var a=corners[side];var b=corners[(side+1)%4];
        Vector2 Normal(Vector2 delta)=>new Vector2(-delta.y,delta.x).normalized;
        var normal=Normal(b-a);
        var startNormal=(normal+Normal(a-corners[(side+3)%4])).normalized;
        var endNormal=(normal+Normal(corners[(side+2)%4]-b)).normalized;
        float length=Vector2.Distance(a,b);
        var vs=new Vector3[(along+1)*(across+1)];var uv=new Vector2[vs.Length];
        var ts=new System.Collections.Generic.List<int>();
        for(int i=0;i<=along;i++)
        {
            float t=Mathf.Lerp(from,to,i/(float)along);
            var p=Vector2.Lerp(a,b,t);
            var n=normal;
            if(t*length<1.2f) n=Vector2.Lerp(startNormal,normal,Mathf.SmoothStep(0,1,t*length/1.2f)).normalized;
            else if((1-t)*length<1.2f) n=Vector2.Lerp(endNormal,normal,Mathf.SmoothStep(0,1,(1-t)*length/1.2f)).normalized;
            float noise=Mathf.PerlinNoise(p.x*.18f+31.2f,p.y*.18f+9.1f);
            float width=Mathf.Lerp(1.2f,2.2f,noise);
            float wind=1-Mathf.SmoothStep(0,1,(p.x-corners[0].x)/2f);
            float height=Mathf.Lerp(Mathf.Lerp(.25f,.6f,noise),Mathf.Lerp(.5f,.8f,noise),wind);
            // Fade the bank down beside the three-metre entrance, instead of leaving
            // vertical cut ends. Skip the central opening entirely in the caller.
            if(side==3)
            {
                float entryDistance=Mathf.Max(-3.5f-p.x,p.x+.5f);
                height*=Mathf.SmoothStep(0,1,entryDistance/1.2f);
            }
            for(int j=0;j<=across;j++)
            {
                float w=j/(float)across;
                var xz=p+n*(width*(w-.5f));
                var point=ContactGround(terrain,new Vector3(xz.x,0,xz.y));
                float profile=Mathf.Sin(w*Mathf.PI);profile*=profile;
                int k=i*(across+1)+j;
                vs[k]=point+Vector3.up*(height*profile);uv[k]=xz;
                if(i==along||j==across) continue;
                ts.AddRange(new[]{k,k+1,k+across+2,k,k+across+2,k+across+1});
            }
        }
        ContactMesh(root,"ChurchDrift_"+contactDrifts++,vs,ts.ToArray(),uv,snow);
    }

    static void BuildChurchContact(Transform church, MeshCollider terrain)
    {
        var root = new GameObject("Church Snow Contact").transform;
        var snow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_GroundHaze.mat");
        var bounds = ChurchBounds(church);
        var corners = new[] { new Vector2(bounds.min.x,bounds.min.z),new Vector2(bounds.min.x,bounds.max.z),
            new Vector2(bounds.max.x,bounds.max.z),new Vector2(bounds.max.x,bounds.min.z) };
        for(int side=0;side<4;side++)
        {
            var a=corners[side]; var b=corners[(side+1)%4];
            int count=Mathf.CeilToInt(Vector2.Distance(a,b)/1.35f);
            for(int i=0;i<count;i++)
            {
                float from=i/(float)count,to=(i+1)/(float)count;
                var p=Vector2.Lerp(a,b,(from+to)*.5f);
                if(side==3 && p.x > -3.5f && p.x < -.5f) continue;
                // Clip to the exact opening; keep a continuous snow bank on both sides.
                if(side==3)
                {
                    float xa=Vector2.Lerp(a,b,from).x, xb=Vector2.Lerp(a,b,to).x;
                    if(xa>=-.5f && xb<-.5f) to=Mathf.InverseLerp(a.x,b.x,-.5f);
                    if(xa> -3.5f && xb<=-3.5f) from=Mathf.InverseLerp(a.x,b.x,-3.5f);
                }
                ChurchDrift(root,terrain,corners,side,from,to,snow);
            }
        }
        // Main cruciform wall footprint, plus west annex and detached bell tower.
        // Front wall is split around the portal, so the entry is never snowed shut.
        var walls=new[] {new Vector2(-4.5f,-8),new Vector2(-6.5f,-8),new Vector2(-6.5f,-3.5f),
            new Vector2(-9,-3.5f),new Vector2(-9,3.5f),new Vector2(-6.5f,3.5f),new Vector2(-6.5f,8),
            new Vector2(-5.15f,8),new Vector2(-5.2f,8.6f)};
        var outline=new System.Collections.Generic.List<Vector2>(walls);
        for(int i=1;i<=16;i++)
        { float angle=Mathf.PI-i*Mathf.PI/16;outline.Add(new Vector2(-2+Mathf.Cos(angle)*3.2f,8.6f+Mathf.Sin(angle)*3.2f)); }
        outline.AddRange(new[]{new Vector2(1.15f,8),new Vector2(2.5f,8),new Vector2(2.5f,3.5f),
            new Vector2(5,3.5f),new Vector2(5,-3.5f),new Vector2(2.5f,-3.5f),new Vector2(2.5f,-8),new Vector2(.5f,-8)});
        for(int i=0;i<outline.Count-1;i++) WallSnow(root,"WallSnow_"+contactStrips,terrain,outline[i],outline[i+1],snow);
        void Loop(Vector2[] points)
        { for(int i=0;i<points.Length;i++) WallSnow(root,"WallSnow_"+contactStrips,terrain,points[i],points[(i+1)%points.Length],snow); }
        Loop(new[]{new Vector2(-15.5f,-.5f),new Vector2(-15.5f,5.5f),new Vector2(-9,5.5f),new Vector2(-9,-.5f)});
        // Tower lower octagon, matching the Blender base profile.
        float y=Mathf.Sqrt(2.6f*2.6f-2.1f*2.1f), ny=Mathf.Sqrt(2.6f*2.6f-1.3f*1.3f);
        Loop(new[]{new Vector2(12.6f,-2-y),new Vector2(11.8f,-2-ny),new Vector2(9.2f,-2-ny),
            new Vector2(8.4f,-2-y),new Vector2(8.4f,-2+y),new Vector2(9.2f,-2+ny),
            new Vector2(11.8f,-2+ny),new Vector2(12.6f,-2+y)});
    }

    static void ValidateContact(Transform church, MeshCollider terrain)
    {
        // Independently raycast the persisted meshes' foot vertices, including the
        // 2 cm wall shelves and 1 cm shadow decals. No counter-only acceptance.
        foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            string name=filter.name;
            bool bank=name.StartsWith("ChurchDrift_");
            bool mound=name.StartsWith("PostDrift_");
            bool shadow=name.StartsWith("RockContact_");
            bool wall=name.StartsWith("WallSnow_");
            if(!bank&&!mound&&!shadow&&!wall) continue;
            var vertices=filter.sharedMesh.vertices;
            if(bank)
                for(int i=0;i<vertices.Length;i+=17)
                {
                    CheckContact(terrain,filter.transform.TransformPoint(vertices[i]),0f);
                    CheckContact(terrain,filter.transform.TransformPoint(vertices[i+16]),0f);
                }
            if(mound||shadow)
                for(int i=vertices.Length-24;i<vertices.Length;i++)
                    CheckContact(terrain,filter.transform.TransformPoint(vertices[i]),shadow?.01f:0f);
            if(wall)
            {
                int topCount=vertices.Length/2;
                for(int i=8;i<topCount;i+=9)
                {
                    CheckContact(terrain,filter.transform.TransformPoint(vertices[i]),0f);
                    CheckContact(terrain,filter.transform.TransformPoint(vertices[i+topCount]),-.02f);
                }
            }
        }
        var center=ContactGround(terrain,new Vector3(-2f,0,-12.8f));
        entryCleared=Mathf.Abs(center.y-(TerrainHeight(-2f,-12.8f)-.06f))<.003f;
        // Raycast the imported stone mesh itself, rather than assuming that the
        // five Blender steps survived the import or are still above the snow.
        int exposedTreads = 0;
        var treadHeights = new float[] { float.NegativeInfinity, float.NegativeInfinity };
        foreach (var filter in church.GetComponentsInChildren<MeshFilter>())
        {
            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            Physics.SyncTransforms();
            for (int i=0;i<2;i++)
            {
                var p=new Vector3(-2f,500f,-10.65f+i*.3f);
                if (collider.Raycast(new Ray(p,Vector3.down),out var hit,1000f))
                    treadHeights[i]=Mathf.Max(treadHeights[i],hit.point.y);
            }
            UnityEngine.Object.DestroyImmediate(collider);
        }
        for(int i=0;i<2;i++)
        {
            var p=new Vector3(-2f,0,-10.65f+i*.3f);
            float expected=churchSeatY+(i+1)*.2f;
            if(Mathf.Abs(treadHeights[i]-expected)<.01f && treadHeights[i]-ContactGround(terrain,p).y>.1f)
                exposedTreads++;
        }
        entryCleared &= exposedTreads==2;
        Debug.Log($"[CONTACT_ENTRY] exposed_treads={exposedTreads} tread0_y={treadHeights[0]:F4} tread1_y={treadHeights[1]:F4}");
        int rocks=0,posts=0;
        foreach(var tr in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        { if(tr.name.StartsWith("ClusterRock_")) rocks++; if(tr.name.StartsWith("FencePost_")) posts++; }
        Debug.Log($"[CONTACT] church_drift_segments={contactDrifts} wall_snow_strips={contactStrips} entry_cleared={entryCleared.ToString().ToLowerInvariant()} rock_shadows={contactShadows} post_drifts={contactPosts} float_errors={contactFloatErrors}");
        Debug.Log($"[CONTACT_CHECK] rocks={rocks} posts={posts} max_ground_error_m={contactMaxError:F6} entry_m=3x4 entry_depth_m=0.06 wall_thickness_m=0.02");
        if(contactDrifts<24||contactStrips<4||!entryCleared||contactShadows!=0||contactPosts!=posts||contactFloatErrors!=0)
            throw new System.InvalidOperationException("Contact hard gate failed");
    }

    static void BuildWarmDetails()
    {
        var root = new GameObject("Winter Warm Lights").transform;
        var glow = MakeLit("Assets/Settings/M_WindowGlow.mat", Hex("#FFAA42"), 0f, 0f);
        glow.EnableKeyword("_EMISSION");
        glow.SetColor("_EmissionColor", new Color(4.5f, 1.5f, .15f));
        var windowFrame = MakeLit("Assets/Settings/M_WindowInset.mat", Hex("#252C35"), 0f, 0f);
        windowFrame.SetFloat("_Cull", 0);
        void Window(string name, Vector3 position, float width, float height, float yaw)
        {
            var vs = new System.Collections.Generic.List<Vector3> { Vector3.zero };
            vs.Add(new Vector3(-width/2,0,0)); vs.Add(new Vector3(width/2,0,0));
            for (int i=0;i<=16;i++)
            { float a=i*Mathf.PI/16; vs.Add(new Vector3(Mathf.Cos(a)*width/2,height-width/2+Mathf.Sin(a)*width/2,0)); }
            var ts = new System.Collections.Generic.List<int>();
            for(int i=2;i<vs.Count-1;i++) ts.AddRange(new[]{1,i+1,i});
            // A narrow dark reveal isolates the luminous opening from warm stonework.
            float outerWidth = width + .26f, outerHeight = height + .22f;
            var rim = new System.Collections.Generic.List<Vector3> { Vector3.zero,
                new Vector3(-outerWidth/2,0,0), new Vector3(outerWidth/2,0,0) };
            for (int i=0;i<=16;i++)
            { float a=i*Mathf.PI/16; rim.Add(new Vector3(Mathf.Cos(a)*outerWidth/2,
                outerHeight-outerWidth/2+Mathf.Sin(a)*outerWidth/2,0)); }
            // Open ring, rather than a filled arch covering the luminous insert.
            int profileCount = vs.Count - 1;
            var ring = new System.Collections.Generic.List<Vector3>();
            for (int i=1;i<rim.Count;i++) ring.Add(rim[i] + Vector3.down*.09f);
            for (int i=1;i<vs.Count;i++) ring.Add(vs[i]);
            var ringTris = new System.Collections.Generic.List<int>();
            for (int i=0;i<profileCount;i++)
            {
                int j=(i+1)%profileCount;
                ringTris.AddRange(new[]{i,j,profileCount+j,i,profileCount+j,profileCount+i});
            }
            var frame = SaveMountainMesh(root,name+"_Reveal",ring.ToArray(),ringTris.ToArray(),new Material(windowFrame));
            frame.rotation=Quaternion.Euler(0,yaw,0);
            frame.position=position+Vector3.up*churchSeatY+frame.rotation*new Vector3(0,0,.025f);
            var mat = new Material(glow);
            var tr=SaveMountainMesh(root,name,vs.ToArray(),ts.ToArray(),mat);
            tr.position=position + Vector3.up * churchSeatY; tr.rotation=Quaternion.Euler(0,yaw,0);
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
        var fireGlow = new Material(glow) { name = "M_BrazierGlow" };
        fireGlow.SetColor("_EmissionColor", glow.GetColor("_EmissionColor") * .5f);
        AssetDatabase.DeleteAsset("Assets/Settings/M_BrazierGlow.mat");
        AssetDatabase.CreateAsset(fireGlow, "Assets/Settings/M_BrazierGlow.mat");
        for(int i=0;i<2;i++)
        {
            var p=new Vector3(i==0?-6f:3f,0,-14f); p.y=TerrainHeight(p.x,p.z);
            SpikeArt.Box(root,"BrazierStone_"+i,new Vector3(.85f,1.1f,.85f),p+Vector3.up*.55f,pedestal);
            var fire=GameObject.CreatePrimitive(PrimitiveType.Sphere); fire.name="BrazierEmber_"+i;
            fire.transform.SetParent(root); fire.transform.position=p+Vector3.up*1.25f;
            fire.transform.localScale=new Vector3(.65f,.8f,.65f); fire.GetComponent<Renderer>().sharedMaterial=fireGlow;
            var light=new GameObject("BrazierPoint_"+i).AddComponent<Light>();
            light.transform.position=p+Vector3.up*1.8f; light.transform.SetParent(root);
            light.type=LightType.Point; light.color=Hex("#FFB45B"); light.range=7; light.intensity=2.2f;
            light.transform.rotation=Quaternion.Euler(90,0,0);
            light.spotAngle=155; light.innerSpotAngle=110;
            light.renderMode=LightRenderMode.ForcePixel;
            var ps=new GameObject("BrazierFlames_"+i).AddComponent<ParticleSystem>();
            ps.transform.SetParent(root); ps.transform.position=p+Vector3.up*1.35f;
            var main=ps.main; main.startLifetime=.8f; main.startSpeed=.9f; main.startSize=.22f;
            main.startColor=Hex("#FFB44D"); main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission; emission.rateOverTime=16;
            var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Cone; shape.radius=.2f; shape.angle=12;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=fireGlow;
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
        // 前景低频积雪起伏：局部振幅 .1–.3m，波长约 33–52m。
        // z>=-18（含整个教堂基座）与远景均保持原高度；边界平滑淡出。
        float mask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-140f, -110f, z))
            * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-30f, -18f, z)))
            * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(65f, 90f, Mathf.Abs(x))));
        float amplitude = Mathf.Lerp(.1f, .3f, Mathf.PerlinNoise(x / 48f + 7f, z / 48f + 11f));
        float snowDrift = amplitude * (.65f * Mathf.Sin(x * .15f + z * .12f)
            + .35f * Mathf.Cos(z * .19f - x * .08f));
        // 教堂后方雪丘：单一正弦会给出光溜溜的"半个球"轮廓，叠加多尺度噪声与二次谐波打碎。
        float hillMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 50f, z));
        float hills = 5.2f * Mathf.Sin(x / 41f + 0.9f)
                    + 2.4f * Mathf.Sin(x / 18f + 2.3f)
                    + 1.1f * Mathf.Sin(x / 8.5f - 0.4f)
                    + 3.2f * (Mathf.PerlinNoise(x / 55f + 3.7f, z / 70f + 1.9f) - .5f)
                    + 1.4f * (Mathf.PerlinNoise(x / 17f + 8.1f, z / 21f + 5.3f) - .5f);
        float original = height + mask * snowDrift + hills * hillMask;
        if(z >= -40f) return original; // Existing church, approach and far field stay fixed.
        float dx = Mathf.Abs(x-ClimbRoute.CenterX(z));
        float bank = ClimbRoute.Depth * Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.5f,4.5f,dx));
        float shoulder = 1.1f * Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.5f,15f,dx));
        float route = ClimbRoute.Height(z) + bank + shoulder + snowDrift * Mathf.Clamp01((dx-4.5f)/10);
        // Blend back to the original snow field outside the playable ridge and at the old approach.
        float blend = (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(25,65,dx)))
            * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-46,-40,z)));
        return Mathf.Lerp(original,route,blend);
    }

    static MeshCollider BuildTerrain(Material ground)
    {
        const int xCells = 150; // x=-150..150，每 2 m 一列。
        var rows = new System.Collections.Generic.List<float>();
        for (int z = -230; z < -100; z++) rows.Add(z);
        for (int z = -100; z <= 100; z += 2) rows.Add(z);
        for (int z = 110; z <= 1200; z += 10) rows.Add(z);   // 原 560 处地面直接截断，形成一条硬地平线压在群山脚下
        int columns = xCells + 1;
        int triangleCount = xCells * (rows.Count - 1) * 2; // 地面延伸到 1200m 后约 6.5 万面。
        if (triangleCount >= 140000)
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
        var mesh = new Mesh { name = "Terrain", indexFormat=IndexFormat.UInt32, vertices = vertices, uv = uv, triangles = triangles };
        ClearEntrySnow(mesh);
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
        Debug.Log("[SNOW] albedo=0.86 color=#EDF3F8 shadow=#B7C9DC drift_amplitude_m=0.1..0.3 protected_z_min=-18 far_terrain_unchanged=true");
        Debug.Log($"[SPIKE] terrain mesh verts={vertices.Length} tris={triangleCount} extent=x150 z-230..1200");
        // 直接读取 x=0 的网格顶点（含横向起伏），而非简报中的近似参考值。
        float z100 = vertices[rows.IndexOf(100f) * columns + xCells / 2].y;
        float z200 = vertices[rows.IndexOf(200f) * columns + xCells / 2].y;
        float z400 = vertices[rows.IndexOf(400f) * columns + xCells / 2].y;
        float z560 = vertices[rows.IndexOf(560f) * columns + xCells / 2].y;
        Debug.Log($"[SPIKE] terrain edge sample z100={z100:F4} z200={z200:F4} z400={z400:F4} z560={z560:F4}");
        return collider;
    }

    // Snow2 is an opt-in terrain material. Shared church, paving, rock caps and
    // contact materials retain their original shader branch and transforms.
    static void BuildSnowSurface(MeshCollider terrain)
    {
        var rockCenters = new System.Collections.Generic.List<Vector2>();
        foreach (var filter in GameObject.Find("Props").GetComponentsInChildren<MeshFilter>())
            if (filter.name.StartsWith("ClusterRock_"))
                rockCenters.Add(new Vector2(filter.transform.position.x, filter.transform.position.z));
        var eye = EyeAt(5f,-82f);
        float RoadX(float z) => ClimbRoute.CenterX(z);
        float Protect(float x,float z)
        {
            float path = z >= -220 && z <= -18 ? Mathf.Abs(x-RoadX(z)) : 100f;
            float mask = Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.2f,6.5f,path));
            foreach(var r in rockCenters)
                mask *= Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.7f,3.1f,Vector2.Distance(r,new Vector2(x,z))));
            return mask;
        }
        float Drift(float x,float z)
        {
            float d=Vector2.Distance(new Vector2(x,z),new Vector2(eye.x,eye.z));
            float mask=Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,12,d))
                *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(72,80,d)))
                *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-30,-18,z))) * Protect(x,z);
            // Wind +X: long axes -19 / +12 / +27 degrees. Phase warping
            // and Perlin amplitude break the periodic crests (6/11/18 m).
            float warp=1.8f*(Mathf.PerlinNoise(x/15+31,z/15+9)-.5f);
            float waves=.50f*Mathf.Sin((-.208f*x+.978f*z+warp)*2*Mathf.PI/11)
                +.30f*Mathf.Sin((.326f*x+.946f*z-warp)*2*Mathf.PI/18+1.7f)
                +.20f*Mathf.Sin((-.454f*x+.891f*z+warp)*2*Mathf.PI/6+3.2f);
            float amp=Mathf.Lerp(.15f,.5f,Mathf.PerlinNoise(x/19+11,z/23+37));
            // A broad lee hollow and its windward bank give the foreground a
            // readable profile, with the same +X wind axis as the smaller waves.
            float lee=.48f*Mathf.Exp(-Mathf.Pow((x+7)/14f,2)-Mathf.Pow((z+64)/6.5f,2));
            float bank=.22f*Mathf.Exp(-Mathf.Pow((x-17)/10f,2)-Mathf.Pow((z+54)/3.5f,2));
            return mask*Mathf.Clamp(amp*(.8f*waves+.4f*(Mathf.PerlinNoise(x/9+4,z/7+8)-.5f))-lee+bank,-.5f,.5f);
        }
        // Sample the original mesh, preserving its exact triangulated surface
        // under the existing path, props and character; only the snow moves.
        float OldHeight(float x,float z) => terrain.Raycast(new Ray(new Vector3(x,500,z),Vector3.down),out var hit,1500)
            ? hit.point.y : TerrainHeight(x,z);
        var xs=new System.Collections.Generic.List<float>();
        var zs=new System.Collections.Generic.List<float>();
        for(float x=-150;x<=150; x+= x>=-55 && x<65 ? .25f : (x>=-76 && x<86 ? .5f : 2f)) xs.Add(x);
        for(float z=-230;z<=1200; z+=z>=-142 && z< -22 ? .25f : (z>=-230 && z< -18 ? .5f : (z>=-100&&z<100?2:10))) zs.Add(z);
        // Keep original rows at the entrance and far field.
        for(int z=-100;z<=100;z+=2) if(!zs.Contains(z)) zs.Add(z);
        for(int z=110;z<=1200;z+=10) if(!zs.Contains(z)) zs.Add(z);
        zs.Sort();
        int cols=xs.Count;
        var vs=new Vector3[cols*zs.Count]; var uv=new Vector2[vs.Length]; var relief=new Vector2[vs.Length];
        var tris=new int[(cols-1)*(zs.Count-1)*6];
        int drifts=0;
        for(int j=0;j<zs.Count;j++) for(int i=0;i<cols;i++)
        {
            float x=xs[i],z=zs[j],ridge=ForegroundRidge(x,z),h=Drift(x,z); int v=j*cols+i;
            vs[v]=new Vector3(x,OldHeight(x,z)+h+ridge,z); uv[v]=new Vector2(x,z); relief[v]=new Vector2(h+ridge,ridge);
            if(h>.045f)
            {
                bool peak=true;
                for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                    if((dx!=0||dz!=0)&&h<=Drift(x+dx*.5f,z+dz*.5f))peak=false;
                if(peak)drifts++;
            }
            if(i==cols-1||j==zs.Count-1)continue;
            int t=(j*(cols-1)+i)*6;
            tris[t]=v;tris[t+1]=v+cols;tris[t+2]=v+1;
            tris[t+3]=v+1;tris[t+4]=v+cols;tris[t+5]=v+cols+1;
        }
        // Include zero-height edge vertices: interpolation spreads the footprint
        // to the whole adjacent triangle, not just its displaced vertices.
        foregroundSurfaceClear=float.PositiveInfinity;
        for(int t=0;t<tris.Length;t+=3)
        {
            if(relief[tris[t]].y<=0 && relief[tris[t+1]].y<=0 && relief[tris[t+2]].y<=0)continue;
            for(int j=0;j<3;j++)
            {
                var p=vs[tris[t+j]];
                foregroundSurfaceClear=Mathf.Min(foregroundSurfaceClear,Mathf.Abs(p.x-ClimbRoute.CenterX(p.z)));
            }
        }
        // Preserve the original entrance subdivisions and every triangle behind
        // z=-18 exactly; resampling that 6 cm clearing would move church seating.
        var source=terrain.sharedMesh;
        var mergedVertices=new System.Collections.Generic.List<Vector3>(vs);
        var mergedUV=new System.Collections.Generic.List<Vector2>(uv);
        var mergedRelief=new System.Collections.Generic.List<Vector2>(relief);
        var mergedTriangles=new System.Collections.Generic.List<int>();
        for(int t=0;t<tris.Length;t+=3)
            if(vs[tris[t]].z<=-18 && vs[tris[t+1]].z<=-18 && vs[tris[t+2]].z<=-18)
                mergedTriangles.AddRange(new[]{tris[t],tris[t+1],tris[t+2]});
        int offset=mergedVertices.Count;var original=source.vertices;var originalTriangles=source.triangles;
        mergedVertices.AddRange(original);mergedUV.AddRange(source.uv);
        for(int v=0;v<original.Length;v++)mergedRelief.Add(Vector2.zero);
        for(int t=0;t<originalTriangles.Length;t+=3)
            if(original[originalTriangles[t]].z>=-18 && original[originalTriangles[t+1]].z>=-18 && original[originalTriangles[t+2]].z>=-18)
                mergedTriangles.AddRange(new[]{offset+originalTriangles[t],offset+originalTriangles[t+1],offset+originalTriangles[t+2]});
        var mesh=new Mesh {name="Snow2Terrain",indexFormat=IndexFormat.UInt32,vertices=mergedVertices.ToArray(),uv=mergedUV.ToArray(),uv2=mergedRelief.ToArray(),triangles=mergedTriangles.ToArray()};
        mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
        const string meshPath="Assets/Settings/Snow2Terrain.asset";
        AssetDatabase.DeleteAsset(meshPath);AssetDatabase.CreateAsset(mesh,meshPath);
        terrain.GetComponent<MeshFilter>().sharedMesh=mesh;
        terrain.sharedMesh=mesh;
        var snow=new Material(terrain.GetComponent<MeshRenderer>().sharedMaterial){name="M_Snow2Terrain"};
        snow.SetFloat("_SnowRelief",1); snow.SetColor("_SnowShadowColor",Hex("#B9CBDE"));
        const string matPath="Assets/Settings/M_Snow2Terrain.mat";
        AssetDatabase.DeleteAsset(matPath);AssetDatabase.CreateAsset(snow,matPath);
        terrain.GetComponent<MeshRenderer>().sharedMaterial=snow;
        Physics.SyncTransforms();
        float Height(float x,float z) => terrain.Raycast(new Ray(new Vector3(x,500,z),Vector3.down),out var hit,1500)?hit.point.y:TerrainHeight(x,z);
        BuildForegroundDetails(terrain);
        Debug.Log($"[SNOW2] drifts={drifts} sastrugi=heightfield exposure_patches=0 tiling_near=0.6 mid=1.6 far=3.0 shadow_tint=#B9CBDE");
        Debug.Log("[CLOSEUP_PATCHES] rock_contact=0 snow_exposure=0 shadows=realtime");
    }

    // Foreground is the 60 m ground-plane disc of the fixed art-runtime eye.
    // This does not move the camera or touch the terrace (all new vertices z < -22).
    static bool ForegroundArea(float x, float z) => z < -22f && z > -142f
        && Vector2.Distance(new Vector2(x,z),new Vector2(5,-82)) < 60f;

    static float ForegroundRidge(float x, float z)
    {
        if(!ForegroundArea(x,z)) return 0;
        float clear=Mathf.Abs(x-ClimbRoute.CenterX(z));
        if(clear<=3.7f) return 0; // Include a full triangle of guard space beyond the 3 m lane.
        // Route tangent + 28 degrees; local wind follows the switchback continuously.
        float tangent=(ClimbRoute.CenterX(z+.1f)-ClimbRoute.CenterX(z-.1f))/.2f;
        float angle=Mathf.Atan(tangent)+28f*Mathf.Deg2Rad;
        float sn=Mathf.Sin(angle),cs=Mathf.Cos(angle);
        float along=x*sn+z*cs, across=x*cs-z*sn;
        // Vary row spacing 1.4..2.0 m and phase; no additional ridge renderer.
        float row=Mathf.Floor(across/1.7f);
        float phase=Mathf.PerlinNoise(row*.37f+71,19)*5.8f;
        float col=Mathf.Floor((along+phase)/6.2f);
        float hsh=Mathf.PerlinNoise(col*.71f+37,row*.83f+91);
        float length=Mathf.Lerp(1.5f,6f,hsh);
        float u=((along+phase)-col*6.2f-3.1f)/(length*.5f);
        float width=Mathf.Lerp(.65f,.8f,Mathf.PerlinNoise(col+61,row+27));
        float offset=.12f*Mathf.Sin(along*.9f+row);
        float v=(across-(row+.5f)*1.7f-offset)/(width*.5f);
        if(Mathf.Abs(u)>=1 || Mathf.Abs(v)>=1) return 0;
        float edge=Mathf.Clamp01((60-Vector2.Distance(new Vector2(x,z),new Vector2(5,-82)))/2);
        float protection=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.7f,4.7f,clear));
        return Mathf.Lerp(.10f,.25f,hsh)*Mathf.Pow(1-u*u,.55f)
            *(1-Mathf.Abs(v))*edge*protection;
    }

    static void BuildForegroundDetails(MeshCollider terrain)
    {
        var root=new GameObject("Foreground Snowfield Details").transform;
        root.gameObject.AddComponent<ForegroundDetailProbe>();
        var random=new System.Random(731103);
        float Range(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
        float Height(float x,float z)=>terrain.Raycast(new Ray(new Vector3(x,500,z),Vector3.down),out var hit,1500)
            ?hit.point.y:TerrainHeight(x,z);
        var vs=new System.Collections.Generic.List<Vector3>();
        var colors=new System.Collections.Generic.List<Color>();
        var ts=new System.Collections.Generic.List<int>();
        float minimum=foregroundSurfaceClear;
        void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
        {
            foreach(var v in new[]{a,b,c})
            {
                if(!ForegroundArea(v.x,v.z) || Mathf.Abs(v.x-ClimbRoute.CenterX(v.z))<3f)
                    throw new System.InvalidOperationException("Foreground detail entered protected area: "+v);
                minimum=Mathf.Min(minimum,Mathf.Abs(v.x-ClimbRoute.CenterX(v.z)));
                ts.Add(vs.Count);vs.Add(v);colors.Add(color.linear);
            }
        }
        var mat=new Material(Shader.Find("Mistbell/ForegroundSnowfield")){name="M_ForegroundSnowfield"};
        const string matPath="Assets/Settings/M_ForegroundSnowfield.mat";
        AssetDatabase.DeleteAsset(matPath);AssetDatabase.CreateAsset(mat,matPath);
        void Save(string name,bool shadow)
        {
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32,vertices=vs.ToArray(),colors=colors.ToArray(),triangles=ts.ToArray()};
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path="Assets/Settings/"+name+".asset";
            AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(mesh,path);
            var go=new GameObject(name);go.transform.SetParent(root,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;
            renderer.shadowCastingMode=shadow?ShadowCastingMode.On:ShadowCastingMode.Off;
            vs.Clear();colors.Clear();ts.Clear();
        }
        bool Safe(float x,float z,float radius)=>ForegroundArea(x,z)
            &&Vector2.Distance(new Vector2(x,z),new Vector2(5,-82))<58-radius
            &&z<-23-radius &&z>-140+radius
            &&Mathf.Abs(x-ClimbRoute.CenterX(z))>4.5f+radius;
        void Rock(float x,float z,float rx,float rz,float height,float ground,bool snow)
        {
            const int sides=8;
            Vector3 Ring(int j,int ring)
            {
                float a=j*2*Mathf.PI/sides;
                float r=ring==0?1f:(ring==1?.86f:.52f);
                float y=ring==0?-.12f:(ring==1?height*.68f:height);
                return new Vector3(x+Mathf.Cos(a)*rx*r,ground+y,z+Mathf.Sin(a)*rz*r);
            }
            for(int j=0;j<sides;j++)
            {
                int n=(j+1)%sides;
                for(int k=0;k<2;k++)
                {
                    Color c=k==1&&snow?Hex("#E4EDF5"):Color.Lerp(Hex("#514D49"),Hex("#837D73"),(j%3)/2f);
                    Triangle(Ring(j,k),Ring(j,k+1),Ring(n,k+1),c);
                    Triangle(Ring(j,k),Ring(n,k+1),Ring(n,k),c);
                }
                Triangle(Ring(j,2),new Vector3(x,ground+height+.025f,z),Ring(n,2),snow?Hex("#F6FAFD"):Hex("#837D73"));
            }
        }
        // One renderer for all 5-rock clusters: 5 rocks / 100 m² of eligible snow.
        int clusters=0,rocks=0;float area=0,covered=0;
        for(float z=-142;z<-22;z+=.25f)for(float x=-55;x<65;x+=.25f)
        {
            if(!ForegroundArea(x,z)||Mathf.Abs(x-ClimbRoute.CenterX(z))<3.2f)continue;
            area+=.0625f;
            if(ForegroundRidge(x,z)>.0025f)
            {
                covered+=.0625f;
                minimum=Mathf.Min(minimum,Mathf.Abs(x-ClimbRoute.CenterX(z)));
            }
        }
        // 10 m 网格 → 77 簇/385 块，读起来像散落的木箱；疏到 22 m 网格、每簇 3 块。
        for(float z=-136;z<-26;z+=22)for(float x=-50;x<60;x+=22)
        {
            float cx=x+Range(-2,2),cz=z+Range(-2,2);
            if(!Safe(cx,cz,3))continue;
            clusters++;
            for(int i=0;i<3;i++)
            {
                float a=Range(0,Mathf.PI*2),r=Range(.3f,2.3f);
                float px=cx+Mathf.Cos(a)*r,pz=cz+Mathf.Sin(a)*r;
                Rock(px,pz,Range(.25f,.55f),Range(.3f,.7f),Range(.12f,.45f),Height(px,pz),true);rocks++;
            }
        }
        Save("FGD_Bedrock",true);
        int grass=0;
        for(int side=-1;side<=1;side+=2)
        {
            for(int attempt=0,count=0;attempt<1000&&count<8;attempt++)
            {
                float z=Range(-130,-32),x=ClimbRoute.CenterX(z)+side*Range(6,30);
                if(!Safe(x,z,1))continue;
                float ground=Height(x,z),height=Range(.15f,.35f);
                for(int j=0;j<7;j++)
                {
                    float angle=Range(0,Mathf.PI*2),r=Range(.02f,.18f);
                    var a=new Vector3(x+Mathf.Cos(angle)*r,ground-.025f,z+Mathf.Sin(angle)*r);
                    var b=a+new Vector3(Range(-.08f,.08f),height,Range(-.08f,.08f));
                    var w=new Vector3(.012f,0,.008f);var c=Hex("#3A342C");
                    Triangle(a-w,b,a+w,c);Triangle(a+w,b,a-w,c);
                    var branch=b+new Vector3(.08f,-height*.25f,.035f);
                    Triangle(b-Vector3.up*height*.5f,branch,b-Vector3.up*height*.5f+w,c);
                    Triangle(b-Vector3.up*height*.5f+w,branch,b-Vector3.up*height*.5f,c);
                }
                count++;grass++;
            }
            Save(side<0?"FGD_GrassLeft":"FGD_GrassRight",false);
        }
        // Three positions 50..54 m apart along the actual bent route; all fit
        // inside the 60 m camera disc (its diameter is 120 m, not 60 m).
        int cairns=0;
        foreach(float z in new[]{-133f,-85f,-37f})
        {
            float x=ClimbRoute.CenterX(z)+(cairns%2==0?-4.4f:4.4f),ground=Height(x,z);
            for(int j=0;j<4;j++)
            {
                float scale=1-j*.18f,h=.19f;
                Rock(x+j*.018f,z,.42f*scale,.34f*scale,h,ground,j==3);
                ground+=h-.025f;
            }
            cairns++;
        }
        Save("FGD_Cairns",true);
        float coverage=covered/area;
        Debug.Log(FormattableStringInvariant($"[FGD_LAYOUT] sastrugi_cov={coverage:F5} rock_clusters={clusters} rocks={rocks} grass_patches={grass} cairns={cairns} renderer_batches=4 path_clear_min={minimum:F4} eligible_area={area:F2} rock_density={rocks*100/area:F3} foreground_center=5,-82 radius=60"));
        if(coverage<.18f||coverage>.30f||minimum<3||grass<12||grass>20||clusters==0)
            throw new System.InvalidOperationException("Foreground layout gate failed");
    }

    // 沿揭示机位视轴横向展开；指定色板与远距薄雾共同形成层次。
    static void BuildMountains()
    {
        var root = new GameObject("Mountain Ridges").transform;
        uint fingerprint = 2166136261u;
        // Fixed integer hash: mesh generation never consumes Unity's shared random state.
        float Hash(uint value)
        {
            unchecked
            {
                value ^= value >> 16; value *= 0x7feb352du;
                value ^= value >> 15; value *= 0x846ca68bu;
                value ^= value >> 16;
            }
            return (value & 0x00ffffffu) / 16777216f;
        }
        float Rig(float phase) => 1f - Mathf.Abs(2f * Mathf.Repeat(phase, 1f) - 1f);
        // Closed 3D ridges: continuous asymmetric flanks, angular crest and end caps.
        for (int layer = 0; layer < 3; layer++)
        {
            const int n = 240, rows = 40;
            float distance = 700f + layer * 340f;
            var peakHeights = new float[n];
            var vertices = new Vector3[n * rows];
            var indices = new System.Collections.Generic.List<int>();
            for (int r = 0; r < rows; r++)
            for (int i = 0; i < n; i++)
            {
                float x = -700f + i * 1400f / (n - 1);
                float peak = 120f + layer * 55f;
                for (int octave = 0; octave < 4; octave++)
                {
                    float frequency = .006f / (1 << octave);
                    float amplitude = octave == 0 ? 72f : octave == 1 ? 48f : octave == 2 ? 10f : 4f;
                    float phaseOffset = Hash((uint)(731 + layer * 17));
                    // Lock octave 1 to alternating primary peaks: >25m height contrast,
                    // even after the two smaller, independently phased octaves are added.
                    if (octave == 1) phaseOffset = phaseOffset * .5f + .25f;
                    else if (octave > 1) phaseOffset = Hash((uint)(731 + layer * 17 + octave));
                    peak += amplitude * Rig(x * frequency + phaseOffset);
                }
                peakHeights[i] = peak;
                float t = r / (float)(rows - 1);
                // Crest lies on row 26: continuous front slope and steep rear wall.
                const float crest = 26f / (rows - 1);
                float flank = t <= crest ? t / crest : (1f - t) / (1f - crest);
                float h = -10f + (peak + 10f) * Mathf.Pow(flank, .55f);
                // Warp drainage horizontally, with only a gentle bend down each flank.
                // This breaks regular concertina folds without any height-based shelves.
                float drainageX = x + 16f * (Mathf.PerlinNoise(x * .012f + layer * 7f, 3.1f) - .5f)
                    + 4f * Mathf.Sin(t * 2.1f + x * .013f);
                float gully = 2f * (.7f * Rig(drainageX * .035f + Hash((uint)(951 + layer)))
                    + .3f * Rig(drainageX * .075f + Hash((uint)(1121 + layer)))) - 1f;
                float envelope = Mathf.Sin(t * Mathf.PI);
                h += 6f * gully * envelope * (1f - flank);
                float z = distance + (t - .5f) * 170f + 18f * gully * envelope;
                vertices[r * n + i] = new Vector3(x, h, z);
                unchecked
                {
                    fingerprint = (fingerprint ^ (uint)Mathf.RoundToInt(h * 1000f)) * 16777619u;
                    fingerprint = (fingerprint ^ (uint)Mathf.RoundToInt(z * 1000f)) * 16777619u;
                }
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
            mat.SetTexture("_SnowMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/Snow_Pixel.png"));
            mat.SetColor("_RockShade", Hex("#5B6470"));
            // Ridge snow uses broad skylight; gain is local to this material, never scene exposure.
            mat.SetColor("_SnowColor", Hex("#EAF1F7") * 1.27f);
            mat.SetColor("_SnowShade", Hex("#B9C8D4"));
            mat.SetColor("_AirColor", Hex("#7B97B4"));
            // Local height fraction follows each irregular crest, measured from the -10m foot.
            mat.SetFloat("_SnowHeight", .62f);
            mat.SetFloat("_SnowWorldFloor", 125f + layer * 55f);
            mat.SetFloat("_SnowSoftness", .004f);
            mat.SetFloat("_FogFloor", 0f);
            mat.SetFloat("_Aerial", new[] { .30f, .62f, .86f }[layer]);
            var ridge = SaveMountainMesh(root, "Mountain_" + (layer+1), flat, tris, mat);
            var ridgeMesh = ridge.GetComponent<MeshFilter>().sharedMesh;
            var normals = ridgeMesh.normals;
            for (int i = 0; i < normals.Length; i++)
            {
                float blend = normals[i].y < .30f ? .62f : .50f;
                normals[i] = Vector3.Slerp(normals[i], smoothNormals[indices[i]], blend).normalized;
            }
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
        Debug.Log($"[MOUNTAINS] cols=240 rows=40 continuousFlanks=true seed=731 octaves=4 crestRow=26 gullyZ=18m gullyH=6m snowline=.62 aerial=.30/.62/.86 meshHash={fingerprint:X8}");
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

    static void BuildSky()
    {
        var shader = Shader.Find("Mistbell/SkyGradient");
        if (shader == null) { Debug.LogError("[SKY] shader missing"); return; }
        var mat = new Material(shader) { name = "M_SkyGradient" };
        mat.SetColor("_TopColor", Hex("#43597A"));
        mat.SetColor("_MidColor", Hex("#8AA0BA"));
        mat.SetColor("_HorizonColor", Hex("#DCE6EF"));
        mat.SetColor("_CloudColor", Hex("#F4F7FA"));
        mat.SetColor("_CloudShadow", Hex("#A9B6C6"));
        mat.SetFloat("_CloudAmount", .7f);
        mat.SetFloat("_CloudScale", .6f);
        mat.SetFloat("_CloudHeight", .22f);
        AssetDatabase.DeleteAsset("Assets/Settings/M_SkyGradient.mat");
        AssetDatabase.CreateAsset(mat, "Assets/Settings/M_SkyGradient.mat");

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Sky Dome";
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.position = new Vector3(0f, 0f, -100f);
        go.transform.localScale = Vector3.one * 1300f;   // 半径 650m，必须小于相机 far(1500)
        // 天空球 UV 接缝会显示成一条竖直暗带（画面左 1/10 处）；转 37° 让接缝落到镜头外。
        go.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Debug.Log("[SKY] gradient dome r=650m clouds=0.55 z=-100 (far=1500)");
    }

    static void BuildSnowfall()
    {
        // 8x8 圆形雪花贴图，程序化生成并落盘（构建包需要真实资产）
        const string texPath = "Assets/Art/Textures/Snowflake.png";
        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(4f, 4f)) / 3.4f;
            float a = Mathf.Clamp01(1f - d);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
        }
        tex.Apply();
        System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        var mat = new Material(Shader.Find("Sprites/Default")) { name = "M_Snowflake" };
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        mat.SetColor("_Color", new Color(1f, 1f, 1f, .85f));
        AssetDatabase.DeleteAsset("Assets/Settings/M_Snowflake.mat");
        AssetDatabase.CreateAsset(mat, "Assets/Settings/M_Snowflake.mat");

        var go = new GameObject("Snowfall");
        go.transform.position = new Vector3(0f, 26f, -80f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 18f;
        main.startSpeed = .6f;
        main.startSize = new ParticleSystem.MinMaxCurve(.22f, .45f);
        main.startColor = new Color(1f, 1f, 1f, .9f);
        main.maxParticles = 9000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = .012f;
        var em = ps.emission; em.rateOverTime = 2600f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(320f, 44f, 300f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(.9f, 1.7f);
        vel.y = new ParticleSystem.MinMaxCurve(-1.9f, -1.3f);
        vel.z = new ParticleSystem.MinMaxCurve(.2f, .8f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Debug.Log("[SNOWFALL] rate=1800/s lifetime=18s size=.06-.14m box=320x44x300 wind=+x");
    }

    static void BuildMist()
    {
        // No camera-facing sheets: fog exists only at world Y 0..15m in the surface shaders.
        Debug.Log("[SPIKE] mist height=0..15m terrain/ridge fragments only; screen bands=0");
    }

    static void BuildFootprints(MeshCollider terrain)
    {
        var root = new GameObject("Snow Footprints").transform;
        var material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_GroundHaze.mat"))
            { name = "M_Footprint" };
        material.SetFloat("_Footprint", 1f);
        material.SetFloat("_TrailSrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_TrailDstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_TrailZWrite", 0);
        material.renderQueue = 3000;
        // 原来只靠 _Footprint 的 8% 压暗，在 0.86 albedo 的雪上完全看不见；直接压基色
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(Hex("#AFC0D2").r, Hex("#AFC0D2").g, Hex("#AFC0D2").b, .55f));
        if (material.HasProperty("_SnowAlbedo")) material.SetFloat("_SnowAlbedo", 0.62f);
        AssetDatabase.DeleteAsset("Assets/Settings/M_Footprint.mat");
        AssetDatabase.CreateAsset(material, "Assets/Settings/M_Footprint.mat");
        // 从相机脚下一直铺到台阶：让视线沿脚印走上山（原来只有教堂前 20 个，前景是空的）
        const int steps = 64;
        for (int i = 0; i < steps; i++)
        {
            float t = i / (float)(steps - 1);
            float z = Mathf.Lerp(-208f, -14f, t);
            float pathX = ClimbRoute.CenterX(z);
            float x = (z > -18f ? 2.1f + 2f * (1f - t) * (1f - t) : pathX) + (i % 2 == 0 ? -.18f : .18f);
            var vertices = new Vector3[25];
            for (int v = 0; v < vertices.Length; v++)
            {
                float angle = (v - 1) * Mathf.PI * 2 / 24f;
                var point = new Vector3(x, 0, z) + (v == 0 ? Vector3.zero :
                    new Vector3(Mathf.Cos(angle) * .15f, 0, Mathf.Sin(angle) * .25f));
                terrain.Raycast(new Ray(point + Vector3.up * 100, Vector3.down), out var hit, 200);
                vertices[v] = new Vector3(point.x, hit.point.y + .012f, point.z);
            }
            var triangles = new int[72];
            for (int v = 0; v < 24; v++)
            { triangles[v*3]=0; triangles[v*3+1]=1+(v+1)%24; triangles[v*3+2]=v+1; }
            SaveMountainMesh(root, "Footprint_"+i, vertices, triangles, new Material(material));
        }
        Debug.Log("[SNOW] footprints=64 ellipse=.3x.5m darken=.08 curve_z=-208..-14 follows_path=true");
    }

    static void BuildStonePath(MeshCollider terrainCollider, Material stone)
    {
        var samples = new System.Collections.Generic.List<Vector3>();
        for(int segment=0;segment<4;segment++)
        {
            int count=Mathf.CeilToInt(Vector2.Distance(ClimbRoute.Points[segment],ClimbRoute.Points[segment+1])/4.8f);
            for(int i=0;i<count;i++) samples.Add(ClimbRoute.Point(segment,i/(float)count));
        }
        samples.Add(ClimbRoute.Point(3,1));
        var points=samples.ToArray();
        for (int i = 0; i < points.Length; i++)
            if (terrainCollider.Raycast(new Ray(points[i] + Vector3.up * 100f, Vector3.down),
                                out RaycastHit hit, 200f))
                points[i].y = hit.point.y + 0.035f;
        var root = new GameObject("Stone Path").transform;

        // 独立路面材质：不叠雪覆盖，压暗到潮湿石板
        var paving = new Material(stone) { name = "M_Paving" };
        paving.SetTextureScale("_BaseMap", Vector2.one / 4.8f);
        if (paving.HasProperty("_BaseColor")) paving.SetColor("_BaseColor", Hex("#8C8378"));
        if (paving.HasProperty("_StoneSurface")) paving.SetFloat("_StoneSurface", 0f);
        AssetDatabase.DeleteAsset("Assets/Settings/M_Paving.mat");
        AssetDatabase.CreateAsset(paving, "Assets/Settings/M_Paving.mat");

        int tiles = 0;
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 a = points[i], b = points[i + 1];
            Vector3 delta = b - a;
            float len = delta.magnitude;
            Vector3 fwd = delta.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            for (int row = 0; row < 2; row++)
            for (int k = 0; k < 3; k++)
            {
                float along = (k + .5f) / 3f;
                Vector3 center = a + fwd * (len * along) + right * ((row - .5f) * 1.58f);
                if (terrainCollider.Raycast(new Ray(center + Vector3.up * 100f, Vector3.down),
                        out RaycastHit hit, 200f))
                    center.y = hit.point.y + 0.035f;
                float w = 1.52f;
                float d = (len / 3f) - .06f;               // 4–8 cm 缝
                var tile = SpikeArt.Box(root, "Paving_" + (tiles++),
                    new Vector3(w, .09f, d), center, paving);
                tile.transform.rotation = Quaternion.LookRotation(fwd);
            }
        }
        Debug.Log($"[PATH2] tiles={tiles} gap_cm=4..8 rotated=true lift_cm=3.5 material=M_Paving #8C8378");
    }

    static bool ArgFlag(string name) =>
        System.Array.IndexOf(System.Environment.GetCommandLineArgs(), name) >= 0;

    static void BuildProps(MeshCollider terrain, Material stone, Material wood)
    {
        var root = new GameObject("Props").transform;
        var roadside = new GameObject("Roadside Props").transform;
        roadside.SetParent(root, false);
        var rockMat = MakeLit("Assets/Settings/M_WinterRock.mat", Hex("#6B6157"), 0f, .02f);
        var rockShade = MakeLit("Assets/Settings/M_WinterRockShade.mat", Hex("#4E463E"), 0f, .02f);
        var snowMat = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_GroundHaze.mat"))
            { name = "M_RockSnow" };
        snowMat.SetColor("_BaseColor", Hex("#E7EDF4"));
        snowMat.SetFloat("_SnowAlbedo", .76f);
        AssetDatabase.DeleteAsset("Assets/Settings/M_RockSnow.mat");
        AssetDatabase.CreateAsset(snowMat, "Assets/Settings/M_RockSnow.mat");
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
        for (int attempt = 0; attempt < 3000 && centers.Count < 16; attempt++)
        {
            var candidate = new Vector2(Range(-40f, 48f), Range(-90f, 24f));
            float nearest = float.PositiveInfinity;
            foreach (var center in centers) nearest = Mathf.Min(nearest, Vector2.Distance(center, candidate));
            if (nearest < 8f || nearest > 25f) continue;
            if (Mathf.Abs(candidate.x) < 18f && candidate.y > -18f) continue;
            centers.Add(candidate);
        }
        int rocks = 0, halfBuried = 0, minFaces = int.MaxValue, maxFaces = 0;
        var minSize = Vector3.one * float.PositiveInfinity;
        var maxSize = Vector3.one * float.NegativeInfinity;
        foreach (var center in centers)
        {
            float radius = Range(.65f, 1.1f);
            int count = rnd.Next(3, 7);
            for (int j = 0; j < count && rocks < 80; j++)
            {
                float angle = Range(0f, Mathf.PI * 2f), offset = radius * Mathf.Sqrt(Range(0f, 1f));
                var location = new Vector3(center.x + Mathf.Cos(angle) * offset, 0f,
                    center.y + Mathf.Sin(angle) * offset);
                // 散布半径也不可侵入基座保护区。
                if (Mathf.Abs(location.x) < 18f && location.z > -18f) continue;
                var p = Snap(location);
                const int faces = 80;
                var mesh = BuildWinterRock(rnd);
                var rock = new GameObject("ClusterRock_" + rocks);
                rock.transform.SetParent(root, false);
                // 三轴旋转已烘入网格；以实际世界轴包围盒检查厚度与尺寸。
                var size = mesh.bounds.size;
                if (size.y / Mathf.Max(size.x, size.z) < .45f ||
                    Mathf.Min(size.x, Mathf.Min(size.y, size.z)) < .45f - .00001f ||
                    Mathf.Max(size.x, Mathf.Max(size.y, size.z)) > 1.5f + .00001f)
                    throw new System.InvalidOperationException("Winter boulder bounds violated: " + size);
                float burial = Range(.30f, .45f);
                float bottomAtGround = p.y - mesh.bounds.min.y;
                rock.transform.position = new Vector3(p.x, bottomAtGround - burial * size.y, p.z);
                if (burial >= .30f && burial <= .45f) halfBuried++;
                minSize = Vector3.Min(minSize, size); maxSize = Vector3.Max(maxSize, size);
                minFaces = Mathf.Min(minFaces, faces); maxFaces = Mathf.Max(maxFaces, faces);
                string path = "Assets/Settings/ClusterRock_" + (rocks + 1) + ".asset";
                AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
                rock.AddComponent<MeshFilter>().sharedMesh = mesh;
                rock.AddComponent<MeshRenderer>().sharedMaterials = new[] {rockMat, snowMat, rockShade};
                Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "[ROCK] id={0} actual_size_m={1:F4},{2:F4},{3:F4} faces={4} burial_height_ratio={5:F4} flat_normals=true",
                    rocks, size.x, size.y, size.z, faces, burial));
                // Closeup fallback: the contact patches still form stars on the slope.
                // Keep only the rock's realtime shadow, as requested.
                rocks++;
            }
        }
        float halfBuriedRatio = rocks == 0 ? 0f : halfBuried / (float)rocks;
        if (rocks < 40 || rocks > 80 || halfBuriedRatio < .95f)
            throw new System.InvalidOperationException("Winter rock specification violated");
        Debug.Log($"[ROCK2] count={rocks} faces_per_rock=80 radial_jitter=0.18 burial=0.30..0.45 size=0.45..1.5 flat_top=false");
        int posts = 0, rails = 0;
        // 两侧栏栅 + 高度抖动 + 柱顶积雪：单侧细栏在远景里会退化成一条悬浮的线。
        for (int side = 0; side < 2; side++)
        {
            float offset = side == 0 ? -2.6f : 2.6f;
            Vector3 previous = Vector3.zero;
            int i=0;
            for(int segment=0;segment<4;segment++)
            for(int step=0;step<=Mathf.CeilToInt(Vector2.Distance(ClimbRoute.Points[segment],ClimbRoute.Points[segment+1])/1.6f);step++)
            {
                int count=Mathf.CeilToInt(Vector2.Distance(ClimbRoute.Points[segment],ClimbRoute.Points[segment+1])/1.6f);
                if(segment>0 && step==0) continue;
                Vector3 p = Snap(ClimbRoute.Point(segment,step/(float)count,offset));
                if(p.z>-19.25f) continue; // Six metres clear before the stairs.
                i++;
                float jitter = 1f - ((i * 37) % 13) / 60f;
                float h = 1.05f * jitter;
                SpikeArt.Box(roadside, "FencePost_" + posts++, new Vector3(0.16f, h, 0.16f),
                    p + Vector3.up * (h * 0.5f - 0.11f), fenceWood);
                SpikeArt.Box(roadside, "FenceSnow_" + (posts - 1), new Vector3(0.19f, 0.05f, 0.19f),
                    p + Vector3.up * (h - 0.10f), snowMat);
                if (i > 1)
                    foreach (float height in new[] { 0.44f, 0.80f })
                    {
                        Vector3 a = previous + Vector3.up * height, b = p + Vector3.up * height;
                        var delta = b - a;
                        var rail = SpikeArt.Box(roadside, "FenceRail_" + rails++,
                            new Vector3(0.06f, 0.05f, delta.magnitude), (a + b) * 0.5f, fenceWood);
                        rail.transform.rotation = Quaternion.LookRotation(delta);
                    }
                ContactMound(roadside, "PostDrift_" + (posts - 1), terrain, p,
                    Vector2.one * Mathf.Lerp(.18f, .28f, ((posts * 17) % 11) / 10f),
                    Mathf.Lerp(.10f, .16f, ((posts * 7) % 9) / 8f), snowMat);
                contactPosts++;
                previous = p;
            }
        }
        // Sparse dry verge in Z01, low paired shrubs at the treeline, open meadow in Z03.
        for(int segment=0;segment<3;segment++)
        for(int k=0;k<24;k++)
        {
            float t=(k+.5f)/24;
            float side=k%2==0?-1:1;
            var p=Snap(ClimbRoute.Point(segment,t,side*Range(3.6f,5.4f)));
            if(segment==0)
            {
                SpikeArt.Box(roadside,"TrailGravel_"+k,new Vector3(.25f,.12f,.3f),p+Vector3.up*.04f,rockMat);
                for(int blade=0;blade<3;blade++)
                    SpikeArt.Box(roadside,"DryGrass_"+k+"_"+blade,new Vector3(.025f,.24f,.035f),p+new Vector3(blade*.06f,.12f,.24f),fenceWood);
            }
            else if(segment==1)
                for(int stem=0;stem<4;stem++) {
                    var bush=SpikeArt.Box(roadside,"TrailShrub_"+k+"_"+stem,new Vector3(.06f,.48f,.06f),p+new Vector3(stem*.12f,.22f,0),fenceWood);
                    bush.transform.rotation=Quaternion.Euler(15,stem*43,stem*12-18);
                }
            else if(k%4==0)
                SpikeArt.Box(roadside,"MeadowStone_"+k,new Vector3(.5f,.24f,.4f),p+Vector3.up*.08f,rockMat);
        }
        var boundary=Snap(ClimbRoute.Point(0,1,1.5f));
        SpikeArt.Box(roadside,"BoundaryStone",new Vector3(.5f,.8f,.35f),boundary+Vector3.up*.4f,rockMat);
        Debug.Log($"[SPIKE] fence posts={posts} rails={rails}");
    }

    // Subdiv-1 icosphere: 42 unique positions / 80 triangles. Split render
    // vertices per triangle so the boulder keeps crisp, low-poly face normals.
    static Mesh BuildWinterRock(System.Random random)
    {
        float Range(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
        float t = (1f + Mathf.Sqrt(5f)) * .5f;
        var points = new System.Collections.Generic.List<Vector3> {
            new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0),
            new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t),
            new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1)
        };
        for (int i = 0; i < points.Count; i++) points[i] = points[i].normalized;
        int[] ico = {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        var midpoints = new System.Collections.Generic.Dictionary<long, int>();
        int Midpoint(int a, int b)
        {
            long key = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
            if (midpoints.TryGetValue(key, out int index)) return index;
            index = points.Count;
            points.Add((points[a] + points[b]).normalized);
            midpoints.Add(key, index);
            return index;
        }
        var triangles = new System.Collections.Generic.List<int>();
        for (int i = 0; i < ico.Length; i += 3)
        {
            int a = ico[i], b = ico[i+1], c = ico[i+2];
            int ab = Midpoint(a,b), bc = Midpoint(b,c), ca = Midpoint(c,a);
            triangles.AddRange(new[] {a,ab,ca, b,bc,ab, c,ca,bc, ab,bc,ca});
        }
        if (points.Count != 42 || triangles.Count != 240)
            throw new System.InvalidOperationException("Icosphere topology violated");
        for (int i = 0; i < points.Count; i++) points[i] *= Range(.82f, 1.18f);

        Vector3[] shaped = new Vector3[points.Count];
        Bounds bounds = default;
        bool valid = false;
        for (int attempt = 0; attempt < 128 && !valid; attempt++)
        {
            var scale = new Vector3(Range(.55f,1.25f), Range(.60f,1.05f), Range(.55f,1.25f));
            var rotation = Quaternion.Euler(Range(-12f,12f), Range(0f,360f), Range(-12f,12f));
            for (int i = 0; i < points.Count; i++) shaped[i] = rotation * Vector3.Scale(points[i], scale);
            bounds = new Bounds(shaped[0], Vector3.zero);
            foreach (var point in shaped) bounds.Encapsulate(point);
            var size = bounds.size;
            valid = size.y / Mathf.Max(size.x,size.z) >= .45f &&
                Mathf.Min(size.x,Mathf.Min(size.y,size.z)) / Mathf.Max(size.x,Mathf.Max(size.y,size.z)) > .31f;
            // Reject near-horizontal faces as well as slab-like bounding boxes.
            for (int i = 0; i < triangles.Count && valid; i += 3)
            {
                var n = Vector3.Cross(shaped[triangles[i+1]] - shaped[triangles[i]],
                    shaped[triangles[i+2]] - shaped[triangles[i]]).normalized;
                if (Mathf.Abs(n.y) > .9999f) valid = false;
            }
        }
        if (!valid) throw new System.InvalidOperationException("Cannot shape winter boulder");
        // Uniform metre scaling preserves the jitter and proportions; all AABB
        // dimensions stay in .45..1.5 m, without flattening the vertical axis.
        float smallest = Mathf.Min(bounds.size.x, Mathf.Min(bounds.size.y,bounds.size.z));
        float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y,bounds.size.z));
        float minMetres = .45f / smallest, maxMetres = 1.5f / largest;
        // Favour substantial boulders within the same size bounds, preserving
        // their readable mass and cast shadows after 30–45% burial.
        float metres = Range(Mathf.Lerp(minMetres, maxMetres, .5f), maxMetres);
        for (int i = 0; i < shaped.Length; i++) shaped[i] = (shaped[i] - bounds.center) * metres;
        var vertices = new System.Collections.Generic.List<Vector3>();
        var groups = new System.Collections.Generic.List<int>[] {
            new System.Collections.Generic.List<int>(), new System.Collections.Generic.List<int>(),
            new System.Collections.Generic.List<int>() };
        for (int i = 0; i < triangles.Count; i += 3)
        {
            var a = shaped[triangles[i]];
            var b = shaped[triangles[i+1]];
            var c = shaped[triangles[i+2]];
            if (Vector3.Dot(Vector3.Cross(b-a,c-a), (a+b+c)/3f) < 0f)
            { var swap = b; b = c; c = swap; }
            var normal = Vector3.Cross(b-a,c-a).normalized;
            int material = normal.y > .62f ? 1 :
                (Vector3.Dot(normal,new Vector3(-.6f,.34f,-.72f)) > 0f ? 0 : 2);
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            groups[material].AddRange(new[] {start,start+1,start+2});
        }
        var mesh = new Mesh {name="WinterRock_Icosphere", vertices=vertices.ToArray(), subMeshCount=3};
        for (int i = 0; i < 3; i++) mesh.SetTriangles(groups[i],i);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
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
