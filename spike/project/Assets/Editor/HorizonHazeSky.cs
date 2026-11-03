using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Patch only atmosphere materials in the existing scene. No scene rebuild.
public static class HorizonHazeSky
{
    public static void Apply()
    {
        var sky = GameObject.Find("Sky Dome").GetComponent<MeshRenderer>().sharedMaterial;
        sky.SetFloat("_CloudAmount", .7f);
        sky.SetFloat("_CloudScale", .6f);
        // SkyGradient now evaluates to _HorizonColor at world-direction Y=0.
        // Copy the authored color directly: Unity handles each shader's color space.
        var authoredHorizon = sky.GetColor("_HorizonColor");
        var terrain = GameObject.Find("Terrain").GetComponent<MeshRenderer>().sharedMaterial;
        terrain.SetColor("_FarHazeColor", authoredHorizon);
        terrain.SetFloat("_FarHazeStrength", .85f);
        EditorUtility.SetDirty(sky);
        EditorUtility.SetDirty(terrain);
        var mountains = GameObject.Find("Painted Mountain Layers");
        if (mountains != null)
        foreach (var renderer in mountains.GetComponentsInChildren<MeshRenderer>(true))
        foreach (var material in renderer.sharedMaterials)
        {
            if (material == null || material.shader.name != "Mistbell/PaintedMountain") continue;
            material.SetColor("_HorizonFogColor", authoredHorizon);
            material.SetFloat("_HorizonFog", 1f);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[HORIZON_SETUP] haze_start=600 haze_end=1100 haze_far=0.85 haze_color=#{ColorUtility.ToHtmlStringRGB(authoredHorizon)} sky_steps=64 dither=8x8 cloud_scale=0.6 cloud_coverage=0.7");
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        Apply();
        MeasureContour();
        var report = UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/ArtSpike.unity" },
            locationPathName = "Builds/Mac/Mistbell.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        });
        Debug.Log($"[HORIZON_BUILD] result={report.summary.result} errors={report.summary.totalErrors}");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }

    public static void Diagnose()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        MeasureContour();
    }

    static void MeasureContour()
    {
        Object.FindFirstObjectByType<ShotRig>().Apply(0);
        var cam = Camera.main;
        var oldAspect = cam.aspect;
        cam.aspect = 1920f/1080f;
        Physics.SyncTransforms();
        var terrain = GameObject.Find("Terrain").GetComponent<MeshCollider>();
        var lines = new System.Collections.Generic.List<string> { "x,y,distance_m" };
        // Exclude the central church/terrace; measure both exposed snow silhouettes.
        for (int x = 19; x < 1901; x++)
        {
            if (x >= 384 && x < 1728) continue;
            float lo=600, hi=860;
            RaycastHit hit=default;
            for (int i=0;i<16;i++)
            {
                float y=(lo+hi)*.5f;
                var ray=cam.ViewportPointToRay(new Vector3((x+.5f)/1920f,1-y/1080f,0));
                if (terrain.Raycast(ray,out var sample,1500)) { hi=y; hit=sample; }
                else lo=y;
            }
            if (hit.distance <= 0) throw new System.InvalidOperationException("Horizon contour missed terrain");
            lines.Add(System.FormattableString.Invariant($"{x},{hi:F3},{hit.distance:F3}"));
        }
        System.IO.Directory.CreateDirectory("../logs");
        System.IO.File.WriteAllLines("../logs/horizon_contour.csv", lines);
        cam.aspect=oldAspect;
        Debug.Log($"[HORIZON_CONTOUR] columns={lines.Count-1} source=terrain_collider file=../logs/horizon_contour.csv");
    }
}
