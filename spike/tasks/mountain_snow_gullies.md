只做一件事：山体上那些**白色折线"沟"**太假了（当前 `Shots/art_runtime.png`：岩面上有 3–4 条白色 Z 字形细线一路折到山腰，像随手画的铅笔线）。改成"山脊下方的短雪沟"。不改教堂/玩法/相机/天空/地平线雾。

必读：`Shots/art_runtime.png` 的岩面（L1/L2 两层最明显）、`MountainBackdrop.cs` 里生成白色雪沟/雪带的那段（`snow` 与岩面混合处）。

【要改】
1) **长度**：雪沟最下延到岩面的 **35%** 高度就淡出（现在一直折到 60–70%）。
2) **形状**：只允许**单折**（1 次方向反转）；`gully_reversals ≤ 1`。当前 Z 字形是 2–3 次反转。
3) **宽度**：沟宽 3–7 px，边缘 1 px 软化；不要超过 8 px。
4) **数量**：每个峰面最多 **2 条**；只在**阴面**（不受光那侧）出现，亮面只有雪帽。
5) **亮度**：雪沟亮度不要纯白，用 `#DCE8F2`，且在渐隐段向岩色过渡（不要硬断）。

【自检打印】
"[GULLY] count_l1=<n> count_l2=<n> max_len_pct=<f> max_reversals=<n> width_px=<min>-<max> side=shadow_only"
硬门槛：`max_len_pct ≤ 35`、`max_reversals ≤ 1`、`width_px` 在 3–7、`mountain_luma 0.42–0.60`、`seam_max_grad ≤ 0.035`、`magenta_px=0`。

【必须自己跑】
`capture_runtime.sh`（3 张）、`-demo-cinema`、`-demo-reveal`（gate=pass）。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-enter-church`、`-demo-journal`、`-demo-trail 10`、`measure_shot.py`。
⚠️ 演示一律用 `bash spike/tools/run_demos.sh "<参数>"`（内含 caffeinate + `open -n -a`）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改教堂/玩法/相机/天空/地平线雾/measure_shot.py。
