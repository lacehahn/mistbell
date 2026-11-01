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
        var roofMat = ChurchMaterial("roof", "#A06B4A");
        var wood = ChurchMaterial("wood", "#6B4A32");
        var ground = SpikeArt.MakeTextured("Assets/Settings/M_Ground.mat", TEX + "grass.png", TEX + "grass_n.png",
                                           3.2f, Hex("#5E7A2E"), 0.08f, 0.8f);

        // 使用指定色板作为材质基色，避免贴图工具的均值补偿产生 HDR 色值。
        ground.SetColor("_BaseColor", Hex("#5E7A2E"));
        EditorUtility.SetDirty(stone);
        EditorUtility.SetDirty(roofMat);
        EditorUtility.SetDirty(ground);

        // ---------- 地形：一段上坡（坡面在 z 方向的解析式 y = GroundY(z)）----------
        var slope = SpikeArt.Box(null, "Slope", new Vector3(200f, 2f, 260f),
                                 new Vector3(0f, SlopeOriginY(), -30f), ground);
        slope.transform.localRotation = Quaternion.Euler(-SlopeDeg, 0f, 0f);

        Physics.SyncTransforms();
        BuildMountains();
        BuildStonePath(slope.GetComponent<Collider>(), stone);

        // ---------- Blender FBX：保留导出时的米制比例与原点 ----------
        var church = ImportChurch(stone, roofMat, wood);
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
        if (slope.GetComponent<Collider>().Raycast(
                new Ray(new Vector3(pilgrimPos.x, 500f, pilgrimPos.z), Vector3.down),
                out RaycastHit terrainHit, 1000f))
            terrainY = terrainHit.point.y;
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
        sun.intensity = 2.2f;   // 提亮教堂立面与像素细节。
        sun.color = new Color(1f, 0.94f, 0.85f);
        sun.transform.rotation = Quaternion.Euler(38f, 25f, 0f);   // 光从相机侧约 45° 打过来：相机可见的两个立面一亮一中，保留体积感
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;

        var fill = new GameObject("Warm Fill").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.6f;
        fill.color = Hex("#FFD9A0");
        fill.transform.rotation = Quaternion.Euler(40f, 205f, 0f);
        fill.shadows = LightShadows.None;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.49f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.62f, 0.68f, 0.74f);
        RenderSettings.fogStartDistance = 80f;
        RenderSettings.fogEndDistance = 1800f; // 远景薄雾保留 250–700 m 山脊的色阶。

        // ---------- 后期 Volume ----------
        var profile = BuildProfile("Assets/Settings/MistbellPost.asset");
        var volGo = new GameObject("Global Volume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1f;
        vol.profile = profile;

        // ---------- 相机 ----------
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        cam.fieldOfView = Fov;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1500f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.66f, 0.72f, 0.80f);
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
        var groundPose = ShotRig.Make("art_ground", Vector3.zero, 26f, 7f, 15f, 36f);
        groundPose.position = EyeAt(0f, -14f);
        rig.poses.Add(groundPose);

        rig.Apply(0);

        Directory.CreateDirectory("Assets/Scenes");
        const string scenePath = "Assets/Scenes/ArtSpike.unity";

        var shotter = new GameObject("Screenshotter");
        shotter.AddComponent<Screenshotter>();

        // Spike 直接进入场景，避免 macOS 无人值守验收停在启动画面的渲染等待。
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.MTRendering = false; // macOS Spike 截图使用单渲染线程，避免启动时线程等待。
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

    static Transform ImportChurch(Material stone, Material roof, Material wood)
    {
        const string path = "Assets/Art/Church/gergeti_church.fbx";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new System.InvalidOperationException("Cannot import " + path);
        var church = ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).transform;
        church.name = "Church";
        church.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        church.localScale = Vector3.one;
        int stoneCount = 0, roofCount = 0, woodCount = 0;
        long triangles = 0;
        foreach (var filter in church.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            if (mesh == null) continue;
            for (int i = 0; i < mesh.subMeshCount; i++)
                triangles += (long)mesh.GetIndexCount(i) / 3;
            var renderer = filter.GetComponent<Renderer>();
            if (renderer == null) continue;
            var materials = new Material[mesh.subMeshCount];
            var original = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string names = (filter.name + " " + mesh.name + " " +
                    (i < original.Length && original[i] != null ? original[i].name : "")).ToLowerInvariant();
                if (names.Contains("roof")) { materials[i] = roof; roofCount++; }
                else if (names.Contains("wood")) { materials[i] = wood; woodCount++; }
                else { materials[i] = stone; stoneCount++; }
            }
            renderer.sharedMaterials = materials;
        }
        var bounds = ChurchBounds(church);
        Debug.Log($"[SPIKE] church imported tris={triangles} bounds={bounds.size.x:F2}x{bounds.size.y:F2}");
        Debug.Log($"[SPIKE] church bounds min={bounds.min} max={bounds.max} root scale={church.localScale} rotation={church.eulerAngles}");
        Debug.Log($"[SPIKE] church materials stone={stoneCount} roof={roofCount} wood={woodCount} (submesh assignments; unclassified=stone)");
        if (triangles <= 5000) throw new System.InvalidOperationException("Church must exceed 5000 triangles");
        return church;
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
            if (!r.name.ToLowerInvariant().Contains("porch")) continue;
            if (!hasPorch) { porchBounds = r.bounds; hasPorch = true; }
            else porchBounds.Encapsulate(r.bounds);
        }
        if (!hasPorch) throw new System.InvalidOperationException("Church has no porch renderers");
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

    // 沿揭示机位视轴横向展开；指定色板与远距薄雾共同形成层次。
    static void BuildMountains()
    {
        const int samples = 81;
        var forward = Quaternion.Euler(0f, -2.632f, 0f) * Vector3.forward;
        var right = Quaternion.Euler(0f, -2.632f, 0f) * Vector3.right;
        float[] distances = { 250f, 450f, 700f };
        string[] colors = { "#4E6478", "#6E8399", "#93A6B8" };
        var root = new GameObject("Mountain Ridges").transform;
        for (int layer = 0; layer < 3; layer++)
        {
            var random = new System.Random(7301 + layer * 101);
            var heights = new float[samples];
            for (int i = 0; i < samples; i++)
                heights[i] = Mathf.Clamp(290f + 52f * Mathf.Sin(i * 0.21f + layer)
                    + 36f * Mathf.Sin(i * 0.57f + layer * 2f)
                    + (float)random.NextDouble() * 42f - 21f, 200f, 400f);
            var vertices = new Vector3[samples * 2];
            var triangles = new int[(samples - 1) * 6];
            for (int i = 0; i < samples; i++)
            {
                var lateral = right * (-600f + 1200f * i / (samples - 1));
                vertices[i * 2] = lateral + Vector3.down * 150f;
                vertices[i * 2 + 1] = lateral + Vector3.up * heights[i];
                if (i == samples - 1) continue;
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2; triangles[t + 4] = v + 1; triangles[t + 5] = v + 3;
            }
            var ridge = SaveMountainMesh(root, "Mountain_" + (layer + 1), vertices, triangles,
                                         Hex(colors[layer]));
            // 贴地仰视约 13°；山体基底沉入谷底，使三层山脊进入上半幅。
            float[] valleyDepths = { 275f, 215f, 90f };
            ridge.position = forward * distances[layer] + Vector3.down * valleyDepths[layer];
            Debug.Log($"[SPIKE] ridge {layer + 1} vertices={vertices.Length} distance={distances[layer]:F0}");
            if (layer == 2)
            {
                var snowVertices = (Vector3[])vertices.Clone();
                for (int i = 0; i < samples; i++)
                {
                    snowVertices[i * 2] = vertices[i * 2 + 1] - Vector3.up * 9f;
                    snowVertices[i * 2 + 1] = vertices[i * 2 + 1];
                }
                var snow = SaveMountainMesh(ridge, "Mountain_Snow", snowVertices, triangles, Hex("#EAF1F6"));
                snow.localPosition = -forward * 0.1f;
            }
        }
    }

    static Transform SaveMountainMesh(Transform parent, string name, Vector3[] vertices,
                                      int[] triangles, Color color)
    {
        var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        string meshPath = "Assets/Settings/" + name + ".asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_" + name };
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Cull", 0f);
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

    static void BuildStonePath(Collider terrain, Material stone)
    {
        // 12 段 S 形石板路，自下坡连接正面台阶。
        var points = new Vector3[13];
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / 12f;
            points[i] = new Vector3(3f * Mathf.Sin(t * 2f * Mathf.PI), 0f, Mathf.Lerp(-70f, -13.25f, t));
        }
        for (int i = 0; i < points.Length; i++)
            if (terrain.Raycast(new Ray(points[i] + Vector3.up * 100f, Vector3.down),
                                out RaycastHit hit, 200f))
                points[i].y = hit.point.y + 0.035f;
        var root = new GameObject("Stone Path").transform;
        for (int i = 0; i < points.Length - 1; i++)
        {
            var delta = points[i + 1] - points[i];
            var segment = SpikeArt.Box(root, "Paving_" + (i + 1),
                new Vector3(3.2f, 0.07f, delta.magnitude + 0.08f),
                (points[i] + points[i + 1]) * 0.5f - Vector3.up * 0.035f, stone);
            segment.transform.rotation = Quaternion.LookRotation(delta, terrain.transform.up);
        }
        Debug.Log($"[SPIKE] stone path segments={points.Length - 1} material=existing M_Stone (stone_ashlar; no Substance material available)");
    }

    /// <summary>坡面解析式：6° 上坡，z=0 处地面 y=0。</summary>
    static float GroundY(float z) => Mathf.Tan(SlopeDeg * Mathf.Deg2Rad) * z;

    // 顶面：y = posY + tan(a)*(z-posZ) + 1/cos(a)。
    // posZ=-30；半深 130 只控制覆盖范围，不能作为中心偏移代入。
    static float SlopeOriginY() => GroundY(-30f) - 1f / Mathf.Cos(SlopeDeg * Mathf.Deg2Rad);
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
        color.postExposure.Override(0.2f);
        color.contrast.Override(14f);
        color.saturation.Override(-6f);
        color.colorFilter.Override(new Color(1f, 0.97f, 0.92f));

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.28f);
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
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return profile;
    }
}
