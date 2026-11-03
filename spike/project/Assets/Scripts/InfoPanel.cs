using UnityEngine;
using UnityEngine.UI;

public sealed class InfoPanel : MonoBehaviour
{
    public bool IsOpen => overlay != null && overlay.activeSelf;
    public int ClosedFrame { get; private set; } = -1;
    public int ParagraphCount => paragraphs == null ? 0 : paragraphs.Length;
    public PoiData Current { get; private set; }
    GameObject overlay, prompt;
    Text title;
    Text[] paragraphs;
    Font font;
    public Font UiFont => font;
    public string BodyLanguage { get; private set; } = "both";
    public System.Func<string, bool> IsVisited;
    public System.Action<PoiData> OnClosed;
    int openedFrame;

    void Awake()
    {
        if (PlayerController.CaptureMode) { gameObject.SetActive(false); return; }
        font = Font.CreateDynamicFontFromOSFont(new[] { "PingFang SC", "Microsoft YaHei", "STHeiti", "Arial Unicode MS", "Arial" }, 24);
        // Preload one shared font size: mixed dynamic atlas rebuilds can invalidate earlier labels.
        font.RequestCharactersInTexture("按 E / Esc 关闭按 E 查看", 24, FontStyle.Normal);
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        overlay = Box("Backdrop", transform, new Color(0, 0, 0, .62f), Vector2.zero, Vector2.zero);
        var full = overlay.GetComponent<RectTransform>(); full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one; full.sizeDelta = Vector2.zero;
        // Hard, nested rectangular strokes create a pixel border without new art assets.
        var border = Box("PixelBorder", overlay.transform, new Color(.75f, .69f, .52f), new Vector2(720, 600), Vector2.zero);
        var paper = Box("Panel", border.transform, new Color(.09f, .12f, .16f, .98f), new Vector2(712, 592), Vector2.zero);
        Box("InnerBorder", paper.transform, new Color(.42f, .40f, .32f), new Vector2(700, 580), Vector2.zero);
        var content = Box("Content", paper.transform, new Color(.09f, .12f, .16f), new Vector2(696, 576), Vector2.zero);
        title = Label("Title", content.transform, "", 24, new Vector2(632, 88), new Vector2(0, 226));
        paragraphs = new[] {
            Label("Chinese", content.transform, "", 24, new Vector2(656, 176), new Vector2(0, 82)),
            Label("English", content.transform, "", 24, new Vector2(656, 200), new Vector2(0, -116)) };
        paragraphs[0].horizontalOverflow = HorizontalWrapMode.Overflow;
        Label("Close", content.transform, "按 E / Esc 关闭", 24, new Vector2(632, 34), new Vector2(0, -254)).alignment = TextAnchor.MiddleRight;
        prompt = Label("Prompt", transform, "按 E 查看", 24, new Vector2(240, 48), Vector2.zero).gameObject;
        var corner = prompt.GetComponent<RectTransform>(); corner.anchorMin = corner.anchorMax = Vector2.zero; corner.anchoredPosition = new Vector2(150, 65);
        overlay.SetActive(false); prompt.SetActive(false);
    }
    void Start()
    {
        // Automated regressions must not consume the real player's first-entry hint.
        if(System.Array.Exists(System.Environment.GetCommandLineArgs(),a=>a.StartsWith("-demo-",System.StringComparison.Ordinal))) return;
        const string key="Mistbell.MovementHintSeen";
        if(PlayerPrefs.GetInt(key,0)!=0) return;
        string message="W A S D 移动 · Shift 加速 · E 交互";
        font.RequestCharactersInTexture(message,24,FontStyle.Normal);
        var hint=Label("MovementHint",transform,message,24,new Vector2(1100,48),Vector2.zero);
        hint.alignment=TextAnchor.MiddleCenter;
        Destroy(hint.gameObject,3f);
        PlayerPrefs.SetInt(key,1); PlayerPrefs.Save();
    }
    void Update()
    {
        if (IsOpen && Time.frameCount > openedFrame && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape)))
        Close();
    }
    void LateUpdate()
    {
        if (!IsOpen) return;
        font.RequestCharactersInTexture(Current.titleCn + Current.titleEn + Current.bodyCn + Current.bodyEn + " / · 已读按 E / Esc 关闭按 E 查看", 24, FontStyle.Normal);
        // Dynamic OS fonts can repack their atlas when a previously hidden paragraph returns.
        // Invalidate the generator as well as the canvas mesh so cached UVs cannot survive it.
        foreach (var label in GetComponentsInChildren<Text>())
        {
            label.cachedTextGenerator.Invalidate();
            label.SetVerticesDirty();
        }
    }
    public void Show(PoiData data)
    {
        Current = data;
        string heading = data.titleCn + " / " + data.titleEn;
        font.RequestCharactersInTexture(heading + data.bodyCn + data.bodyEn + "按 E / Esc 关闭按 E 查看", 24, FontStyle.Normal);
        title.text = heading + (IsVisited != null && IsVisited(data.id) ? " · 已读" : "");
        ApplyLanguage(BodyLanguage);
        overlay.SetActive(true); prompt.SetActive(false); openedFrame = Time.frameCount;
    }
    public void SetPrompt(bool visible) { prompt.SetActive(visible); }
    public void Close()
    {
        if (!IsOpen) return;
        overlay.SetActive(false); ClosedFrame = Time.frameCount;
        OnClosed?.Invoke(Current);
    }
    public void ApplyLanguage(string language)
    {
        BodyLanguage = language;
        if (Current == null) return;
        font.RequestCharactersInTexture(Current.titleCn + Current.titleEn + Current.bodyCn + Current.bodyEn + " / · 已读按 E / Esc 关闭按 E 查看", 24, FontStyle.Normal);
        paragraphs[0].text = language == "en" ? "" : Current.bodyCn;
        paragraphs[1].text = language == "cn" ? "" : Current.bodyEn;
        // A dynamic atlas rebuild must refresh unchanged title/footer meshes too.
        foreach (var label in GetComponentsInChildren<Text>(true)) label.SetAllDirty();
        Canvas.ForceUpdateCanvases();
        Debug.Log("[POI] body_lang=" + language);
    }
    public bool ValidateContent() => Current != null &&
        title.text.StartsWith(Current.titleCn + " / " + Current.titleEn, System.StringComparison.Ordinal) && paragraphs.Length == 2 &&
        paragraphs[0].text == (BodyLanguage == "en" ? "" : Current.bodyCn) &&
        paragraphs[1].text == (BodyLanguage == "cn" ? "" : Current.bodyEn);
    GameObject Box(string name, Transform parent, Color color, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = size; rect.anchoredPosition = position;
        var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        return go;
    }
    Text Label(string name, Transform parent, string text, int size, Vector2 dimensions, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = dimensions; rect.anchoredPosition = position;
        var label = go.GetComponent<Text>(); label.font = font; label.fontSize = size; label.text = text;
        label.color = new Color(.94f, .92f, .84f); label.alignment = TextAnchor.UpperLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        return label;
    }
    void OnDestroy() { if (font != null) Destroy(font); }
}
