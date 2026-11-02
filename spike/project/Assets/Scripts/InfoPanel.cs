using UnityEngine;
using UnityEngine.UI;

public sealed class InfoPanel : MonoBehaviour
{
    public const string Title = "Gergeti 圣三一教堂 / Gergeti Trinity Church";
    public const string Chinese = "海拔2,170米，孤悬于卡兹别克山南麓。\n十四世纪，修士为避战乱把圣物带上山，\n此处从此成为高加索最孤独的教堂之一。\n风雪封山时，钟声先穿过云雾抵达谷地，\n人们才知道路还在。";
    public const string English = "At 2,170 m on the southern slope of Mount Kazbek. In the 14th century monks carried relics uphill to escape the wars, and it has been one of the Caucasus' loneliest churches ever since. When snow closes the pass, the bell crosses the mist before the church itself is seen.";
    public bool IsOpen => overlay != null && overlay.activeSelf;
    public int ClosedFrame { get; private set; } = -1;
    public int ParagraphCount => paragraphs == null ? 0 : paragraphs.Length;
    GameObject overlay, prompt;
    Text title;
    Text[] paragraphs;
    Font font;
    int openedFrame;

    void Awake()
    {
        if (PlayerController.CaptureMode) { gameObject.SetActive(false); return; }
        font = Font.CreateDynamicFontFromOSFont(new[] { "PingFang SC", "Microsoft YaHei", "STHeiti", "Arial Unicode MS", "Arial" }, 24);
        // Preload one shared font size: mixed dynamic atlas rebuilds can invalidate earlier labels.
        font.RequestCharactersInTexture(Title + Chinese + English + "按 E / Esc 关闭按 E 查看", 24, FontStyle.Normal);
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
        title = Label("Title", content.transform, Title, 24, new Vector2(632, 88), new Vector2(0, 226));
        paragraphs = new[] {
            Label("Chinese", content.transform, Chinese, 24, new Vector2(656, 176), new Vector2(0, 82)),
            Label("English", content.transform, English, 24, new Vector2(656, 200), new Vector2(0, -116)) };
        Label("Close", content.transform, "按 E / Esc 关闭", 24, new Vector2(632, 34), new Vector2(0, -254)).alignment = TextAnchor.MiddleRight;
        prompt = Label("Prompt", transform, "按 E 查看", 24, new Vector2(240, 48), Vector2.zero).gameObject;
        var corner = prompt.GetComponent<RectTransform>(); corner.anchorMin = corner.anchorMax = Vector2.zero; corner.anchoredPosition = new Vector2(150, 65);
        overlay.SetActive(false); prompt.SetActive(false);
    }
    void Update()
    {
        if (IsOpen && Time.frameCount > openedFrame && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape)))
        { overlay.SetActive(false); ClosedFrame = Time.frameCount; }
    }
    public void Open() { overlay.SetActive(true); prompt.SetActive(false); openedFrame = Time.frameCount; }
    public void SetPrompt(bool visible) { prompt.SetActive(visible); }
    public bool ValidateContent() => title.text == Title && paragraphs.Length == 2 && paragraphs[0].text == Chinese && paragraphs[1].text == English;
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
