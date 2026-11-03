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
        go.transform.position=new Vector3(-2,interior.FloorY+1.05f,8.72f-.425f-1.2f);
        go.transform.rotation=Quaternion.identity;camera.fieldOfView=88;camera.nearClipPlane=.05f;
        foreach(var feature in Resources.FindObjectsOfTypeAll<UnityEngine.Rendering.Universal.FullScreenPassRendererFeature>())
            if(feature.name=="TiltShiftFeature")feature.SetActive(false);
        follow.enabled=false;
        yield return new WaitForSecondsRealtime(1);
        yield return new WaitForEndOfFrame();
        string shot=InteriorShot("altar_closeup");
        Debug.Log($"[ALTAR_SHOT] distance_m=1.200 front=-Z isolated_inspection=False blur=False path={shot} camera={go.transform.position}");
        var frame=ScreenCapture.CaptureScreenshotAsTexture();
        float Sample(string label,float x0,float x1,float y0,float y1,float z)
        {
            var a=camera.WorldToScreenPoint(new Vector3(-2+x0,interior.FloorY+y0,8.72f+z));
            var b=camera.WorldToScreenPoint(new Vector3(-2+x1,interior.FloorY+y1,8.72f+z));
            var rect=Rect.MinMaxRect(a.x/frame.width,a.y/frame.height,b.x/frame.width,b.y/frame.height);
            Debug.Log(FormattableString.Invariant($"[ALTAR_ROI] {label} x={a.x:F3} y={a.y:F3} w={b.x-a.x:F3} h={b.y-a.y:F3} origin=bottom_left"));
            return InteriorSurfaceLuma(frame,rect);
        }
        float cloth=Sample("cloth",-1.02f,1.02f,.72f,1.02f,-.51f);
        float body=(Sample("bodyL",-.42f,-.25f,.20f,.64f,-.425f)+Sample("bodyR",.25f,.42f,.20f,.64f,-.425f))*.5f;
        float trim=0,niche=0;
        for(int i=-1;i<=1;i++)
        {
            trim+=Sample("trim"+i,i*.66f+.182f,i*.66f+.202f,.21f,.65f,-.455f)/3;
            niche+=Sample("niche"+i,i*.66f-.14f,i*.66f+.14f,.23f,.67f,-.397f)/3;
        }
        Debug.Log(FormattableString.Invariant($"[ALTAR_MODEL2] cloth_luma={cloth:F5} trim_luma={trim:F5} body_luma={body:F5} niche_luma={niche:F5} cloth_over_body={cloth/body:F5} space=linear_rec709"));
        float peaks=0,valleys=0;
        float[] centers={-.78f,-.26f,.26f,.78f};
        for(int i=0;i<centers.Length;i++)
        {
            float x=centers[i];
            peaks+=Sample("peak"+i,x-.003f,x+.003f,.79f,.98f,-.531f)/4;
            valleys+=Sample("valley"+i,x+.047f,x+.053f,.79f,.98f,-.496f)/4;
        }
        Debug.Log(FormattableString.Invariant($"[ALTAR_FOLDS] peak_luma={peaks:F5} valley_luma={valleys:F5} contrast={peaks/valleys-1:F5}"));
        Destroy(frame);
        Application.Quit(0);
    }
}
