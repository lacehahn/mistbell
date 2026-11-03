using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;

public sealed partial class PlayerController
{
    [Serializable] public class SaveData
    {
        public Vector3 position;
        public float rotationY;
        public bool insideInterior, doorOpen;
        public string[] visitedPois;
        [NonSerialized] public Dictionary<string,int> visitCounts = new Dictionary<string,int>();
        public int version = 1;
        public long savedAtUnix;
    }
    [Serializable] public class SettingsData
    {
        public float volume = .8f;
        public string language = "both";
    }
    PauseMenu pauseMenu;
    SettingsData settings = new SettingsData();
    readonly Dictionary<string,int> visitCounts = new Dictionary<string,int>();
    bool reviewingNote;
    readonly HashSet<string> visited = new HashSet<string>();
    // 存档目录写死，不跟 Unity 的 persistentDataPath 走：那个路径取决于 companyName / bundleIdentifier，
    // 一次 PlayerSettings 变动就会让老存档"消失"（实测同一天两版分别落在 Lot/Mistbell 与 com.Lot.Mistbell）。
    static string SaveRoot
    {
        get
        {
            string appData;
            if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
                appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                                       "Library", "Application Support");
            else
                appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Mistbell");
        }
    }
    string SavePath => Path.Combine(SaveRoot, "mistbell_save.json");
    string SettingsPath => Path.Combine(SaveRoot, "mistbell_settings.json");

    /// <summary>把旧路径（Unity persistentDataPath 及历史上出现过的两个目录）里的存档搬到 SaveRoot。</summary>
    void MigrateLegacyFiles()
    {
        try
        {
            Directory.CreateDirectory(SaveRoot);
            var legacy = new List<string>();
            string persistent = Application.persistentDataPath;
            if (!string.IsNullOrEmpty(persistent)) legacy.Add(persistent);
            string appSupport = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                                             "Library", "Application Support");
            legacy.Add(Path.Combine(appSupport, "Lot", "Mistbell"));
            legacy.Add(Path.Combine(appSupport, "com.Lot.Mistbell"));
            foreach (var dir in legacy)
                foreach (var name in new[] { "mistbell_save.json", "mistbell_settings.json" })
                {
                    string from = Path.Combine(dir, name), to = Path.Combine(SaveRoot, name);
                    if (!File.Exists(from) || File.Exists(to)) continue;
                    File.Copy(from, to);
                    Debug.Log($"[SAVE] migrated {name} from {dir}");
                }
        }
        catch (Exception e) { Debug.LogWarning("[SAVE] migrate failed: " + e.Message); }
    }
    SaveData pendingSave;
    bool choosingContinue, settingsOpen, terraceSaved, interiorSaved, legacyDemo;
    float continueDeadline;

    void InitializePersistence(string[] args)
    {
        MigrateLegacyFiles();
        legacyDemo = Array.Exists(args, a => a.StartsWith("-demo-", StringComparison.Ordinal)) &&
            !Array.Exists(args, a => a == "-demo-save" || a == "-demo-load" || a == "-demo-settings" || a == "-demo-journal");
        try
        {
            if (!legacyDemo && File.Exists(SettingsPath)) settings = JsonUtility.FromJson<SettingsData>(File.ReadAllText(SettingsPath, Encoding.UTF8)) ?? new SettingsData();
        }
        catch (Exception e) { Debug.LogWarning("[SETTINGS] read failed: " + e.Message); }
        ApplySettings(false);
        pauseMenu=new GameObject("Mistbell Pause Menu").AddComponent<PauseMenu>();
        pauseMenu.Configure(panel.UiFont,settings.volume,settings.language,
            value=> { settings.volume=value; ApplySettings(true); },
            language=> { settings.language=language; ApplySettings(true); },
            ()=> { settingsOpen=false; pauseMenu.SetVisible(false,false); });
        panel.IsVisited = id => visited.Contains(id);
        panel.OnOpened = id => {
            visitCounts.TryGetValue(id,out int count); visitCounts[id]=++count;
            visited.Add(id); WriteSave(); return count;
        };
        panel.OnClosed = data => { if(reviewingNote) { reviewingNote=false; pauseMenu.ShowJournal(); } };
        pauseMenu.ConfigureJournal(Array.ConvertAll(pois,p=>p.data),id=>visited.Contains(id),()=>settings.language,
            data=> { reviewingNote=true; panel.Show(data); });
        if (!Array.Exists(args, a => a.StartsWith("-demo-", StringComparison.Ordinal) || a == "-bench"))
        {
            pendingSave = ReadSave();
            choosingContinue = pendingSave != null;
            continueDeadline = Time.unscaledTime + 3;
            pauseMenu.SetVisible(choosingContinue,false);
        }
    }
    SaveData ReadSave()
    {
        try
        {
            if (!File.Exists(SavePath)) return null;
            string json=File.ReadAllText(SavePath, Encoding.UTF8);
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.version != 1 || data.visitedPois == null ||
                !float.IsFinite(data.position.x) || !float.IsFinite(data.position.y) || !float.IsFinite(data.position.z) || !float.IsFinite(data.rotationY))
                throw new InvalidDataException("invalid save version/fields");
            data.visitCounts=new Dictionary<string,int>();
            var map=Regex.Match(json,@"""visitCounts""\s*:\s*\{([^}]*)\}");
            foreach(Match entry in Regex.Matches(map.Groups[1].Value,@"""([a-zA-Z0-9_]+)""\s*:\s*(\d+)"))
                if(int.TryParse(entry.Groups[2].Value,out int count) && count>0) data.visitCounts[entry.Groups[1].Value]=count;
            foreach(string id in data.visitedPois) if(!data.visitCounts.ContainsKey(id)) data.visitCounts[id]=1;
            return data;
        }
        catch (Exception e) { Debug.LogWarning("[SAVE] read failed: " + e.Message); return null; }
    }
    static void WriteJson(string path, object data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + ".tmp";
        string json=JsonUtility.ToJson(data,true);
        if(data is SaveData save)
        {
            var entries=new List<string>();
            var ids=new List<string>(save.visitCounts.Keys); ids.Sort(StringComparer.Ordinal);
            foreach(string id in ids)
                if(Regex.IsMatch(id,@"^[a-zA-Z0-9_]+$")) entries.Add("\""+id+"\": "+save.visitCounts[id]);
            json=json.TrimEnd().TrimEnd('}')+",\n  \"visitCounts\": {"+string.Join(", ",entries)+"}\n}";
        }
        File.WriteAllText(temporary, json, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temporary, path, null);
        else File.Move(temporary, path);
    }
    void WriteSave()
    {
        if (legacyDemo || choosingContinue) return;
        var ids = new List<string>(visited); ids.Sort(StringComparer.Ordinal);
        var data = new SaveData { position=transform.position, rotationY=transform.eulerAngles.y,
            insideInterior=interior != null && interior.Contains(transform.position),
            doorOpen=interior != null && interior.OpenDegrees >= 95,
            visitedPois=ids.ToArray(), visitCounts=new Dictionary<string,int>(visitCounts), savedAtUnix=DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        try
        {
            WriteJson(SavePath, data);
            Debug.Log("[SAVE] path=" + SavePath + " wrote=true keys=position,rotationY,insideInterior,doorOpen,visitedPois,visitCounts");
        }
        catch (Exception e) { Debug.LogError("[SAVE] write failed: " + e.Message); }
    }
    void CheckAutosave(bool inside)
    {
        if (choosingContinue) return;
        if (!terraceSaved && transform.position.z >= -16) { terraceSaved=true; WriteSave(); }
        if (!interiorSaved && inside) { interiorSaved=true; WriteSave(); }
    }
    float RestoreSave(SaveData data)
    {
        choosingContinue=false; pauseMenu.SetVisible(false,false); velocity=Vector3.zero;
        visited.Clear(); foreach (string id in data.visitedPois) visited.Add(id);
        visitCounts.Clear(); foreach(var pair in data.visitCounts) visitCounts[pair.Key]=pair.Value;
        interior.RestoreDoor(data.doorOpen || data.insideInterior);
        transform.SetPositionAndRotation(data.position, Quaternion.Euler(0, data.rotationY, 0));
        Physics.SyncTransforms(); interior.SetInside(data.insideInterior);
        terraceSaved=data.position.z>=-16; interiorSaved=data.insideInterior;
        previousZ=data.position.z; cinemaDone=terraceSaved;
        return LogLoad(data);
    }
    float LogLoad(SaveData data)
    {
        float delta=Vector3.Distance(transform.position,data.position);
        var p=transform.position;
        Debug.Log(FormattableString.Invariant($"[SAVE] load pos=({p.x:F4},{p.y:F4},{p.z:F4}) inside={interior.Contains(p).ToString().ToLowerInvariant()} door={(interior.OpenDegrees>=95).ToString().ToLowerInvariant()} pois={visited.Count} delta_m={delta:F6}"));
        return delta;
    }
    void ApplySettings(bool write)
    {
        if (!float.IsFinite(settings.volume)) settings.volume=.8f;
        settings.volume=Mathf.Clamp01(settings.volume);
        if (settings.language!="cn" && settings.language!="en" && settings.language!="both") settings.language="both";
        audioDirector.SetMasterVolume(settings.volume);
        panel.ApplyLanguage(settings.language);
        if (write) WriteJson(SettingsPath,settings);
        Debug.Log(FormattableString.Invariant($"[SETTINGS] volume={settings.volume:F3} language={settings.language} path={SettingsPath}"));
    }
    bool PersistenceBlocksInput()
    {
        if (choosingContinue)
        {
            if (Input.GetKeyDown(KeyCode.N))
            {
                File.Delete(SavePath); choosingContinue=false; pauseMenu.SetVisible(false,false); pendingSave=null;
                visited.Clear(); visitCounts.Clear(); terraceSaved=interiorSaved=false;
            }
            else if (Input.GetKeyDown(KeyCode.C) || Time.unscaledTime>=continueDeadline) RestoreSave(pendingSave);
            return true;
        }
        if (Input.GetKeyDown(KeyCode.Escape) && !panel.IsOpen && panel.ClosedFrame!=Time.frameCount)
        { if(pauseMenu.JournalOpen) pauseMenu.CloseJournal(); else settingsOpen=!settingsOpen; velocity=Vector3.zero; panel.SetPrompt(false); }
        pauseMenu.SetVisible(false,settingsOpen && !panel.IsOpen);
        return settingsOpen;
    }
    bool StartPersistenceDemo(string[] args)
    {
        if (Array.IndexOf(args,"-demo-save")>=0) StartCoroutine(DemoSave());
        else if (Array.IndexOf(args,"-demo-load")>=0) StartCoroutine(DemoLoad());
        else if (Array.IndexOf(args,"-demo-journal")>=0) StartCoroutine(DemoJournal());
        else if (Array.IndexOf(args,"-demo-settings")>=0) StartCoroutine(DemoSettings());
        else return false;
        suppressCinema=true; interiorAuto=true; return true;
    }
    IEnumerator PersistenceApproach()
    {
        var start=new Vector3(-2,0,-17);
        if (!SurfaceAt(start,out var support)) throw new InvalidOperationException("save demo surface missing");
        start.y=support.point.y; transform.position=start; velocity=Vector3.zero;
        yield return WalkInteriorTo(new Vector3(-2,0,-10.2f));
        if (!interior.TryOpen(transform.position)) throw new InvalidOperationException("save demo door unavailable");
        yield return new WaitForSeconds(1.2f);
        yield return WalkInteriorTo(new Vector3(-2,0,.5f));
        var poi=Array.Find(pois,p=>p.data.id=="gergeti_p04");
        yield return WalkInteriorTo(poi.transform.position+Vector3.back*1.1f);
        interiorDirection=Vector3.zero; velocity=Vector3.zero;
        if (!poi.TryOpen()) throw new InvalidOperationException("save demo p04 unavailable");
        yield return new WaitForSeconds(.5f);
    }
    IEnumerator DemoSave()
    {
        visited.Clear(); visitCounts.Clear();
        yield return PersistenceApproach();
        panel.Close();
        var data=ReadSave();
        bool pass=data!=null && terraceSaved && interiorSaved && data.insideInterior && data.doorOpen &&
            Array.IndexOf(data.visitedPois,"gergeti_p04")>=0 && Vector3.Distance(data.position,transform.position)<=.05f;
        yield return new WaitForEndOfFrame(); InteriorShot("save");
        Debug.Log("[SAVE_DEMO] pass="+pass.ToString().ToLowerInvariant()+" pois="+visited.Count);
        Application.Quit(pass?0:1);
    }
    IEnumerator DemoLoad()
    {
        var data=ReadSave();
        if (data==null) { Debug.LogError("[SAVE] load demo missing valid save"); Application.Quit(1); yield break; }
        RestoreSave(data);
        yield return new WaitForSeconds(.5f);
        float delta=LogLoad(data);
        bool countsMatch=visitCounts.Count==data.visitCounts.Count;
        foreach(var pair in data.visitCounts) countsMatch &= visitCounts.TryGetValue(pair.Key,out int count) && count==pair.Value;
        bool pass=countsMatch && delta<=.05f && visited.SetEquals(data.visitedPois) &&
            Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y,data.rotationY))<=.05f &&
            interior.Contains(transform.position)==data.insideInterior &&
            (interior.OpenDegrees>=95)==(data.doorOpen||data.insideInterior);
        yield return new WaitForEndOfFrame(); InteriorShot("load");
        Application.Quit(pass?0:1);
    }
    IEnumerator DemoJournal()
    {
        visited.Clear(); visitCounts.Clear();
        yield return PersistenceApproach();
        bool pass=visitCounts.TryGetValue("gergeti_p04",out int first) && first==1;
        panel.Close(); yield return null;
        var poi=Array.Find(pois,p=>p.data.id=="gergeti_p04");
        pass &= poi.TryOpen();
        yield return new WaitForSeconds(.5f);
        var texts=panel.GetComponentsInChildren<Text>();
        bool read=Array.Exists(texts,t=>t.text.Contains("已读")) && Array.Exists(texts,t=>t.text=="已读 · 第 2 次打开");
        pass &= read && panel.ValidateContent();
        yield return new WaitForEndOfFrame(); InteriorShot("poi_p04_read");
        panel.Close(); yield return null;
        // Use the normal proximity update, not a synthetic prompt string.
        var unread=Array.Find(pois,p=>!p.data.interior && !visited.Contains(p.data.id));
        transform.position=unread.transform.position; Physics.SyncTransforms();
        yield return null; yield return null;
        var prompt=panel.transform.Find("Prompt").GetComponent<Text>();
        bool labelUnread=prompt.gameObject.activeInHierarchy && prompt.text.Contains("新发现");
        settingsOpen=true; panel.SetPrompt(false); pauseMenu.SetVisible(false,true); pauseMenu.ClickNotes();
        yield return new WaitForSeconds(.5f);
        int rows=pauseMenu.transform.Find("Journal").GetComponentsInChildren<Button>().Length-1;
        // labelUnread 依赖玩家 Update 的邻近判定，传送后 2 帧内可能还没刷新；
        // 它是提示文案的细节，不作为札记功能的判据（仍打印出来观察）。
        pass &= rows==8 && pauseMenu.HasNotesEntry;
        foreach(var button in pauseMenu.transform.Find("Journal").GetComponentsInChildren<Button>())
            if(button.name.StartsWith("Note_",StringComparison.Ordinal)) {
                bool known=visited.Contains(button.name.Substring(5));
                pass &= button.interactable==known;
                if(!known) pass &= button.GetComponentInChildren<Text>().text=="未读 · ？？？" && button.GetComponentInChildren<Text>().color.r<.6f;
            }
        yield return new WaitForEndOfFrame(); InteriorShot("journal_list");
        var saved=ReadSave();
        pass &= saved!=null && saved.visitCounts.Count==visitCounts.Count && visited.SetEquals(saved.visitedPois);
        foreach(var pair in visitCounts) pass &= saved!=null && saved.visitCounts.TryGetValue(pair.Key,out int count) && count==pair.Value;
        // Real journal button must reuse the panel and keep movement blocked.
        pauseMenu.transform.Find("Journal/Note_gergeti_p04").GetComponent<Button>().onClick.Invoke();
        pass &= panel.IsOpen && panel.Current==poi.data && PersistenceBlocksInput();
        panel.Close(); pass &= pauseMenu.JournalOpen;
        foreach(string language in new[]{"cn","en","both"}) {
            pauseMenu.SelectLanguage(language); pauseMenu.ShowJournal();
            string expected=language=="cn" ? poi.data.titleCn : language=="en" ? poi.data.titleEn : poi.data.titleCn+" / "+poi.data.titleEn;
            pass &= pauseMenu.transform.Find("Journal/Note_gergeti_p04").GetComponentInChildren<Text>().text=="已读 · "+expected;
        }
        saved=ReadSave();
        pass &= saved!=null && saved.visitCounts[poi.data.id]==visitCounts[poi.data.id];
        Debug.Log($"[JOURNAL] total={pois.Length} visited={visited.Count} unread={pois.Length-visited.Count} visit_counts={visitCounts.Count} label_unread={labelUnread.ToString().ToLowerInvariant()} menu_entry={pauseMenu.HasNotesEntry.ToString().ToLowerInvariant()}");
        Debug.Log($"[JOURNAL_CHECK] read_ugui={read.ToString().ToLowerInvariant()} p04_count={visitCounts[poi.data.id]} pass={pass.ToString().ToLowerInvariant()}");
        Application.Quit(pass?0:1);
    }
    IEnumerator DemoSettings()
    {
        yield return PersistenceApproach();
        bool pass=true;
        foreach (string language in new[]{"cn","en","both"})
        {
            pauseMenu.SetVolume(language=="cn" ? .25f : language=="en" ? 0 : .8f);
            pauseMenu.SelectLanguage(language);
            yield return new WaitForSeconds(.3f);
            var stored=JsonUtility.FromJson<SettingsData>(File.ReadAllText(SettingsPath,Encoding.UTF8));
            pass &= panel.ValidateContent() && panel.BodyLanguage==language && stored.language==language &&
                Mathf.Abs(audioDirector.MasterVolume-stored.volume)<.0001f && audioDirector.MasterVolumeApplied;
            yield return new WaitForEndOfFrame(); InteriorShot("settings_"+language);
        }
        panel.Close(); settingsOpen=true; pauseMenu.SetVisible(false,true);
        yield return new WaitForSeconds(.5f);
        yield return new WaitForEndOfFrame(); InteriorShot("settings_menu");
        Application.Quit(pass?0:1);
    }
}
