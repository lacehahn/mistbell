using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Runtime UGUI controls; no authored scene, camera or art changes.</summary>
public sealed class PauseMenu : MonoBehaviour
{
    GameObject menu, resume, journal;
    Button notesEntry;
    public bool JournalOpen => journal != null && journal.activeSelf;
    public bool HasNotesEntry => notesEntry != null && notesEntry.GetComponentInChildren<Text>().text == "札记 Notes";
    Slider volume;
    Text volumeLabel;
    readonly Button[] languages = new Button[3];
    Font font;
    public void Configure(Font sharedFont, float initialVolume, string initialLanguage,
        Action<float> setVolume, Action<string> setLanguage, Action back)
    {
        font=sharedFont;
        var canvas=gameObject.AddComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=120;
        var scaler=gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        if (FindFirstObjectByType<EventSystem>()==null)
            new GameObject("Mistbell UI Events",typeof(EventSystem),typeof(StandaloneInputModule));
        font.RequestCharactersInTexture("检测到上次进度 · 按 C 继续 / 按 N 重新开始（3 秒后自动继续）设置 / Settings音量 Volume 0123456789%中文English双语返回游戏退出游戏",24,FontStyle.Normal);
        resume=Label(transform,"检测到上次进度 · 按 C 继续 / 按 N 重新开始（3 秒后自动继续）",new Vector2(1600,70),new Vector2(0,460)).gameObject;
        menu=Box(transform,new Vector2(640,520),Vector2.zero,new Color(.09f,.12f,.16f,.98f));
        Label(menu.transform,"设置 / Settings",new Vector2(560,48),new Vector2(0,210));
        volumeLabel=Label(menu.transform,"",new Vector2(560,48),new Vector2(0,110));
        var sliderGo=Box(menu.transform,new Vector2(500,32),new Vector2(0,55),new Color(.3f,.3f,.3f));
        volume=sliderGo.AddComponent<Slider>(); volume.minValue=0; volume.maxValue=1;
        var handle=Box(sliderGo.transform,new Vector2(24,40),Vector2.zero,new Color(.8f,.76f,.62f));
        volume.handleRect=handle.GetComponent<RectTransform>(); volume.targetGraphic=handle.GetComponent<Image>();
        volume.direction=Slider.Direction.LeftToRight; volume.SetValueWithoutNotify(initialVolume);
        volume.onValueChanged.AddListener(value=> { SetVolumeLabel(value); setVolume(value); });
        SetVolumeLabel(initialVolume);
        string[] names={"中文","English","双语"}, codes={"cn","en","both"};
        for(int i=0;i<3;i++)
        {
            int index=i;
            languages[i]=MakeButton(menu.transform,names[i],new Vector2((i-1)*180, -15),new Vector2(170,52),
                ()=>{ setLanguage(codes[index]); HighlightLanguage(codes[index]); });
        }
        HighlightLanguage(initialLanguage);
        MakeButton(menu.transform,"返回游戏",new Vector2(0,-95),new Vector2(520,55),back);
        MakeButton(menu.transform,"退出游戏",new Vector2(0,-225),new Vector2(520,55),()=>Application.Quit(0));
        notesEntry=MakeButton(menu.transform,"札记 Notes",new Vector2(0,-160),new Vector2(520,55),()=>ShowJournal());
        SetVisible(false,false);
    }
    void LateUpdate()
    {
        if (!resume.activeSelf && !menu.activeSelf && !JournalOpen) return;
        var labels=GetComponentsInChildren<Text>();
        var content=new System.Text.StringBuilder();
        foreach(var label in labels) content.Append(label.text);
        font.RequestCharactersInTexture(content.ToString(),24,FontStyle.Normal);
        foreach(var label in labels) { label.cachedTextGenerator.Invalidate(); label.SetVerticesDirty(); }
    }
    public void SetVisible(bool choosing, bool settings)
    {
        resume.SetActive(choosing); menu.SetActive(settings && !JournalOpen);
    }
    PoiData[] notePois;
    Func<string,bool> wasVisited;
    Func<string> noteLanguage;
    Action<PoiData> openNote;
    public void ConfigureJournal(PoiData[] data, Func<string,bool> read, Func<string> language, Action<PoiData> open)
    { notePois=data; wasVisited=read; noteLanguage=language; openNote=open; }
    public void ClickNotes() => notesEntry.onClick.Invoke();
    public void CloseJournal()
    { if(journal!=null) { journal.SetActive(false); journal.name="Closed Journal"; Destroy(journal); journal=null; } }
    public void ShowJournal()
    {
        CloseJournal(); menu.SetActive(false);
        journal=Box(transform,new Vector2(850,700),Vector2.zero,new Color(.09f,.12f,.16f,.98f));
        journal.name="Journal";
        Label(journal.transform,"札记 Notes",new Vector2(790,48),new Vector2(0,310));
        for(int i=0;i<notePois.Length;i++)
        {
            var data=notePois[i]; bool read=wasVisited(data.id); string lang=noteLanguage();
            string title=lang=="cn" ? data.titleCn : lang=="en" ? data.titleEn : data.titleCn+" / "+data.titleEn;
            var button=MakeButton(journal.transform,(read ? "已读 · "+title : "未读 · ？？？"),
                new Vector2(0,240-i*60),new Vector2(790,54),()=> { CloseJournal(); openNote(data); });
            button.name="Note_"+data.id; button.interactable=read;
            if(!read) button.GetComponentInChildren<Text>().color=new Color(.45f,.47f,.49f);
        }
        MakeButton(journal.transform,"返回设置",new Vector2(0,-290),new Vector2(790,54),()=> { CloseJournal(); menu.SetActive(true); });
    }
    void SetVolumeLabel(float value) => volumeLabel.text="音量 / Volume " + Mathf.RoundToInt(value*100) + "%";
    void HighlightLanguage(string language)
    {
        int selected=language=="cn" ? 0 : language=="en" ? 1 : 2;
        for(int i=0;i<languages.Length;i++) languages[i].GetComponent<Image>().color=
            i==selected ? new Color(.45f,.40f,.28f) : new Color(.2f,.24f,.29f);
    }
    public void SelectLanguage(string language) => languages[language=="cn"?0:language=="en"?1:2].onClick.Invoke();
    public void SetVolume(float value) => volume.value=value;
    Button MakeButton(Transform parent,string text,Vector2 position,Vector2 size,Action action)
    {
        var go=Box(parent,size,position,new Color(.2f,.24f,.29f));
        var button=go.AddComponent<Button>(); button.targetGraphic=go.GetComponent<Image>();
        button.onClick.AddListener(()=>action());
        Label(go.transform,text,size,Vector2.zero);
        return button;
    }
    GameObject Box(Transform parent,Vector2 size,Vector2 position,Color color)
    {
        var go=new GameObject("Menu Control",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
        go.transform.SetParent(parent,false);
        var r=go.GetComponent<RectTransform>(); r.sizeDelta=size; r.anchoredPosition=position;
        go.GetComponent<Image>().color=color;
        return go;
    }
    Text Label(Transform parent,string text,Vector2 size,Vector2 position)
    {
        var go=new GameObject("Menu Text",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));
        go.transform.SetParent(parent,false);
        var r=go.GetComponent<RectTransform>(); r.sizeDelta=size; r.anchoredPosition=position;
        var label=go.GetComponent<Text>(); label.font=font; label.fontSize=24; label.text=text;
        label.color=new Color(.94f,.92f,.84f); label.alignment=TextAnchor.MiddleCenter; label.raycastTarget=false;
        return label;
    }
}
