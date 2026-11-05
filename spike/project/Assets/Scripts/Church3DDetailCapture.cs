using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Evidence-only pose: never serializes camera, ShotRig or character changes.
public sealed class Church3DDetailCapture : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Configure()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"-church3d-shots")<0)return;
        var standard=UnityEngine.Object.FindFirstObjectByType<Screenshotter>();standard.enabled=false;
        new GameObject("Church3D Capture Session").AddComponent<Church3DDetailCapture>();
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1);
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-shot");
        string dir=Path.GetDirectoryName(args[index+1]);Directory.CreateDirectory(dir);
        var rig=UnityEngine.Object.FindFirstObjectByType<ShotRig>();var cam=Camera.main;
        var window=GameObject.Find("WarmWindow_FrontL").transform;
        var target=new Vector3(-2,window.position.y+1,window.position.z);
        var close=ShotRig.Look("church_closeup",target+Vector3.back*12,target,35);
        Debug.Log($"[CHURCH3D_CAPTURE] distance_m={Vector3.Distance(close.position,target):F3} facade_z={target.z:F3} front=true");
        // Both are material inspection frames, like existing art_* poses: full-frame focus.
        foreach(var vol in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if(vol.profile!=null)foreach(var dof in vol.profile.components.OfType<DepthOfField>())dof.active=false;
        foreach(var pose in new[]{rig.poses.First(p=>p.name=="art_runtime"),close})
        {
            cam.transform.SetPositionAndRotation(pose.position,Quaternion.Euler(pose.euler));cam.fieldOfView=pose.fov;
            yield return null;yield return null;yield return null;yield return new WaitForEndOfFrame();
            var image=ScreenCapture.CaptureScreenshotAsTexture();string path=Path.Combine(dir,pose.name+".png");
            File.WriteAllBytes(path,image.EncodeToPNG());Destroy(image);
            Debug.Log($"[SHOT] written {path} exists={File.Exists(path)} bytes={new FileInfo(path).Length}");
        }
        Application.Quit();
    }
}
