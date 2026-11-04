using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PerformanceBuild
{
    public static void RunClimb()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        ClimbFeelBuild.Apply();
        Run();
    }

    // Development enables the render counters; no profiler connection or deep profiling.
    public static void Run()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/ArtSpike.unity" },
            locationPathName = "Builds/Mac/Mistbell.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.Development
        });
        Debug.Log($"[PERF_BUILD] result={report.summary.result} errors={report.summary.totalErrors}");
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}

// Unity's baked static batch makes these source meshes unreadable in the player.
// Keep only these sources available for the explicit material merge instead.
// This callback edits the build's scene copy, never the authored scene or mesh assets.
public sealed class PerformanceStaticSources : UnityEditor.Build.IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report)
    {
        if (report == null || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-perf-build-baseline") >= 0) return;
        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!renderer.name.StartsWith("TerraceWall_", System.StringComparison.Ordinal)) continue;
            var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
            if ((flags & StaticEditorFlags.BatchingStatic) == 0) continue;
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
            count++;
        }
        Debug.Log($"[PERF_BUILD] explicit_batch_sources={count}");
    }
}
