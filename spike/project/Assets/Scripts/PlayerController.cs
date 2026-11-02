using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

/// <summary>Playable slice. Capture flags always take precedence over demos and input.</summary>
public sealed class PlayerController : MonoBehaviour
{
    public MeshCollider ground;
    public MeshCollider stairRamp;
    public Collider stairDeck;
    // Reserved physics layers; ramp contact overrides accept only PlayerLayer.
    public const int PlayerLayer = 8, TraversalLayer = 9;
    const float StepUpMax = .28f, StepProbe = .35f, StepLiftSpeed = 12f;
    const float Acceleration = 12f, Deceleration = 18f, TurnSpeed = 540f;
    Vector3 velocity;
    Collider[] terraceSurfaces;
    bool stairsDemo;
    float deckY;
    public InfoPanel panel;
    public Interactable entrance;
    public bool Moving { get; private set; }
    public int Row { get; private set; } = 3;
    public static bool CaptureMode => Array.IndexOf(Environment.GetCommandLineArgs(), "-shot") >= 0 ||
        Array.IndexOf(Environment.GetCommandLineArgs(), "-shots") >= 0;
    public static readonly Vector3 FollowOffset = new Vector3(0, 5.5f, -14);
    public const float LookHeight = 3.5f;
    AudioDirector audioDirector;
    int lastSoundFrame = -1;
    Interactable[] pois;
    Camera follow;
    Material sprite;
    float animationTime, demoSeconds, elapsed, maxGroundError;
    int frames, misses;
    bool walkingDemo;
    Vector3 walkStart;

    void Awake()
    {
        if (CaptureMode) { enabled = false; return; }
        gameObject.layer = PlayerLayer;
        // Runtime spawn only: screenshot pilgrim and the ShotRig table are untouched.
        var spawn = new Vector3(0, 0, -88);
        if (ground.Raycast(new Ray(spawn + Vector3.up * 500, Vector3.down), out var spawnHit, 1000))
            spawn.y = spawnHit.point.y;
        transform.position = spawn;
        audioDirector = gameObject.AddComponent<AudioDirector>();
    }

    void Start()
    {
        terraceSurfaces = stairRamp.transform.parent.GetComponentsInChildren<Collider>();
        Physics.SyncTransforms();
        Debug.Log("[MOVE] step_up_max=0.28 ramp=true accel=12 decel=18 spawn_z=-88");
        GetComponent<PilgrimAnimator>().enabled = false;
        sprite = GetComponent<MeshRenderer>().material;
        follow = Camera.main;
        follow.transform.position = transform.position + FollowOffset;
        follow.transform.LookAt(transform.position + Vector3.up * LookHeight);
        // Fit the real tower at the 20 m approach, using only the playable camera.
        // CaptureMode exits in Awake, so the ShotRig camera table stays unchanged.
        var reveal = GetComponent<MistReveal>();
        var near = new Vector3(0, 0, -29);
        if (reveal != null && ground.Raycast(new Ray(near + Vector3.up * 500, Vector3.down), out var nearHit, 1000))
        {
            near.y = nearHit.point.y;
            var eye = near + FollowOffset;
            var inverse = Quaternion.Inverse(Quaternion.LookRotation(near + Vector3.up * LookHeight - eye));
            float topSlope = 0;
            foreach (var mf in reveal.church.GetComponentsInChildren<MeshFilter>())
            foreach (var vertex in mf.sharedMesh.vertices)
            {
                var local = inverse * (mf.transform.TransformPoint(vertex) - eye);
                if (local.z > 0) topSlope = Mathf.Max(topSlope, local.y / local.z);
            }
            // Screen top at y=.12 (top-left origin); .76 = 1 - 2*.12.
            follow.fieldOfView = 2 * Mathf.Atan(topSlope / .76f) * Mathf.Rad2Deg;
            Debug.Log($"[CLOSEUP_CAMERA] playable_fov={follow.fieldOfView:F3}");
        }
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
        var args = Environment.GetCommandLineArgs();
        int poiIndex = Array.IndexOf(args, "-demo-poi");
        if (poiIndex >= 0)
        {
            if (poiIndex + 1 >= args.Length || Array.Find(pois, p => p.data.id == args[poiIndex + 1]) == null)
            { Debug.LogError("[POI] invalid -demo-poi id"); Application.Quit(1); return; }
            StartCoroutine(DemoPoi(Array.Find(pois, p => p.data.id == args[poiIndex + 1])));
        }
        int index = Array.IndexOf(args, "-demo-walk");
        if (index >= 0)
        {
            if (index + 1 >= args.Length || !float.TryParse(args[index+1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out demoSeconds) || demoSeconds <= 0)
            { Debug.LogError("[PLAY] invalid -demo-walk duration"); Application.Quit(1); return; }
            walkingDemo = true;
            walkStart = transform.position;
        }
        if (Array.IndexOf(args, "-demo-walk-stairs") >= 0)
        {
            stairsDemo = walkingDemo = true;
            demoSeconds = 8;
            var start = new Vector3(0, 0, -16);
            if (!SurfaceAt(start, out var startHit) ||
                !stairDeck.Raycast(new Ray(stairDeck.bounds.center + Vector3.up * 10, Vector3.down), out var deckHit, 20))
            { Debug.LogError("[MOVE] stair test surface missing"); Application.Quit(1); return; }
            start.y = startHit.point.y;
            transform.position = walkStart = start;
            deckY = deckHit.point.y;
        }
        if (Array.IndexOf(args, "-demo-interact") >= 0) StartCoroutine(DemoInteract());
    }

    void Update()
    {
        Vector3 direction = Vector3.zero;
        bool sprint = false;
        float dt = Time.deltaTime;
        if (walkingDemo)
        {
            dt = Mathf.Min(dt, demoSeconds - elapsed);
            direction = Vector3.forward;
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
        }
        Vector3 before = transform.position;
        bool grounded = false;
        // Substeps keep probes ahead of movement even during a long render frame.
        int substeps = Mathf.Max(1, Mathf.CeilToInt(dt / .02f));
        float stepDt = dt / substeps;
        for (int i = 0; i < substeps; i++) grounded = Move(direction, sprint, stepDt);
        var nearest = NearestPoi();
        panel.SetPrompt(nearest != null && !panel.IsOpen);
        if (Input.GetKeyDown(KeyCode.E) && nearest != null) nearest.TryOpen();
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
                Mathf.Abs(animationTime - elapsed) < .001f;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[PLAY] walk_start=({0:F3},{1:F3},{2:F3}) walk_end=({3:F3},{4:F3},{5:F3}) distance={6:F3} row={7} fps_ok={8}",
                walkStart.x, walkStart.y, walkStart.z, transform.position.x, transform.position.y,
                transform.position.z, distance, Row, fpsOK.ToString().ToLowerInvariant()));
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[PLAY] ground_error_max={0:F6} raycast_misses={1} fps={2:F2} animation_fps=10 frames=6",
                maxGroundError, misses, fps));
            bool pass;
            if (stairsDemo)
            {
                bool climbed = transform.position.y >= deckY - .1f &&
                    transform.position.z >= stairDeck.bounds.max.z && distance >= 6;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[MOVE] stair_test_start_y={0:F3} end_y={1:F3} deck_y={2:F3} climbed={3} distance={4:F3}",
                    walkStart.y, transform.position.y, deckY, climbed.ToString().ToLowerInvariant(), distance));
                pass = climbed && misses == 0;
            }
            else pass = distance >= 15 && maxGroundError <= .02f && misses == 0;
            Application.Quit(pass && Row >= 0 && Row <= 3 && fpsOK &&
                audioDirector.ClipsPass && audioDirector.Footsteps >= 6 ? 0 : 1);
            walkingDemo = false;
        }
    }

    // Query terrain AND independent terrace colliders. Bounds reject almost all
    // terrace meshes before raycasting; decorative snow/props cannot become ground.
    bool SurfaceAt(Vector3 position, out RaycastHit hit)
    {
        var ray = new Ray(new Vector3(position.x, 500, position.z), Vector3.down);
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
        var direction = velocity.normalized;
        var candidate = transform.position + velocity * dt;
        // The .35m uphill look-ahead recognizes individual .18m treads; a higher
        // face is a wall. A pre-lifted foot already occupies the upcoming tread
        // height: keep that support when .35m spans two .34m tread boundaries.
        bool probeFound = SurfaceAt(transform.position + direction * StepProbe, out var probe);
        if (!SurfaceAt(candidate, out var next)) { misses++; velocity = Vector3.zero; return false; }
        float supportY = Mathf.Max(current.point.y, transform.position.y);
        if (next.point.y - supportY > StepUpMax + .001f ||
            (probeFound && probe.point.y - supportY > StepUpMax + .001f))
        { velocity = Vector3.zero; return true; }
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
        follow.transform.position = Vector3.Lerp(follow.transform.position,
            transform.position + FollowOffset, .12f);
        follow.transform.rotation = Quaternion.Lerp(follow.transform.rotation,
            Quaternion.LookRotation(transform.position + Vector3.up * LookHeight - follow.transform.position), .12f);
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
            if (!poi.InRange) continue;
            float d = (transform.position - poi.transform.position).sqrMagnitude;
            if (d < distance) { distance = d; nearest = poi; }
        }
        return nearest;
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
