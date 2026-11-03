using System;
using UnityEngine;

/// <summary>
/// 玩家脚印：按**累计位移**（不是时间）每隔 0.75 m 落一个脚印贴片，左右脚交替偏移 ±0.18 m。
/// 贴片池预分配 120 个（环形复用，不产生 GC）；踩在石板/室内的石头地面上不落脚印。
/// 场景里原有的 64 个静态脚印保留（"来过的人"的痕迹），只统计不修改。
/// </summary>
public sealed class FootprintTrail : MonoBehaviour
{
    public const float Spacing = .75f;
    public const int Capacity = 120;
    const float Lift = .012f;
    const float HalfWidth = .13f;   // 椭圆半径：0.26 m 宽
    const float HalfLength = .22f;  // 0.44 m 长
    const int Verts = 25;

    readonly GameObject[] patches = new GameObject[Capacity];
    readonly Mesh[] meshes = new Mesh[Capacity];
    readonly Vector3[] vertices = new Vector3[Verts];
    readonly int[] triangles = new int[72];
    readonly RaycastHit[] hits = new RaycastHit[8];

    AudioDirector audioDirector;
    Material material;
    Transform root;
    float remainder, groundErrorMax;
    int next, contacts;

    public int Spawns { get; private set; }
    public int Despawns { get; private set; }
    public int PavingSkipped { get; private set; }
    public int UnevenSkipped { get; private set; }
    public int StaticKept { get; private set; }
    public float GroundErrorMax => groundErrorMax;
    public float Distance { get; private set; }

    public void Initialize(AudioDirector audio)
    {
        audioDirector = audio;

        // 场景里的静态脚印当模板（材质、尺寸都和它一致），没有就退到 URP Lit，绝不抛异常：
        // 这个组件是装饰层，初始化失败不能把 PlayerController.Awake 打断（曾经因此所有演示卡死）。
        MeshRenderer template = null;
        foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if (renderer.name.StartsWith("Footprint_", StringComparison.Ordinal))
            {
                StaticKept++;
                if (template == null) template = renderer;
            }

        material = template != null && template.sharedMaterial != null
            ? new Material(template.sharedMaterial) { name = "M_PlayerFootprint" }
            : new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_PlayerFootprint" };
        // 新脚印比静态痕迹淡一点（0.55）：区分"我刚走过的路"和"别人早年的路"。
        // 贴片是 25 顶点的小面片、alpha 常为 1（地面雾着色器不吃 alpha），
        // 所以"深浅"只能靠基色贴近雪色 + _Footprint 降档；静态脚印用 1.0/#AFC0D2，玩家脚印更淡。
        if (material.HasProperty("_BaseColor"))
        {
            var c = material.GetColor("_BaseColor");
            c = new Color(.765f, .827f, .890f, 1f);   // #C3D3E3
            material.SetColor("_BaseColor", c);
        }
        if (material.HasProperty("_Footprint")) material.SetFloat("_Footprint", .55f);
        material.renderQueue = 3000;
        if (material.HasProperty("_TrailSrcBlend")) material.SetFloat("_TrailSrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_TrailDstBlend")) material.SetFloat("_TrailDstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_TrailZWrite")) material.SetFloat("_TrailZWrite", 0f);

        for (int v = 0; v < 24; v++)   // 24 个扇形三角形 = 72 索引；v<Verts 会越界
        {
            triangles[v * 3] = 0;
            triangles[v * 3 + 1] = 1 + (v + 1) % 24;
            triangles[v * 3 + 2] = v + 1;
        }

        root = new GameObject("Player Footprint Trail").transform;
        for (int i = 0; i < Capacity; i++)
        {
            var go = new GameObject("Trail_" + i, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            var mesh = new Mesh { name = "TrailFootprint_" + i };
            mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.SetActive(false);
            meshes[i] = mesh;
            patches[i] = go;
        }
    }

    /// <summary>每帧把位移喂进来；内部按 0.75 m 的间距补脚印，与帧率无关。</summary>
    public void RecordMovement(Vector3 from, Vector3 to)
    {
        if (root == null) return;
        Vector3 delta = to - from;
        delta.y = 0;
        float length = delta.magnitude;
        if (length < 1e-5f) return;
        Distance += length;
        Vector3 forward = delta / length;

        float along = Spacing - remainder;
        while (along <= length)
        {
            Contact(Vector3.Lerp(from, to, along / length), forward);
            along += Spacing;
            if (Spawns > 4000) break;   // 防御：异常位移（传送/演示跳点）不要死循环
        }
        remainder = (remainder + length) % Spacing;
    }

    void Contact(Vector3 point, Vector3 forward)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float side = (contacts++ % 2 == 0 ? -.18f : .18f);
        point += right * side;

        if (!Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            return;

        var colliderRenderer = hit.collider.GetComponent<MeshRenderer>();
        string materialName = colliderRenderer != null && colliderRenderer.sharedMaterial != null
            ? colliderRenderer.sharedMaterial.name
            : string.Empty;
        if (materialName.Contains("Paving") || materialName.Contains("Interior") ||
            hit.collider.name.StartsWith("Interior", StringComparison.Ordinal))
        {
            PavingSkipped++;
            return;
        }

        // 每个顶点单独向下取地面高度，脚印会顺着坡面贴上去，不会插进雪里或浮空。
        // groundErrorMax 记录单个脚印覆盖范围内地面高差（0.5 m 内高差越大，贴片越"折"）。
        float minGround = float.MaxValue, maxGround = float.MinValue;
        for (int v = 0; v < Verts; v++)
        {
            float angle = (v - 1) * Mathf.PI * 2f / 24f;
            var offset = v == 0 ? Vector3.zero
                : new Vector3(Mathf.Cos(angle) * HalfWidth, 0, Mathf.Sin(angle) * HalfLength);
            Vector3 world = point + right * offset.x + forward * offset.z;
            float groundY = hit.point.y;
            if (Physics.Raycast(world + Vector3.up * 2f, Vector3.down, out var vertexHit, 4f, ~0, QueryTriggerInteraction.Ignore))
                groundY = vertexHit.point.y;
            vertices[v] = new Vector3(world.x, groundY + Lift, world.z);
            minGround = Mathf.Min(minGround, groundY);
            maxGround = Mathf.Max(maxGround, groundY);
        }

        float span = maxGround - minGround;
        if (span > 0f) groundErrorMax = Mathf.Max(groundErrorMax, span);
        // 台阶边缘一个 0.5 m 脚印会横跨两级（高差 >0.12 m），贴片会被拉成一条竖带——直接不落。
        if (span > .12f) { UnevenSkipped++; return; }

        int slot = next % Capacity;
        if (patches[slot].activeSelf) Despawns++;
        var mesh = meshes[slot];
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        patches[slot].SetActive(true);
        next++;
        Spawns++;
        if (audioDirector != null) audioDirector.PlayFootstep();
    }

    public void LogSummary()
    {
        Debug.Log($"[TRAIL] spacing={Spacing:F2}m max={Capacity} spawns={Spawns} despawns={Despawns} " +
                  $"ground_span_max={groundErrorMax:F4} paving_skipped={PavingSkipped} uneven_skipped={UnevenSkipped} static_kept={StaticKept} " +
                  $"distance={Distance:F2}");
    }
}
