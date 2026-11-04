using System;
using System.Collections;
using System.IO;
using UnityEngine;

public sealed partial class PlayerController
{
    IEnumerator DemoClimbFeel()
    {
        interiorAuto=true; interiorDirection=Vector3.zero;
        int bad=0, crossed=0, samples=0;
        float maxError=0;
        string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../../Shots"));
        // Use the same Builds ancestor convention as the other acceptance captures.
        var root=new DirectoryInfo(Application.dataPath);
        while(root!=null&&root.Name!="Builds")root=root.Parent;
        dir=Path.Combine(root!=null?root.Parent.FullName:Directory.GetCurrentDirectory(),"Shots");Directory.CreateDirectory(dir);
        for(int f=0;f<3;f++) {
            var p=ClimbFeel.Point(ClimbFeel.FlightStart(f)-3);
            var start=new Vector3(p.x,0,p.y);
            if(!SurfaceAt(start,out var h)){Application.Quit(1);yield break;}
            start.y=h.point.y; transform.position=start;velocity=Vector3.zero;previousZ=start.z;
            follow.transform.position=start+FollowOffset+Vector3.up*(.5f*ClimbFeel.Progress(start.z));
            yield return new WaitForSecondsRealtime(.7f);
            if(f==0) {
                yield return new WaitForEndOfFrame();
                File.WriteAllBytes(Path.Combine(dir,"play_view.png"),CaptureClimbFrame());
            }
            float deadline=Time.realtimeSinceStartup+12;
            float endZ=ClimbFeel.Point(ClimbFeel.FlightStart(f)+ClimbFeel.Steps*ClimbFeel.Tread+2.1f).y;
            while(transform.position.z<endZ && Time.realtimeSinceStartup<deadline) {
                float z=transform.position.z+.7f;
                interiorDirection=new Vector3(ClimbRoute.CenterX(z)-transform.position.x,0,.7f).normalized;
                yield return null; samples++;
                if(!SurfaceAt(transform.position,out h))bad++;
                else {float error=Mathf.Abs(transform.position.y-h.point.y);maxError=Mathf.Max(maxError,error);if(error>.02f)bad++;}
            }
            interiorDirection=Vector3.zero;velocity=Vector3.zero;
            if(transform.position.z>=endZ)crossed++;else bad++;
            if(f==0) {
                // Inspect the actual solid flight from its lower platform, facing uphill.
                enabled=false;
                p=ClimbFeel.Point(ClimbFeel.FlightStart(f)-1.7f);
                var eye=new Vector3(p.x,0,p.y);SurfaceAt(eye,out h);eye.y=h.point.y+1.2f;
                p=ClimbFeel.Point(ClimbFeel.FlightStart(f)+1.5f);
                var aim=new Vector3(p.x,0,p.y);SurfaceAt(aim,out h);aim.y=h.point.y;
                follow.transform.position=eye;follow.transform.LookAt(aim);follow.fieldOfView=60;
                yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();
                File.WriteAllBytes(Path.Combine(dir,"climb_steps.png"),CaptureClimbFrame());
                enabled=true;
            }
        }
        var spawn=ClimbRoute.Spawn;SurfaceAt(spawn,out var fogSupport);spawn.y=fogSupport.point.y;
        transform.position=spawn;velocity=Vector3.zero;previousZ=spawn.z;
        yield return new WaitForSecondsRealtime(.2f);float baseDensity=RenderSettings.fogDensity;
        var plateau=new Vector3(0,0,-10);SurfaceAt(plateau,out fogSupport);plateau.y=fogSupport.point.y;
        transform.position=plateau;previousZ=plateau.z;
        yield return new WaitForSecondsRealtime(.2f);float topDensity=RenderSettings.fogDensity;
        float fogPct=100*(topDensity/baseDensity-1);
        if(Mathf.Abs(fogPct-24)>.01f)bad++;
        Debug.Log(FormattableString.Invariant($"[CLIMB_FOG_SAMPLE] base={baseDensity:F6} top={topDensity:F6} increase_pct={fogPct:F4} height_gain={plateau.y-spawn.y:F3}"));
        Debug.Log(FormattableString.Invariant($"[CLIMB_WALK] flights={crossed} samples={samples} bad={bad} ground_error_max={maxError:F6} raycast_misses={misses}"));
        Application.Quit(crossed==3&&bad==0&&misses==0?0:1);
    }
    byte[] CaptureClimbFrame() {
        var frame=ScreenCapture.CaptureScreenshotAsTexture();var bytes=frame.EncodeToPNG();Destroy(frame);return bytes;
    }
}
