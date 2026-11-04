using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Patch only the pilgrim material in the current scene; preserve all existing art edits.
public static class PilgrimHeightBuild
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        var pilgrim=UnityEngine.Object.FindFirstObjectByType<PilgrimAnimator>();
        var mat=pilgrim.GetComponent<MeshRenderer>().sharedMaterial;
        mat.SetFloat("_ScaleY",1.81f);mat.SetFloat("_ScaleX",1.81f*64f/80f);
        EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();
        var interior=UnityEngine.Object.FindFirstObjectByType<ChurchInterior>();
        Physics.SyncTransforms();
        float clearance=float.PositiveInfinity;
        for(float z=-9;z<=-7;z+=.1f)for(float x=-2.3f;x<=-1.699f;x+=.1f)
            if(Physics.Raycast(new Vector3(x,interior.FloorY+1.7f,z),Vector3.up,out var hit,10,1<<PlayerController.BlockerLayer,QueryTriggerInteraction.Ignore))
                clearance=Mathf.Min(clearance,hit.distance);
        var rs=interior.GetComponentsInChildren<MeshRenderer>(true);
        var backs=rs.Where(r=>r.name.StartsWith("Interior_V2Pew_0_") && r.name.EndsWith("_Back")).OrderBy(r=>r.bounds.center.z).ToArray();
        float pitch=backs[1].bounds.center.z-backs[0].bounds.center.z;
        float back=rs.First(r=>r.name=="Interior_V2Pew_0_0_Rim").bounds.max.y-interior.FloorY;
        var altar=rs.First(r=>r.name=="Interior_Altar");
        Debug.Log(FormattableString.Invariant($"[PILGRIM_CLEARANCE] doorway_min_head_gap={clearance:F5} sampled_width=0.6 player_h=1.7 pew_pitch={pitch:F5} pew_back_h={back:F5} altar_bounds_h={altar.bounds.size.y:F5} look_h={PlayerController.LookHeight:F2}"));
        if(clearance<.03f)throw new Exception("Pilgrim doorway clearance failed");
        EditorSceneManager.SaveScene(pilgrim.gameObject.scene);
        PerformanceBuild.Run();
    }
}
