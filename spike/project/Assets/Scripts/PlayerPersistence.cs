using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public sealed partial class PlayerController
{
    [Serializable] public class SaveData
    {
        public Vector3 position;
        public float rotationY;
        public bool insideInterior, doorOpen;
        public string[] visitedPois;
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
    readonly HashSet<string> visited = new HashSet<string>();
    string SavePath => Path.Combine(Application.persistentDataPath, "mistbell_save.json");
    string SettingsPath => Path.Combine(Application.persistentDataPath, "mistbell_settings.json");
    SaveData pendingSave;
    bool choosingContinue, settingsOpen, terraceSaved, interiorSaved, legacyDemo;
    float continueDeadline;

    void InitializePersistence(string[] args)
    {
        legacyDemo = Array.Exists(args, a => a.StartsWith("-demo-", StringComparison.Ordinal)) &&
            !Array.Exists(args, a => a == "-demo-save" || a == "-demo-load" || a == "-demo-settings");
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
        panel.OnClosed = data => { if (data != null) { visited.Add(data.id); WriteSave(); } };
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
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath, Encoding.UTF8));
            if (data == null || data.version != 1 || data.visitedPois == null ||
                !float.IsFinite(data.position.x) || !float.IsFinite(data.position.y) || !float.IsFinite(data.position.z) || !float.IsFinite(data.rotationY))
                throw new InvalidDataException("invalid save version/fields");
            return data;
        }
        catch (Exception e) { Debug.LogWarning("[SAVE] read failed: " + e.Message); return null; }
    }
    static void WriteJson(string path, object data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
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
            visitedPois=ids.ToArray(), savedAtUnix=DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        try
        {
            WriteJson(SavePath, data);
            Debug.Log("[SAVE] path=" + SavePath + " wrote=true keys=position,rotationY,insideInterior,doorOpen,visitedPois");
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
                visited.Clear(); terraceSaved=interiorSaved=false;
            }
            else if (Input.GetKeyDown(KeyCode.C) || Time.unscaledTime>=continueDeadline) RestoreSave(pendingSave);
            return true;
        }
        if (Input.GetKeyDown(KeyCode.Escape) && !panel.IsOpen && panel.ClosedFrame!=Time.frameCount)
        { settingsOpen=!settingsOpen; velocity=Vector3.zero; panel.SetPrompt(false); }
        pauseMenu.SetVisible(false,settingsOpen);
        return settingsOpen;
    }
    bool StartPersistenceDemo(string[] args)
    {
        if (Array.IndexOf(args,"-demo-save")>=0) StartCoroutine(DemoSave());
        else if (Array.IndexOf(args,"-demo-load")>=0) StartCoroutine(DemoLoad());
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
        visited.Clear();
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
        bool pass=delta<=.05f && visited.SetEquals(data.visitedPois) &&
            Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y,data.rotationY))<=.05f &&
            interior.Contains(transform.position)==data.insideInterior &&
            (interior.OpenDegrees>=95)==(data.doorOpen||data.insideInterior);
        yield return new WaitForEndOfFrame(); InteriorShot("load");
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
