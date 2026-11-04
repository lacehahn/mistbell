using System;
using System.Collections;
using UnityEngine;

public sealed partial class PlayerController
{
    bool AuditCameraFrame()
    {
        var pose=new ShotPose { position=follow.transform.position,euler=follow.transform.eulerAngles,fov=follow.fieldOfView };
        float px=GetComponent<PilgrimAnimator>().ProjectedHeight(pose,out bool contained);
        float px1080=px*1080f/Screen.height;
        Debug.Log(FormattableString.Invariant($"[CAM_FRAME] dist={-FollowOffset.z:F2} h={FollowOffset.y:F2} look={LookHeight:F2} fov={follow.fieldOfView:F2} pilgrim_px={px1080:F3} pilgrim_pct={100*px/Screen.height:F3} actual_h={Screen.height} contained={contained}"));
        bool pass=contained && px1080>=140 && px1080<=165;
        if(!pass) { Debug.LogError("[CAM_FRAME] framing gate failed"); Application.Quit(1); }
        return pass;
    }

    IEnumerator DemoCameraDistance()
    {
        // Exercise the normal gameplay rig on the approach path; no substitute inspection camera.
        var start=new Vector3(ClimbRoute.CenterX(-110),0,-110);
        if(!SurfaceAt(start,out var support)) { Debug.LogError("[CAM_TEST] support missing"); Application.Quit(1); yield break; }
        start.y=support.point.y; transform.position=start; previousZ=start.z;
        follow.transform.position=start+FollowOffset;
        follow.transform.LookAt(start+Vector3.up*LookHeight);
        yield return new WaitForSecondsRealtime(2);
        yield return new WaitForEndOfFrame();
        bool frame=AuditCameraFrame();
        float top=0,bottom=1,left=1,right=0;
        foreach(var v in churchVertices) {
            var q=follow.WorldToViewportPoint(v);
            top=Mathf.Max(top,q.y);bottom=Mathf.Min(bottom,q.y);left=Mathf.Min(left,q.x);right=Mathf.Max(right,q.x);
        }
        bool composition=top<=1 && bottom>=.5f && left>=0 && right<=1;
        Debug.Log(FormattableString.Invariant($"[CAM_COMPOSITION] top={top:F5} bottom={bottom:F5} left={left:F5} right={right:F5} complete_upper={composition}"));
        InteriorShot("cam_play_1080");
        bool cameraPass=true;
        interiorAuto=true;
        for(int phase=0;phase<2;phase++)
        {
            if(phase==1)
            {
                // Repeat beside the retaining wall and stairs, after preserving the requested walk shot.
                start=new Vector3(0,0,-23);
                if(!SurfaceAt(start,out support)) { Application.Quit(1); yield break; }
                start.y=support.point.y; transform.position=start; previousZ=start.z; velocity=Vector3.zero;
                follow.transform.position=start+FollowOffset;
                follow.transform.LookAt(start+Vector3.up*LookHeight);
                yield return new WaitForSecondsRealtime(.5f);
            }
            int samples=0,bad=0; float minGround=float.PositiveInfinity,minWall=float.PositiveInfinity,t=0;
            while(t<6)
            {
                float nextZ=transform.position.z+(t<3?1:-1);
                interiorDirection=new Vector3(ClimbRoute.CenterX(nextZ)-transform.position.x,0,nextZ-transform.position.z).normalized;
                yield return new WaitForEndOfFrame();
                t+=Time.deltaTime; samples++;
                var eye=follow.transform.position;
                if(ground.Raycast(new Ray(eye+Vector3.up*100,Vector3.down),out var floor,200))
                { minGround=Mathf.Min(minGround,eye.y-floor.point.y); if(eye.y-floor.point.y<.05f)bad++; }
                else bad++;
                float wall=CameraWallGap(eye); minWall=Mathf.Min(minWall,wall);
                if(wall<.149f)bad++;
            }
            interiorDirection=Vector3.zero;
            if(phase==0) InteriorShot("cam_walk_1080");
            Debug.Log(FormattableString.Invariant($"[CAM_TEST] phase={phase} seconds={t:F3} samples={samples} bad={bad} ground_gap={minGround:F5} wall_gap={minWall:F5} turned=true"));
            cameraPass &= bad==0;
        }
        Application.Quit(frame && composition && cameraPass?0:1);
    }
}
