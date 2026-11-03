using System.Collections;
using Unity.Profiling;
using UnityEngine;

// Screenshot-only instrumentation. No camera, movement or lighting changes.
public sealed class ForegroundDetailProbe : MonoBehaviour
{
    public IEnumerator Measure()
    {
        using(var draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1))
        {
            if(!draws.Valid) { Debug.Log("[FGD_RENDER] counters=unavailable (use PerformanceBuild.Run)");yield break; }
            int sync=QualitySettings.vSyncCount,target=Application.targetFrameRate;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            var renderers=GetComponentsInChildren<MeshRenderer>();
            double on=0,off=0,seconds=0;
            for(int i=0;i<90;i++)yield return null;
            for(int i=0;i<120;i++) {yield return null;on+=draws.LastValue;seconds+=Time.unscaledDeltaTime;}
            foreach(var r in renderers)r.enabled=false;
            for(int i=0;i<6;i++)yield return null;
            for(int i=0;i<120;i++) {yield return null;off+=draws.LastValue;}
            foreach(var r in renderers)r.enabled=true;
            for(int i=0;i<6;i++)yield return null;
            Debug.Log(System.FormattableString.Invariant($"[FGD_RENDER] new_drawcalls={System.Math.Round((on-off)/120)} drawcalls={on/120:F2} fps={120/seconds:F2} samples=120 resolution={Screen.width}x{Screen.height}"));
            QualitySettings.vSyncCount=sync;Application.targetFrameRate=target;
        }
    }
}
