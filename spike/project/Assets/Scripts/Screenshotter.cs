using System;
using System.Linq;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 运行时截图器：让构建出的播放器自己渲染并落盘，供 AI 读图。
/// 用法：
///   单张：Mistbell.app/Contents/MacOS/Mistbell -shot /绝对路径/out.png
///   多机位：Mistbell.app/Contents/MacOS/Mistbell -shots /绝对路径/目录   （遍历场景里 ShotRig 的机位表）
/// 好处：走真实运行时渲染路径，Volume 后处理（Bloom/DoF/调色/暗角/颗粒）会生效。
/// </summary>
public class Screenshotter : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(Shot());
    }

    static string ArgValue(string flag)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag) return args[i + 1];
        }
        return null;
    }

    IEnumerator Shot()
    {
        string shotsDir = ArgValue("-shots");
        string outPath = ArgValue("-shot");
        if (string.IsNullOrEmpty(shotsDir) && string.IsNullOrEmpty(outPath)) yield break;   // 正常游玩不截屏

        // 预热：让窗口、相机、Volume、材质全部就绪。曾经只等 3 帧，
        // 结果同样构建两次截图亮度差 2 倍（整帧均一压暗，连天空都暗）——那是抓到了窗口刚合成/淡入中的帧。
        // 现在固定等 1.0 s 真实时间（与帧率无关），再逐机位各等 3 帧。
        yield return new WaitForSecondsRealtime(1.0f);

        var cam = Camera.main;
        var pipeline = GraphicsSettings.currentRenderPipeline;
        Debug.Log("[SHOT] pipeline = " + (pipeline != null ? pipeline.GetType().Name : "NULL"));
        if (cam != null)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            Debug.Log("[SHOT] camera=" + cam.name + " postProcess=" + (data != null ? data.renderPostProcessing.ToString() : "no-data") + " fov=" + cam.fieldOfView);
        }

        // 后处理兜底：资产里的 VolumeProfile 子组件在构建包里丢失时，运行时重建一份。
        // ⚠️ 必须保证「全局只有一个有效 Volume」：两个都带 ACES 时色调映射会被叠两次，
        // 画面整体压暗近一半，而且是否叠加取决于资产子组件这次有没有随包加载 —— 曝光标定全废。
        var vols = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
        foreach (var v in vols)
            Debug.Log("[SHOT] found volume " + v.name + " priority=" + v.priority + " enabled=" + v.enabled
                      + " comps=" + (v.profile != null ? v.profile.components.Count.ToString() : "null"));

        Volume vol = null;
        foreach (var v in vols)
            if (v.profile != null && v.profile.components.Count > 0) { vol = v; break; }

        if (vol == null)
        {
            var go = new GameObject("RuntimeVolume");
            vol = go.AddComponent<Volume>();
            vol.priority = 2f;
            vol.profile = BuildRuntimeProfile();
            Debug.Log("[SHOT] runtime volume built, components=" + vol.profile.components.Count);
        }
        else
        {
            Debug.Log("[SHOT] using scene volume=" + vol.name + " components=" + vol.profile.components.Count);
        }

        // 其余 Volume 一律停用，避免重复色调映射
        foreach (var v in vols)
            if (v != vol) v.enabled = false;

        vol.isGlobal = true;
        vol.weight = 1f;

        if (!string.IsNullOrEmpty(shotsDir))
        {
            Directory.CreateDirectory(shotsDir);
            var rig = UnityEngine.Object.FindFirstObjectByType<ShotRig>();
            if (rig == null || rig.poses.Count == 0)
            {
                Debug.LogError("[SHOT] 场景里没有 ShotRig 或机位表为空，退回单张 -shot");
            }
            else
            {
                for (int i = 0; i < rig.poses.Count; i++)
                {
                    rig.Apply(i);
                    var p = rig.poses[i];
                    // 按该机位的视线中心对焦：远景机位不该被前景虚化规则坑到（反之亦然）
                    var camNow = Camera.main;
                    var dofComp = vol.profile != null ? vol.profile.components.OfType<DepthOfField>().FirstOrDefault() : null;
                    if (dofComp != null && camNow != null)
                    {
                        // art_* 是用来判断材质/构图的参考图，必须全幅清晰；景深只在玩法视角里生效。
                        bool referenceShot = p.name.StartsWith("art_", System.StringComparison.Ordinal);
                        dofComp.active = !referenceShot;
                        if (!referenceShot)
                        {
                            float dist = 25f;
                            if (Physics.Raycast(camNow.transform.position, camNow.transform.forward, out var focusHit, 600f))
                                dist = focusHit.distance;
                            dist = Mathf.Clamp(dist, 5f, 400f);
                            dofComp.focusDistance.Override(dist);
                            dofComp.gaussianStart.Override(Mathf.Max(2f, dist * 0.55f));
                            dofComp.gaussianEnd.Override(dist * 3.2f);
                        }
                    }
                    yield return null;
                    yield return null;
                    yield return null;
                    var c = Camera.main;
                    Debug.Log(string.Format("[SHOT] pose[{0}] {1} pos=({2:F1},{3:F1},{4:F1}) fov={5:F1}",
                        i, p.name, c.transform.position.x, c.transform.position.y, c.transform.position.z, c.fieldOfView));
                    var pilgrim = UnityEngine.Object.FindFirstObjectByType<PilgrimAnimator>();
                    if (pilgrim != null) pilgrim.ReportMeasurements(rig);
                    if(i==0)
                    {
                        var probe=UnityEngine.Object.FindFirstObjectByType<ForegroundDetailProbe>();
                        if(probe!=null)yield return probe.Measure();
                    }
                    var file = Path.Combine(shotsDir, p.name + ".png");
                    if (File.Exists(file)) File.Delete(file);
                    ScreenCapture.CaptureScreenshot(file);
                    yield return new WaitForSecondsRealtime(0.4f);
                    Debug.Log("[SHOT] written " + file + " exists=" + File.Exists(file) + " bytes=" + (File.Exists(file) ? new FileInfo(file).Length : 0));
                    if(p.name=="art_close" && (Array.IndexOf(Environment.GetCommandLineArgs(),"-style3-family")>=0 || Array.IndexOf(Environment.GetCommandLineArgs(),"-style4-family")>=0))
                        yield return CaptureStoneFamily(shotsDir,file);

                }
                Application.Quit();
                yield break;
            }
        }

        if (string.IsNullOrEmpty(outPath)) { Application.Quit(); yield break; }
        var dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        ScreenCapture.CaptureScreenshot(outPath);
        yield return new WaitForSecondsRealtime(0.4f);
        Debug.Log("[SHOT] written " + outPath + " exists=" + File.Exists(outPath));

        yield return null;
        Application.Quit();
    }

    // Opt-in measurement only: same wall, camera, lighting and post stack, changing only stone.
    // No scene assets or gameplay state are written. All renderer bindings are restored.
    IEnumerator CaptureStoneFamily(string dir,string churchShot)
    {
        var renderers=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        Material sub=null;
        foreach(var r in renderers) foreach(var m in r.sharedMaterials)
            if(m!=null && (m.name=="Stone_Sub" || m.name=="Stone_Sub_Coping") && sub==null)
            {
                sub=new Material(m); sub.SetVector("_ForgeCopingPlane",Vector4.zero);
            }
        if(sub==null) {Debug.LogError("[STYLE4_CAPTURE] Stone_Sub missing");yield break;}
        File.Copy(churchShot,Path.Combine(dir,"style4_family_church.png"),true);
        var originals=new System.Collections.Generic.Dictionary<MeshRenderer,Material[]>();
        try
        {
            foreach(var r in renderers)
            {
                var original=r.sharedMaterials; var changed=(Material[])original.Clone(); bool swap=false;
                for(int i=0;i<changed.Length;i++) if(changed[i]!=null && changed[i].name=="M_ChurchStone")
                {changed[i]=sub;swap=true;}
                if(swap) {originals[r]=original;r.sharedMaterials=changed;}
            }
            if(originals.Count==0) {Debug.LogError("[STYLE4_CAPTURE] church wall missing");yield break;}
            yield return null; yield return null; yield return null;
            string path=Path.Combine(dir,"style4_family_sub.png");
            if(File.Exists(path))File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(.4f);
            Debug.Log("[STYLE4_CAPTURE] same_wall=True swapped_renderers="+originals.Count+" file="+path);
        }
        finally {foreach(var pair in originals)pair.Key.sharedMaterials=pair.Value; Destroy(sub);}
    }

    /// <summary>运行时构建 HD-2D 后期栈：ACES + Bloom + Bokeh 移轴感 + 调色 + 暗角 + 颗粒。</summary>
    static VolumeProfile BuildRuntimeProfile()
    {
        var p = ScriptableObject.CreateInstance<VolumeProfile>();

        var tonemap = p.Add<Tonemapping>(true);
        tonemap.mode.Override(TonemappingMode.ACES);

        var bloom = p.Add<Bloom>(true);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(1.3f);
        bloom.scatter.Override(0.72f);
        bloom.tint.Override(new Color(1f, 0.95f, 0.87f));

        var dof = p.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(38f);
        dof.aperture.Override(5.6f);
        dof.focalLength.Override(70f);
        dof.bladeCount.Override(6);

        var color = p.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.4f);
        color.contrast.Override(14f);
        color.saturation.Override(-6f);
        color.colorFilter.Override(new Color(1f, 0.96f, 0.90f));

        var white = p.Add<WhiteBalance>(true);
        white.temperature.Override(12f);

        var vig = p.Add<Vignette>(true);
        vig.intensity.Override(0.32f);
        vig.smoothness.Override(0.45f);

        var grain = p.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin2);
        grain.intensity.Override(0.22f);
        grain.response.Override(0.8f);

        return p;
    }
}
