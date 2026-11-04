using UnityEngine;
using UnityEditor.SceneManagement;

public static class AltarCrossAudit
{
    // Never save candidate meshes/transforms. These are diagnostic counterfactuals.
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        var interior = Object.FindFirstObjectByType<ChurchInterior>();
        var eye = new Vector3(-2, interior.FloorY + 1.75f, -4.78f);
        foreach (var collider in interior.GetComponentsInChildren<Collider>(true))
            if (collider.name == "Interior_Iconostasis")
                Debug.Log($"[ALTAR_WALL_COLLIDER] name={collider.name} type={collider.GetType().Name} position={collider.transform.position:F4} bounds_min={collider.bounds.min:F4} bounds_max={collider.bounds.max:F4} enabled={collider.enabled}");
        AltarCrossVisibility.Check(interior, eye);
        foreach (float shift in new[] { -.12f, -.16f, -.20f, -.32f })
        foreach (float height in new[] { 1.10f, .95f })
        foreach (float raise in new[] { 0f, .08f })
            AltarCrossVisibility.Check(interior, eye, null, shift, height, raise);
        Debug.Log("[ALTAR_CROSS_AUDIT] candidates=16 scene_saved=false extra_shift_032_tests_original_v1_position_minus_020=true");
    }
}
