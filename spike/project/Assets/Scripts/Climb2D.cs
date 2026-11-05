using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Isolated, software-rasterized 480x270 placeholder side route. No authored 3D objects are edited.</summary>
public sealed class Climb2D : MonoBehaviour
{
    public Texture2D atlas;
    public const int Width=480, Height=270, Steps=48;
    const float RampLength=900, Rise=120, Landing=40, StepHeight=8, StepDepth=10;
    const float StairStart=2*(RampLength+Landing), RouteEnd=StairStart+Steps*StepHeight;
    const float WalkSpeed=47, StairSpeed=Steps*StepHeight/7f;
    readonly float[] factors={.2f,.5f,.8f};
    readonly string[] shots={"start","ramp_mid","turn","stairs_mid","before_transition"};
    readonly float[] shotAt={0,450,920,StairStart+192,RouteEnd};
    Color32[] pixels, atlasPixels;
    Texture2D frame;
    Vector2 foot, eye;
    float progress, clock, walkClock, stairClock, fog, animationClock, lastProgress, startedAt;
    float maxSupportError, maxCameraError, maxFrameDt;
    int bad, samples, shotIndex, direction=1, animationFrame, idleSamples, leftSamples, rightSamples;
    bool automatic, transitioning, in3D, capturePending;
    string output;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void SelectMode()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"-demo-climb2d")<0) return;
        SceneManager.sceneLoaded+=Redirect;
    }
    static void Redirect(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded-=Redirect;
        if(scene.name=="Climb2D") return;
        // sceneLoaded precedes Start: suppress normal 3D demos while switching to the isolated scene.
        foreach(var root in scene.GetRootGameObjects()) root.SetActive(false);
        var go=new GameObject("Climb2D loader"); DontDestroyOnLoad(go);
        go.AddComponent<Climb2DLoader>();
    }

    void Start()
    {
        startedAt=Time.realtimeSinceStartup;
        Application.runInBackground=true;
        QualitySettings.vSyncCount=0; Application.targetFrameRate=60;
        automatic=Array.IndexOf(Environment.GetCommandLineArgs(),"-climb2d-auto")>=0;
        output=Path.Combine(Application.persistentDataPath,"Climb2D");
        var args=Environment.GetCommandLineArgs(); int at=Array.IndexOf(args,"-climb2d-output");
        if(at>=0 && at+1<args.Length) output=Path.GetFullPath(args[at+1]);
        Directory.CreateDirectory(output);
        frame=new Texture2D(Width,Height,TextureFormat.RGBA32,false) { filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp };
        pixels=new Color32[Width*Height]; atlasPixels=atlas.GetPixels32();
        foot=Support(0); eye=new Vector2(240,135);
        float rise=(Support(900).y-Support(0).y)+(Support(1840).y-Support(940).y);
        Debug.Log(FormattableString.Invariant($"[CLIMB2D] geometry ramps=2 rise={rise:F0}px lengths={RampLength:F0}/{RampLength:F0} angle={Mathf.Atan2(Rise,RampLength)*Mathf.Rad2Deg:F3} landings={Landing:F0} stairs={Steps} step={StepHeight:F0}x{StepDepth:F0} total_stair_rise={Support(RouteEnd).y-Support(StairStart).y:F0}px"));
        Debug.Log($"[CLIMB2D] mode=side resolution={frame.width}x{frame.height} scale=4 parallax={string.Join("/",Array.ConvertAll(factors,f=>f.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)))}");
        Debug.Log("[CLIMB2D] input=A/D stair_up=A/Left/W/Up stair_down=D/Right/S/Down follow=0.15 atlas_rows=left:1,right:2 fps=10 auto="+automatic);
        RenderFrame();
    }

    // Both input and the audit traverse the same route; reverse input returns along the current segment.
    public static Vector2 Support(float distance)
    {
        distance=Mathf.Clamp(distance,0,RouteEnd);
        if(distance<=900) return new Vector2(40+distance,40+distance*Rise/RampLength);
        if(distance<=940) return new Vector2(940+distance-900,160);
        if(distance<=1840) return new Vector2(980-(distance-940),160+(distance-940)*Rise/RampLength);
        if(distance<=StairStart) return new Vector2(80-(distance-1840),280);
        return new Vector2(40,280+Mathf.Min(Steps,Mathf.FloorToInt((distance-StairStart)/StepHeight))*StepHeight);
    }

    void Update()
    {
        if(in3D || transitioning || capturePending) return;
        float dt=Time.unscaledDeltaTime;
        clock+=dt; maxFrameDt=Mathf.Max(maxFrameDt,dt);
        float axis=InputAxis();
        lastProgress=progress;
        // Split at segment boundaries so the first stair frame cannot use ramp speed.
        float remaining=dt;
        while(remaining>0 && axis!=0)
        {
            float speed=progress>=StairStart?StairSpeed:WalkSpeed;
            float boundary=axis>0?(progress<StairStart?StairStart:RouteEnd):(progress>StairStart?StairStart:0);
            float portion=Mathf.Min(remaining,Mathf.Abs(boundary-progress)/speed);
            if(portion<.000001f) { progress=Mathf.Clamp(progress+axis*.0001f,0,RouteEnd); break; }
            if(progress>=StairStart) stairClock+=portion; else walkClock+=portion;
            progress=Mathf.Clamp(progress+axis*speed*portion,0,RouteEnd); remaining-=portion;
        }
        bool moving=Mathf.Abs(progress-lastProgress)>.00001f;
        Vector2 before=foot; foot=Support(progress);
        if(moving) { animationClock+=dt; animationFrame=Mathf.FloorToInt(animationClock*10)%6; if(Mathf.Abs(foot.x-before.x)>.0001f) direction=foot.x>before.x?1:-1; }
        else { animationClock=0; animationFrame=0; }
        var desired=new Vector2(Mathf.Clamp(foot.x,240,780),foot.y+55);
        float smooth=1-Mathf.Pow(1-.15f,dt*60);
        eye=Vector2.Lerp(eye,desired,smooth);
        // Audit support against the visible segment equations, independently of route sampling.
        float surfaceY=progress<=900?40+(foot.x-40)*Rise/RampLength:
            progress<=940?160:progress<=1840?160+(980-foot.x)*Rise/RampLength:
            progress<=StairStart?280:280+Mathf.Round((foot.y-280)/StepHeight)*StepHeight;
        float err=Mathf.Abs(foot.y-surfaceY); maxSupportError=Mathf.Max(maxSupportError,err);
        if(!moving) { idleSamples++; if(animationFrame!=0) bad++; }
        else if(direction<0) leftSamples++; else rightSamples++;
        float screenY=foot.y-(eye.y-135), screenX=foot.x-(eye.x-240);
        if(!float.IsFinite(foot.y) || err>.01f || screenX<0 || screenX>=480 || screenY<0 || screenY>=230) bad++;
        samples++; maxCameraError=Mathf.Max(maxCameraError,Vector2.Distance(eye,desired));
        RenderFrame();
        if(automatic && shotIndex<shots.Length && progress>=shotAt[shotIndex]) StartCoroutine(Capture());
        else if(progress>=RouteEnd) StartCoroutine(Transition());
        if(automatic && clock>65) { Debug.LogError("[CLIMB2D] timeout bad="+(bad+1)); Application.Quit(1); }
    }

    float InputAxis()
    {
        bool left=Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow), right=Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow);
        if(automatic)
        {
            // Drive the same A/D mapping as a player, including stop and a brief downhill reversal.
            left=false; right=false;
            if(clock>=.3f && clock<.6f) right=true;
            else if(clock>=.6f && clock<.9f) left=true;
            else if(clock>=.9f) { left=progress>=940; right=!left; }
        }
        if(progress>=StairStart) return left||Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:right||Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?-1:0;
        // On a return landing, continue left into the second ramp; reverse right onto the first ramp.
        bool returnLeg=progress>=940;
        return left==right?0:(right?1:-1)*(returnLeg?-1:1);
    }

    IEnumerator Capture()
    {
        capturePending=true;
        yield return new WaitForEndOfFrame();
        string path=Path.Combine(output,"climb2d_"+shots[shotIndex]+".png");
        var capture=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(path,capture.EncodeToPNG());
        Debug.Log(FormattableString.Invariant($"[CLIMB2D_SHOT] name={shots[shotIndex]} size={capture.width}x{capture.height} progress={progress:F3} foot=({foot.x:F2},{foot.y:F2}) path={path}"));
        if(capture.width!=1920 || capture.height!=1080) bad++;
        Destroy(capture); shotIndex++; capturePending=false;
    }

    IEnumerator Transition()
    {
        transitioning=true; float started=Time.realtimeSinceStartup;
        while(Time.realtimeSinceStartup-started<.6f) { fog=Mathf.Clamp01((Time.realtimeSinceStartup-started)/.6f); yield return null; }
        fog=1; yield return new WaitForEndOfFrame();
        float fadeSeconds=Time.realtimeSinceStartup-started;
        DontDestroyOnLoad(gameObject);
        yield return SceneManager.LoadSceneAsync("ArtSpike",LoadSceneMode.Single);
        yield return null; yield return null;
        var player=FindFirstObjectByType<PlayerController>();
        bool arrived=player!=null && player.ArriveFromClimb2D(out float groundError);
        if(!arrived) bad++;
        yield return new WaitForSecondsRealtime(.5f);
        var cam=Camera.main;
        float distance=player!=null && cam!=null?player.transform.position.z-cam.transform.position.z:0;
        bool sceneOK=SceneManager.GetActiveScene().name=="ArtSpike" && player!=null && player.enabled && cam!=null && cam.enabled;
        if(!sceneOK || Mathf.Abs(distance-11.5f)>.3f || fadeSeconds<.6f || fadeSeconds>.75f) bad++;
        float clear=Time.realtimeSinceStartup;
        while(Time.realtimeSinceStartup-clear<.6f) { fog=1-(Time.realtimeSinceStartup-clear)/.6f; yield return null; }
        fog=0; in3D=true;
        float totalSeconds=Time.realtimeSinceStartup-startedAt;
        if(automatic && (totalSeconds<40 || totalSeconds>60 || stairClock<6 || stairClock>8 || shotIndex!=5 || idleSamples==0 || leftSamples==0 || rightSamples==0)) bad++;
        Debug.Log($"[CLIMB2D] input_audit idle_samples={idleSamples} left_samples={leftSamples} right_samples={rightSamples} idle_frame=0");
        Debug.Log(FormattableString.Invariant($"[CLIMB2D] measured walk_s={walkClock:F3} stairs_s={stairClock:F3} total_s={totalSeconds:F3} fade_s={fadeSeconds:F3} samples={samples} support_error_px={maxSupportError:F6} camera_lag_max_px={maxCameraError:F3} max_dt={maxFrameDt:F4} shots={shotIndex}"));
        Debug.Log(FormattableString.Invariant($"[CLIMB2D] arrival scene={SceneManager.GetActiveScene().name} position={player?.transform.position.ToString("F3")} camera_z_distance={distance:F3} valid={sceneOK && arrived} bad={bad}"));
        Debug.Log("[CLIMB2D] ramps=2 rise=240px stairs=48 transition="+(sceneOK && arrived && bad==0?"ok":"FAIL"));
        yield return new WaitForEndOfFrame();
        if(automatic)
        {
            var arrival=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output,"arrival_3d.png"),arrival.EncodeToPNG()); Destroy(arrival);
            Application.Quit(bad==0?0:1);
        }
        Destroy(gameObject);
    }

    void RenderFrame()
    {
        float ox=eye.x-240, oy=eye.y-135;
        for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
        {
            float wx=x+ox, wy=y+oy;
            Color color=Color.Lerp(new Color(.71f,.79f,.83f),new Color(.35f,.49f,.61f),y/270f);
            for(int l=0;l<3;l++)
            {
                // Periodic analytic silhouettes: horizontal wrapping is continuous at every 512px boundary.
                float phase=Mathf.Repeat(x+eye.x*factors[l],512)/512f*Mathf.PI*2;
                float ridge=90-l*24+(40-l*7)*Mathf.Sin(phase)+18*Mathf.Sin(phase*3+l);
                if(y+eye.y*factors[l]*.12f<ridge) color=Color.Lerp(new Color(.53f,.64f,.70f),new Color(.31f,.43f,.49f),l/2f);
            }
            // Snow slopes border both edges of every walkable strip, including the switchback landing.
            for(int leg=0;leg<2;leg++)
            {
                float left=leg==0?40:80, right=980;
                if(wx<left || wx>right) continue;
                float top=leg==0?40+Mathf.Min(wx-40,900)*Rise/RampLength:160+(980-wx)*Rise/RampLength;
                float below=top-wy;
                if(below>=-8 && below<30)
                {
                    if(below<0 || below>7) color=new Color(.83f,.87f,.87f);
                    else color=new Color(.46f,.45f,.42f);
                    if(below>20) color=new Color(.66f,.73f,.76f);
                }
            }
            if(wx>=40 && wx<=80 && wy>=250 && wy<=288) color=wy>=280?new Color(.83f,.87f,.87f):new Color(.47f,.46f,.43f);
            if(wy>=280 && wy<=664 && wx>=25 && wx<=55)
            {
                int step=Mathf.Clamp(Mathf.FloorToInt((wy-280)/8),0,47);
                float sx=step%2==0?30:40;
                color=new Color(.82f,.86f,.87f);
                if(wx>=sx && wx<sx+StepDepth) color=(wy-280)%8>=6?new Color(.72f,.74f,.72f):new Color(.41f,.44f,.46f);
            }
            if(wx>=25 && wx<=75 && wy>=650 && wy<=664) color=new Color(.83f,.87f,.87f);
            pixels[y*Width+x]=color;
        }
        int px=Mathf.RoundToInt(foot.x-ox)-16, py=Mathf.RoundToInt(foot.y-oy);
        int row=direction>0?2:1;
        // Existing 64x80 atlas cell at half size: 32x40 logical pixels, nearest sample, idle frame zero.
        for(int y=0;y<40;y++) for(int x=0;x<32;x++)
        {
            int dx=px+x,dy=py+y;
            if(dx<0||dx>=Width||dy<0||dy>=Height) continue;
            Color32 c=atlasPixels[((3-row)*80+y*2)*atlas.width+animationFrame*64+x*2];
            if(c.a>0) pixels[dy*Width+dx]=Color32.Lerp(pixels[dy*Width+dx],c,c.a/255f);
        }
        frame.SetPixels32(pixels); frame.Apply(false);
    }
    void OnGUI()
    {
        GUI.depth=-1000;
        if(!in3D && frame!=null && SceneManager.GetActiveScene().name=="Climb2D")
        {
            int scale=Mathf.Max(1,Mathf.FloorToInt(Mathf.Min(Screen.width/(float)Width,Screen.height/(float)Height)));
            GUI.DrawTexture(new Rect((Screen.width-Width*scale)/2,(Screen.height-Height*scale)/2,Width*scale,Height*scale),frame,ScaleMode.StretchToFill,false);
        }
        if(fog>0) { GUI.color=new Color(1,1,1,fog); GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture); GUI.color=Color.white; }
    }
    void OnDestroy() { if(frame!=null) Destroy(frame); }
}

public sealed class Climb2DLoader : MonoBehaviour
{
    IEnumerator Start() { yield return SceneManager.LoadSceneAsync("Climb2D"); Destroy(gameObject); }
}
