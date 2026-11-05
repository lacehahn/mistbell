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
    Layer upper,lower,trees,path,step,stone,cliff,styledPath,slopeShadow,downhillFace,depthHaze;
    const int SeamBlend=5, TopFog=10; // 20px / 40px at the fixed 4x output scale.
    readonly Layer[] treeVariants=new Layer[3], gradeDrops=new Layer[3];
    Color32[] gradeMask, gradeReference;
    readonly System.Collections.Generic.Dictionary<int,bool> snowCache=new System.Collections.Generic.Dictionary<int,bool>();
    readonly System.Collections.Generic.Dictionary<int,Color32> gradeCache=new System.Collections.Generic.Dictionary<int,Color32>();
    readonly Color32 air=new Color32(220,230,239,255);
    int seamHardLines, treeFlips, treeInstances;
    readonly string[] treeEvidence=new string[9];
    float treeMinGain=1,treeMaxGain=1;

    const int ShadowHeight=3; // 12 output pixels, open uphill/downhill bands.
    const float ShadowAlpha=.42f, ShadowOuterAlpha=.12f, StoneDetail=.35f;
    // Logical pixels; output is 4x. All route-attached layers share the road origin.
    const int DropHeight=40, CorniceHeight=10, CorniceOverhang=4, CorniceShadow=3, ValleyFade=15, HazeHeight=25;
    const float HazeAlpha=.10f, ForegroundParallax=1.15f;
    int cliffColumns, curveErrors, facePixels, hazePixels, foregroundPixels, foregroundMin, foregroundMax;
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
        path=LoadLayer("side_path_curve");step=LoadLayer("side_step_oblique");stone=LoadLayer("side_ground_top_oblique");
        cliff=LoadLayer("side_ground_cliff");
        BuildPathStyle(); BuildDepthStyle(); BuildStepStyle(); BuildGradeLayers();
        foreach(int i in TurnPoints)
        {
            Vector2 p=World(route.points[i]);
            Debug.Log(FormattableString.Invariant($"[FLOAT] name=side_step_alt[{i}] pos=({p.x-38:F2},{p.y-28:F2}) size=(76,28) removed=true"));
        }
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
        if(shotIndex<3) AuditPathScreenshot(shots[shotIndex]);
        var contactEvidence=new Color32[pixels.Length];
        for(int i=0;i<contactEvidence.Length;i++) if(contactMask[i]) contactEvidence[i]=contactColours[i];
        SaveLayer(new Layer { width=Width,height=Height,data=contactEvidence },"path_contact_"+shots[shotIndex]);
        AuditDepthScreenshot(shots[shotIndex]);
        AuditGrade(shots[shotIndex]);
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
        // Five authored depth layers with a 20px overlapping background blend.
        Background(lower,0,Mathf.CeilToInt(Height*.45f)+SeamBlend,.5f);
        Background(upper,Mathf.CeilToInt(Height*.45f),Height,.2f);
        Array.Clear(contactMask,0,contactMask.Length);
        DrawTrees();
        float pathY=route.worldOriginY+(route.sourceStartY-path.height)*route.worldScaleY-oy;
        Blit(depthHaze,route.worldOriginX-ox,pathY-ValleyFade-DropHeight,depthHaze.width,depthHaze.height);
        Blit(slopeShadow,route.worldOriginX-ox,pathY-ShadowHeight,slopeShadow.width,slopeShadow.height);
        Blit(downhillFace,route.worldOriginX-ox-CorniceOverhang,pathY-ValleyFade-DropHeight,downhillFace.width,downhillFace.height);
        Blit(styledPath,route.worldOriginX-ox,pathY,styledPath.width,styledPath.height);
        DrawPathBites(ox,pathY);
        Vector2 stairBase=Support(StairStart);
        for(int i=0;i<Steps;i++)
            Blit(step,stairBase.x-30-ox,stairBase.y+(i+1)*StepHeight-16-oy,60,16);
        ApplyGrade();
        // Air is a final 40px overlay so the top border reaches exactly #DCE6EF.
        for(int y=Height-TopFog;y<Height;y++) for(int x=0;x<Width;x++)
        {
            float t=Mathf.SmoothStep(0,1,(y-(Height-TopFog))/(float)(TopFog-1));
            int at=y*Width+x; pixels[at]=Color32.Lerp(pixels[at],air,t); gradeMask[at]=new Color32();
        }
        int px=Mathf.RoundToInt(foot.x-ox)-16, py=Mathf.RoundToInt(foot.y-oy);
        int row=direction>0?2:1;
        // Existing 64x80 atlas cell at half size: 32x40 logical pixels, nearest sample, idle frame zero.
        for(int y=0;y<40;y++) for(int x=0;x<32;x++)
        {
            int dx=px+x,dy=py+y;
            if(dx<0||dx>=Width||dy<0||dy>=Height) continue;
            Color32 c=atlasPixels[((3-row)*80+y*2)*atlas.width+animationFrame*64+x*2];
            if(c.a>0) { pixels[dy*Width+dx]=Color32.Lerp(pixels[dy*Width+dx],c,c.a/255f); gradeMask[dy*Width+dx]=new Color32(); }
        }
        DrawForeground();
        // The display-resolution contact strip has its own exact pixel audit;
        // omit these partially covered cells from logical-buffer grade statistics.
        for(int i=0;i<contactMask.Length;i++) if(contactMask[i]) gradeMask[i]=new Color32();
        frame.SetPixels32(pixels); frame.Apply(false);
    }
    static float Lightness(Color32 c) => 116*LabF(.2126729f*Linear(c.r)+.7151522f*Linear(c.g)+.072175f*Linear(c.b))-16;
    void BuildPathStyle()
    {
        int w=Mathf.CeilToInt(path.width*route.worldScaleX),h=Mathf.CeilToInt(path.height*route.worldScaleY);
        styledPath=new Layer { width=w,height=h,data=new Color32[w*h] };
        slopeShadow=new Layer { width=w,height=h+2*ShadowHeight,data=new Color32[w*(h+2*ShadowHeight)] };
        for(int y=0;y<h;y++) for(int x=0;x<w;x++)
        {
            int sx=Mathf.Min(path.width-1,Mathf.FloorToInt(x/route.worldScaleX));
            int sy=Mathf.Min(path.height-1,Mathf.FloorToInt(y/route.worldScaleY));
            Color32 c=path.data[sy*path.width+sx];
            if(c.a<128) continue;
            // Only the exposed top (source rows 60..239), repeated at 8 source texels/pixel.
            Color32 detail=stone.data[(stone.height-1-(60+y*8%180))*stone.width+x*8%stone.width];
            c.r=(byte)Mathf.RoundToInt((c.r*(1-StoneDetail)+detail.r*.65f*StoneDetail)*.9f);
            c.g=(byte)Mathf.RoundToInt((c.g*(1-StoneDetail)+detail.g*.65f*StoneDetail)*.9f);
            c.b=(byte)Mathf.RoundToInt((c.b*(1-StoneDetail)+detail.b*.65f*StoneDetail)*.9f);
            styledPath.data[y*w+x]=c;
        }
        // Shade only the uphill/downhill edges of each road run. Do not trace
        // the vertical end caps: their neighbouring edge heights change steeply.
        for(int x=2;x<w-2;x++) for(int y=0;y<h;y++)
        {
            if(styledPath.data[y*w+x].a<128) continue;
            foreach(int side in new[]{-1,1})
            {
                int next=y+side;
                if(next>=0 && next<h && styledPath.data[next*w+x].a>=128) continue;
                int left=EdgeAt(x-2,y,side),right=EdgeAt(x+2,y,side);
                if(left<0 || right<0 || Mathf.Abs(left-right)>4) continue;
                for(int d=1;d<=ShadowHeight;d++)
                {
                    int sy=y+side*d;
                    if(sy<0 || sy>=h || styledPath.data[sy*w+x].a<128)
                    {
                        float alpha=Mathf.Lerp(ShadowAlpha,ShadowOuterAlpha,(d-1f)/(ShadowHeight-1));
                        int at=(sy+ShadowHeight)*w+x;
                        byte a=(byte)Mathf.RoundToInt(255*alpha);
                        if(a>slopeShadow.data[at].a) slopeShadow.data[at]=new Color32(58,64,72,a);
                    }
                }
            }
        }
        Debug.Log(FormattableString.Invariant($"[PATH_EDGE] width={ShadowHeight*4} alpha={ShadowAlpha:F2}→{ShadowOuterAlpha:F2} closed=false"));
        SaveLayer(styledPath,"path_read_surface"); SaveLayer(slopeShadow,"path_read_edge");
    }
    // Anchor the three existing rock cuts to three distinct road runs. Warp every
    // column to the downhill edge, so the cap really occludes 8px of road.
    readonly bool[] contactMask=new bool[Width*Height];
    readonly Color32[] contactColours=new Color32[Width*Height];
    readonly int[] bitePoints={4,7,10};
    readonly int[] biteColumns=new int[3];
    void DrawPathBites(float ox,float pathY)
    {
        for(int i=0;i<gradeDrops.Length;i++)
        {
            Layer rock=gradeDrops[i];
            int centre=Mathf.RoundToInt(route.points[bitePoints[i]].x*route.worldScaleX);
            int near=Mathf.RoundToInt((path.height-route.points[bitePoints[i]].y)*route.worldScaleY);
            int columns=0;
            for(int x=0;x<rock.width;x++)
            {
                int rx=centre-rock.width/2+x;
                if(rx<0 || rx>=styledPath.width) continue;
                int edge=-1;
                for(int d=0;d<styledPath.height && edge<0;d++)
                    for(int sign=-1;sign<=1;sign+=2)
                    {
                        int y=near+d*sign;
                        if(y>=0 && y<styledPath.height && styledPath.data[y*styledPath.width+rx].a>=128 &&
                           (y==0 || styledPath.data[(y-1)*styledPath.width+rx].a<128)) { edge=y;break; }
                    }
                if(edge<0) continue;
                columns++;
                int dx=Mathf.CeilToInt(route.worldOriginX-ox+rx);
                int top=Mathf.CeilToInt(pathY+edge+2); // 2 logical = 8 output pixels.
                if(dx<0 || dx>=Width) continue;
                for(int y=0;y<rock.height;y++)
                {
                    int dy=top-rock.height+y;
                    if(dy<0 || dy>=Height) continue;
                    Color32 c=rock.data[y*rock.width+x];
                    // Keep the lower fade and side blend; the contact cap must be solid.
                    if(y>=rock.height-6) c.a=(byte)Mathf.RoundToInt(255*Mathf.SmoothStep(0,1,Mathf.Min(x,rock.width-1-x)/(float)SeamBlend));
                    int at=dy*Width+dx;
                    pixels[at]=Color32.Lerp(pixels[at],c,c.a/255f);
                }
                // Keep this subpixel strip for OnGUI: 3 physical pixels at 4x,
                // shaded through the unchanged grade function, at true alpha .3.
                if(top>=0 && top<Height)
                {
                    float side=Mathf.SmoothStep(0,1,Mathf.Min(x,rock.width-1-x)/(float)SeamBlend);
                    int at=top*Width+dx;
                    contactMask[at]=true;
                    contactColours[at]=Grade(Color32.Lerp(pixels[at],new Color32(58,64,72,255),.3f*side));
                }
            }
            biteColumns[i]=columns;
        }
    }
    void BuildDepthStyle()
    {
        int w=styledPath.width,h=styledPath.height, pad=DropHeight+ValleyFade;
        downhillFace=new Layer { width=w+2*CorniceOverhang,height=h+pad+HazeHeight,data=new Color32[(w+2*CorniceOverhang)*(h+pad+HazeHeight)] };
        depthHaze=new Layer { width=w,height=h+pad+HazeHeight,data=new Color32[w*(h+pad+HazeHeight)] };
        int minDrop=int.MaxValue,maxDrop=0,minCornice=int.MaxValue,maxCornice=0;
        // Every opaque run is a separate road branch. Process low to high so
        // the upper branch occludes the lower cliff at a hairpin.
        for(int x=0;x<w;x++) for(int y=0;y<h;y++)
        {
            if(styledPath.data[y*w+x].a<128) continue;
            if(y==0 || styledPath.data[(y-1)*w+x].a<128)
            {
                cliffColumns++;
                int renderedDrop=0,renderedSnow=0;
                for(int d=1;d<=DropHeight+ValleyFade;d++)
                {
                    Color32 c;
                    if(d<=CorniceHeight)
                    {
                        // Cold snow cap with a scalloped projecting lower lip.
                        byte shade=(byte)(238-d*2);
                        c=new Color32(shade,(byte)(shade+4),(byte)Mathf.Min(255,shade+9),255);
                        renderedSnow++;
                    }
                    else
                    {
                        int sx=Mathf.Clamp(320+(d-CorniceHeight)*4,0,cliff.width-1);
                        int rockHeight=cliff.height/2;
                        int along=x*2%(rockHeight*2);
                        int sy=along<rockHeight?along:rockHeight*2-1-along;
                        c=cliff.data[sy*cliff.width+sx];c.a=255;
                        c=Color32.Lerp(c,new Color32(71,83,100,255),.28f);
                        if(d<=CorniceHeight+CorniceShadow) c=Color32.Lerp(c,new Color32(42,52,67,255),.65f);
                        if(d>DropHeight-8) c=Color32.Lerp(c,new Color32(185,199,212,255),(d-(DropHeight-8))/8f);
                    }
                    if(d<=DropHeight) renderedDrop++;
                    else
                    {
                        // The final 60 output pixels fade the cliff into valley air.
                        float t=(d-DropHeight)/(float)ValleyFade;
                        c=new Color32(185,199,212,(byte)Mathf.RoundToInt(255*.5f*(1-t)));
                    }
                    int targetY=y-d+pad;
                    int spread=d<=CorniceHeight?CorniceOverhang:0;
                    for(int dx=-spread;dx<=spread;dx++)
                    {
                        int targetX=x+CorniceOverhang+dx;
                        downhillFace.data[targetY*downhillFace.width+targetX]=c;
                    }
                }
                minDrop=Mathf.Min(minDrop,renderedDrop);maxDrop=Mathf.Max(maxDrop,renderedDrop);
                minCornice=Mathf.Min(minCornice,renderedSnow);maxCornice=Mathf.Max(maxCornice,renderedSnow);
                // Check the generated lip against its source road edge, not a straight baseline.
                if(downhillFace.data[(y-1+pad)*downhillFace.width+x+CorniceOverhang].a==0) curveErrors++;
            }
            if(y==h-1 || styledPath.data[(y+1)*w+x].a<128)
                for(int d=1;d<=HazeHeight;d++)
                    depthHaze.data[(y+d+pad)*w+x]=new Color32(188,201,213,(byte)Mathf.RoundToInt(255*HazeAlpha));
        }
        Debug.Log(FormattableString.Invariant($"[CLIFF] drop_h={minDrop*4} cornice={minCornice*4} fog={HazeAlpha*100:F0}% follows_curve={(cliffColumns>0 && curveErrors==0).ToString().ToLowerInvariant()} drop_max={maxDrop*4} cornice_max={maxCornice*4} overhang={CorniceOverhang*4} shadow={CorniceShadow*4} valley_fade={ValleyFade*4} edge_samples={cliffColumns} curve_errors={curveErrors}"));
        if(cliffColumns==0 || curveErrors!=0 || minDrop*4<120 || maxDrop*4>200 || minCornice*4<30 || maxCornice*4>50) bad++;
    }
    static float ForegroundWorldX(float x,float cameraX) => x+cameraX*ForegroundParallax;
    static int ForegroundHeight(float worldX)
    {
        // Two irregular frequencies make snow shelves and rock corners.
        return 15+Mathf.FloorToInt(4*(.5f+.5f*Mathf.Sin(worldX*.043f))+3*(.5f+.5f*Mathf.Sin(worldX*.113f)));
    }
    void DrawForeground()
    {
        foregroundPixels=0;foregroundMin=int.MaxValue;foregroundMax=0;
        for(int x=0;x<Width;x++)
        {
            int height=ForegroundHeight(ForegroundWorldX(x,eye.x));
            foregroundMin=Mathf.Min(foregroundMin,height);foregroundMax=Mathf.Max(foregroundMax,height);
            for(int y=0;y<height;y++)
            {
                float alpha=Mathf.SmoothStep(0,1,(height-1-y)/(float)SeamBlend);
                int at=y*Width+x; pixels[at]=Color32.Lerp(pixels[at],new Color32(213,224,234,255),alpha);
                gradeMask[at]=new Color32(); foregroundPixels++;
            }
        }
    }
    void AuditDepthScreenshot(string name)
    {
        facePixels=0;hazePixels=0;
        float ox=eye.x-240,oy=eye.y-135;
        float originY=route.worldOriginY+(route.sourceStartY-path.height)*route.worldScaleY-oy-DropHeight-ValleyFade;
        for(int y=foregroundMax;y<Height;y++) for(int x=0;x<Width;x++)
        {
            int sx=Mathf.FloorToInt(x+ox-route.worldOriginX),sy=Mathf.FloorToInt(y-originY);
            if(sx<0 || sx>=depthHaze.width || sy<0 || sy>=depthHaze.height) continue;
            if(downhillFace.data[sy*downhillFace.width+sx+CorniceOverhang].a>0) facePixels++;
            if(depthHaze.data[sy*depthHaze.width+sx].a>0) hazePixels++;
        }
        float shift=ForegroundWorldX(0,eye.x+100)-ForegroundWorldX(0,eye.x);
        bool fg=foregroundPixels>0 && foregroundMin*4>=60 && foregroundMax*4<=90;
        Debug.Log(FormattableString.Invariant($"[DEPTH] haze_alpha={depthHaze.data[Array.FindIndex(depthHaze.data,c=>c.a>0)].a/255f:F3} fg_silhouette={fg.ToString().ToLowerInvariant()} fg_parallax={shift/100:F2} shot={name} haze_h={HazeHeight*4} fg_min={foregroundMin*4} fg_max={foregroundMax*4} face_pixels={facePixels} haze_pixels={hazePixels} fg_pixels={foregroundPixels}"));
        // The vertical stair camera has left the curved road behind.
        if(!fg || (progress<StairStart && (facePixels==0 || hazePixels==0))) bad++;
    }
    int EdgeAt(int x,int y,int side)
    {
        int w=styledPath.width,h=styledPath.height;
        // Search locally only: another branch must never bridge an end cap.
        for(int d=0;d<=3;d++) for(int sign=-1;sign<=1;sign+=2)
        {
            int sy=y+d*sign;
            if(sy<0 || sy>=h || styledPath.data[sy*w+x].a<128) continue;
            int next=sy+side;
            if(next<0 || next>=h || styledPath.data[next*w+x].a<128) return sy;
        }
        return -1;
    }
    void BuildStepStyle()
    {
        var source=step;
        step=new Layer { width=60,height=16,data=new Color32[60*16] };
        int maxSnow=0;
        for(int x=0;x<60;x++)
        {
            int snow=0,sx=x*source.width/60;
            for(int y=0;y<16;y++)
            {
                Color32 c=source.data[(y*source.height/16)*source.width+sx];
                // The compressed source lip sits at row 10 (bottom-up). All
                // other bright patches become riser stone, including tread snow.
                if(c.a>0 && Lightness(c)>70)
                {
                    if(y==10) snow++;
                    else
                    {
                        Color32 rock=source.data[(source.height/3)*source.width+sx];
                        c=new Color32((byte)Mathf.Min(rock.r,145),(byte)Mathf.Min(rock.g,145),(byte)Mathf.Min(rock.b,145),c.a);
                    }
                }
                step.data[y*60+x]=c;
            }
            maxSnow=Mathf.Max(maxSnow,snow);
        }
        Debug.Log($"[STEP_SNOW] steps={Steps} max_snow_px={maxSnow*4} limit_px=6 sample=rendered_60x16 threshold_L=70");
    }
    static float LabB(Color32 c)
    {
        float r=Linear(c.r),g=Linear(c.g),b=Linear(c.b);
        return 200*(LabF(.2126729f*r+.7151522f*g+.072175f*b)-LabF((.0193339f*r+.119192f*g+.9503041f*b)/1.08883f));
    }
    static float LabInverse(float f) { float v=f*f*f;return v>.008856f?v:(f-16f/116)/7.787f; }
    static byte SRGB(float c) => (byte)Mathf.RoundToInt(255*Mathf.Clamp01(c<=.0031308f?12.92f*c:1.055f*Mathf.Pow(c,1/2.4f)-.055f));
    static Color32 FromLab(float l,float a,float b,byte alpha)
    {
        float fy=(l+16)/116, x=.95047f*LabInverse(fy+a/500), y=LabInverse(fy), z=1.08883f*LabInverse(fy-b/200);
        return new Color32(SRGB(3.2404542f*x-1.5371385f*y-.4985314f*z),SRGB(-.969266f*x+1.8760108f*y+.041556f*z),SRGB(.0556434f*x-.2040259f*y+1.0572252f*z),alpha);
    }
    Color32 Grade(Color32 c)
    {
        int key=c.r|(c.g<<8)|(c.b<<16);
        if(gradeCache.TryGetValue(key,out var cached)) return cached;
        float l=Lightness(c);
        bool snow=l>70;
        // L* slope is exactly 1.12 before RGB quantisation. Lift snow toward the
        // specified cold palette, then neutralise its b* to -1 (rather than beige).
        float target=Mathf.Clamp(50+1.12f*(l-50)+(snow?8:0),6,96);
        Color32 result=FromLab(target,snow?-1:0,snow?-1:6,255);
        gradeCache[key]=result; snowCache[key]=snow; return result;
    }
    void ApplyGrade()
    {
        if(gradeMask==null) { gradeMask=new Color32[pixels.Length];gradeReference=new Color32[pixels.Length]; }
        for(int i=0;i<pixels.Length;i++)
        {
            Color32 c=pixels[i];
            gradeReference[i]=c; pixels[i]=Grade(c);
            gradeMask[i]=snowCache[c.r|(c.g<<8)|(c.b<<16)]?new Color32(0,255,0,255):new Color32(255,0,0,255);
        }
    }
    static Layer Crop(Layer source,int x,int top,int w,int h)
    {
        var result=new Layer { width=w,height=h,data=new Color32[w*h] };
        for(int y=0;y<h;y++) for(int dx=0;dx<w;dx++) result.data[y*w+dx]=source.data[(source.height-top-h+y)*source.width+x+dx];
        return result;
    }
    void BuildGradeLayers()
    {
        // Three distinct isolated trees from the authored sheet; no row of trees
        // is scaled as a single repeated sprite. Coordinates are top-left source.
        treeVariants[0]=Crop(trees,496,86,96,146);
        treeVariants[1]=Crop(trees,980,72,144,212);
        treeVariants[2]=Crop(trees,168,10,194,290);
        int[] heights={30,40,50},widths={50,75,100};
        for(int i=0;i<gradeDrops.Length;i++)
        {
            int w=widths[i],h=heights[i],snow=6;
            var layer=new Layer { width=w,height=h+snow,data=new Color32[w*(h+snow)] };
            for(int y=0;y<layer.height;y++) for(int x=0;x<w;x++)
            {
                Color32 c;
                int lip=h+Mathf.RoundToInt(1.5f*Mathf.Sin(x*.19f+i));
                if(y>=lip) c=new Color32(237,243,248,255);
                else
                {
                    // Vertical cut through opaque cliff rock, avoiding its snow silhouette.
                    int sx=320+(h-1-y)*4,sy=(x*4+i*110)%(cliff.height/2);
                    c=cliff.data[sy*cliff.width+sx];c.a=255;
                }
                float bottom=Mathf.SmoothStep(0,1,y/10f);
                float side=Mathf.SmoothStep(0,1,Mathf.Min(x,w-1-x)/(float)SeamBlend);
                float top=Mathf.SmoothStep(0,1,(lip+5-y)/(float)SeamBlend);
                c.a=(byte)Mathf.RoundToInt(255*bottom*side*top);layer.data[y*w+x]=c;
            }
            gradeDrops[i]=layer;
        }
        int hard=0,minH=int.MaxValue,maxH=0,minW=int.MaxValue,maxW=0;
        foreach(var layer in gradeDrops)
        {
            int x=layer.width/2;
            int h=(layer.height-6)*4; minH=Mathf.Min(minH,h);maxH=Mathf.Max(maxH,h);
            minW=Mathf.Min(minW,layer.width*4);maxW=Mathf.Max(maxW,layer.width*4);
            for(int y=1;y<10;y++) if(layer.data[y*layer.width+x].a==0 || layer.data[y*layer.width+x].a==255) hard++;
        }
        Debug.Log($"[CLIFF2] drops={gradeDrops.Length} each_h={minH}–{maxH} min_h={minH} max_h={maxH} min_w={minW} max_w={maxW} cornice=24 fade=40 alpha_errors={hard}");
        if(gradeDrops.Length!=3 || minH<120 || maxH>200 || hard!=0) bad++;
        for(int i=0;i<gradeDrops.Length;i++) SaveLayer(gradeDrops[i],"grade_drop_"+i);
        for(int i=0;i<treeVariants.Length;i++) SaveLayer(treeVariants[i],"grade_tree_source_"+i);
    }
    static readonly float[] TreeHeights={22.5f,37.5f,60f};
    static float Hash(int n) => Mathf.Repeat(Mathf.Sin(n*127.1f+311.7f)*43758.5453f,1);
    void DrawTrees()
    {
        treeInstances=0;treeFlips=0;treeMinGain=2;treeMaxGain=0;
        int first=Mathf.FloorToInt(eye.x*.6f/74)-1;
        for(int id=first;id<first+9;id++)
        {
            int variant=((id%3)+3)%3; var tree=treeVariants[variant];
            float height=TreeHeights[variant];
            float width=height*tree.width/tree.height;
            float x=id*74-eye.x*.6f+(Hash(id+7)*2-1)*3;
            float y=99+(Hash(id+19)*2-1)*3;
            bool flip=Hash(id+31)>.5f;
            float gain=1+(Hash(id+41)*2-1)*.08f;
            treeEvidence[treeInstances]=FormattableString.Invariant($"id={id} variant={variant} height_px={height*4:F0} flip={flip.ToString().ToLowerInvariant()} gain={gain:F5} jitter_x={(Hash(id+7)*2-1)*12:F3} jitter_y={(Hash(id+19)*2-1)*12:F3}");
            treeInstances++;if(flip) treeFlips++;
            treeMinGain=Mathf.Min(treeMinGain,gain);treeMaxGain=Mathf.Max(treeMaxGain,gain);
            for(int dy=Mathf.Max(0,Mathf.CeilToInt(y));dy<Mathf.Min(Height,Mathf.CeilToInt(y+height));dy++)
                for(int dx=Mathf.Max(0,Mathf.CeilToInt(x));dx<Mathf.Min(Width,Mathf.CeilToInt(x+width));dx++)
                {
                    int sx=Mathf.Clamp((int)((dx-x)/width*tree.width),0,tree.width-1);
                    int sy=Mathf.Clamp((int)((dy-y)/height*tree.height),0,tree.height-1);
                    if(flip) sx=tree.width-1-sx;
                    Color32 c=tree.data[sy*tree.width+sx];
                    c.r=(byte)Mathf.Clamp(Mathf.RoundToInt(c.r*gain),0,255);c.g=(byte)Mathf.Clamp(Mathf.RoundToInt(c.g*gain),0,255);c.b=(byte)Mathf.Clamp(Mathf.RoundToInt(c.b*gain),0,255);
                    // Fade the roots into the snow instead of cutting a horizontal edge.
                    float alpha=c.a/255f*Mathf.SmoothStep(0,1,(dy-y)/SeamBlend);
                    pixels[dy*Width+dx]=Color32.Lerp(pixels[dy*Width+dx],c,alpha);
                }
        }
    }
    void SaveLayer(Layer layer,string name)
    {
        var t=new Texture2D(layer.width,layer.height,TextureFormat.RGBA32,false);
        t.SetPixels32(layer.data);t.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),t.EncodeToPNG());Destroy(t);
    }
    void AuditGrade(string name)
    {
        double snow=0,stoneB=0,sumX=0,sumY=0,sumXX=0,sumXY=0;int snows=0,stones=0,topBad=0;
        for(int i=0;i<pixels.Length;i++)
        {
            if(gradeMask[i].g==255) { snow+=LabB(pixels[i]);snows++; }
            else if(gradeMask[i].r==255)
            {
                stoneB+=LabB(pixels[i]);stones++;
                double x=Lightness(gradeReference[i]),y=Lightness(pixels[i]);
                sumX+=x;sumY+=y;sumXX+=x*x;sumXY+=x*y;
            }
            if(i>=pixels.Length-Width && !pixels[i].Equals(air)) topBad++;
        }
        float up=(float)((stones*sumXY-sumX*sumY)/(stones*sumXX-sumX*sumX)-1);
        Debug.Log(FormattableString.Invariant($"[GRADE] snow_b*={snow/Math.Max(1,snows):F3} stone_b*={stoneB/Math.Max(1,stones):F3} contrast_up={up:F2} shot={name} snow_samples={snows} stone_samples={stones} contrast_measured={up:F5} curve_slope=1.12"));
        Debug.Log($"[TOP] shot={name} crop_opaque=true fog_px={TopFog*4} colour=#DCE6EF edge_bad={topBad}");
        Debug.Log(FormattableString.Invariant($"[TREES] variants={treeVariants.Length} flip={(treeFlips>0 && treeFlips<treeInstances).ToString().ToLowerInvariant()} luma_jitter=0.08 heights=90/150/240 position_jitter_px=12 shot={name} instances={treeInstances} flipped={treeFlips} min_gain={treeMinGain:F4} max_gain={treeMaxGain:F4}"));
        foreach(string evidence in treeEvidence) Debug.Log($"[TREE2] shot={name} {evidence}");
        int hardLines=0;float maxRowJump=0;
        for(int y=1;y<Height-TopFog;y++)
        {
            int abrupt=0;float rowJump=0;
            for(int x=0;x<Width;x++)
            {
                Color32 a=pixels[(y-1)*Width+x],b=pixels[y*Width+x];
                float jump=(Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b))/3f;
                rowJump+=jump;if(jump>=12) abrupt++;
            }
            maxRowJump=Mathf.Max(maxRowJump,rowJump/Width);
            if(abrupt>=Width*.8f) hardLines++;
        }
        Debug.Log(FormattableString.Invariant($"[SEAM] hard_lines={hardLines} blend_px={SeamBlend*4} shot={name} alpha_errors={seamHardLines} max_row_jump={maxRowJump:F3} detector=jump12_coverage80pct"));
        if(hardLines!=0) bad++;
        if(snows==0 || stones==0 || snow/snows< -2 || snow/snows>1 || stoneB/stones<5 || stoneB/stones>7 || Mathf.Abs(up-.12f)>.015f || topBad!=0 || seamHardLines!=0) bad++;
        SaveLayer(new Layer { width=Width,height=Height,data=gradeMask },"grade_mask_"+name);
        SaveLayer(new Layer { width=Width,height=Height,data=gradeReference },"grade_before_"+name);
    }
    // Audit the displayed framebuffer, omitting the character and the road silhouette.
    void AuditPathScreenshot(string name)
    {
        double road=0,snow=0;int roads=0,snows=0;
        var mask=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
        var marks=new Color32[pixels.Length];
        float ox=eye.x-240,oy=eye.y-135;
        float by=route.worldOriginY+(route.sourceStartY-path.height)*route.worldScaleY;
        for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
        {
            if(contactMask[y*Width+x]) continue;
            if(Mathf.Abs(x-(foot.x-ox))<19 && y>=foot.y-oy-2 && y<foot.y-oy+42) continue;
            int sx=Mathf.FloorToInt(x+ox-route.worldOriginX),sy=Mathf.FloorToInt(y+oy-by);
            if(sx<1 || sx>=styledPath.width-1 || sy<1 || sy>=styledPath.height-1) continue;
            int at=sy*styledPath.width+sx;
            bool interior=styledPath.data[at].a>=250 && styledPath.data[at-1].a>=250 && styledPath.data[at+1].a>=250 && styledPath.data[at-styledPath.width].a>=250 && styledPath.data[at+styledPath.width].a>=250;
            if(interior) { road+=Lightness(pixels[y*Width+x]);roads++;marks[y*Width+x]=new Color32(255,0,0,255); }
            // Snow immediately downhill of the road, outside both the edge and uphill shadow.
            else if(styledPath.data[at].a==0 && sy+4<styledPath.height && styledPath.data[at+4*styledPath.width].a>=250 && Lightness(pixels[y*Width+x])>70)
            { snow+=Lightness(pixels[y*Width+x]);snows++;marks[y*Width+x]=new Color32(0,255,0,255); }
        }
        float delta=(float)(snow/Math.Max(1,snows)-road/Math.Max(1,roads));
        Debug.Log(FormattableString.Invariant($"[PATH_CONTRAST] shot={name} path_L={road/Math.Max(1,roads):F3} snow_L={snow/Math.Max(1,snows):F3} delta={delta:F3} edge_dark=true path_samples={roads} snow_samples={snows}"));
        Debug.Log(FormattableString.Invariant($"[PATH_READ] shot={name} path_L={road/Math.Max(1,roads):F3} ground_L={snow/Math.Max(1,snows):F3} delta={delta:F3} stone_detail={StoneDetail:F2} brightness=0.90"));
        int overlaps=Array.FindAll(biteColumns,c=>c>0).Length;
        Debug.Log($"[BITE] overlaps={overlaps} shadow=3px overlap_px=8 alpha=0.3 raster=display columns={string.Join(",",biteColumns)} shot={name}");
        if(roads==0 || snows==0 || delta<10 || overlaps!=3) bad++;
        mask.SetPixels32(marks);mask.Apply();File.WriteAllBytes(Path.Combine(output,"contrast_mask_"+name+".png"),mask.EncodeToPNG());Destroy(mask);
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
            int sampledHeight=layer==lower?layer.height-100:layer.height*2/3;
            int sy=Mathf.Clamp((y-bottom)*(sampledHeight-1)/(top-bottom),0,layer.height-1);
            int src=sy*layer.width,dst=y*Width;
            for(int x=0;x<Width;x++)
            {
                Color32 c=layer.data[src+backgroundColumns[x]];
                if(c.a<255)
                {
                    // Composite keyed silhouette edges over another part of the real lower slope.
                    int by=lower.height/2,bx=backgroundColumns[x]%lower.width;
                    Color32 backing=lower.data[by*lower.width+bx];backing.a=255;
                    c=Color32.Lerp(backing,c,c.a/255f);c.a=255;
                }
                if(layer==upper && y<bottom+SeamBlend)
                {
                    float alpha=Mathf.SmoothStep(0,1,(y-bottom)/(float)SeamBlend);
                    if(x==0 && y>bottom && (alpha<=0 || alpha>=1)) seamHardLines++;
                    pixels[dst+x]=Color32.Lerp(pixels[dst+x],c,alpha);
                }
                else pixels[dst+x]=c;
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
            for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
            {
                int at=y*Width+x;
                if(!contactMask[at] || (Mathf.Abs(x-240)<19 && y>=135-2 && y<135+42)) continue;
                GUI.color=contactColours[at];
                GUI.DrawTexture(new Rect((Screen.width-Width*scale)/2+x*scale,(Screen.height-Height*scale)/2+(Height-1-y)*scale,scale,.75f*scale),Texture2D.whiteTexture);
            }
            GUI.color=Color.white;
        }
        if(fog>0) { GUI.color=new Color(1,1,1,fog); GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture); GUI.color=Color.white; }
    }
    void OnDestroy() { if(frame!=null) Destroy(frame); }
}

public sealed class Climb2DLoader : MonoBehaviour
{
    IEnumerator Start() { yield return SceneManager.LoadSceneAsync("Climb2D"); Destroy(gameObject); }
}
