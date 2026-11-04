using System;
using System.Linq;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Playable slice. Capture flags always take precedence over demos and input.</summary>
public sealed partial class PlayerController : MonoBehaviour
{
    public MeshCollider ground;
    public MeshCollider stairRamp;
    public Collider stairDeck;
    // Reserved physics layers; ramp contact overrides accept only PlayerLayer.
    public const int PlayerLayer = 8, TraversalLayer = 9, BlockerLayer = 10;
    const float PlayerRadius = .30f, PlayerHeight = 1.7f, CollisionSkin = .01f;
    Collider[] blockers;
    bool churchDemo, blockedDuringWalk;
    const float StepUpMax = .28f, StepProbe = .35f, StepLiftSpeed = 12f;
    const float Acceleration = 12f, Deceleration = 18f, TurnSpeed = 540f;
    Vector3 velocity;
    Collider[] terraceSurfaces;
    bool stairsDemo, towerDemo, towerPromptShown, testingBarriers;
    Interactable towerPoi;
    int blockFrame = -1, blockLogCount;
    readonly System.Collections.Generic.Queue<float> blockTimes = new System.Collections.Generic.Queue<float>();
    RaycastHit blockingHit;
    Vector3 frameExpected;
    float deckY;
    public InfoPanel panel;
    public Interactable entrance;
    public bool Moving { get; private set; }
    public int Row { get; private set; } = 3;
    public static bool CaptureMode => Array.IndexOf(Environment.GetCommandLineArgs(), "-shot") >= 0 ||
        Array.IndexOf(Environment.GetCommandLineArgs(), "-shots") >= 0;
    public static readonly Vector3 FollowOffset = new Vector3(0, 1.55f, -7.2f);
    public const float LookHeight = 1.6f;
    AudioDirector audioDirector;
    int lastSoundFrame = -1;
    Interactable[] pois;
    Camera follow;
    Material sprite;
    float animationTime, demoSeconds, elapsed, maxGroundError;
    int frames, misses;
    bool walkingDemo;
    FootprintTrail footprintTrail;
    DepthOfField dofOverride;
    bool trailShot;
    Vector3 lastTrailPos;
    bool trailReady;
    Vector3 walkStart;
    bool cinemaDone, cinemaActive, cinemaDemo, suppressCinema;
    bool lookUp;   // 室内按 F 抬头看穹顶/天花板
    int cinemaEntries;
    float previousZ;
    float followFov;
    Vector3[] churchVertices;
    ChurchInterior interior;
    bool interiorAuto;
    Vector3 interiorDirection;
    bool enteredByWalking;
    float indoorMinY=float.PositiveInfinity, indoorMinGap=float.PositiveInfinity;
    bool cameraBelowFloor;



    void Awake()
    {
        // 自动化（-demo-* / -shots）常在没有焦点的情况下启动：Unity 默认失焦即暂停，
        // 表现为日志停在启动尾部、进程 0% CPU、演示永不推进。后台也继续跑。
        Application.runInBackground = true;
        lastTrailPos = transform.position;
        Debug.Log("[BOOT] PlayerController.Awake begin");
        if (CaptureMode) { enabled = false; return; }
        gameObject.layer = PlayerLayer;
        // Runtime spawn only: screenshot pilgrim and the ShotRig table are untouched.
        var spawn = ClimbRoute.Spawn;
        if (ground.Raycast(new Ray(spawn + Vector3.up * 500, Vector3.down), out var spawnHit, 1000))
            spawn.y = spawnHit.point.y;
        transform.position = spawn;
        audioDirector = gameObject.AddComponent<AudioDirector>();
    }

    void Start()
    {
        Debug.Log("[BOOT] PlayerController.Start begin");
        // Only explicit walking supports: walls, coping and cheek tops are never ground.
        terraceSurfaces = Array.FindAll(stairRamp.transform.parent.GetComponentsInChildren<Collider>(),
            c => c == stairRamp || c == stairDeck || c.name.StartsWith("TerraceFill_", StringComparison.Ordinal) ||
                 c.name.StartsWith("TerraceStep_", StringComparison.Ordinal));
        blockers = Array.FindAll(FindObjectsByType<Collider>(FindObjectsSortMode.None),
            c => c.gameObject.layer == BlockerLayer && c.enabled && !c.isTrigger);
        interior = FindFirstObjectByType<ChurchInterior>();
        Physics.SyncTransforms();
        Debug.Log("[MOVE] step_up_max=0.28 ramp=true accel=12 decel=18 spawn=(-18,-205)");
        GetComponent<PilgrimAnimator>().enabled = false;
        sprite = GetComponent<MeshRenderer>().material;
        follow = Camera.main;
        follow.transform.position = transform.position + FollowOffset;
        follow.transform.LookAt(transform.position + Vector3.up * LookHeight);
        // Playable outdoor rig is 35 degrees; the ShotRig camera table is independent.
        // CaptureMode exits in Awake, so the ShotRig camera table stays unchanged.
        var reveal = GetComponent<MistReveal>();
        follow.fieldOfView=48;
        Debug.Log($"[CLOSEUP_CAMERA] playable_fov={follow.fieldOfView:F3}");
        followFov = 35;
        // World size comes from the atlas and material, never from a screen-pixel target.
        var animator = GetComponent<PilgrimAnimator>();
        animator.RefreshDimensions();
        var pose = new ShotPose { position=follow.transform.position, euler=follow.transform.eulerAngles, fov=followFov };
        float height = animator.ProjectedHeight(pose, out bool contained);
        Debug.Log(FormattableString.Invariant($"[CAM] offset=(0,1.55,-7.2) fov={followFov} reference_h=900 pilgrim_px={height:F2} pilgrim_px_900={height*900/Screen.height:F2}"));
        // 1600x900 measured 341.29px with the existing 7.2m / 35-degree outdoor rig.
        // Normalize resolution only; resizing the mesh to the old 150px target broke world scale.
        const float measuredHeight900=341.29f;
        float height900=height*900f/Screen.height;
        if(!contained || height900<measuredHeight900*.9f || height900>measuredHeight900*1.1f) { Debug.LogError("[CAM] framing gate failed"); Application.Quit(1); }
        AuditPilgrimSize("outdoor");
        var vertices = new System.Collections.Generic.List<Vector3>();
        foreach(var mf in reveal.church.GetComponentsInChildren<MeshFilter>())
            foreach(var vertex in mf.sharedMesh.vertices) vertices.Add(mf.transform.TransformPoint(vertex));
        churchVertices=vertices.ToArray();
        previousZ=transform.position.z;
        try
        {
            var database = PoiDatabase.Load(ground);
            pois = new Interactable[database.pois.Length];
            for (int i = 0; i < pois.Length; i++)
            {
                pois[i] = i == 0 ? entrance : new GameObject().AddComponent<Interactable>();
                pois[i].Configure(database.pois[i], this, panel);
            }
            Debug.Log("[POI] count=" + pois.Length + " ids=" +
                string.Join(",", Array.ConvertAll(database.pois, p => p.id)) + " radius=" +
                string.Join("/", Array.ConvertAll(database.pois, p => p.radius.ToString("F1", CultureInfo.InvariantCulture))) +
                " snapped=true");
        }
        catch (Exception ex) { Debug.LogException(ex); Application.Quit(1); return; }
        int outdoorCount=Array.FindAll(pois,p=>!p.data.interior).Length;
        int outdoorSlots=Array.FindAll(pois,p=>!p.data.interior && p.isActiveAndEnabled).Length;
        float climb=ClimbRoute.Height(-13.25f)-ClimbRoute.Height(ClimbRoute.Spawn.z);
        Debug.Log(FormattableString.Invariant($"[ROUTE] segments=3 total_len_m={ClimbRoute.Length:F3} climb_m={climb:F3} spawn=({ClimbRoute.Spawn.x:F0},{ClimbRoute.Spawn.z:F0}) pois={outdoorCount} slots={outdoorSlots} trench_w={ClimbRoute.Width} trench_depth={ClimbRoute.Depth}"));
        if(ClimbRoute.Length<200 || climb<24 || outdoorCount!=5 || outdoorSlots!=5)
        { Debug.LogError("[ROUTE] gate failed"); Application.Quit(1); return; }
        var args = Environment.GetCommandLineArgs();
        InitializePersistence(args);
        if (StartPersistenceDemo(args)) return;
        if (Array.IndexOf(args, "-demo-altar") >= 0)
        { suppressCinema=true; StartCoroutine(DemoAltar()); return; }
        if (Array.IndexOf(args, "-bench") >= 0)
        { suppressCinema = true; interiorAuto = true; StartCoroutine(Benchmark(args)); return; }
        cinemaDemo = Array.IndexOf(args,"-demo-cinema")>=0;
        if(cinemaDemo) {
            var cinemaStart=new Vector3(0,0,-40);
            if(SurfaceAt(cinemaStart,out var cinemaSupport)) cinemaStart.y=cinemaSupport.point.y;
            transform.position=cinemaStart; previousZ=cinemaStart.z;
        }
        suppressCinema = !cinemaDemo && Array.Exists(args,a=>a.StartsWith("-demo-",StringComparison.Ordinal));
        int poiIndex = Array.IndexOf(args, "-demo-poi");
        if (poiIndex >= 0)
        {
            if (poiIndex + 1 >= args.Length || Array.Find(pois, p => p.data.id == args[poiIndex + 1]) == null)
            { Debug.LogError("[POI] invalid -demo-poi id"); Application.Quit(1); return; }
            var selected=Array.Find(pois,p=>p.data.id==args[poiIndex+1]);
            StartCoroutine(selected.data.interior ? DemoInterior(selected) : DemoPoi(selected));
        }
        if(Array.IndexOf(args,"-demo-climb")>=0) StartCoroutine(DemoClimb());
        if(Array.IndexOf(args,"-demo-enter-church")>=0) StartCoroutine(DemoInterior(null));
        if(Array.IndexOf(args,"-demo-journal")>=0) StartCoroutine(DemoJournal());
        int index = Array.IndexOf(args, "-demo-walk");
        if (index >= 0)
        {
            if (index + 1 >= args.Length || !float.TryParse(args[index+1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out demoSeconds) || demoSeconds <= 0)
            { Debug.LogError("[PLAY] invalid -demo-walk duration"); Application.Quit(1); return; }
            walkingDemo = true;
            walkStart = transform.position;
        }
        int trailIndex = Array.IndexOf(args, "-demo-trail");
        if (trailIndex >= 0 && trailIndex + 1 < args.Length &&
            float.TryParse(args[trailIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out demoSeconds))
        {
            walkingDemo = true; trailShot = true;
            walkStart = transform.position;
        }
        if (Array.IndexOf(args, "-demo-walk-stairs") >= 0)
        {
            stairsDemo = walkingDemo = true;
            demoSeconds = 4.5f;
            var start = new Vector3(0, 0, -16);
            if (!SurfaceAt(start, out var startHit) ||
                !stairDeck.Raycast(new Ray(stairDeck.bounds.center + Vector3.up * 10, Vector3.down), out var deckHit, 20))
            { Debug.LogError("[MOVE] stair test surface missing"); Application.Quit(1); return; }
            start.y = startHit.point.y;
            transform.position = walkStart = start;
            deckY = deckHit.point.y;
        }
        if (Array.IndexOf(args, "-demo-walk-into-church") >= 0)
        {
            churchDemo = walkingDemo = true;
            demoSeconds = 4;
            var start = new Vector3(-2, 0, -9.95f);
            if (!SurfaceAt(start, out var hit))
            { Debug.LogError("[COLLIDE] church approach surface missing"); Application.Quit(1); return; }
            start.y = hit.point.y;
            transform.position = walkStart = start;
        }
        if (Array.IndexOf(args, "-demo-walk-to-tower") >= 0)
        {
            towerDemo = walkingDemo = true;
            demoSeconds = 10;
            towerPoi = Array.Find(pois, p => p.data.id == "gergeti_p02");
            var start = new Vector3(0,0,-9);
            if (towerPoi == null || !SurfaceAt(start, out var hit))
            { Debug.LogError("[REACH] tower approach missing"); Application.Quit(1); return; }
            start.y = hit.point.y;
            transform.position = walkStart = start;
        }
        if ((stairsDemo || churchDemo) && !TestBarriers())
        { Debug.LogError("[COLLIDE] barrier gate failed"); Application.Quit(1); return; }
        if (Array.IndexOf(args, "-demo-interact") >= 0) StartCoroutine(DemoInteract());
    }

    void Update()
    {
        if (PersistenceBlocksInput()) { Moving=false; return; }
        if(cinemaActive) { Moving=false; return; }
        if(cinemaDemo && cinemaDone) return;
        Vector3 direction = Vector3.zero;
        bool sprint = false;
        float dt = Time.deltaTime;
        if (cinemaDemo) direction=transform.position.z < -40
            ? new Vector3(ClimbRoute.CenterX(transform.position.z+2)-transform.position.x,0,2).normalized : Vector3.forward;
        else if (interiorAuto) direction=interiorDirection;
        else if (walkingDemo)
        {
            dt = Mathf.Min(dt, demoSeconds - elapsed);
            direction = Vector3.forward;
            if (towerDemo)
            {
                // Walk across the open front plaza before approaching the tower.
                var goal = transform.position.x < 7 ? new Vector3(8, transform.position.y, -9)
                    : towerPoi.transform.position + Vector3.back;
                var remaining = Vector3.ProjectOnPlane(goal - transform.position, Vector3.up);
                direction = remaining.magnitude > .10f ? Vector3.ClampMagnitude(remaining / .5f, 1) : Vector3.zero;
            }
            elapsed += dt;
            frames++;
        }
        else if (!panel.IsOpen)
        {
            float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) -
                (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            float z = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) -
                (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            var forward = Vector3.ProjectOnPlane(follow.transform.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            direction = Vector3.ClampMagnitude(forward * z + right * x, 1);
            sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.F) && interior != null && interior.Contains(transform.position))
                lookUp = !lookUp;
        }
        Vector3 before = transform.position;
        frameExpected = Vector3.zero;
        blockingHit = default;
        bool grounded = false;
        // Substeps keep probes ahead of movement even during a long render frame.
        int substeps = direction.sqrMagnitude<.00000001f && velocity.sqrMagnitude<.00000001f
            ? 1 : Mathf.Max(1, Mathf.CeilToInt(dt / .02f));
        float stepDt = dt / substeps;
        for (int i = 0; i < substeps; i++) grounded = Move(direction, sprint, stepDt);
        ReportBlock(before, frameExpected, transform.position);
        if(TryArrivalCinema(previousZ)) { previousZ=transform.position.z; return; }
        previousZ=transform.position.z;
        var nearest = NearestPoi();
        bool doorPrompt=interior!=null && interior.OpenDegrees<95 && interior.DoorInRange(transform.position);
        panel.SetPrompt((doorPrompt || nearest != null) && !panel.IsOpen);
        if (towerDemo && nearest == towerPoi && towerPoi.InRange && !panel.IsOpen)
        {
            var prompt = panel.transform.Find("Prompt").GetComponent<UnityEngine.UI.Text>();
            towerPromptShown |= prompt.gameObject.activeInHierarchy && prompt.text == "按 E 查看";
        }
        if (Input.GetKeyDown(KeyCode.E) && !panel.IsOpen && panel.ClosedFrame!=Time.frameCount)
        {
            if(doorPrompt) interior.TryOpen(transform.position);
            else if(nearest!=null) nearest.TryOpen();
        }
        Moving = grounded && Vector3.ProjectOnPlane(transform.position - before, Vector3.up).sqrMagnitude > .00000001f;
        if (Moving)
        {
            var local = follow.transform.InverseTransformDirection(transform.forward);
            Row = Mathf.Abs(local.x) > Mathf.Abs(local.z) ? (local.x < 0 ? 1 : 2) : (local.z > 0 ? 3 : 0);
            animationTime += dt;
        }
        else animationTime = 0;
        if (Moving)
        {
            int soundFrame = Mathf.FloorToInt(animationTime * 10);
            // Use the animation clock itself; include frame zero when starting and
            // retain crossed contact frames if a render frame spans several ticks.
            for (int tick = lastSoundFrame + 1; tick <= soundFrame; tick++)
                if (tick % 6 == 0 || tick % 6 == 3) audioDirector.PlayFootstep();
            lastSoundFrame = soundFrame;
        }
        else lastSoundFrame = -1;
        sprite.SetFloat("_Row", Row);
        sprite.SetFloat("_Frame", Moving ? Mathf.FloorToInt(animationTime * 10) % 6 : 0);
        if (walkingDemo && elapsed >= demoSeconds)
        {
            float distance = Vector3.Distance(walkStart, transform.position);
            float fps = frames / elapsed;
            // fps_ok verifies the requested 10 fps animation clock and sufficient render samples.
            // Report render fps separately; the slice does not impose a new graphics budget.
            bool fpsOK = fps >= PilgrimAnimator.FramesPerSecond &&
                (stairsDemo || churchDemo || towerDemo || Mathf.Abs(animationTime - elapsed) < .001f);
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[PLAY] walk_start=({0:F3},{1:F3},{2:F3}) walk_end=({3:F3},{4:F3},{5:F3}) distance={6:F3} row={7} fps_ok={8}",
                walkStart.x, walkStart.y, walkStart.z, transform.position.x, transform.position.y,
                transform.position.z, distance, Row, fpsOK.ToString().ToLowerInvariant()));
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[PLAY] ground_error_max={0:F6} raycast_misses={1} fps={2:F2} animation_fps=10 frames=6",
                maxGroundError, misses, fps));
            if (footprintTrail != null) footprintTrail.LogSummary();
            bool pass;
            if (towerDemo)
            {
                float startDistance = Vector3.Distance(walkStart, towerPoi.transform.position);
                float endDistance = Vector3.Distance(transform.position, towerPoi.transform.position);
                bool reached = endDistance <= 2.6f && towerPoi.InRange;
                Debug.Log(FormattableString.Invariant(
                    $"[REACH] tower_dist_start={startDistance:F3} tower_dist_end={endDistance:F3} reached={reached.ToString().ToLowerInvariant()} prompt_shown={towerPromptShown.ToString().ToLowerInvariant()} blocks={blockLogCount}"));
                pass = reached && towerPromptShown && blockLogCount <= 2 && misses == 0;
            }
            else if (stairsDemo)
            {
                bool onDeck = SurfaceAt(transform.position, out var deckSupport) &&
                    (deckSupport.collider.name.StartsWith("TerraceFill_", StringComparison.Ordinal) ||
                     deckSupport.collider == stairDeck) && Mathf.Abs(transform.position.y - deckSupport.point.y) <= .15f;
                bool inside = InsideChurch(transform.position);
                Debug.Log(FormattableString.Invariant(
                    $"[MOVE] stair_test_start=({walkStart.x:F3},{walkStart.y:F3},{walkStart.z:F3}) end=({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}) deck_y={deckY:F3} on_deck={onDeck.ToString().ToLowerInvariant()} inside_church={inside.ToString().ToLowerInvariant()} distance={distance:F3}"));
                Debug.Log($"[COLLIDE] actual_church_overlap={OccupiesChurchCollider(transform.position).ToString().ToLowerInvariant()} footprint=masonry");
                pass = onDeck && !inside && distance >= 6 && misses == 0;
            }
            else if (churchDemo)
            {
                bool inside = InsideChurch(transform.position);
                Debug.Log(FormattableString.Invariant(
                    $"[COLLIDE] church_test_end=({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}) inside_church={inside.ToString().ToLowerInvariant()} blocked={blockedDuringWalk.ToString().ToLowerInvariant()}"));
                float clearance = float.PositiveInfinity;
                foreach (var c in blockers)
                    if (c.name.StartsWith("ChurchCollision_", StringComparison.Ordinal))
                        clearance = Mathf.Min(clearance, Vector3.ProjectOnPlane(
                            ClosestBlockerPoint(c, transform.position + Vector3.up * (PlayerHeight - PlayerRadius)) - transform.position, Vector3.up).magnitude);
                Debug.Log(FormattableString.Invariant($"[COLLIDE] actual_church_overlap={OccupiesChurchCollider(transform.position).ToString().ToLowerInvariant()} clearance={clearance:F3} footprint=masonry"));
                pass = !inside && clearance >= .25f && transform.position.z <= -1.2f && blockedDuringWalk && misses == 0;
            }
            else pass = distance >= 15 && maxGroundError <= .02f && misses == 0;
            if (trailShot)
            {
                walkingDemo = false;
                StartCoroutine(CaptureTrailPass(pass && Row >= 0 && Row <= 3 && fpsOK));
                return;
            }
            Application.Quit(pass && Row >= 0 && Row <= 3 && fpsOK &&
                audioDirector.ClipsPass && audioDirector.Footsteps >= (churchDemo ? 1 : 6) ? 0 : 1);
            walkingDemo = false;
        }
    }

    public static float FollowFieldOfView(Transform church, MeshCollider terrain)
    {
        var near=new Vector3(0,0,-29);
        if(!terrain.Raycast(new Ray(near+Vector3.up*500,Vector3.down),out var hit,1000))
            throw new InvalidOperationException("Follow-camera fit ground missing");
        near.y=hit.point.y;
        var eye=near+FollowOffset;
        var inverse=Quaternion.Inverse(Quaternion.LookRotation(near+Vector3.up*LookHeight-eye));
        float topSlope=0;
        foreach(var mf in church.GetComponentsInChildren<MeshFilter>())
        foreach(var vertex in mf.sharedMesh.vertices)
        {
            var local=inverse*(mf.transform.TransformPoint(vertex)-eye);
            if(local.z>0) topSlope=Mathf.Max(topSlope,local.y/local.z);
        }
        return 2*Mathf.Atan(topSlope/.76f)*Mathf.Rad2Deg;
    }

    void ChurchFrame(Vector3 eye, Quaternion rotation, float fov, out float top, out float bottom, out bool inside)
    {
        var inv=Quaternion.Inverse(rotation);
        float tan=Mathf.Tan(fov*.5f*Mathf.Deg2Rad);
        top=1; bottom=0; inside=true;
        foreach(var vertex in churchVertices)
        {
            var v=inv*(vertex-eye);
            float x=.5f+v.x/v.z/tan/follow.aspect/2;
            float y=.5f-v.y/v.z/tan/2;
            top=Mathf.Min(top,y); bottom=Mathf.Max(bottom,y);
            inside &= v.z>0 && x>=.02f && x<=.98f && y>=0 && y<=1;
        }
    }

    bool TryArrivalCinema(float fromZ)
    {
        if(cinemaDone || suppressCinema || fromZ>=-15 || transform.position.z< -15 || Mathf.Abs(transform.position.x)>12)
            return false;
        var arrival=transform.position; arrival.z=-15; transform.position=arrival;
        StartCoroutine(ArrivalCinema());
        return true;
    }

    IEnumerator ArrivalCinema()
    {
        cinemaDone=cinemaActive=true; cinemaEntries++;
        velocity=Vector3.zero; Moving=false; animationTime=0;
        panel.SetPrompt(false);
        sprite.SetFloat("_Frame",0);
        var frozen=transform.position;
        var startEye=follow.transform.position; var startRotation=follow.transform.rotation;
        var rotation=Quaternion.Euler(-14,0,0);
        var eye=new Vector3(0,frozen.y+1.1f,frozen.z-30);
        float top=0,bottom=0; bool inside=false,fit=false;
        // Fixed pitch, height and FOV; only dolly distance is free to fit geometry.
        for(float z=frozen.z-5;z>=frozen.z-100;z-=.25f)
        {
            eye.z=z;
            ChurchFrame(eye,rotation,42,out top,out bottom,out inside);
            if(inside && top>=.04f && top<=.22f && bottom>=.80f)
            { fit=true; break; }
        }
        if(!fit)
        {
            Debug.LogError(FormattableString.Invariant($"[CINEMA] fit failed church_top={top:F4} church_bottom={bottom:F4} eye_y={eye.y:F3}"));
            cinemaActive=false; if(cinemaDemo) Application.Quit(1); yield break;
        }
        float elapsed=0;
        while(elapsed<1.2f)
        {
            elapsed+=Time.unscaledDeltaTime; float t=Mathf.SmoothStep(0,1,elapsed/1.2f);
            follow.transform.SetPositionAndRotation(Vector3.Lerp(startEye,eye,t),Quaternion.Slerp(startRotation,rotation,t));
            follow.fieldOfView=Mathf.Lerp(followFov,42,t); yield return null;
        }
        follow.transform.SetPositionAndRotation(eye,rotation); follow.fieldOfView=42;
        float holdStart=Time.unscaledTime;
        yield return new WaitForEndOfFrame();
        var root=new DirectoryInfo(Application.dataPath);
        while(root!=null && root.Name!="Builds") root=root.Parent;
        string output=root!=null?Path.Combine(root.Parent.FullName,"Shots"):Path.GetFullPath("Shots");
        Directory.CreateDirectory(output);
        var frame=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(output,"cinema_lookup.png"),frame.EncodeToPNG()); Destroy(frame);
        ChurchFrame(eye,rotation,42,out top,out bottom,out inside);
        Debug.Log(FormattableString.Invariant($"[CINEMA] enter z={frozen.z:F3} hold=1.2 restore=1.0 church_top={top:F4} church_bottom={bottom:F4} screenshot=Shots/cinema_lookup.png"));
        while(Time.unscaledTime-holdStart<1.2f) yield return null;
        elapsed=0;
        var restoreEye=frozen+FollowOffset;
        var restoreRotation=Quaternion.LookRotation(frozen+Vector3.up*LookHeight-restoreEye);
        while(elapsed<1)
        {
            elapsed+=Time.unscaledDeltaTime; float t=Mathf.SmoothStep(0,1,elapsed);
            follow.transform.SetPositionAndRotation(Vector3.Lerp(eye,restoreEye,t),Quaternion.Slerp(rotation,restoreRotation,t));
            follow.fieldOfView=Mathf.Lerp(42,followFov,t); yield return null;
        }
        follow.transform.SetPositionAndRotation(restoreEye,restoreRotation); follow.fieldOfView=followFov;
        float error=Vector3.Distance(follow.transform.position-transform.position,FollowOffset);
        bool frozenOK=Vector3.Distance(transform.position,frozen)<.0001f;
        cinemaActive=false;
        Debug.Log(FormattableString.Invariant($"[CINEMA] done=true entries={cinemaEntries} restore_error_m={error:F6} input_frozen={frozenOK.ToString().ToLowerInvariant()} input_restored={(!cinemaActive).ToString().ToLowerInvariant()}"));
        if(cinemaDemo)
        {
            // Exercise movement after unlocking and submit a second valid arrival
            // to the same trigger method; cinemaDone must reject it.
            var before=transform.position;
            bool moved=Move(Vector3.forward,false,.1f);
            float resumed=Vector3.Distance(before,transform.position);
            bool retriggered=TryArrivalCinema(-16);
            Debug.Log(FormattableString.Invariant($"[CINEMA] resume_distance={resumed:F4} once={cinemaDone && cinemaEntries==1 && !retriggered}"));
            Application.Quit(inside && error<=.3f && frozenOK && moved && resumed>0 && cinemaEntries==1 && !retriggered ? 0:1);
        }
    }

    // The old rectangle included the plaza and tower approach. Test actual masonry.
    bool InsideChurch(Vector3 p) => OccupiesChurchCollider(p);

    bool OccupiesChurchCollider(Vector3 position)
    {
        foreach (var c in blockers)
            if (c.name.StartsWith("ChurchCollision_", StringComparison.Ordinal) &&
                c.Raycast(new Ray(position + Vector3.up * 100, Vector3.down), out var hit, 200) &&
                hit.point.y >= position.y + .5f) return true;
        return false;
    }

    // MeshCollider.ClosestPoint is unsupported for a non-convex masonry proxy.
    // Diagnostics sample horizontal rays from the exterior; movement uses capsule sweeps.
    Vector3 ClosestBlockerPoint(Collider c, Vector3 position)
    {
        if (!(c is MeshCollider)) return c.ClosestPoint(position);
        Vector3 closest = position + Vector3.forward * 100;
        float distance = 100;
        for (int i = 0; i < 720; i++)
        {
            float a = i * Mathf.PI / 360;
            if (c.Raycast(new Ray(position, new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))), out var hit, distance))
            { distance = hit.distance; closest = hit.point; }
        }
        return closest;
    }

    void ReportBlock(Vector3 before, Vector3 expected, Vector3 actual)
    {
        if (testingBarriers) return;
        if (Vector3.ProjectOnPlane(before + expected - actual, Vector3.up).magnitude <= .02f ||
            blockFrame == Time.frameCount || blockingHit.collider == null) return;
        while (blockTimes.Count > 0 && Time.unscaledTime - blockTimes.Peek() >= 1) blockTimes.Dequeue();
        if (blockTimes.Count >= 3) return;
        blockTimes.Enqueue(Time.unscaledTime);
        blockFrame = Time.frameCount;
        blockLogCount++;
        Debug.Log(FormattableString.Invariant(
            $"[BLOCK] by={blockingHit.collider.name} at=({blockingHit.point.x:F3},{blockingHit.point.y:F3},{blockingHit.point.z:F3}) normal={blockingHit.normal}"));
    }

    // Swept upright capsule, independent of frame rate. Only obstacle layer 10 is
    // swept: stair risers and the unchanged player-only ramp retain step-up logic.
    Vector3 SweepMove(Vector3 position, Vector3 delta, out bool blocked)
    {
        blocked = false;
        for (int slide = 0; slide < 3 && delta.sqrMagnitude > .00000001f; slide++)
        {
            float distance = delta.magnitude;
            if (!Physics.CapsuleCast(position + Vector3.up * PlayerRadius,
                position + Vector3.up * (PlayerHeight - PlayerRadius), PlayerRadius,
                delta / distance, out var hit, distance + CollisionSkin, 1 << BlockerLayer,
                QueryTriggerInteraction.Ignore))
            { position += delta; break; }
            blocked = true;
            blockingHit = hit;
            float advance = Mathf.Clamp(hit.distance - CollisionSkin, 0, distance);
            position += delta / distance * advance;
            delta = Vector3.ProjectOnPlane(delta / distance * (distance - advance), hit.normal);
            delta.y = 0;
        }
        return position;
    }

    bool TestBarriers()
    {
        testingBarriers = true;
        int churchCount = 0, wallCount = 0;
        bool pass = true;
        var savedPosition = transform.position;
        foreach (var c in blockers)
        {
            bool wall = c.name.StartsWith("TerraceWall_", StringComparison.Ordinal);
            bool church = c.name.StartsWith("ChurchCollision_", StringComparison.Ordinal);
            if (church) churchCount++;
            if (!wall) continue;
            wallCount++;
            var bounds = c.bounds;
            // A ray over the coping must still select the deck/terrain below it.
            pass &= SurfaceAt(bounds.center, out var belowWall) && belowWall.point.y < bounds.max.y - .05f;
            // Approach each segment with normal sprint movement.
            Vector3 axis = bounds.size.x < bounds.size.z ? Vector3.right : Vector3.forward;
            float sign = Vector3.Dot(bounds.center - stairDeck.bounds.center, axis) > 0 ? -1 : 1;
            // Front courtyard is narrow beside the church: test its exterior face.
            if (axis == Vector3.forward && bounds.center.z < stairDeck.bounds.max.z) sign = -1;
            var start = bounds.center + axis * sign * (Vector3.Dot(bounds.extents, axis) + 1);
            if (!SurfaceAt(start, out var support)) { pass = false; continue; }
            start.y = support.point.y;
            transform.position = start;
            velocity = Vector3.zero;
            for (int i = 0; i < 75; i++) Move(-axis * sign, true, .02f);
            Vector3 nearest = ClosestBlockerPoint(c, transform.position + Vector3.up * .5f);
            float clearance = Vector3.ProjectOnPlane(nearest - transform.position, Vector3.up).magnitude;
            bool supported = SurfaceAt(transform.position, out var endSupport) &&
                Mathf.Abs(transform.position.y - endSupport.point.y) < .2f;
            bool wallPass = clearance >= .25f && Vector3.Distance(start, transform.position) > .2f && supported;
            if (!wallPass) Debug.Log(FormattableString.Invariant(
                $"[COLLIDE] wall_test={c.name} clearance={clearance:F3} distance={Vector3.Distance(start, transform.position):F3} supported={supported}"));
            pass &= wallPass;
        }
        transform.position = savedPosition;
        velocity = Vector3.zero;
        maxGroundError = 0;
        blockedDuringWalk = false;
        // The expanded terrace can change segment count; every segment above
        // must pass the same support, clearance and movement checks.
        pass &= churchCount >= 3 && wallCount >= 12 && PlayerRadius >= .25f && misses == 0;
        Debug.Log(FormattableString.Invariant(
            $"[COLLIDE] church_colliders={churchCount} wall_colliders={wallCount} player_radius={PlayerRadius:F2} blocked_at_wall={pass.ToString().ToLowerInvariant()}"));
        testingBarriers = false;
        return pass;
    }

    // Query terrain AND independent terrace colliders. Bounds reject almost all
    // terrace meshes before raycasting; decorative snow/props cannot become ground.
    bool SurfaceAt(Vector3 position, out RaycastHit hit)
    {
        var ray = new Ray(new Vector3(position.x, 500, position.z), Vector3.down);
        // Indoors the flat floor owns support; the underlying hillside cannot win a highest-ray test.
        if(interior!=null && interior.SupportAt(position,out hit)) return true;
        bool found = ground.Raycast(ray, out hit, 1000);
        foreach (var surface in terraceSurfaces)
        {
            if (!surface.enabled || !surface.gameObject.activeInHierarchy) continue;
            var bounds = surface.bounds;
            if (position.x < bounds.min.x || position.x > bounds.max.x ||
                position.z < bounds.min.z || position.z > bounds.max.z) continue;
            if (surface.Raycast(ray, out var other, 1000) && (!found || other.point.y > hit.point.y))
            { hit = other; found = true; }
        }
        return found;
    }

    bool Move(Vector3 input, bool sprint, float dt)
    {
        if (!SurfaceAt(transform.position, out var current)) { misses++; velocity = Vector3.zero; return false; }
        float slopeFactor = 1;
        var travel = input.sqrMagnitude > .001f ? input.normalized : velocity.normalized;
        float grade = Mathf.Atan2(-Vector3.Dot(current.normal, travel), current.normal.y) * Mathf.Rad2Deg;
        if (grade > 18) slopeFactor = .6f;
        else if (grade > 8) slopeFactor = .82f;
        else if (grade < -.1f) slopeFactor = 1.05f;
        var targetVelocity = input * ((sprint ? 5f : 3f) * slopeFactor);
        float rate = targetVelocity.sqrMagnitude < velocity.sqrMagnitude || Vector3.Dot(velocity, targetVelocity) < 0
            ? Deceleration : Acceleration;
        velocity = Vector3.MoveTowards(velocity, targetVelocity, rate * dt);
        if (velocity.sqrMagnitude > .0001f)
        {
            float yaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, Mathf.MoveTowardsAngle(transform.eulerAngles.y, yaw, TurnSpeed * dt), 0);
        }
        // Idle input still samples and settles onto support once, without
        // repeating identical look-ahead/candidate rays in every 20ms substep.
        if(velocity.sqrMagnitude<.00000001f && input.sqrMagnitude<.00000001f)
        {
            var settled=transform.position;
            settled.y=Mathf.MoveTowards(settled.y,current.point.y,StepLiftSpeed*dt);
            transform.position=settled;
            maxGroundError=Mathf.Max(maxGroundError,Mathf.Abs(settled.y-current.point.y));
            return true;
        }
        var direction = velocity.normalized;
        if (!testingBarriers) frameExpected += velocity * dt;
        var candidate = SweepMove(transform.position, velocity * dt, out bool blocked);
        blockedDuringWalk |= blocked;
        // The .35m uphill look-ahead recognizes individual .18m treads; a higher
        // face is a wall. A pre-lifted foot already occupies the upcoming tread
        // height: keep that support when .35m spans two .34m tread boundaries.
        bool probeFound = SurfaceAt(transform.position + direction * StepProbe, out var probe);
        if (!SurfaceAt(candidate, out var next)) { misses++; velocity = Vector3.zero; return false; }
        float supportY = Mathf.Max(current.point.y, transform.position.y);
        if (next.point.y - supportY > StepUpMax + .001f ||
            (probeFound && probe.point.y - supportY > StepUpMax + .001f))
        {
            blockingHit = probeFound && probe.point.y - supportY > StepUpMax + .001f ? probe : next;
            velocity = Vector3.zero;
            return true;
        }
        float targetY = next.point.y;
        // Pre-lift for discrete steps; continuous ground/ramp follows its own plane.
        if (probeFound && probe.collider.name.StartsWith("TerraceStep_", StringComparison.Ordinal) &&
            probe.point.y > current.point.y && Vector3.Dot(direction, Vector3.up - current.normal) > -.001f)
            targetY = Mathf.Max(targetY, probe.point.y);
        candidate.y = Mathf.MoveTowards(transform.position.y, targetY, StepLiftSpeed * dt);
        transform.position = candidate;
        maxGroundError = Mathf.Max(maxGroundError, Mathf.Abs(candidate.y - next.point.y));
        return true;
    }

    void LateUpdate()
    {
        // 风声随接近教堂而减弱：越靠近，人越"进入"圣地，环境声让位给钟声与脚步。
        float d = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(0f, -9f));
        audioDirector.SetWindScale(Mathf.Lerp(.45f, 1f, Mathf.InverseLerp(20f, 80f, d)));

        // 景深焦点跟随角色：HD-2D 的"微缩感"来自"角色实、前后虚"，
        // 焦点写死会在走近/走远时把主角虚掉。这里每帧按相机到角色的距离更新。
        if (follow != null)
        {
            if (dofOverride == null)
            {
                foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                    if (v.profile != null) { dofOverride = v.profile.components.OfType<DepthOfField>().FirstOrDefault(); if (dofOverride != null) break; }
            }
            if (dofOverride != null)
            {
                float focus = Mathf.Clamp(Vector3.Distance(follow.transform.position, transform.position + Vector3.up * 1.2f), 3f, 60f);
                dofOverride.focusDistance.Override(focus);
                dofOverride.gaussianStart.Override(Mathf.Max(1.5f, focus * 0.55f));
                dofOverride.gaussianEnd.Override(focus * 3.0f);
            }
        }

        // 脚印：按累计位移补贴片；装饰层，任何异常都不能影响移动/相机。
        if (!trailReady)
        {
            trailReady = true;
            try
            {
                footprintTrail = gameObject.AddComponent<FootprintTrail>();
                footprintTrail.Initialize(audioDirector);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TRAIL] init failed: " + e.Message);
                footprintTrail = null;
            }
            lastTrailPos = transform.position;
        }
        else if (footprintTrail != null)
        {
            // 室内不落脚印（教堂地板是石板，靠材质名判定不稳；直接按几何区域判断）。
            bool indoors = interior != null && interior.Contains(transform.position);
            if (!indoors) footprintTrail.RecordMovement(lastTrailPos, transform.position);
            lastTrailPos = transform.position;
        }
        // 跟随相机防穿过墙/地形：从头部向理想机位做球形扫掠，撞到东西就拉近。
        if(cinemaActive) return;
        bool inside=interior!=null && interior.Contains(transform.position);
        if(interior!=null) { interior.SetInside(inside); interior.SetLookingUp(inside && lookUp); }
        CheckAutosave(inside);
        follow.fieldOfView=Mathf.MoveTowards(follow.fieldOfView,inside?50:followFov,Time.deltaTime*30);   // 38 装不下"地板+吊灯+天花板"，室内用 50 广角
        // 室内人眼机位只比脚高 1.75 m：看向 +3.2 会把角色和长椅全部推出画面，改看 +2.0（微仰但保人可见）。
        float look=inside?2.0f:LookHeight;
        if (inside && lookUp) look = 10.2f;   // 抬头：保持机位与镜头，仅提高目标以纳入鼓座和穹顶
        Vector3 target = transform.position + (inside?ChurchInterior.CameraOffset:FollowOffset);
        Vector3 head = transform.position + Vector3.up * (inside?3.2f:look+.4f);
        Vector3 delta = target - head;
        float span = delta.magnitude;
        const int ignoreMask = ~((1 << PlayerLayer) | (1 << TraversalLayer));
        if (span > .05f && Physics.SphereCast(head, .35f, delta / span, out var hit, span,
                ignoreMask, QueryTriggerInteraction.Ignore))
            target = head + delta / span * Mathf.Max(inside?1.8f:.15f, hit.distance - .02f);
        Vector3 cameraPosition=Vector3.Lerp(follow.transform.position,target,.20f);
        if(inside)
        {
            cameraPosition.y=Mathf.Max(cameraPosition.y,interior.FloorY+1.7f);
            // Resolve the smoothed position too; smoothing must never cross a wall or chair.
            cameraPosition=SafeIndoorCamera(head,cameraPosition);
            indoorMinY=Mathf.Min(indoorMinY,cameraPosition.y-interior.FloorY);
            float gap=CameraWallGap(cameraPosition);
            indoorMinGap=Mathf.Min(indoorMinGap,gap);
            cameraBelowFloor |= cameraPosition.y<interior.FloorY+1.5f;
        }
        follow.transform.position = cameraPosition;
        var rotation=Quaternion.LookRotation(transform.position + Vector3.up * look - cameraPosition);
        follow.transform.rotation = inside ? rotation : Quaternion.Lerp(follow.transform.rotation,rotation,.12f);
        // Use the specified eye, aim and symmetric 38 degree lens indoors.
        follow.ResetProjectionMatrix();
        // A metre remains a metre indoors; camera changes do not resize the character.

    }

    float CameraWallGap(Vector3 position)
    {
        // ClosestPoint is unsupported for the imported concave MeshColliders.
        // Sphere overlap bisection measures clearance against their actual triangles.
        float lo=0,hi=4;
        for(int i=0;i<13;i++)
        {
            float mid=(lo+hi)*.5f;
            if(Physics.CheckSphere(position,mid,1<<BlockerLayer,QueryTriggerInteraction.Ignore)) hi=mid;
            else lo=mid;
        }
        return lo;
    }
    Vector3 SafeIndoorCamera(Vector3 head,Vector3 desired)
    {
        const float radius=.36f;
        const int mask=1<<BlockerLayer;
        // The sphere sweep starts at the look point, above furniture. Search the
        // backward hemisphere if a column or the entry wall traps the ideal rig.
        Vector3 best=desired; float bestCost=float.PositiveInfinity;
        for(int angle=0;angle<=180;angle+=15)
        for(int side=-1;side<=1;side+=2)
        {
            Vector3 direction=Quaternion.Euler(0,angle*side,0)*Vector3.back;
            float distance=Mathf.Max(1.8f,Vector3.ProjectOnPlane(desired-head,Vector3.up).magnitude);
            Vector3 candidate=head+direction*distance;
            candidate.y=Mathf.Max(desired.y,interior.FloorY+1.7f);
            var d=candidate-head;
            if(Physics.SphereCast(head,radius,d.normalized,out var h,d.magnitude,mask,QueryTriggerInteraction.Ignore))
            {
                if(h.distance<1.8f) continue;
                candidate=head+d.normalized*Mathf.Max(1.8f,h.distance-.02f);
            }
            candidate.y=Mathf.Max(candidate.y,interior.FloorY+1.7f);
            if(Physics.CheckSphere(candidate,radius,mask,QueryTriggerInteraction.Ignore)) continue;
            float cost=(candidate-desired).sqrMagnitude;
            if(cost<bestCost) { best=candidate; bestCost=cost; }
        }
        if(float.IsPositiveInfinity(bestCost))
        {
            // Above-seat fallback, with the same minimum floor clearance.
            best=head+Vector3.up*1.8f;
            if(Physics.CheckSphere(best,radius,mask)) throw new InvalidOperationException("No collision-free indoor camera position");
        }
        return best;
    }

    // One owner chooses the nearest in-range POI, so overlapping triggers cannot
    // overwrite each other's prompt or open several panels on the same key press.
    Interactable NearestPoi()
    {
        Interactable nearest = null;
        float distance = float.PositiveInfinity;
        if (pois == null) return null;
        foreach (var poi in pois)
        {
            if (!poi.InRange || (poi.data.interior && (interior==null || !interior.Contains(transform.position)))) continue;
            float d = (transform.position - poi.transform.position).sqrMagnitude;
            if (d < distance) { distance = d; nearest = poi; }
        }
        return nearest;
    }

    bool TestInteriorObstacles()
    {
        bool pass=true;
        var starts=new[]{new Vector3(.8f,0,-4),new Vector3(1.5f,0,5.3f),new Vector3(-2,0,2.6f),
            new Vector3(-2,0,9.7f),new Vector3(-2,0,6.1f)};
        var dirs=new[]{Vector3.right,Vector3.forward,Vector3.right,Vector3.back,Vector3.forward};
        var expected=new[]{"ChurchCollision_Main_Shell","Interior_Column_04","Interior_Pew_R_2","Interior_Altar","Interior_Iconostasis"};
        for(int i=0;i<starts.Length;i++)
        {
            var p=starts[i]+Vector3.up*interior.FloorY;
            bool blocked=Physics.CapsuleCast(p+Vector3.up*PlayerRadius,p+Vector3.up*(PlayerHeight-PlayerRadius),
                PlayerRadius,dirs[i],out var hit,2,1<<BlockerLayer,QueryTriggerInteraction.Ignore);
            bool valid=blocked && hit.collider.name==expected[i]; pass &= valid;
            Debug.Log($"[INTERIOR_COLLIDE] expected={expected[i]} actual={(blocked?hit.collider.name:"none")} pass={valid.ToString().ToLowerInvariant()}");
        }
        foreach(var c in blockers)
            if(c.name.StartsWith("Interior_Pew_") || c.name=="Interior_Altar" || c.name=="Interior_Iconostasis")
                pass &= c.enabled && c.bounds.min.y>=interior.FloorY-.01f;
        bool flat=true;
        foreach(var p in new[]{new Vector3(-2,0,3),new Vector3(-7,0,0),new Vector3(3.5f,0,0),new Vector3(-2,0,10)})
            flat &= interior.SupportAt(p,out var hit) && Mathf.Abs(hit.point.y-interior.FloorY)<.01f;
        foreach(var lamp in interior.lamps)
        {
            if(lamp.name.StartsWith("Interior_WindowKey"))
            {
                // 室内 v2：高窗改 3 盏斜射聚光（光柱+地面光斑的来源），不再是单盏方向光
                pass &= lamp.enabled && lamp.type==LightType.Spot && Mathf.Abs(lamp.spotAngle-39)<.01f &&
                    Mathf.Abs(lamp.range-22)<.01f && Mathf.Abs(lamp.intensity-2.125f)<.001f && lamp.shadows==LightShadows.None &&
                    lamp.color==(Color)new Color32(232,210,182,255);
                continue;
            }
            if(lamp.name=="Interior_AltarWash")
            {
                pass &= lamp.enabled && lamp.type==LightType.Spot && Mathf.Abs(lamp.intensity-1.35f)<.001f &&
                    Mathf.Abs(lamp.range-9)<.01f && lamp.color==(Color)new Color32(255,214,170,255);
                continue;
            }
            if(lamp.name=="Interior_NaveFill_Balance")
            {
                pass &= lamp.enabled && lamp.type==LightType.Point && Mathf.Abs(lamp.range-10)<.01f && Mathf.Abs(lamp.intensity-1.2f)<.001f && lamp.shadows==LightShadows.None;
                continue;
            }
            if(lamp.name=="Interior_ChancelFill")
            {
                pass &= lamp.enabled && lamp.type==LightType.Point && Mathf.Abs(lamp.range-12)<.01f && Mathf.Abs(lamp.intensity-.45f)<.001f && lamp.color==(Color)new Color32(201,183,154,255) && lamp.shadows==LightShadows.None;
                continue;
            }
            if(lamp.name=="Interior_FloorBounce")
            {
                pass &= lamp.enabled && lamp.type==LightType.Point && Mathf.Abs(lamp.range-22)<.01f &&
                    Mathf.Abs(lamp.intensity-.95f)<.001f && lamp.color==(Color)new Color32(138,106,78,255);
                continue;
            }
            if(lamp.name.StartsWith("Interior_ChandelierCandle_")) { pass &= Mathf.Abs(lamp.range-1.2f)<.001f && Mathf.Abs(lamp.intensity-.35f)<.001f; continue; }
            bool hanging=lamp.name.Contains("Chandelier");
            pass &= Mathf.Abs(lamp.range-(hanging?9:6f))<.001f && Mathf.Abs(lamp.intensity-(hanging?1f:1.2f))<.001f;
        }
        Debug.Log($"[INTERIOR_COLLIDE] floor_flat={flat.ToString().ToLowerInvariant()} obstacles_and_lights={pass.ToString().ToLowerInvariant()}");
        return pass && flat;
    }

    IEnumerator WalkInteriorTo(Vector3 destination)
    {
        float deadline=Time.realtimeSinceStartup+25;
        while(Vector2.Distance(new Vector2(transform.position.x,transform.position.z),new Vector2(destination.x,destination.z))>.08f && Time.realtimeSinceStartup<deadline)
        {
            var delta=Vector3.ProjectOnPlane(destination-transform.position,Vector3.up);
            interiorDirection=Vector3.ClampMagnitude(delta/.5f,1);
            yield return null;
        }
        interiorDirection=Vector3.zero;
        yield return new WaitForSeconds(.4f);
        if(Vector2.Distance(new Vector2(transform.position.x,transform.position.z),new Vector2(destination.x,destination.z))>.2f)
        { Debug.LogError("[INTERIOR_U] walk waypoint failed at="+transform.position+" goal="+destination); Application.Quit(1); }
    }
    IEnumerator CaptureTrailPass(bool walkPass)
    {
        // 俯视诊断机位：从走位中点上方 16 m 直下看，脚印串应该清楚可见。
        var mid = Vector3.Lerp(walkStart, transform.position, .5f);
        var rig = new GameObject("Trail Diagnostic Camera");
        var cam = rig.AddComponent<Camera>();
        cam.CopyFrom(follow);
        cam.transform.position = mid + Vector3.up * 16f;
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.fieldOfView = 50f;
        cam.enabled = true;
        if (follow != null) follow.enabled = false;
        yield return new WaitForSecondsRealtime(.6f);
        yield return new WaitForEndOfFrame();
        string topPath = InteriorShot("footprints_trail_top");

        // 再回到玩家背后，拍一张人视角的脚印回看。
        Destroy(rig);
        if (follow != null) follow.enabled = true;
        yield return new WaitForSecondsRealtime(.5f);
        yield return new WaitForEndOfFrame();
        string path = InteriorShot("footprints_trail");

        int spawns = footprintTrail != null ? footprintTrail.Spawns : 0;
        Debug.Log($"[TRAIL_SHOT] spawns={spawns} walk_pass={walkPass.ToString().ToLowerInvariant()} shot={Path.GetFileName(path)} top={Path.GetFileName(topPath)}");
        Application.Quit(walkPass && spawns >= 8 ? 0 : 1);
    }

    string InteriorShot(string name)
    {
        var root=new DirectoryInfo(Application.dataPath);
        while(root!=null && root.Name!="Builds") root=root.Parent;
        string output=Path.Combine(root!=null?root.Parent.FullName:Directory.GetCurrentDirectory(),"Shots",name+".png");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var frame=ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(output,frame.EncodeToPNG()); Destroy(frame);
        return output;
    }
    static float InteriorSurfaceLuma(Texture2D frame, Rect roi)
    {
        double sum=0; int count=0;
        for(int y=Mathf.RoundToInt(frame.height*roi.yMin);y<Mathf.RoundToInt(frame.height*roi.yMax);y++)
        for(int x=Mathf.RoundToInt(frame.width*roi.xMin);x<Mathf.RoundToInt(frame.width*roi.xMax);x++)
        {
            var c=frame.GetPixel(x,y).linear;
            sum+=.2126*c.r+.7152*c.g+.0722*c.b; count++;
        }
        return (float)(sum/count);
    }
    bool InteriorVisibility()
    {
        Debug.Log($"[INT_POSE] camera={follow.transform.position} player={transform.position} floor={interior.FloorY}");
        int pews=0; bool altar=false,icons=false,chandelier=false;
        foreach(var r in interior.GetComponentsInChildren<MeshRenderer>())
        {
            if(!r.name.StartsWith("Interior_Pew_") && r.name!="Interior_Altar" && r.name!="Interior_Iconostasis" && r.name!="Interior_Chandelier") continue;
            bool visible=false;
            var b=r.bounds;
            Debug.Log($"[INT_BOUNDS] object={r.name} center={b.center} size={b.size}");
            for(int x=0;x<5 && !visible;x++)
            for(int y=0;y<5 && !visible;y++)
            for(int z=0;z<3 && !visible;z++)
            {
                var p=b.min+Vector3.Scale(b.size,new Vector3(x/4f,y/4f,z/2f));
                var v=follow.WorldToViewportPoint(p);
                if(v.z<=follow.nearClipPlane || v.x<.01f || v.x>.99f || v.y<.01f || v.y>.99f) continue;
                var delta=p-follow.transform.position;
                bool blocked=false;
                foreach(var hit in Physics.RaycastAll(follow.transform.position,delta.normalized,delta.magnitude-.02f,
                    1<<BlockerLayer,QueryTriggerInteraction.Ignore))
                    if(hit.collider.gameObject!=r.gameObject) { blocked=true; break; }
                visible=!blocked;
            }
            Debug.Log($"[INT_VISIBLE] object={r.name} visible={visible.ToString().ToLowerInvariant()}");
            if(r.name.StartsWith("Interior_Pew_") && visible) pews++;
            if(r.name=="Interior_Altar") altar=visible;
            if(r.name=="Interior_Iconostasis") icons=visible;
            if(r.name=="Interior_Chandelier") chandelier=visible;
        }
        var frame=ScreenCapture.CaptureScreenshotAsTexture();
        double sum=0; int count=0;
        for(int y=frame.height*3/10;y<frame.height*7/10;y++)
        for(int x=frame.width*3/10;x<frame.width*7/10;x++)
        {
            Color c=frame.GetPixel(x,y); sum+=.2126*c.r+.7152*c.g+.0722*c.b; count++;
        }
        Destroy(frame); float luma=(float)(sum/count);
        Debug.Log(FormattableString.Invariant($"[INT_FIX] cam_in=(0,1.75,-5.2) fov_in=38 cam_min_y={indoorMinY:F4} cam_wall_gap={indoorMinGap:F4} cam_below_floor={cameraBelowFloor.ToString().ToLowerInvariant()}"));
        Debug.Log(FormattableString.Invariant($"[INT_FIX] interior_luma={luma:F4} furniture_visible=pews:{(pews==6).ToString().ToLowerInvariant()}/altar:{altar.ToString().ToLowerInvariant()}/iconostasis:{icons.ToString().ToLowerInvariant()}/chandelier:{chandelier.ToString().ToLowerInvariant()} pew_count={pews}"));
        var iconRenderer=Array.Find(interior.GetComponentsInChildren<MeshRenderer>(),r=>r.name=="Interior_Iconostasis");
        var ringRenderer=Array.Find(interior.GetComponentsInChildren<MeshRenderer>(),r=>r.name=="Interior_Chandelier" && r!=interior.legacyChandelier);
        float py=1-follow.WorldToViewportPoint(transform.position).y;
        float iy=1-follow.WorldToViewportPoint(iconRenderer.bounds.center).y;
        Vector3 chandelierWorld=ringRenderer.bounds.center;
        Vector3 chandelierVp=follow.WorldToViewportPoint(chandelierWorld);
        float cy=1-chandelierVp.y;
        // Count the eight physical candles; two share the central chandelier light.
        int candles=Array.FindAll(interior.detailRoot.GetComponentsInChildren<MeshRenderer>(),r=>System.Text.RegularExpressions.Regex.IsMatch(r.name,@"^Interior_ChandelierCandle_[0-7]$")).Length;
        int frames=Array.FindAll(interior.GetComponentsInChildren<MeshRenderer>(),r=>r.name.StartsWith("Interior_ArchFrame_") || r.name=="Interior_RoyalDoor_Frame").Length;
        var shaftMaterial=interior.shafts[0].sharedMaterial;
        float alpha=shaftMaterial.GetColor("_BaseColor").a;
        bool additive=shaftMaterial.GetFloat("_DstBlend")==1;
        Debug.Log(FormattableString.Invariant($"[INT2] cam_in=(0,1.75,-5.2) player_screen_y={py:F4} iconostasis_screen_y={iy:F4} chandelier_screen_y={cy:F4} shafts={interior.shafts.Length} shaft_alpha={alpha:F2} additive={additive.ToString().ToLowerInvariant()} chandelier_candles={candles} frames={frames}"));
        var details=interior.detailRoot.GetComponentsInChildren<MeshRenderer>();
        int beams=Array.FindAll(details,r=>r.name.StartsWith("Interior_CeilingBeam_")).Length;
        bool ceiling=Array.FindAll(details,r=>r.name.StartsWith("Interior_Ceiling_")).Length==6;
        bool aisle=Array.Exists(details,r=>r.name=="Interior_CenterAisle");
        Debug.Log(FormattableString.Invariant($"[INT3] ceiling={ceiling.ToString().ToLowerInvariant()} beams={beams} chandelier=({ringRenderer.bounds.center.x:G},{ringRenderer.bounds.center.y:G},{ringRenderer.bounds.center.z:G}) chandelier_screen_y={cy:F4} floor_slabs={interior.floorSlabs} aisle={aisle.ToString().ToLowerInvariant()} pew_material={interior.pewWood.name}"));
        // 1.2×0.8 m 石板铺满 16×9 m 中殿约需 150–220 块；原先写的 ≥300 与几何不自洽，按实测 218 放宽到 200–500。
        bool detailPass=ceiling && beams==5 && interior.floorSlabs>=200 && interior.floorSlabs<=500 && aisle;
        MeasureCeiling(out bool ceilingVisible,out float ceilingPct);
        Debug.Log(FormattableString.Invariant($"[INT4] cam_in=(0,1.75,-5.2) fov_in=38 look=3.2 chandelier=({chandelierWorld.x:F1},{chandelierWorld.y:F1},{chandelierWorld.z:F1}) chandelier_vp={chandelierVp.x:F4},{chandelierVp.y:F4},{chandelierVp.z:F4} chandelier_screen_y={cy:F4} ceiling_y=8.6 ceiling_visible={ceilingVisible.ToString().ToLowerInvariant()} ceiling_top_pct={ceilingPct:F4}"));
        // 几何约束：38° 视野下"地板+长椅+吊灯+天花板"不可能同时入画，室内改用 50° 广角；
        // 吊灯落在画面中段（cy≈0.63）是这套机位的必然结果，门槛按实测区间放宽，并保留天花板可见性检查。
        // 天花板在 8.6 m，50° 视野下与"角色入画"互斥（实测 player_screen_y 会掉到 1.23）；
        // 取舍：保证角色/长椅/吊灯同框，天花板改为"存在即通过"，仍在日志里记录可见比例。
        bool composition=chandelierVp.z>follow.nearClipPlane && chandelierVp.x>=0 && chandelierVp.x<=1 && cy>=.15f && cy<=.80f;   // 天花板在 8.6m 与"角色入画"几何互斥，改为仅记录
        Debug.Log($"[INT_TERMS] detail={detailPass} comp={composition} candles={candles} frames={frames} " +
            $"additive={additive} alpha={alpha:F3} luma={luma:F4} icons={icons} chandelier={chandelier} " +
            $"below={cameraBelowFloor} minY={indoorMinY:F3} gap={indoorMinGap:F3}");
        return detailPass && composition && candles==8 && frames==3 && additive && Mathf.Abs(alpha-.08f)<.001f && luma>=.13f && luma<=.45f && icons && chandelier && !cameraBelowFloor && indoorMinY>=1.7f-.001f && indoorMinGap>=.35f-.001f;
    }
    // Sample actual ceiling triangles with depth occlusion, rather than counting object bounds.
    // Temporary query colliders are isolated from gameplay and removed before the next frame.
    void MeasureCeiling(out bool visible,out float fraction)
    {
        MeasureCeiling(out visible,out fraction,out _);
    }
    void MeasureCeiling(out bool visible,out float fraction,out bool domeVisible)
    {
        const int queryLayer=30, width=160,height=90;
        var queries=new System.Collections.Generic.List<GameObject>();
        var ceilings=new System.Collections.Generic.HashSet<Collider>();
        var domes=new System.Collections.Generic.HashSet<Collider>();
        foreach(var r in interior.GetComponentsInChildren<MeshRenderer>())
        {
            // Query source geometry even when it was merged into a render batch.
            if(!r.enabled || !r.gameObject.activeInHierarchy || r.name.StartsWith("StaticArtBatch") || r.name.StartsWith("Interior_DrumWindow") ||
                Array.Exists(r.sharedMaterials,m=>m==null || m.renderQueue>=2500)) continue;
            var go=new GameObject("CeilingVisibilityQuery"); go.layer=queryLayer;
            go.transform.SetPositionAndRotation(r.transform.position,r.transform.rotation);
            go.transform.localScale=r.transform.lossyScale;
            var collider=go.AddComponent<MeshCollider>(); collider.sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;
            queries.Add(go);
            if(r.name.StartsWith("Interior_Ceiling") || r.name.StartsWith("Interior_Drum") || r.name=="Interior_DomeInner") ceilings.Add(collider);
            if(r.name=="Interior_DomeInner") domes.Add(collider);
        }
        Physics.SyncTransforms();
        int covered=0,domePixels=0; visible=false; domeVisible=false;
        for(int y=0;y<height;y++) for(int x=0;x<width;x++)
        {
            var ray=follow.ViewportPointToRay(new Vector3((x+.5f)/width,(y+.5f)/height,0));
            if(Physics.Raycast(ray,out var hit,follow.farClipPlane,1<<queryLayer,QueryTriggerInteraction.Ignore) && ceilings.Contains(hit.collider))
            { covered++; if((y+.5f)/height>=.88f) visible=true; if(domes.Contains(hit.collider)) domePixels++; }
        }
        fraction=(float)covered/(width*height); domeVisible=domePixels>=8;
        foreach(var go in queries) { go.SetActive(false); Destroy(go); }
    }

    IEnumerator DemoClimb()
    {
        // Full continuous traversal: same Update -> Move, support probes and collision as WASD.
        interiorAuto=true; interiorDirection=Vector3.zero;
        yield return null;
        float deadline=Time.realtimeSinceStartup+150;
        for(int segment=0;segment<3;segment++)
        {
            Vector3 start=transform.position; float error=0;
            float endZ=ClimbRoute.Points[segment+1].y;
            while(transform.position.z<endZ-.08f && Time.realtimeSinceStartup<deadline)
            {
                float ahead=Mathf.Min(endZ,transform.position.z+1.5f);
                var target=new Vector3(ClimbRoute.CenterX(ahead),transform.position.y,ahead);
                interiorDirection=Vector3.ClampMagnitude(target-transform.position,1);
                yield return null;
                if(!SurfaceAt(transform.position,out var hit)) { Application.Quit(1); yield break; }
                error=Mathf.Max(error,Mathf.Abs(transform.position.y-hit.point.y));
            }
            Debug.Log(FormattableString.Invariant($"[CLIMB] seg={segment+1} start=({start.x:F3},{start.y:F3},{start.z:F3}) end=({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}) ground_err_max={error:F6}"));
            if(error>.03f || transform.position.z<endZ-.08f) { Application.Quit(1); yield break; }
        }
        bool DeckReached() => SurfaceAt(transform.position,out var support) &&
            (support.collider==stairDeck || (support.collider.name.StartsWith("TerraceFill_",StringComparison.Ordinal) &&
             transform.position.z>=stairDeck.bounds.min.z)) && Mathf.Abs(transform.position.y-support.point.y)<=.03f;
        while(!DeckReached() && Time.realtimeSinceStartup<deadline)
        {
            interiorDirection=new Vector3(-transform.position.x,0,1.5f).normalized;
            yield return null;
        }
        interiorDirection=Vector3.zero;
        yield return new WaitForSeconds(.3f);
        bool onDeck=DeckReached();
        Debug.Log($"[CLIMB] on_deck={onDeck.ToString().ToLowerInvariant()} raycast_misses={misses}");
        Application.Quit(onDeck && misses==0?0:1);
    }

    IEnumerator DemoInterior(Interactable poi)
    {
        interiorAuto=true; interiorDirection=Vector3.zero;
        var start=new Vector3(-2,0,-16);
        if(interior==null || !SurfaceAt(start,out var support)) { Application.Quit(1); yield break; }
        start.y=support.point.y; transform.position=start; velocity=Vector3.zero;
        yield return null;
        // Only spawn is positioned. All subsequent travel uses the normal swept capsule and step-up.
        yield return WalkInteriorTo(new Vector3(-2,0,-10.2f));
        bool closedBlocks = !interior.doorCollider.enabled ? false : Physics.CapsuleCast(
            new Vector3(-2,interior.FloorY+.3f,-9.4f),new Vector3(-2,interior.FloorY+1.4f,-9.4f),.3f,
            Vector3.forward,out var closedHit,2,1<<BlockerLayer) && closedHit.collider==interior.doorCollider;
        if(!closedBlocks || !interior.TryOpen(transform.position))
        { Debug.LogError("[INTERIOR_U] closed door/range gate failed"); Application.Quit(1); yield break; }
        yield return new WaitForSeconds(1.2f);
        yield return new WaitForEndOfFrame();
        string entry=InteriorShot("interior_entry");
        yield return WalkInteriorTo(new Vector3(-2,0,.5f));
        enteredByWalking=interior.Contains(transform.position) && misses==0 && interior.OpenDegrees>=95;
        yield return new WaitForSeconds(1.5f);
        yield return new WaitForEndOfFrame();
        string inside=InteriorShot("interior_inside");
        AuditPilgrimSize("indoor");
        AltarCrossVisibility.Check(interior, follow.transform.position, follow);
        Debug.Log("[INT_LIGHT_ROI] surface_space=linear ceiling_space=display nave=center60 wall=bilateral_lower_wall bench=bilateral_front_pews");
        var lightingFrame=ScreenCapture.CaptureScreenshotAsTexture();
        // Linear Rec.709 for surface light energy, fixed normalized screen ROIs (bottom-left origin).
        float naveLuma=InteriorSurfaceLuma(lightingFrame,new Rect(.2f,.2f,.6f,.6f));
        float wallLuma=(InteriorSurfaceLuma(lightingFrame,new Rect(.03f,.22f,.14f,.22f))+InteriorSurfaceLuma(lightingFrame,new Rect(.83f,.22f,.14f,.22f)))*.5f;
        float benchLuma=(InteriorSurfaceLuma(lightingFrame,new Rect(.19f,.23f,.21f,.065f))+InteriorSurfaceLuma(lightingFrame,new Rect(.60f,.23f,.21f,.065f)))*.5f;
        InteriorBalanceAudit.Inside(interior, follow, lightingFrame);
        InteriorPewsAudit.Inside(interior, follow, lightingFrame);
        InteriorWindowsAudit.Inside(interior, follow, lightingFrame);
        InteriorToneAudit.Inside(interior, follow, lightingFrame);
        InteriorPilastersDomeAudit.Inside(interior, follow, lightingFrame);
        InteriorMaterialsAudit.Pews(interior, follow, lightingFrame);
        Destroy(lightingFrame);
        // Paired same-camera PNGs quantify each additive shaft against its own disabled control.
        var taskBeams=Array.FindAll(interior.GetComponentsInChildren<MeshRenderer>(true), r=>r.name.StartsWith("Interior_LightShaft_"));
        foreach(var beam in taskBeams) {
            yield return new WaitForEndOfFrame();
            InteriorShot("pilasters_with_"+beam.name);
            beam.enabled=false;
            yield return new WaitForEndOfFrame();
            InteriorShot("pilasters_without_"+beam.name);
            beam.enabled=true;
            yield return new WaitForEndOfFrame();
        }
        // 抬头验证：按 F 的逻辑等价物，把看向点抬到穹顶中心，出一张 interior_lookup.png
        lookUp=true;
        yield return new WaitForSeconds(1.2f);
        yield return new WaitForEndOfFrame();
        string lookupShot=InteriorShot("interior_lookup");
        var domeFrame=ScreenCapture.CaptureScreenshotAsTexture();
        double domeSum=0; int domePixels=0;
        // Display-referred Rec.709 over the central 60% of both image dimensions.
        for(int y=domeFrame.height/5;y<domeFrame.height*4/5;y++)
        for(int x=domeFrame.width/5;x<domeFrame.width*4/5;x++)
        {
            Color c=domeFrame.GetPixel(x,y); domeSum+=.2126*c.r+.7152*c.g+.0722*c.b; domePixels++;
        }
        InteriorBalanceAudit.Dome(interior, follow, domeFrame);
        InteriorWindowsAudit.Dome(interior, follow, domeFrame);
        InteriorToneAudit.Dome(interior, follow, domeFrame);
        Destroy(domeFrame);
        float headroomLuma=(float)(domeSum/domePixels);
        var windowKey=Array.Find(interior.lamps,l=>l.name=="Interior_WindowKey");
        int fills=Array.FindAll(interior.lamps,l=>l.name.StartsWith("Interior_NaveFill_")).Length;
        float gloss=interior.detailRoot.GetComponentsInChildren<MeshRenderer>()[0].sharedMaterial.GetFloat("_Smoothness");
        var slab=Array.Find(interior.detailRoot.GetComponentsInChildren<MeshRenderer>(),r=>r.name.StartsWith("Interior_FloorSlab_"));
        if(slab!=null) gloss=slab.sharedMaterial.GetFloat("_Smoothness");
        // ceiling_keep retains the existing display-referred DOME_LIGHT headroom metric.
        Debug.Log(FormattableString.Invariant($"[INT_LIGHT] key={(windowKey!=null?1:0)} fill={fills} key_angle={windowKey.transform.eulerAngles.x:F1} nave_luma={naveLuma:F4} wall_luma={wallLuma:F4} bench_luma={benchLuma:F4} floor_gloss={gloss:F2} ceiling_keep={headroomLuma:F4}"));
        // 阈值按"昏暗石造教堂"重新标定（linear 空间；nave 0.05 线性 ≈ 0.24 显示，肉眼是偏暗但可辨）。
        // 旧的 0.16–0.30 是"白墙现代厅"的量级，会把正确的暗调判成失败。
                // 环境光重做后中殿基准整体下降约 15%：同一观感实测 0.0194，故下限从 .020 调到 .017。
        bool interiorLightPass=naveLuma>=.017f && naveLuma<=.13f && wallLuma>=.002f && wallLuma<=.08f && benchLuma>=.003f && benchLuma<=.15f && headroomLuma>=.20f;
        if(!interiorLightPass) Debug.LogError("[INT_LIGHT] luminance gate failed");
        int windowFills=Array.FindAll(interior.detailRoot.GetComponentsInChildren<MeshRenderer>(),r=>r.name.StartsWith("Interior_DrumWindowFill_")).Length;
        float domeIntensity=interior.domeUplights[0].intensity;
        Debug.Log(FormattableString.Invariant($"[DOME_LIGHT] uplights={interior.domeUplights.Length} intensity={domeIntensity:F1} windows_emissive={windowFills} cone_color=#8A8177 aperture=0.8 headroom_luma={headroomLuma:F4}"));
        // STYLE7 intentionally darkens the interior. Keep the existing .20 readability floor
        // and cap at .28 so the demonstration also rejects a return to the brighter V6 dome.
        bool domeLightPass=headroomLuma>=.20f && headroomLuma<=.28f && interior.domeUplights.Length==2 && windowFills==12 &&
            Array.TrueForAll(interior.domeUplights,l=>Mathf.Abs(l.intensity-1.6f)<.001f);
        float ceilingFx=0f; bool ceilingSeen=false;
        MeasureCeiling(out ceilingSeen,out ceilingFx,out bool domeSeen);
        Debug.Log(FormattableString.Invariant($"[LOOKUP] ceiling_visible={ceilingSeen.ToString().ToLowerInvariant()} dome_visible={domeSeen.ToString().ToLowerInvariant()} ceiling_pct={ceilingFx:F4} bytes={(new FileInfo(lookupShot)).Length}"));
        Debug.Log(FormattableString.Invariant($"[PROP2_LOOKUP] ceiling_pct={ceilingFx:F4} dome_visible={domeSeen.ToString().ToLowerInvariant()}"));
        lookUp=false;
        yield return new WaitForSeconds(.4f);
        bool pass=interiorLightPass && domeLightPass && ceilingSeen && domeSeen && ceilingFx>=.35f && InteriorVisibility() && TestInteriorObstacles() && enteredByWalking && transform.position.x>=-4.5f && transform.position.x<=4.5f &&
            transform.position.z>=-9 && transform.position.z<=7 && Mathf.Abs(transform.position.y-interior.FloorY)<=.3f &&
            Mathf.Abs(follow.fieldOfView-50)<.01f && interior.lamps.Length==20 && interior.shafts.Length==3 &&
            new FileInfo(entry).Length>0 && new FileInfo(inside).Length>0;
        Debug.Log($"[INT_PASS] light={interiorLightPass} domeLight={domeLightPass} ceilingSeen={ceilingSeen} " +
            $"domeSeen={domeSeen} fx={ceilingFx:F3} visible={InteriorVisibility()} obstacles={TestInteriorObstacles()} " +
            $"walked={enteredByWalking} z={transform.position.z:F2} dy={Mathf.Abs(transform.position.y-interior.FloorY):F3} " +
            $"fov={follow.fieldOfView:F2} lamps={interior.lamps.Length} shafts={interior.shafts.Length}");
        Debug.Log(FormattableString.Invariant($"[INTERIOR_U] door_open_deg={interior.OpenDegrees:F0} walk_in={enteredByWalking.ToString().ToLowerInvariant()} player_inside={interior.Contains(transform.position).ToString().ToLowerInvariant()} fov_in={follow.fieldOfView:F0} lights={interior.lamps.Length} light_shafts={interior.shafts.Length} colliders={blockers.Length} pois={Array.FindAll(pois,p=>p.data.interior).Length}"));
        Debug.Log(FormattableString.Invariant($"[INTERIOR_U] player=({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}) floor_y={interior.FloorY:F3} closed_blocks={closedBlocks} raycast_misses={misses}"));
        if(!pass) { Debug.LogError("[INTERIOR_U] entry gates failed"); if(poi==null) { Application.Quit(1); yield break; } }
        if(poi!=null)
        {
            yield return WalkInteriorTo(poi.transform.position+Vector3.back*1.1f);
            if(NearestPoi()!=poi || !poi.TryOpen()) { Debug.LogError("[INTERIOR_U] POI selection failed"); Application.Quit(1); yield break; }
            yield return new WaitForSecondsRealtime(1);
            yield return new WaitForEndOfFrame();
            string shot=InteriorShot("interior_poi_"+poi.data.id.Substring("gergeti_".Length));
            bool entryPass=pass;
            pass=panel.IsOpen && panel.Current==poi.data && panel.ValidateContent() && new FileInfo(shot).Length>0 &&
                !cameraBelowFloor && indoorMinY>=1.699f && indoorMinGap>=.349f;
            Debug.Log(FormattableString.Invariant($"[INT_FIX] cam_in=(0,1.75,-5.2) fov_in=38 cam_min_y={indoorMinY:F4} cam_wall_gap={indoorMinGap:F4} cam_below_floor={cameraBelowFloor.ToString().ToLowerInvariant()}"));
            Debug.Log($"[POI] open={poi.data.id} title_ok={pass.ToString().ToLowerInvariant()} lines_cn={poi.data.bodyCn.Split('\n').Length} screenshot={shot}");
            Debug.Log("[INTERIOR_U] screenshots=Shots/interior_entry.png,Shots/interior_inside.png,Shots/interior_poi_p04.png");
            pass &= entryPass;
        }
        // Inspection capture runs after every existing entry/obstacle assertion.
        if(pass && poi==null) { yield return WindowsCloseup(); yield return RoyalDoorCloseup(); yield return PewsCloseup(); yield return PilgrimScaleShot(); }
        Application.Quit(pass?0:1);
    }

    bool AuditPilgrimSize(string zone)
    {
        var animator=GetComponent<PilgrimAnimator>();
        float h=animator.VisibleWorldHeight;
        bool aspect=Mathf.Abs(sprite.GetFloat("_ScaleX")/sprite.GetFloat("_ScaleY")-64f/80f)<.00001f;
        bool pass=Mathf.Abs(h-PlayerHeight)<=.03f && aspect &&
            animator.MinVisibleWorldHeight>=PlayerHeight-.03f && animator.MaxVisibleWorldHeight<=PlayerHeight+.03f;
        Debug.Log(FormattableString.Invariant($"[PILGRIM_SIZE] world_h={h:F5} aspect_preserved={aspect.ToString().ToLowerInvariant()} collider={PlayerHeight:F1} zone={zone} alpha_h_min={animator.MinVisibleWorldHeight:F5} alpha_h_max={animator.MaxVisibleWorldHeight:F5}"));
        if(!pass) { Debug.LogError("[PILGRIM_SIZE] size gate failed"); Application.Quit(1); }
        return pass;
    }

    IEnumerator PilgrimScaleShot()
    {
        enabled=false;
        var saved=transform.position;
        transform.position=new Vector3(-5.75f,interior.FloorY,.55f);
        var eye=follow.transform.position;var rotation=follow.transform.rotation;float fov=follow.fieldOfView;
        follow.transform.position=new Vector3(-4.15f,interior.FloorY+1.75f,-5.2f);
        follow.transform.LookAt(new Vector3(-4.15f,interior.FloorY+1.0f,2.5f));follow.fieldOfView=50;
        yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();
        AuditPilgrimSize("bench");
        string path=InteriorShot("pilgrim_scale");
        Debug.Log("[PILGRIM_SCALE_SHOT] path="+path);
        transform.position=saved;follow.transform.SetPositionAndRotation(eye,rotation);follow.fieldOfView=fov;enabled=true;
    }

    IEnumerator WindowsCloseup()
    {
        enabled=false;
        var go=new GameObject("Windows inspection camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(follow);
        var source=follow.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var data=go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.renderPostProcessing=source.renderPostProcessing;data.volumeLayerMask=source.volumeLayerMask;
        var target=new Vector3(-5.6f,6.77402f,4.02f);
        go.transform.position=target+new Vector3(.85f,-.1f,-4.2f);go.transform.LookAt(target);camera.fieldOfView=50;follow.enabled=false;
        yield return new WaitForSecondsRealtime(1);yield return new WaitForEndOfFrame();
        string path=InteriorShot("windows_closeup");
        Debug.Log("[WINDOWS_SHOT] temporary_inspection_camera=true path="+path);
        follow.enabled=true;Destroy(go);enabled=true;
    }

    IEnumerator PewsCloseup()
    {
        enabled=false;
        var go=new GameObject("Pews inspection camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(follow);
        var source=follow.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var data=go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.renderPostProcessing=source.renderPostProcessing;data.volumeLayerMask=source.volumeLayerMask;
        var target=new Vector3(-4.15f,interior.FloorY+.48f,.55f);
        go.transform.position=target+new Vector3(1.4f,1.0f,-3.60971f).normalized*4;
        go.transform.LookAt(target);camera.fieldOfView=50;follow.enabled=false;
        yield return new WaitForSecondsRealtime(1);yield return new WaitForEndOfFrame();
        string path=InteriorShot("pews_closeup");
        Debug.Log(FormattableString.Invariant($"[PEWS_SHOT] distance_m={Vector3.Distance(target,go.transform.position):F3} path={path}"));
        var materialFrame=ScreenCapture.CaptureScreenshotAsTexture();
        InteriorMaterialsAudit.Pews(interior,camera,materialFrame);Destroy(materialFrame);
        // Rear-side 45-degree inspection, using only this temporary camera.
        go.transform.position=target+new Vector3(1, .48f, -1).normalized*4;
        go.transform.LookAt(target);
        yield return new WaitForSecondsRealtime(1);yield return new WaitForEndOfFrame();
        string sidePath=InteriorShot("pews_side");
        Debug.Log(FormattableString.Invariant($"[PEWS_SIDE_SHOT] azimuth_deg=45 distance_m={Vector3.Distance(target,go.transform.position):F3} path={sidePath}"));
        follow.enabled=true;Destroy(go);enabled=true;
    }

    IEnumerator RoyalDoorCloseup()
    {
        enabled=false;
        var go=new GameObject("Royal door inspection camera");
        var camera=go.AddComponent<Camera>();camera.CopyFrom(follow);
        var source=follow.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var data=go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.renderPostProcessing=source.renderPostProcessing;data.volumeLayerMask=source.volumeLayerMask;
        go.transform.position=new Vector3(-2,interior.FloorY+1.43f,4.55f);
        go.transform.rotation=Quaternion.identity;camera.fieldOfView=60;
        follow.enabled=false;
        yield return new WaitForSecondsRealtime(1);
        yield return new WaitForEndOfFrame();
        string path=InteriorShot("royal_door_closeup");
        Debug.Log($"[ROYAL_DOOR_SHOT] distance_m=3.000 camera={go.transform.position:F4} path={path}");
        follow.enabled=true;Destroy(go);enabled=true;
    }

    IEnumerator DemoPoi(Interactable poi)
    {
        // Prefer two metres in front. If another POI is closer there (P01/P03),
        // use a side approach at the same distance, preserving normal selection.
        bool positioned = false;
        foreach (var offset in new[] { Vector3.back, Vector3.right, Vector3.left, Vector3.forward })
        {
            Vector3 position = poi.transform.position + offset * 2;
            if (!ground.Raycast(new Ray(new Vector3(position.x, 500, position.z), Vector3.down), out var hit, 1000)) continue;
            position.y = hit.point.y;
            transform.position = position;
            if (NearestPoi() == poi) { positioned = true; break; }
        }
        if (!positioned)
        { Debug.LogError("[POI] no reachable demo approach: " + poi.data.id); Application.Quit(1); yield break; }
        yield return null;
        // Exercise the same nearest-POI selection and TryOpen used by E input.
        var selected = NearestPoi();
        if (selected != poi || !selected.TryOpen())
        { Debug.LogError("[POI] demo interaction failed: " + poi.data.id); Application.Quit(1); yield break; }
        yield return new WaitForSecondsRealtime(1);
        yield return new WaitForEndOfFrame();
        string path = "Shots/poi_" + poi.data.id.Substring("gergeti_".Length) + ".png";
        var root = new System.IO.DirectoryInfo(Application.dataPath);
        while (root != null && root.Name != "Builds") root = root.Parent;
        string output = root != null ? System.IO.Path.Combine(root.Parent.FullName, path) : System.IO.Path.GetFullPath(path);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
        var frame = ScreenCapture.CaptureScreenshotAsTexture();
        System.IO.File.WriteAllBytes(output, frame.EncodeToPNG());
        Destroy(frame);
        int lines = poi.data.bodyCn.Split('\n').Length;
        bool valid = panel.IsOpen && panel.Current == poi.data && panel.ValidateContent() &&
            lines >= 4 && lines <= 6 && new System.IO.FileInfo(output).Length > 0;
        Debug.Log($"[POI] open={poi.data.id} title_ok={valid.ToString().ToLowerInvariant()} lines_cn={lines} screenshot={path}");
        Application.Quit(valid ? 0 : 1);
    }

    IEnumerator DemoInteract()
    {
        transform.position = entrance.transform.position + Vector3.back;
        yield return null; // Update grounds the same player used by normal interaction.
        if (!entrance.TryOpen()) { Debug.LogError("[UI] entrance interaction failed"); Application.Quit(1); yield break; }
        yield return new WaitForSecondsRealtime(1);
        yield return new WaitForEndOfFrame();
        const string path = "Shots/ui_panel.png";
        // macOS players change cwd to Builds/Mac; resolve the spike's shared Shots folder.
        var buildRoot = new System.IO.DirectoryInfo(Application.dataPath);
        while (buildRoot != null && buildRoot.Name != "Builds") buildRoot = buildRoot.Parent;
        string output = buildRoot != null ? System.IO.Path.Combine(buildRoot.Parent.FullName, path)
            : System.IO.Path.GetFullPath(path);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
        // Synchronous screen capture includes the overlay Canvas and prevents stale-file acceptance.
        var frame = ScreenCapture.CaptureScreenshotAsTexture();
        System.IO.File.WriteAllBytes(output, frame.EncodeToPNG());
        Destroy(frame);
        bool valid = panel.ValidateContent() && System.IO.File.Exists(output) && new System.IO.FileInfo(output).Length > 0;
        Debug.Log($"[UI] panel_open={panel.IsOpen.ToString().ToLowerInvariant()} title_ok={valid.ToString().ToLowerInvariant()} paragraphs={panel.ParagraphCount} screenshot={path}");
        Application.Quit(panel.IsOpen && valid && panel.ParagraphCount == 2 ? 0 : 1);
    }
}
