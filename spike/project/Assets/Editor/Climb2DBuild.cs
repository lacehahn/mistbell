using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Climb2DBuild
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        var pilgrim=UnityEngine.Object.FindFirstObjectByType<PilgrimAnimator>();
        var atlas=(Texture2D)pilgrim.GetComponent<MeshRenderer>().sharedMaterial.mainTexture;
        if(atlas==null || !atlas.isReadable || atlas.width!=384 || atlas.height!=320) throw new Exception("Existing pilgrim atlas must be readable 384x320");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Climb2D").AddComponent<Climb2D>().atlas=atlas;
        var camera=new GameObject("Camera").AddComponent<Camera>(); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/Climb2D.unity");
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{"Assets/Scenes/ArtSpike.unity","Assets/Scenes/Climb2D.unity"},
            locationPathName="Builds/Mac/Mistbell.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development
        });
        Debug.Log($"[CLIMB2D_BUILD] result={result.summary.result} errors={result.summary.totalErrors} size={result.summary.totalSize}");
        if(result.summary.result!=BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
