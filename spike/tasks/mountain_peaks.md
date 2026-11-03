只做一件事：把远山的**山顶从"平台"改成"尖峰"**（当前 `Shots/art_runtime.png`：L2/L3 的雪线是一整条平的白色台面，峰顶有 20–40 px 的直段，像被削平的山）。不改教堂/玩法/相机/天空/地平线雾/雪沟。

必读：`Shots/art_runtime.png`（每层山脊线的峰顶）、`MountainBackdrop.cs` 里生成 `ridge[]` 与雪帽的那段。

【要改】
1) **削平段**：峰顶不允许出现长度 > **12 px** 的近似水平段（|Δy| ≤ 1 px 连续像素）。当前有 20–40 px。
2) **尖峰**：每个主峰顶端做 6–14 px 的收尖（两侧坡度差 ≥ 0.35），并允许每座山体带 **1–2 个次峰**（高度为主峰 62–85%）。
3) **峰数**：L1/L2/L3/L4 分别为 **≥12 / ≥13 / ≥14 / ≥15** 个可辨峰。
4) **雪帽跟脊**：雪/岩分界线必须贴合脊线（分界与脊线的高差 ≤ 3 px），不要出现"整片平雪盖在山顶"。
5) 抖动/大气透视/雪沟规则都不动。

【自检打印】
"[PEAK] flat_top_px_max=<n> peaks=12/13/14/15 summit_slope_delta_min=<f> sub_peaks=<n> snow_cap_offset_max=<f>"
硬门槛：`flat_top_px_max ≤ 12`、各层峰数达标、`summit_slope_delta_min ≥ 0.35`、`snow_cap_offset_max ≤ 3px`、`mountain_luma 0.42–0.60`、`seam_max_grad ≤ 0.035`、`magenta_px=0`。

【必须自己跑】
`capture_runtime.sh`（3 张）、`-demo-cinema`、`-demo-reveal`（gate=pass）。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-enter-church`、`-demo-journal`、`-demo-trail 10`、`measure_shot.py`。
⚠️ 演示一律用 `bash spike/tools/run_demos.sh "<参数>"`（内含 caffeinate + `open -n -a`）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改教堂/玩法/相机/天空/地平线雾/雪沟/measure_shot.py。
