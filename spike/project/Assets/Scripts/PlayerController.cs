using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

/// <summary>Playable slice. Capture flags always take precedence over demos and input.</summary>
public sealed class PlayerController : MonoBehaviour
{
    public MeshCollider ground;
    public InfoPanel panel;
    public Interactable entrance;
    public bool Moving { get; private set; }
    public int Row { get; private set; } = 3;
    public static bool CaptureMode => Array.IndexOf(Environment.GetCommandLineArgs(), "-shot") >= 0 ||
        Array.IndexOf(Environment.GetCommandLineArgs(), "-shots") >= 0;
    Camera follow;
    Material sprite;
    float animationTime, demoSeconds, elapsed, maxGroundError;
    int frames, misses;
    bool walkingDemo;
    Vector3 walkStart;

    void Awake() { if (CaptureMode) enabled = false; }

    void Start()
    {
        GetComponent<PilgrimAnimator>().enabled = false;
        sprite = GetComponent<MeshRenderer>().material;
        follow = Camera.main;
        follow.transform.position = transform.position + new Vector3(0, 8, -22);
        follow.transform.LookAt(transform.position + Vector3.up * 3);
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-demo-walk");
        if (index >= 0)
        {
            if (index + 1 >= args.Length || !float.TryParse(args[index+1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out demoSeconds) || demoSeconds <= 0)
            { Debug.LogError("[PLAY] invalid -demo-walk duration"); Application.Quit(1); return; }
            walkingDemo = true;
            walkStart = transform.position;
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
        var candidate = transform.position + direction * ((sprint ? 5f : 3f) * dt);
        // Exactly one ground query per frame, including idle frames; reject moves off the terrain.
        bool grounded = ground.Raycast(new Ray(new Vector3(candidate.x, 500, candidate.z), Vector3.down),
            out var hit, 1000);
        if (grounded)
        {
            candidate.y = hit.point.y;
            transform.position = candidate;
            maxGroundError = Mathf.Max(maxGroundError, Mathf.Abs(transform.position.y - hit.point.y));
        }
        else misses++;
        Moving = grounded && direction.sqrMagnitude > .001f;
        if (Moving)
        {
            var local = follow.transform.InverseTransformDirection(direction);
            Row = Mathf.Abs(local.x) > Mathf.Abs(local.z) ? (local.x < 0 ? 1 : 2) : (local.z > 0 ? 3 : 0);
            animationTime += dt;
        }
        else animationTime = 0;
        sprite.SetFloat("_Row", Row);
        sprite.SetFloat("_Frame", Moving ? Mathf.FloorToInt(animationTime * 10) % 6 : 0);
        if (walkingDemo && elapsed >= demoSeconds)
        {
            float distance = Vector3.Distance(walkStart, transform.position);
            float fps = frames / elapsed;
            bool fpsOK = fps >= 30;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[PLAY] walk_start=({0:F3},{1:F3},{2:F3}) walk_end=({3:F3},{4:F3},{5:F3}) distance={6:F3} row={7} fps_ok={8}",
                walkStart.x, walkStart.y, walkStart.z, transform.position.x, transform.position.y,
                transform.position.z, distance, Row, fpsOK.ToString().ToLowerInvariant()));
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[PLAY] ground_error_max={0:F6} raycast_misses={1} fps={2:F2} animation_fps=10 frames=6",
                maxGroundError, misses, fps));
            Application.Quit(distance >= 15 && maxGroundError <= .02f && misses == 0 && Row >= 0 && Row <= 3 && fpsOK ? 0 : 1);
            walkingDemo = false;
        }
    }

    void LateUpdate()
    {
        follow.transform.position = Vector3.Lerp(follow.transform.position,
            transform.position + new Vector3(0, 8, -22), .12f);
        follow.transform.rotation = Quaternion.Lerp(follow.transform.rotation,
            Quaternion.LookRotation(transform.position + Vector3.up * 3 - follow.transform.position), .12f);
    }

    IEnumerator DemoInteract()
    {
        transform.position = entrance.transform.position + Vector3.back;
        yield return null; // Update grounds the same player used by normal interaction.
        if (!entrance.TryOpen()) { Debug.LogError("[UI] entrance interaction failed"); Application.Quit(1); yield break; }
        yield return new WaitForSecondsRealtime(1);
        yield return new WaitForEndOfFrame();
        const string path = "Shots/ui_panel.png";
        System.IO.Directory.CreateDirectory("Shots");
        // Synchronous screen capture includes the overlay Canvas and prevents stale-file acceptance.
        var frame = ScreenCapture.CaptureScreenshotAsTexture();
        System.IO.File.WriteAllBytes(path, frame.EncodeToPNG());
        Destroy(frame);
        bool valid = panel.ValidateContent();
        Debug.Log($"[UI] panel_open={panel.IsOpen.ToString().ToLowerInvariant()} title_ok={valid.ToString().ToLowerInvariant()} paragraphs={panel.ParagraphCount} screenshot={path}");
        Application.Quit(panel.IsOpen && valid && panel.ParagraphCount == 2 ? 0 : 1);
    }
}
