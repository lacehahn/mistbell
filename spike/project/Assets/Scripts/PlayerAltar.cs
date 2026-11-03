using System;
using System.Collections;
using UnityEngine;

public sealed partial class PlayerController
{
    // Separate inspection-only camera. Gameplay cameras, demos and lighting are unchanged.
    IEnumerator DemoAltar()
    {
        interiorAuto=true;interiorDirection=Vector3.zero;
        interior.TryOpen(new Vector3(-2,interior.FloorY,-10));
        transform.position=new Vector3(-2,interior.FloorY,.5f);velocity=Vector3.zero;
        yield return new WaitForSecondsRealtime(2);
        enabled=false;
        var go=new GameObject("Altar inspection camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(follow);
        var source=follow.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var data=go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.renderPostProcessing=source.renderPostProcessing;data.volumeLayerMask=source.volumeLayerMask;
        // Keep the scene's tone mapping; suppress only blur in this inspection process.
        foreach(var volume in Resources.FindObjectsOfTypeAll<UnityEngine.Rendering.Volume>())
            if(volume.profile!=null && volume.profile.TryGet<UnityEngine.Rendering.Universal.DepthOfField>(out var dof))dof.active=false;
        // Exactly 1.2m from the front masonry plane (z=8.6-.425), facing +Z.
        go.transform.position=new Vector3(-2,interior.FloorY+1.05f,8.6f-.425f-1.2f);
        go.transform.rotation=Quaternion.identity;camera.fieldOfView=88;camera.nearClipPlane=.05f;
        // Isolate the model only for this inspection image: the existing high sanctuary
        // panels otherwise occlude the upper cross from the mandated 1.2 m viewpoint.
        const int inspectionLayer=30;
        foreach(var renderer in interior.detailRoot.GetComponentsInChildren<MeshRenderer>())
            if(renderer.name=="Interior_Altar_Model")
            {renderer.gameObject.layer=inspectionLayer;renderer.forceRenderingOff=false;renderer.enabled=true;}
        camera.cullingMask=1<<inspectionLayer;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.10f,.085f,.07f);
        foreach(var feature in Resources.FindObjectsOfTypeAll<UnityEngine.Rendering.Universal.FullScreenPassRendererFeature>())
            if(feature.name=="TiltShiftFeature")feature.SetActive(false);
        follow.enabled=false;
        yield return new WaitForSecondsRealtime(1);
        yield return new WaitForEndOfFrame();
        string shot=InteriorShot("altar_closeup");
        Debug.Log($"[ALTAR_SHOT] distance_m=1.200 front=-Z isolated_inspection=True blur=False path={shot} camera={go.transform.position}");
        Application.Quit(0);
    }
}
