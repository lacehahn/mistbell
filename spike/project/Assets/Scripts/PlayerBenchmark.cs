using System;
using System.Collections;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine;

public sealed partial class PlayerController
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ConfigurePerformancePacing()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-perf-unpaced") < 0) return;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Debug.Log("[PERF_PACING] vSync=0 targetFrameRate=-1 (session only)");
    }

    [Serializable] public class BenchResult
    {
        public string pos;
        public float fps_avg, fps_p1, vram_mb;
        public long drawcalls, tris;
    }
    [Serializable] public class BenchReport { public BenchResult[] results; }

    static string BenchArg(string[] args, string flag)
    {
        int i = Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    IEnumerator Benchmark(string[] args)
    {
        if (Screen.width != 1920 || Screen.height != 1080)
        { Debug.LogError($"[BENCH_ERROR] expected=1920x1080 actual={Screen.width}x{Screen.height}"); Application.Quit(1); yield break; }
        // Benchmark throughput without display/compositor pacing; gameplay sessions retain their settings.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Debug.Log($"[BENCH_INFO] device={SystemInfo.deviceModel} gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType} unity={Application.unityVersion} development={Debug.isDebugBuild} resolution={Screen.width}x{Screen.height} warmup_frames_min=180 warmup_seconds_min=3 samples=300 p1=reciprocal_mean_slowest_3_dt vSync={QualitySettings.vSyncCount} targetFrameRate={Application.targetFrameRate}");
        using (var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1))
        using (var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1))
        using (var textures = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory", 1))
        {
            if (!draws.Valid || !triangles.Valid)
            { Debug.LogError("[BENCH_ERROR] render counters unavailable; use PerformanceBuild.Run development player"); Application.Quit(1); yield break; }
            if (!textures.Valid) Debug.LogWarning("[BENCH_INFO] Texture Memory unavailable; vram_mb=-1 (texture allocation counter, not total GPU memory)");
            var report = new BenchReport { results = new BenchResult[3] };
            string[] names = { "z01", "terrace", "interior" };
            Vector3[] positions = { ClimbRoute.Spawn, new Vector3(-2,0,-16), new Vector3(-2,interior.FloorY,.5f) };
            string output = BenchArg(args, "-bench-output") ?? Path.Combine(Application.persistentDataPath, "bench.json");
            string shotDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)), Path.GetFileNameWithoutExtension(output) + "_frames");
            Directory.CreateDirectory(shotDir);
            for (int p = 0; p < 3; p++)
            {
                // Teleport only the benchmark subject. Normal LateUpdate owns camera and indoor state.
                var position = positions[p];
                if (p != 2 && SurfaceAt(position, out var hit)) position.y = hit.point.y;
                if (p == 2)
                {
                    interior.TryOpen(new Vector3(-2,interior.FloorY,-10));
                    yield return new WaitForSecondsRealtime(1.2f);
                }
                transform.position = position; velocity = Vector3.zero; previousZ = position.z;
                float warmStart = Time.realtimeSinceStartup;
                for (int i = 0; i < 180 || Time.realtimeSinceStartup - warmStart < 3; i++) yield return null;
                var dt = new float[300]; double seconds = 0, memory = 0; long dc = 0, tris = 0;
                for (int i = 0; i < 300; i++)
                {
                    // Recorder LastValue and unscaledDeltaTime both describe the completed frame.
                    yield return null;
                    dt[i] = Time.unscaledDeltaTime; seconds += dt[i];
                    dc += draws.LastValue; tris += triangles.LastValue;
                    if (textures.Valid) memory += textures.LastValue;
                }
                Array.Sort(dt);
                var result = report.results[p] = new BenchResult {
                    pos = names[p], fps_avg = (float)(300 / seconds),
                    fps_p1 = 3 / (dt[297] + dt[298] + dt[299]),
                    drawcalls = (long)Math.Round(dc / 300.0), tris = (long)Math.Round(tris / 300.0),
                    vram_mb = textures.Valid ? (float)(memory / 300 / (1024 * 1024)) : -1
                };
                Debug.Log(FormattableString.Invariant($"[BENCH] pos={result.pos} fps_avg={result.fps_avg:F2} fps_p1={result.fps_p1:F2} drawcalls={result.drawcalls} tris={result.tris} vram_mb={result.vram_mb:F2}"));
                yield return new WaitForEndOfFrame();
                var frame = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(shotDir, result.pos + ".png"), frame.EncodeToPNG()); Destroy(frame);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            bool pass = true;
            string beforePath = BenchArg(args, "-bench-before");
            BenchReport before = beforePath == null ? null : JsonUtility.FromJson<BenchReport>(File.ReadAllText(beforePath));
            for (int i = 0; i < 3; i++)
            {
                var a = report.results[i];
                pass &= a.fps_avg >= 55 && a.drawcalls > 0 && a.drawcalls <= 250 && a.tris > 0;
                if (before != null)
                {
                    var b = Array.Find(before.results, r => r.pos == a.pos);
                    Debug.Log($"[BENCH_COMPARE] pos={a.pos}");
                    Debug.Log(FormattableString.Invariant($"[BENCH_OK] before=drawcalls:{b.drawcalls}/tris:{b.tris}/fps:{b.fps_avg:F2} after=drawcalls:{a.drawcalls}/tris:{a.tris}/fps:{a.fps_avg:F2}"));
                }
            }
            Debug.Log($"[BENCH_GATE] pass={pass} output={output}");
            // Baseline mode records failures without preventing the optimization investigation.
            Application.Quit(pass || Array.IndexOf(args, "-bench-baseline") >= 0 ? 0 : 1);
        }
    }
}
