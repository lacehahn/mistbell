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
    GameObject cloudRoot;
    Material cloudMaterial;
    Mesh cloudMesh;
    Bloom bloom;
    Vignette vignette;
    float baseBloom, baseVignette, previousDistance, pulseTime;
    bool bellFired, pulsing, initialized;
    Color farFog, nearFog, farSky, nearSky;

    void Awake()
    {
        // No fog, Volume cloning, logging, coroutine, or camera changes in capture mode.
        if (PlayerController.CaptureMode) enabled = false;
    }

    void Start()
    {
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

    float Distance() => Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
        new Vector2(Anchor.x, Anchor.z));

    void ApplyMist(float d)
    {
        float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(FarDistance, NearDistance, d));
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = Mathf.Lerp(FarStart, NearStart, t);
        RenderSettings.fogEndDistance = Mathf.Lerp(FarEnd, NearEnd, t);
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
        // material/shader/texture or creating an asset. UI/Default needs no variants.
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
        if (cloudRoot != null) cloudRoot.SetActive(false);
        if (bloom != null) bloom.intensity.value = baseBloom;
        if (vignette != null) vignette.intensity.value = baseVignette;
    }

    IEnumerator DemoReveal()
    {
        // Start all normal components before moving the player. Keep normal follow-camera framing.
        yield return null;
        var cam = Camera.main;
        var root = new DirectoryInfo(Application.dataPath);
        while (root != null && root.Name != "Builds") root = root.Parent;
        string output = root != null ? Path.Combine(root.Parent.FullName, "Shots") : Path.GetFullPath("Shots");
        Directory.CreateDirectory(output);
        float[] distances = { 80, 50, 20 };
        double[] edges = new double[distances.Length];
        for (int i = 0; i < distances.Length; i++)
        {
            float d = distances[i];
            Vector3 p = Anchor + Vector3.back * d;
            if (!player.ground.Raycast(new Ray(new Vector3(p.x, 500, p.z), Vector3.down), out var hit, 1000))
            { Debug.LogError("[REVEAL] demo ground ray missed"); Application.Quit(1); yield break; }
            p.y = hit.point.y;
            player.transform.position = p;
            // Teleport the existing follow rig with the player, avoiding frame-rate dependent camera lag.
            cam.transform.position = p + new Vector3(0, 8, -22);
            cam.transform.LookAt(p + Vector3.up * 3);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            yield return new WaitForEndOfFrame();
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            string path = Path.Combine(output, "reveal_" + d.ToString("F0", CultureInfo.InvariantCulture) + ".png");
            File.WriteAllBytes(path, frame.EncodeToPNG());
            RectInt roi = ChurchRect(cam, frame.width, frame.height);
            edges[i] = Edge(frame, roi);
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[REVEAL] d={0:F0} fogStart={1:F3} fogEnd={2:F3} churchEdge={3:F9}",
                Distance(), RenderSettings.fogStartDistance, RenderSettings.fogEndDistance, edges[i]));
            Debug.Log($"[REVEAL] screenshot={path} roi_bottom_left={roi.x},{roi.y},{roi.width},{roi.height} metric=mean_abs_RGB_xy/255");
            Destroy(frame);
        }
        bool pass = edges[0] < edges[1] && edges[1] < edges[2] && edges[2] >= edges[0] * 1.6;
        // Let the single pulse finish so the same run verifies restoration as well.
        while (pulsing) yield return null;
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[REVEAL] gate={0} ratio20_80={1:F6} bell_once={2} pulse_restored={3}",
            pass ? "pass" : "FAIL", edges[2] / Math.Max(edges[0], double.Epsilon), bellFired,
            (bloom == null || Mathf.Approximately(bloom.intensity.value, baseBloom)) &&
            (vignette == null || Mathf.Approximately(vignette.intensity.value, baseVignette))));
        Application.Quit(pass ? 0 : 1);
    }

    RectInt ChurchRect(Camera cam, int width, int height)
    {
        var renderers = church.GetComponentsInChildren<MeshRenderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        Vector2 min = new Vector2(width, height), max = Vector2.zero;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 v = cam.WorldToViewportPoint(corner);
            if (v.z <= 0) throw new InvalidOperationException("Church bounds behind demo camera");
            var pixel = new Vector2(v.x * width, v.y * height);
            min = Vector2.Min(min, pixel); max = Vector2.Max(max, pixel);
        }
        int x0 = Mathf.Clamp(Mathf.FloorToInt(min.x), 0, width - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(min.y), 0, height - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt(max.x), x0 + 1, width);
        int y1 = Mathf.Clamp(Mathf.CeilToInt(max.y), y0 + 1, height);
        return new RectInt(x0, y0, x1 - x0, y1 - y0);
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
