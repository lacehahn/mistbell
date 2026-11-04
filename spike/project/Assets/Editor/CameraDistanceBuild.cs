using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CameraDistanceBuild
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        var pilgrim=UnityEngine.Object.FindFirstObjectByType<PilgrimAnimator>();
        var mat=pilgrim.GetComponent<MeshRenderer>().sharedMaterial;
        mat.SetFloat("_ScaleY",1.60f); mat.SetFloat("_ScaleX",1.60f*64f/80f);
        EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets();
        var interior=UnityEngine.Object.FindFirstObjectByType<ChurchInterior>();
        Physics.SyncTransforms();
        float gap=float.PositiveInfinity;
        for(float z=-9;z<=-7;z+=.1f) for(float x=-2.3f;x<=-1.699f;x+=.1f)
            if(Physics.Raycast(new Vector3(x,interior.FloorY+1.5f,z),Vector3.up,out var hit,10,1<<PlayerController.BlockerLayer,QueryTriggerInteraction.Ignore))
                gap=Mathf.Min(gap,hit.distance);
        var backs=interior.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("Interior_V2Pew_0_") && r.name.EndsWith("_Back")).OrderBy(r=>r.bounds.center.z).ToArray();
        float pitch=backs[1].bounds.center.z-backs[0].bounds.center.z;
        // Physical collision aperture: existing 0.28m row gap is intentionally too narrow for the 0.60m capsule.
        float pewGap=backs[1].bounds.min.z-backs[0].bounds.max.z;
        Debug.Log(FormattableString.Invariant($"[CAM_CLEARANCE] player_h=1.5 diameter=0.6 doorway_head_gap={gap:F5} pew_pitch={pitch:F5} back_gap={pewGap:F5}"));
        if(gap<.03f || backs.Length!=6) throw new Exception("Camera distance clearance failed");
        EditorSceneManager.SaveScene(pilgrim.gameObject.scene);
        PerformanceBuild.Run();
    }
}
