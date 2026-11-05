using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed partial class PlayerController
{
    // Dedicated evidence capture; regular gameplay/reveal cameras are untouched.
    IEnumerator DemoTerrainSlope()
    {
        interiorAuto=true;interiorDirection=Vector3.zero;
        yield return new WaitForSecondsRealtime(1);
        enabled=false;
        var eye=new Vector3(ClimbRoute.CenterX(-204)-36,0,-220);
        if(!ground.Raycast(new Ray(eye+Vector3.up*500,Vector3.down),out var support,1000)){Application.Quit(1);yield break;}
        eye.y=support.point.y+14;
        var aim=new Vector3(ClimbRoute.CenterX(-118),ClimbRoute.Height(-118)+3,-118);
        follow.transform.position=eye;follow.transform.LookAt(aim);follow.fieldOfView=60;
        // Focus this distant survey view on the hillside, using only runtime volume instances.
        foreach(var volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if(volume.profile!=null && volume.profile.TryGet<DepthOfField>(out var dof)) {
                dof.gaussianStart.Override(150);dof.gaussianEnd.Override(300);
            }
        yield return new WaitForSecondsRealtime(.8f);yield return new WaitForEndOfFrame();
        var root=new DirectoryInfo(Application.dataPath);
        while(root!=null&&root.Name!="Builds")root=root.Parent;
        var dir=Path.Combine(root!=null?root.Parent.FullName:Directory.GetCurrentDirectory(),"Shots");Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir,"climb_slope.png"),CaptureClimbFrame());
        Debug.Log("[SLOPE_CAPTURE] climb_slope.png");Application.Quit(0);
    }
}
