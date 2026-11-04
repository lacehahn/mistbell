using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RoyalDoorBuild
{
    static string CollisionSnapshot(ChurchInterior interior) => string.Join("\n",
        interior.GetComponentsInChildren<Collider>(true).Select(c =>
            c.name+"|"+EditorJsonUtility.ToJson(c)+"|"+c.transform.localToWorldMatrix.ToString("F6")).OrderBy(s=>s));
    static string SideSnapshot(ChurchInterior interior) => string.Join("\n",
        interior.detailRoot.GetComponentsInChildren<MeshFilter>(true)
        .Where(f=>f.name=="Interior_V2NicheBack_0" || f.name=="Interior_V2NicheBack_2" ||
            f.name=="Interior_ArchFrame_0" || f.name=="Interior_ArchFrame_2")
        .Select(f=>f.name+"|"+EditorJsonUtility.ToJson(f.sharedMesh)+"|"+f.transform.localToWorldMatrix.ToString("F6")).OrderBy(s=>s));
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        var interior=Object.FindFirstObjectByType<ChurchInterior>();
        string colliders=CollisionSnapshot(interior),sides=SideSnapshot(interior);
        ChurchInteriorBuilder.InstallRoyalDoor(interior.detailRoot.transform,interior.FloorY);
        if(colliders!=CollisionSnapshot(interior) || sides!=SideSnapshot(interior))
            throw new InvalidOperationException("Royal door changed existing collision or side arches");
        // Reapply once to verify object/mesh replacement does not accumulate frames.
        ChurchInteriorBuilder.InstallRoyalDoor(interior.detailRoot.transform,interior.FloorY);
        if(interior.detailRoot.GetComponentsInChildren<MeshFilter>(true).Count(f=>f.name=="Interior_RoyalDoor_Frame")!=1)
            throw new InvalidOperationException("Royal door is not idempotent");
        if(colliders!=CollisionSnapshot(interior) || sides!=SideSnapshot(interior))
            throw new InvalidOperationException("Royal door reapply changed existing geometry");
        Debug.Log("[ROYAL_DOOR_INVARIANTS] colliders_identical=true side_arches_identical=true idempotent=true");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(interior.gameObject.scene);
        EditorSceneManager.SaveScene(interior.gameObject.scene);
        AltarBuild.Verify();
        PerformanceBuild.Run();
    }
}
