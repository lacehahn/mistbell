using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Isolated, software-rasterized 480x270 textured side route. No authored 3D objects are edited.</summary>
public sealed class Climb2D : MonoBehaviour
{
    public Texture2D atlas;
    public const int Width=480, Height=270, Steps=48;
    const float StepHeight=8, TransitionSeconds=.613f;
    const float StairStart=1880, RouteEnd=StairStart+Steps*StepHeight;
    const float WalkSpeed=47, StairSpeed=Steps*StepHeight/7f;
    readonly float[] factors={.2f,.5f,.6f};
    [Serializable] public class RoutePoint { public float x,y; }
    [Serializable] public class ArtRoute { public RoutePoint[] points; public float worldScaleX,worldScaleY,worldOriginX,worldOriginY,sourceStartY; }
    sealed class Layer { public int width,height; public Color32[] data; }
    static ArtRoute route;
    static float[] cumulative;
    Layer upper,lower,trees,path,step,platform;
    float maxBitmapError;
    int artSamples;
    static Vector2 World(RoutePoint p) => new Vector2(route.worldOriginX+p.x*route.worldScaleX,route.worldOriginY+(route.sourceStartY-p.y)*route.worldScaleY);
    static void LoadRoute()
    {
        if(route!=null) return;
        route=JsonUtility.FromJson<ArtRoute>(Resources.Load<TextAsset>("Climb2DArt/route").text);
        cumulative=new float[route.points.Length];
        for(int i=1;i<cumulative.Length;i++) cumulative[i]=cumulative[i-1]+Vector2.Distance(World(route.points[i-1]),World(route.points[i]));
    }
    static Layer LoadLayer(string name)
    {
        var t=Resources.Load<Texture2D>("Climb2DArt/"+name);
        if(t==null || !t.isReadable) throw new InvalidOperationException("Missing readable art: "+name);
        return new Layer { width=t.width,height=t.height,data=t.GetPixels32() };
    }
    void LoadArt()
    {
        LoadRoute(); upper=LoadLayer("side_slope_up");lower=LoadLayer("side_slope_down");trees=LoadLayer("side_slope_trees");
        path=LoadLayer("side_path_curve");step=LoadLayer("side_step_oblique");platform=LoadLayer("side_step_alt");
        shotAt[2]=cumulative[8]/cumulative[cumulative.Length-1]*StairStart;
        AuditArt();
        Debug.Log("[CLIMB2D_ART] layers=5 tree_parallax=0.6 path_follow=curve stairs=48 source=staged");
    }
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

    static float LabF(float v) => v>.008856f?Mathf.Pow(v,1f/3):7.787f*v+16f/116;
    static float Linear(byte b) { float c=b/255f;return c<=.04045f?c/12.92f:Mathf.Pow((c+.055f)/1.055f,2.4f); }
    void AuditArt()
    {
        float seam=0; double bsum=0; int count=0;
        foreach(var layer in new[]{upper,lower,trees,path,step})
        {
            if(layer!=step)
            {
                double d=0;
                for(int y=0;y<layer.height;y++)
                {
                    Color32 a=layer.data[y*layer.width],b=layer.data[y*layer.width+layer.width-1];
                    d+=Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)+Mathf.Abs(a.a-b.a);
                }
                seam=Mathf.Max(seam,(float)(d/(layer.height*4)));
            }
            for(int i=0;i<layer.data.Length;i+=17)
            {
                Color32 c=layer.data[i]; if(c.a<250) continue;
                float r=Linear(c.r),g=Linear(c.g),b=Linear(c.b);
                float fy=LabF(.2126729f*r+.7151522f*g+.072175f*b);
                float luminance=116*fy-16;
                if(luminance<18 || luminance>72) continue;
                bsum+=200*(fy-LabF((.0193339f*r+.119192f*g+.9503041f*b)/1.08883f));count++;
            }
        }
        float hue=(float)(bsum/Math.Max(1,count));
        Debug.Log(FormattableString.Invariant($"[CLIMB2D_ART] layers=5 seam={seam:F3} hue_b={hue:F3} path_follow=curve colour_samples={count}"));
        if(seam>3 || hue<4 || hue>6 || count==0) bad++;
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
        pixels=new Color32[Width*Height]; atlasPixels=atlas.GetPixels32(); LoadArt();
        foot=Support(0); eye=new Vector2(240,135);
        Debug.Log(FormattableString.Invariant($"[CLIMB2D_ART] geometry curve_points={route.points.Length} curve_length_px={cumulative[cumulative.Length-1]:F3} rise_px={Support(StairStart).y-Support(0).y:F3} landings=2 stairs={Steps} step_rise_px={StepHeight:F0}"));
        Debug.Log($"[CLIMB2D] mode=side resolution={frame.width}x{frame.height} scale=4 parallax={string.Join("/",Array.ConvertAll(factors,f=>f.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)))}");
        Debug.Log(FormattableString.Invariant($"[CLIMB2D_ART] transition_target_s={TransitionSeconds:F3}"));
        Debug.Log("[CLIMB2D] input=A/D stair_up=A/Left/W/Up stair_down=D/Right/S/Down follow=0.15 atlas_rows=left:1,right:2 fps=10 auto="+automatic);
        RenderFrame();
    }

    // Both input and the audit traverse the same route; reverse input returns along the current segment.
    public static Vector2 Support(float distance)
    {
        distance=Mathf.Clamp(distance,0,RouteEnd);
        LoadRoute();
        Vector2 end=World(route.points[route.points.Length-1]);
        if(distance>=StairStart) return end+Vector2.up*(Mathf.Min(Steps,Mathf.FloorToInt((distance-StairStart)/StepHeight))*StepHeight);
        float along=distance/StairStart*cumulative[cumulative.Length-1];
        for(int i=1;i<cumulative.Length;i++)
            if(along<=cumulative[i]) return Vector2.Lerp(World(route.points[i-1]),World(route.points[i]),Mathf.InverseLerp(cumulative[i-1],cumulative[i],along));
        return end;
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
        var desired=new Vector2(Mathf.Clamp(foot.x,240,1000),foot.y+55);
        float smooth=1-Mathf.Pow(1-.15f,dt*60);
        eye=Vector2.Lerp(eye,desired,smooth);
        // Independently project the foot onto the authored bitmap centreline.
        float err=float.PositiveInfinity;
        if(progress<StairStart)
        {
            for(int i=1;i<route.points.Length;i++)
            {
                Vector2 a=World(route.points[i-1]),b=World(route.points[i]),ab=b-a;
                err=Mathf.Min(err,Vector2.Distance(foot,a+ab*Mathf.Clamp01(Vector2.Dot(foot-a,ab)/ab.sqrMagnitude)));
            }
            float u=(foot.x-route.worldOriginX)/route.worldScaleX;
            float v=route.sourceStartY-(foot.y-route.worldOriginY)/route.worldScaleY;
            Color32 texel=path.data[Mathf.Clamp(path.height-1-Mathf.RoundToInt(v),0,path.height-1)*path.width+Mathf.Clamp(Mathf.RoundToInt(u),0,path.width-1)];
            if(texel.a<128) { maxBitmapError=Mathf.Max(maxBitmapError,1);bad++; }
            artSamples++;
        }
        else err=Mathf.Abs((foot.y-Support(StairStart).y)/StepHeight-Mathf.Round((foot.y-Support(StairStart).y)/StepHeight));
        maxSupportError=Mathf.Max(maxSupportError,err);
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
            else if(clock>=.9f) { left=progress>=StairStart || Support(Mathf.Min(progress+1,StairStart)).x<Support(progress).x; right=!left; }
        }
        if(progress>=StairStart) return left||Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:right||Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?-1:0;
        // On a return landing, continue left into the second ramp; reverse right onto the first ramp.
        bool returnLeg=progress<StairStart && Support(Mathf.Min(progress+1,StairStart)).x<Support(progress).x;
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
        while(Time.realtimeSinceStartup-started<TransitionSeconds) { fog=Mathf.Clamp01((Time.realtimeSinceStartup-started)/TransitionSeconds); yield return null; }
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
        if(automatic && (totalSeconds<45 || totalSeconds>55 || stairClock<6 || stairClock>8 || shotIndex!=5 || idleSamples==0 || leftSamples==0 || rightSamples==0)) bad++;
        Debug.Log(FormattableString.Invariant($"[CLIMB2D_ART] path_samples={artSamples} centreline_error_px={maxSupportError:F6} bitmap_misses={maxBitmapError:F0} path_follow=curve"));
        Debug.Log($"[CLIMB2D] input_audit idle_samples={idleSamples} left_samples={leftSamples} right_samples={rightSamples} idle_frame=0");
        Debug.Log(FormattableString.Invariant($"[CLIMB2D] measured walk_s={walkClock:F3} stairs_s={stairClock:F3} total_s={totalSeconds:F3} fade_s={fadeSeconds:F3} samples={samples} support_error_px={maxSupportError:F6} camera_lag_max_px={maxCameraError:F3} max_dt={maxFrameDt:F4} shots={shotIndex}"));
        Debug.Log(FormattableString.Invariant($"[CLIMB2D] arrival scene={SceneManager.GetActiveScene().name} position={player?.transform.position.ToString("F3")} camera_z_distance={distance:F3} valid={sceneOK && arrived} bad={bad}"));
        Debug.Log("[CLIMB2D] path=curve stairs=48 transition="+(sceneOK && arrived && bad==0?"ok":"FAIL"));
        yield return new WaitForEndOfFrame();
        if(automatic)
        {
            var arrival=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output,"arrival_3d.png"),arrival.EncodeToPNG()); Destroy(arrival);
            Application.Quit(bad==0?0:1);
        }
        Destroy(gameObject);
    }

    static readonly int[] TurnPoints={8,14};
    void RenderFrame()
    {
        float ox=eye.x-240, oy=eye.y-135;
        // Five authored depth layers: upper 55%, lower 45%, trees, S-band, steps/platforms.
        Background(lower,0,Mathf.CeilToInt(Height*.45f),.5f);
        Background(upper,Mathf.CeilToInt(Height*.45f),Height,.2f);
        float treeWidth=trees.width*.35f;
        float treeOffset=Mathf.Repeat(eye.x*.6f,treeWidth);
        for(int i=-1;i<3;i++) Blit(trees,i*treeWidth-treeOffset,98,treeWidth,trees.height*.35f);
        Blit(path,route.worldOriginX-ox,route.worldOriginY+(route.sourceStartY-path.height)*route.worldScaleY-oy,path.width*route.worldScaleX,path.height*route.worldScaleY);
        foreach(int i in TurnPoints)
        {
            Vector2 p=World(route.points[i]);
            Blit(platform,p.x-38-ox,p.y-28-oy,76,28);
        }
        Vector2 stairBase=Support(StairStart);
        for(int i=0;i<Steps;i++)
            Blit(step,stairBase.x-30-ox,stairBase.y+(i+1)*StepHeight-16-oy,60,16);
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
    readonly int[] backgroundColumns=new int[Width];
    void Background(Layer layer,int bottom,int top,float parallax)
    {
        for(int x=0;x<Width;x++)
        {
            int sx=Mathf.FloorToInt((x+eye.x*parallax)*2);
            int tile=Mathf.FloorToInt(sx/(float)layer.width);
            sx=((sx%layer.width)+layer.width)%layer.width;
            backgroundColumns[x]=(tile&1)!=0?layer.width-1-sx:sx;
        }
        for(int y=bottom;y<top;y++)
        {
            // The lower source has ~80 black pixels above its silhouette. Crop that
            // empty margin instead of stretching keyed pixels into a horizontal band.
            int sampledHeight=layer==lower?layer.height-100:layer.height;
            int sy=Mathf.Clamp((y-bottom)*(sampledHeight-1)/(top-bottom),0,layer.height-1);
            int src=sy*layer.width,dst=y*Width;
            for(int x=0;x<Width;x++)
            {
                Color32 c=layer.data[src+backgroundColumns[x]];
                if(c.a==255) pixels[dst+x]=c;
                else
                {
                    // Composite keyed silhouette edges over another part of the real lower slope.
                    int by=lower.height/2,bx=backgroundColumns[x]%lower.width;
                    Color32 backing=lower.data[by*lower.width+bx];backing.a=255;
                    pixels[dst+x]=Color32.Lerp(backing,c,c.a/255f);pixels[dst+x].a=255;
                }
            }
        }
    }
    void Blit(Layer layer,float x,float y,float width,float height)
    {
        int x0=Mathf.Max(0,Mathf.CeilToInt(x)),x1=Mathf.Min(Width,Mathf.CeilToInt(x+width));
        int y0=Mathf.Max(0,Mathf.CeilToInt(y)),y1=Mathf.Min(Height,Mathf.CeilToInt(y+height));
        for(int dy=y0;dy<y1;dy++) for(int dx=x0;dx<x1;dx++)
        {
            int sx=Mathf.Clamp(Mathf.FloorToInt((dx-x)/width*layer.width),0,layer.width-1);
            int sy=Mathf.Clamp(Mathf.FloorToInt((dy-y)/height*layer.height),0,layer.height-1);
            Color32 c=layer.data[sy*layer.width+sx];
            if(c.a>0) pixels[dy*Width+dx]=Color32.Lerp(pixels[dy*Width+dx],c,c.a/255f);
        }
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
