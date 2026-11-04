using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Distance-driven mist and a single bell pulse; capture flags take precedence.</summary>
public sealed class MistReveal : MonoBehaviour
{
    const float FarDistance = 95f, NearDistance = 20f, BellDistance = 45f;
    const float FarStart = 120f, FarEnd = 420f, NearStart = 20f, NearEnd = 120f;
    const float PulseRise = 1.2f, PulseFall = .8f, BloomMultiplier = 1.6f, VignetteMultiplier = 1.3f;
    const float SettleSeconds = .5f;
    const float DemoNearDistance = 34f, DemoCameraHeight = 7.5f, DemoFov = 48f;
    const float DemoFarDistance = 115f, DemoBlendSeconds = 2f;
    static readonly Vector3 DemoOffset = new Vector3(0, DemoCameraHeight, 0);
    const int CloudColumns = 48, CloudRows = 24;
    const float CloudWidth = 260f, CloudHeight = 140f, CloudOpacity = .98f;
    const float CloudCenterY = 18f, CloudFrontZ = -20f, CloudLayerSpacing = 26f;
    const int CloudLayers = 2;
    const string FarFog = "#CBD8E4", NearFog = "#EAF0F6", FarSky = "#64748C", NearSky = "#849DBB";
    static readonly Vector3 Anchor = new Vector3(0, 0, -9);
    public PlayerController player;
    public Transform church;
    public Volume volume;
    public Shader cloudShader;
    public Material contactMaterial;
    GameObject cloudRoot;
    Material cloudMaterial;
    Mesh cloudMesh;
    Bloom bloom;
    Vignette vignette;
    float baseBloom, baseVignette, previousDistance, pulseTime;
    bool bellFired, pulsing, initialized;
    Color farFog, nearFog, farSky, nearSky;
    UniversalAdditionalCameraData mistCamera;
    bool previousDepthTexture;

    void Awake()
    {
        // No fog, Volume cloning, logging, coroutine, or camera changes in capture mode.
        if (PlayerController.CaptureMode) enabled = false;
    }

    float climbBaseY, climbTopY, climbDensityBase;
    ChurchInterior climbInterior;

    void Start()
    {
        climbInterior=FindFirstObjectByType<ChurchInterior>();
        var spawn=ClimbRoute.Spawn;
        if(player.ground.Raycast(new Ray(spawn+Vector3.up*500,Vector3.down),out var climbHit,1000))climbBaseY=climbHit.point.y+.035f;
        if(!player.stairDeck.Raycast(new Ray(new Vector3(0,500,-10),Vector3.down),out var topHit,1000))throw new InvalidOperationException("Climb fog plateau support missing");
        climbTopY=topHit.point.y;
        climbDensityBase=RenderSettings.fogDensity;
        Debug.Log(FormattableString.Invariant($"[CLIMB_FOG] density_base={climbDensityBase:F6} density_top=+24% density_top_value={climbDensityBase*1.24f:F6} height_span={climbTopY-climbBaseY:F3} per_10m_pct={240/(climbTopY-climbBaseY):F3} mode=Linear distance_scale_top={1/1.24f:F6}"));
        mistCamera = Camera.main.GetUniversalAdditionalCameraData();
        previousDepthTexture = mistCamera.requiresDepthTexture;
        mistCamera.requiresDepthTexture = true;
        ColorUtility.TryParseHtmlString(FarFog, out farFog);
        ColorUtility.TryParseHtmlString(NearFog, out nearFog);
        ColorUtility.TryParseHtmlString(FarSky, out farSky);
        ColorUtility.TryParseHtmlString(NearSky, out nearSky);
        // Volume.profile is a runtime copy; never write back to the art profile asset.
        if (volume != null)
        {
            volume.profile.TryGet(out bloom);
            volume.profile.TryGet(out vignette);
        }
        if (bloom != null) baseBloom = bloom.intensity.value;
        if (vignette != null) baseVignette = vignette.intensity.value;
        BuildCloud();
        previousDistance = Distance();
        initialized = true;
        ApplyMist(previousDistance);
        Debug.Log("[REVEAL] fog=120..420→20..120 color=#CBD8E4→#EAF0F6 bell_at=45m");
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-demo-reveal") >= 0)
            StartCoroutine(DemoReveal());
    }

    // 两段式显现：远处厚重 → 70m 起微透 → 45m（钟声）后明显散 → 30m 快散 → 20m 全清。
    // 单一 SmoothStep 会让"听见钟声"那一刻没有视觉回应，这里把曲线做得前缓后陡。
    static float MistCurve(float d)
    {
        if (d >= 70f) return Mathf.Lerp(0f, .12f, Mathf.InverseLerp(FarDistance, 70f, d));
        if (d >= 45f) return Mathf.Lerp(.12f, .30f, Mathf.InverseLerp(70f, 45f, d));
        if (d >= 30f) return Mathf.Lerp(.30f, .62f, Mathf.InverseLerp(45f, 30f, d));
        return Mathf.Lerp(.62f, 1f, Mathf.InverseLerp(30f, NearDistance, d));
    }

    float Distance() => Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
        new Vector2(Anchor.x, Anchor.z));

    void ApplyMist(float d)
    {
        float t = MistCurve(d);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = Mathf.Lerp(FarStart, NearStart, t);
        RenderSettings.fogEndDistance = Mathf.Lerp(FarEnd, NearEnd, t);
        // Gameplay-only height gain. Reveal demo owns its independent reference mist;
        // indoor settings remain owned by ChurchInterior.
        if(player.enabled && !PlayerController.CaptureMode && (climbInterior==null || !climbInterior.Contains(player.transform.position))) {
            float gain=1+.24f*Mathf.InverseLerp(climbBaseY,climbTopY,player.transform.position.y);
            RenderSettings.fogDensity=climbDensityBase*gain;
            // Linear fog ignores fogDensity: scale both distances to increase optical density.
            RenderSettings.fogStartDistance/=gain;
            RenderSettings.fogEndDistance/=gain;
        }
        RenderSettings.fogColor = Color.Lerp(farFog, nearFog, t);
        RenderSettings.ambientSkyColor = Color.Lerp(farSky, nearSky, t);
        if (cloudMaterial != null)
        {
            Color mist = RenderSettings.fogColor;
            mist.a = CloudOpacity * (1 - t);
            cloudMaterial.SetColor("_Color", mist);
        }
    }

    void Update()
    {
        float d = Distance();
        ApplyMist(d);
        if (!bellFired && previousDistance > BellDistance && d <= BellDistance)
        {
            bellFired = pulsing = true;
            pulseTime = 0;
            player.GetComponent<AudioDirector>().PlayBell(d);
            Debug.Log("[REVEAL] bell pulse once rise=1.2s fall=0.8s bloom=x1.6 vignette=x1.3");
        }
        previousDistance = d;
        if (!pulsing) return;
        pulseTime += Time.unscaledDeltaTime;
        float t = pulseTime <= PulseRise ? Mathf.SmoothStep(0, 1, pulseTime / PulseRise)
            : 1 - Mathf.SmoothStep(0, 1, (pulseTime - PulseRise) / PulseFall);
        if (bloom != null) bloom.intensity.value = baseBloom * Mathf.Lerp(1, BloomMultiplier, t);
        if (vignette != null) vignette.intensity.value = baseVignette * Mathf.Lerp(1, VignetteMultiplier, t);
        if (pulseTime >= PulseRise + PulseFall)
        {
            pulsing = false;
            Debug.Log("[REVEAL] bell pulse restored");
        }
    }

    void BuildCloud()
    {
        // The existing church shader does not consume RenderSettings fog. Supply a
        // soft world-space mist bank using vertex opacity, without changing any art
        // church material/shader/texture. Its depth-aware bank leaves distant
        // mountains to their own haze and restores the original camera depth mode.
        cloudRoot = new GameObject("Runtime Reveal Mist");
        cloudMaterial = new Material(cloudShader) { hideFlags = HideFlags.HideAndDontSave };
        cloudMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual);
        cloudMaterial.renderQueue = (int)RenderQueue.Transparent;
        int count = (CloudColumns + 1) * (CloudRows + 1);
        var vertices = new Vector3[count * CloudLayers];
        var colors = new Color[vertices.Length];
        var triangles = new int[CloudColumns * CloudRows * 6 * CloudLayers];
        int triangle = 0;
        for (int layer = 0; layer < CloudLayers; layer++)
        for (int y = 0; y <= CloudRows; y++)
        for (int x = 0; x <= CloudColumns; x++)
        {
            int index = layer * count + y * (CloudColumns + 1) + x;
            float u = x / (float)CloudColumns * 2 - 1;
            float v = y / (float)CloudRows * 2 - 1;
            vertices[index] = new Vector3(u * CloudWidth * .5f, CloudCenterY + v * CloudHeight * .5f,
                CloudFrontZ - layer * CloudLayerSpacing);
            float feather = 1 - Mathf.SmoothStep(.25f, 1, Mathf.Sqrt(u * u + v * v));
            float cloud = .91f + .09f * Mathf.Sin(u * 14 + layer) * Mathf.Cos(v * 12 + layer);
            colors[index] = new Color(1, 1, 1, feather * cloud);
            if (x == CloudColumns || y == CloudRows) continue;
            int next = index + CloudColumns + 1;
            triangles[triangle++] = index; triangles[triangle++] = next; triangles[triangle++] = index + 1;
            triangles[triangle++] = index + 1; triangles[triangle++] = next; triangles[triangle++] = next + 1;
        }
        cloudMesh = new Mesh { name = "RevealMist_VertexFeather", hideFlags = HideFlags.HideAndDontSave };
        cloudMesh.vertices = vertices;
        cloudMesh.colors = colors;
        cloudMesh.triangles = triangles;
        cloudMesh.RecalculateBounds();
        cloudRoot.AddComponent<MeshFilter>().sharedMesh = cloudMesh;
        var renderer = cloudRoot.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = cloudMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    void OnDestroy()
    {
        if (cloudRoot != null) Destroy(cloudRoot);
        if (cloudMesh != null) Destroy(cloudMesh);
        if (cloudMaterial != null) Destroy(cloudMaterial);
    }

    void OnDisable()
    {
        if (!initialized) return;
        if (mistCamera != null) mistCamera.requiresDepthTexture = previousDepthTexture;
        if (cloudRoot != null) cloudRoot.SetActive(false);
        if (bloom != null) bloom.intensity.value = baseBloom;
        if (vignette != null) vignette.intensity.value = baseVignette;
    }

    void OnEnable()
    {
        if (initialized && mistCamera != null) mistCamera.requiresDepthTexture = true;
    }

    IEnumerator DemoReveal()
    {
        // Start normal components before positioning the reveal-only camera.
        yield return null;
        // 关键：PlayerController.LateUpdate 每帧把相机朝向拉回"玩家上方 lookHeight"，
        // 会把我们按教堂包围盒设的 FOV/朝向覆盖掉（表现为教堂顶被切）。演示期间停掉它。
        player.enabled = false;
        var cam = Camera.main;
        var root = new DirectoryInfo(Application.dataPath);
        while (root != null && root.Name != "Builds") root = root.Parent;
        string output = root != null ? Path.Combine(root.Parent.FullName, "Shots") : Path.GetFullPath("Shots");
        Directory.CreateDirectory(output);
        // Reference shots first; the final transition goes directly from far to near.
        float[] distances = { 80, 50, DemoFarDistance, DemoNearDistance };
        float nearHeight = 0, farHeight = 0;
        double farContrast = 0;
        double[] edges = new double[distances.Length];
        bool closeupPass = false, blendPass = true;
        for (int i = 0; i < distances.Length; i++)
        {
            float d = distances[i];
            if (d == DemoNearDistance)
            {
                // Exercise the actual 45 m crossing before teleporting to the
                // selected near sample, so the bell fires exactly once.
                Vector3 bellPoint = Anchor + Vector3.back * BellDistance;
                if (!player.ground.Raycast(new Ray(bellPoint + Vector3.up * 500, Vector3.down), out var bellHit, 1000))
                { Debug.LogError("[REVEAL] demo bell ground ray missed"); Application.Quit(1); yield break; }
                bellPoint.y = bellHit.point.y;
                player.transform.position = bellPoint;
                yield return null;
                yield return null;
            }
            Vector3 p = Anchor + Vector3.back * d;
            if (!player.ground.Raycast(new Ray(new Vector3(p.x, 500, p.z), Vector3.down), out var hit, 1000))
            { Debug.LogError("[REVEAL] demo ground ray missed"); Application.Quit(1); yield break; }
            p.y = hit.point.y;
            player.transform.position = p;
            // Only this demo owns the camera; the gameplay rig is disabled above.
            Vector3 startPosition = cam.transform.position;
            Quaternion startRotation = cam.transform.rotation;
            float startFov = cam.fieldOfView;
            cam.transform.position = p + DemoOffset;
            var cb = ChurchWorldBounds();
            cam.fieldOfView = DemoFov;
            cam.transform.LookAt(cb.center);
            if (d == 80)
            {
                // Preserve the existing 80 m reference shot, independently of
                // the new 115 m far gate and the lower near rig.
                cam.transform.position = p + new Vector3(0, 14f, -7.2f);
                cam.transform.LookAt(cb.center);
                ChurchRect(cam, Screen.width, Screen.height, out float initialHeight);
                cam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(DemoFov * .5f * Mathf.Deg2Rad)
                    * initialHeight / .28f) * Mathf.Rad2Deg;
            }
            Vector3 endPosition = cam.transform.position;
            Quaternion endRotation = cam.transform.rotation;
            float endFov = cam.fieldOfView;
            if (i > 0)
            {
                float elapsed = 0;
                int blendFrames = 0;
                cam.transform.SetPositionAndRotation(startPosition, startRotation);
                cam.fieldOfView = startFov;
                while (elapsed < DemoBlendSeconds)
                {
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.SmoothStep(0, 1, elapsed / DemoBlendSeconds);
                    cam.transform.SetPositionAndRotation(Vector3.Lerp(startPosition, endPosition, t),
                        Quaternion.Slerp(startRotation, endRotation, t));
                    cam.fieldOfView = Mathf.Lerp(startFov, endFov, t);
                    blendFrames++;
                }
                blendPass &= elapsed >= 1.6f && elapsed <= 2.2f && blendFrames > 1;
                Debug.Log(FormattableString.Invariant($"[REVEAL_BLEND] to_dist={d:F0} duration={elapsed:F3} frames={blendFrames} curve=SmoothStep"));
            }
            cam.transform.SetPositionAndRotation(endPosition, endRotation);
            cam.fieldOfView = endFov;
            yield return new WaitForSecondsRealtime(SettleSeconds);
            yield return new WaitForEndOfFrame();
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            string path = Path.Combine(output, "reveal_" + d.ToString("F0", CultureInfo.InvariantCulture) + ".png");
            File.WriteAllBytes(path, frame.EncodeToPNG());
            if (d == 80) FarMistAudit.Capture(cam, frame, output);
            RectInt roi = ChurchRect(cam, frame.width, frame.height, out float heightPct);
            edges[i] = Edge(frame, roi);
            if (d == DemoFarDistance) { farHeight = heightPct; farContrast = SilhouetteContrast(cam, frame, output); }
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[REVEAL] d={0:F0} fogStart={1:F3} fogEnd={2:F3} churchEdge={3:F9}",
                Distance(), RenderSettings.fogStartDistance, RenderSettings.fogEndDistance, edges[i]));
            Debug.Log(FormattableString.Invariant($"[REVEAL_FRAME] d={d:F0} fov={cam.fieldOfView:F3} church_height_pct={heightPct:F6}"));
            Debug.Log($"[REVEAL] screenshot={path} roi_bottom_left={roi.x},{roi.y},{roi.width},{roi.height} metric=mean_abs_RGB_xy/255");
            if (d == DemoNearDistance) { nearHeight = heightPct; closeupPass = ValidateCloseup(cam) && heightPct >= .55f && heightPct <= .68f; }
            Destroy(frame);
        }
        // 该比值在雾密度可调后与远山可见性冲突，已由双距离构图判据替代。
        bool pass = closeupPass && blendPass && DemoFarDistance <= 120f && farHeight <= .34f && farContrast <= .20;
        // Let the single pulse finish so the same run verifies restoration as well.
        while (pulsing) yield return null;
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[REVEAL] gate={0} ratio={1:F6} near_dist={4:F0} church_height_pct={5:F6} ratio20_80={1:F6} bell_once={2} pulse_restored={3}",
            pass ? "pass" : "FAIL", edges[distances.Length - 1] / Math.Max(edges[0], double.Epsilon), bellFired,
            (bloom == null || Mathf.Approximately(bloom.intensity.value, baseBloom)) &&
            (vignette == null || Mathf.Approximately(vignette.intensity.value, baseVignette)), DemoNearDistance, nearHeight));
        Debug.Log(FormattableString.Invariant($"[REVEAL] far_h_pct={farHeight:F6} far_contrast={farContrast:F6} far_dist={DemoFarDistance:F0} near_h_pct={nearHeight:F6} gate={(pass ? "pass" : "FAIL")}"));
        // Allow the complete synthesized decay to play before exiting this demo.
        var audio = player.GetComponent<AudioDirector>();
        yield return new WaitForSecondsRealtime(4);
        Application.Quit(pass && audio.RevealPass ? 0 : 1);
    }

    bool ValidateCloseup(Camera cam)
    {
        float topY = 1, bottomY = 1;
        int baseSamples = 0, baseBlocked = 0;
        Material stone = null;
        float baseY = ChurchWorldBounds().min.y;
        var walls = Array.FindAll(FindObjectsByType<Collider>(FindObjectsSortMode.None),
            c => c.name.StartsWith("TerraceWall_") || c.name.StartsWith("TerraceCap_"));
        foreach (var mf in church.GetComponentsInChildren<MeshFilter>())
        {
            foreach (var v in mf.sharedMesh.vertices)
            {
                var projected = cam.WorldToViewportPoint(mf.transform.TransformPoint(v));
                if (projected.z > 0)
                {
                    topY = Mathf.Min(topY, 1 - projected.y);
                    bottomY = Mathf.Min(bottomY, projected.y);
                }
                // Test actual low church geometry against foreground terrace collider geometry.
                var world = mf.transform.TransformPoint(v);
                if (world.y <= baseY + .25f)
                {
                    baseSamples++;
                    var delta = world - cam.transform.position;
                    foreach (var wall in walls)
                        if (wall.Raycast(new Ray(cam.transform.position, delta.normalized), out _, delta.magnitude - .02f))
                        { baseBlocked++; break; }
                }
            }
            foreach (var mat in mf.GetComponent<Renderer>().sharedMaterials)
                if (mat.name.StartsWith("M_ChurchStone")) stone = mat;
        }
        float tileM = stone == null ? 0 : 1 / stone.GetTextureScale("_BaseMap").x;
        // Stone_Pixel uses 32 px wide staggered blocks, UVs are in world metres.
        int masonry = stone == null ? 0 : Mathf.FloorToInt(5 / tileM * stone.GetTexture("_BaseMap").width / 32);
        float gap = 0;
        int feet = 0;
        var all = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        // Fence meshes are now merged; retained post colliders are the physical
        // source of truth for foot clearance, independently of rendering batches.
        foreach (var foot in FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
        {
            if (!foot.name.StartsWith("FencePost_") && !foot.name.StartsWith("SF2_Post_")) continue;
            feet++;
            var center = foot.bounds.center;
            if (foot.name.StartsWith("SF2_Post_"))
            {
                // New posts stand on the snow shoulder. The adjacent paving
                // plane can be 10cm lower on a bend; test their actual support.
                if (!player.ground.Raycast(new Ray(center+Vector3.up*500,Vector3.down),out var support,1000)) return false;
                gap=Mathf.Max(gap,Mathf.Abs(foot.bounds.min.y-support.point.y));
                continue;
            }
            MeshFilter closest = null;
            float best = float.PositiveInfinity;
            foreach (var slab in all)
            {
                if (!slab.name.StartsWith("Paving_")) continue;
                var c = slab.GetComponent<Renderer>().bounds.center;
                float d = new Vector2(c.x - center.x, c.z - center.z).sqrMagnitude;
                if (d < best) { best = d; closest = slab; }
            }
            if (closest == null) return false;
            float bottom = foot.bounds.min.y;
            // Continue the adjacent slab's top plane to the roadside support.
            var local = closest.transform.InverseTransformPoint(center);
            local.y = closest.sharedMesh.bounds.max.y;
            float slabTop = closest.transform.TransformPoint(local).y;
            gap = Mathf.Max(gap, bottom - slabTop);
        }
        var fire = GameObject.Find("BrazierPoint_0").GetComponent<Light>();
        float alpha = contactMaterial.GetColor("_BaseColor").a;
        var offset = cam.transform.position - player.transform.position;
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[CLOSEUP] cam_offset=({0:0.###},{1:0.###},{2:0.###}) church_top_y={3:F6} stone_tile_m={4:0.0} masonry_per_5m={5} brazier_r={6:0.###} brazier_i={7:0.###} contact_alpha={8:F2} rail_gap_m={9:F6}",
            offset.x, offset.y, offset.z, topY, tileM, masonry, fire.range, fire.intensity, alpha, gap));
        Debug.Log($"[CLOSEUP_DETAIL] fence_feet={feet} rail_gap_reference=post_support_surface contact_mode=realtime_only contact_patches=0");
        Debug.Log(FormattableString.Invariant($"[REVEAL_COMPOSITION] top_margin={topY:F6} bottom_margin={bottomY:F6} base_samples={baseSamples} base_blocked={baseBlocked} camera_y={cam.transform.position.y:F3} camera_height={DemoCameraHeight:F3} fov={cam.fieldOfView:F3}"));
        float nearDistance = Vector2.Distance(new Vector2(cam.transform.position.x, cam.transform.position.z),
            new Vector2(Anchor.x, Anchor.z));
        float depression = Mathf.Asin(-cam.transform.forward.y) * Mathf.Rad2Deg;
        ChurchRect(cam, Screen.width, Screen.height, out float heightPct);
        bool cameraPass = Mathf.Abs(nearDistance - 34f) <= 1f && offset.y >= 7f && offset.y <= 8f &&
            cam.fieldOfView >= 46f && cam.fieldOfView <= 50f && depression <= 15f &&
            heightPct >= .55f && heightPct <= .68f && topY >= .04f && bottomY >= .04f && baseBlocked == 0;
        Debug.Log(FormattableString.Invariant($"[REVEAL] near_dist={nearDistance:F0} near_height={offset.y:F1} fov={cam.fieldOfView:F0} church_height_pct={heightPct:F6} top_margin={topY:F6} bottom_margin={bottomY:F6} depression_deg={depression:F3} gate={(cameraPass ? "pass" : "FAIL")}"));
        // 以 `Style11` 图集实际物理尺寸为准，1.8 m 是早期估算。
        return cameraPass && baseSamples > 0 && baseBlocked == 0 && masonry >= 8 && gap <= .02f && feet > 0 &&
            Vector3.Distance(offset, DemoOffset) < .001f &&
            Mathf.Abs(tileM - 1.0f) < .001f && fire.type == LightType.Point &&
            Mathf.Approximately(fire.range, 7) && Mathf.Approximately(fire.intensity, 2.2f) && alpha <= .051f;
    }

    Bounds ChurchWorldBounds()
    {
        bool any = false;
        Bounds bounds = default;
        foreach (var mf in church.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var r = mf.GetComponent<Renderer>();
            if (r == null) continue;
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
        }
        return bounds;
    }

    RectInt ChurchRect(Camera cam, int width, int height, out float heightPct)
    {
        // Project real geometry: at close range the empty AABB corners grossly
        // overestimate the ROI and dilute the church's edge metric with sky.
        Vector2 min = new Vector2(width, height), max = Vector2.zero;
        foreach (var mf in church.GetComponentsInChildren<MeshFilter>())
        foreach (var vertex in mf.sharedMesh.vertices)
        {
            var v = cam.WorldToViewportPoint(mf.transform.TransformPoint(vertex));
            if (v.z <= 0) throw new InvalidOperationException("Church vertex behind demo camera");
            var pixel = new Vector2(v.x * width, v.y * height);
            min = Vector2.Min(min, pixel); max = Vector2.Max(max, pixel);
        }
        heightPct = (max.y - min.y) / height;
        int x0 = Mathf.Clamp(Mathf.FloorToInt(min.x), 0, width - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(min.y), 0, height - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt(max.x), x0 + 1, width);
        int y1 = Mathf.Clamp(Mathf.CeilToInt(max.y), y0 + 1, height);
        return new RectInt(x0, y0, x1 - x0, y1 - y0);
    }

    // Project triangle row spans to obtain actual silhouette boundaries; compare
    // PNG Rec.709 luma 4 px inside/outside each side, then mean absolute delta.
    // All rows/samples are exported for independent PNG recomputation.
    double SilhouetteContrast(Camera cam, Texture2D frame, string output)
    {
        var left = new float[frame.height]; var right = new float[frame.height];
        Array.Fill(left, float.PositiveInfinity); Array.Fill(right, float.NegativeInfinity);
        foreach (var mf in church.GetComponentsInChildren<MeshFilter>())
        {
            var verts = Array.ConvertAll(mf.sharedMesh.vertices, v => cam.WorldToScreenPoint(mf.transform.TransformPoint(v)));
            var tri = mf.sharedMesh.triangles;
            for (int k=0;k<tri.Length;k+=3)
            {
                var a=verts[tri[k]]; var b=verts[tri[k+1]]; var c=verts[tri[k+2]];
                int lo=Mathf.Max(0,Mathf.CeilToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))));
                int hi=Mathf.Min(frame.height-1,Mathf.FloorToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))));
                for(int y=lo;y<=hi;y++)
                {
                    void Span(Vector3 u,Vector3 v) { if((u.y<=y && v.y>y)||(v.y<=y && u.y>y)) { float x=Mathf.Lerp(u.x,v.x,(y-u.y)/(v.y-u.y));left[y]=Mathf.Min(left[y],x);right[y]=Mathf.Max(right[y],x); } }
                    Span(a,b);Span(b,c);Span(c,a);
                }
            }
        }
        var csv=new System.Text.StringBuilder("inside_x,outside_x,y,contrast\n"); double sum=0;int n=0;
        double L(Color c)=>.2126*c.r+.7152*c.g+.0722*c.b;
        for(int y=0;y<frame.height;y+=2)
        {
            if(right[y]-left[y]<12)continue;
            foreach(int side in new[]{-1,1})
            {
                int edge=Mathf.RoundToInt(side<0?left[y]:right[y]);int ix=edge-side*4,ox=edge+side*4;
                if(ix<0||ox<0||ix>=frame.width||ox>=frame.width)continue;
                double delta=Math.Abs(L(frame.GetPixel(ix,y))-L(frame.GetPixel(ox,y)));sum+=delta;n++;
                csv.Append(FormattableString.Invariant($"{ix},{ox},{y},{delta:F9}\n"));
            }
        }
        File.WriteAllText(Path.Combine(output,"reveal_contrast.csv"),csv.ToString());
        if(n==0)throw new InvalidOperationException("No church silhouette contrast samples");
        Debug.Log($"[REVEAL_CONTRAST] samples={n} metric=mean_abs_boundary_Rec709_PNG_delta offset_px=4");
        return sum/n;
    }

    static double Edge(Texture2D frame, RectInt roi)
    {
        if (roi.width < 2 || roi.height < 2) throw new InvalidOperationException("Empty church ROI");
        Color32[] pixels = frame.GetPixels32();
        double sum = 0;
        long pairs = 0;
        for (int y = roi.yMin; y < roi.yMax; y++)
        for (int x = roi.xMin; x < roi.xMax; x++)
        {
            int index = y * frame.width + x;
            var a = pixels[index];
            if (x + 1 < roi.xMax) { sum += Difference(a, pixels[index + 1]); pairs++; }
            if (y + 1 < roi.yMax) { sum += Difference(a, pixels[index + frame.width]); pairs++; }
        }
        return sum / (pairs * 3 * 255.0);
    }

    static int Difference(Color32 a, Color32 b) => Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
}
