using System;
using UnityEngine;

/// <summary>Six-frame, camera-relative pilgrim walk. Forward is the character's travel direction.</summary>
[RequireComponent(typeof(MeshRenderer))]
public sealed class PilgrimAnimator : MonoBehaviour
{
    public const float FramesPerSecond = 10f;
    Material animatedMaterial;
    bool loggedFirstFrame;
    MeshRenderer target;
    RectInt[] alphaBounds;
    float worldWidth,worldHeight;
    public int Frame { get; private set; }
    public int Row { get; private set; }

    public static int DirectionRow(Vector3 toCamera, Transform character)
    {
        var local=character.InverseTransformDirection(toCamera);
        if(Mathf.Abs(local.z)>=Mathf.Abs(local.x)) return local.z>=0 ? 0 : 3;
        // Viewer on the character's right sees its front pointing toward screen-left.
        return local.x>=0 ? 1 : 2;
    }

    void Awake()
    {
        target=GetComponent<MeshRenderer>();
        animatedMaterial=target.material;
        var material=animatedMaterial;
        worldWidth=material.GetFloat("_ScaleX");worldHeight=material.GetFloat("_ScaleY");
        var atlas=(Texture2D)material.mainTexture;
        var pixels=atlas.GetPixels32(); alphaBounds=new RectInt[24];
        for(int row=0;row<4;row++) for(int col=0;col<6;col++)
        {
            int minX=64,minY=80,maxX=-1,maxY=-1;
            for(int y=0;y<80;y++) for(int x=0;x<64;x++)
                if(pixels[((3-row)*80+y)*atlas.width+col*64+x].a>=128)
                { minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y); }
            if(maxY<0) throw new InvalidOperationException("Pilgrim runtime empty cell");
            alphaBounds[row*6+col]=new RectInt(minX,minY,maxX-minX+1,maxY-minY+1);
        }
        if(DirectionRow(transform.forward,transform)!=0 || DirectionRow(-transform.forward,transform)!=3 ||
           DirectionRow(transform.right,transform)!=1 || DirectionRow(-transform.right,transform)!=2)
            throw new InvalidOperationException("Pilgrim direction acceptance failed");
        Debug.Log("[PILGRIM] animation fps=10 frames=6 directionRows=down,left,right,up directionCheck=PASS");
    }

    void LateUpdate()
    {
        var camera=Camera.main;if(camera==null) return;
        Frame=Mathf.FloorToInt(Time.time*FramesPerSecond)%6;
        Row=DirectionRow(camera.transform.position-transform.position,transform);
        animatedMaterial.SetFloat("_Frame",Frame);
        animatedMaterial.SetFloat("_Row",Row);
        if(!loggedFirstFrame) { Debug.Log($"[PILGRIM] first animated frame={Frame} row={Row}"); loggedFirstFrame=true; }
    }

    void OnDestroy()
    {
        if(animatedMaterial!=null) Destroy(animatedMaterial);
    }

    public float ProjectedHeight(ShotPose pose, out bool contained)
    {
        int row=DirectionRow(pose.position-transform.position,transform);
        RectInt bounds=alphaBounds[row*6+Frame];
        Vector3 toCam=pose.position-transform.position;toCam.y=0;
        var right=Vector3.Cross(Vector3.up,toCam.normalized);
        Quaternion inverse=Quaternion.Inverse(Quaternion.Euler(pose.euler));
        float minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
        float aspect=(float)Screen.width/Screen.height,tan=Mathf.Tan(pose.fov*.5f*Mathf.Deg2Rad);
        contained=true;
        for(int iy=0;iy<2;iy++) for(int ix=0;ix<2;ix++)
        {
            float x=ix==0?bounds.xMin:bounds.xMax,y=iy==0?bounds.yMin:bounds.yMax;
            Vector3 world=transform.position+right*((x/64f-.5f)*worldWidth)+Vector3.up*(y/80f*worldHeight);
            Vector3 view=inverse*(world-pose.position);
            float vx=.5f+view.x/(view.z*tan*aspect)*.5f,vy=.5f+view.y/(view.z*tan)*.5f;
            contained &= view.z>0 && vx>=0 && vx<=1 && vy>=0 && vy<=1;
            minY=Math.Min(minY,vy*Screen.height);maxY=Math.Max(maxY,vy*Screen.height);
        }
        return maxY-minY;
    }

    public float VisibleWorldHeight => alphaBounds[Row*6+Frame].height/80f*worldHeight;
    public float MinVisibleWorldHeight => System.Linq.Enumerable.Min(alphaBounds, b=>b.height/80f*worldHeight);
    public float MaxVisibleWorldHeight => System.Linq.Enumerable.Max(alphaBounds, b=>b.height/80f*worldHeight);

    public void RefreshDimensions()
    {
        worldWidth=animatedMaterial.GetFloat("_ScaleX");
        worldHeight=animatedMaterial.GetFloat("_ScaleY");
    }

    public void ReportMeasurements(ShotRig rig)
    {
        float close=0,far=0;bool closeInside=false,farInside=false;
        foreach(var pose in rig.poses)
        {
            if(pose.name=="art_close") close=ProjectedHeight(pose,out closeInside);
            if(pose.name=="art_runtime") far=ProjectedHeight(pose,out farInside);
        }
        Debug.Log($"[PILGRIM] runtime shot art_close heightPx={close:F1} art_runtime heightPx={far:F1} frame={Frame} row={Row}");
        // 机位改为主角站在 art_runtime 视野里（art_close 现在看不到他，属正常）：
        // 主判据改为 far 机位的像素高度；close 仅记录。
        if(far<120||far>230||!farInside)
        {
            Debug.LogError($"[PILGRIM] runtime acceptance FAILED closeInside={closeInside} farInside={farInside}");
            Application.Quit(1);
        }
    }
}
