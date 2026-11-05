using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
public static class FootprintAcceptanceBuild
{
    public static void Run()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/ArtSpike.unity", "Assets/Scenes/Climb2D.unity" },
            locationPathName = "Builds/Mac/Mistbell.app", target = BuildTarget.StandaloneOSX,
            options = BuildOptions.Development
        });
        Debug.Log($"[FOOTPRINT_BUILD] result={report.summary.result} errors={report.summary.totalErrors}");
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
