只做一件事：把远山**从"黑墙"拉回"明亮的雪山"**，并消除层与层之间的**竖向接缝**。保留上一轮加的岩理/雪沟/林线结构。

必读：`Shots/art_runtime.png`（现状：山近乎全黑，能看到 x≈230/470/700/880 处的硬竖缝）、`MountainBackdrop.cs`。

【问题】
1) 岩色起手太暗（#3E4A58 / #4E5A68 / #5F6B78），叠上林线与雪沟后整片山读成黑墙，`dark_ratio_lower` 0.031→**0.137**。
2) 四层 Quad 横向拼接处有**硬竖缝**（每层宽度固定、边缘无羽化）。

【要改】
1) **抬亮岩色**：#5A6878 / #6B7888 / #7C8896（比现在各亮约 +0.10 明度），并保证"受光面 / 背光面"明度差仍在 0.06–0.10。
2) **雪帽提亮**：雪色 #F2F7FB、雪帽下缘过渡 #C9D7E5 保留。
3) **林线收敛**：只在 L1 底部 **0–7%** 高度、透明度 0.45（现在 10%、更实），L2 降到 0.2，L3/L4 不画。
4) **消除竖缝**：每层 Quad 横向**多铺 12%**（overscan），并在左右两端做 **8% 宽 alpha 羽化**；相邻层之间的横向相位错开 ≥150 px，避免接缝对齐。
5) **整体再降高 8%**，让天空露出更多（现在山填满了上半屏）。

【自检打印】
"[MTN11] rock_tints=#5A6878/#6B7888/#7C8896 lit_shadow_delta=<f> treeline_pct=0.07 treeline_alpha=0.45 overscan=0.12 feather=0.08 seam_max_grad=<f> height_scale=<f>"
"[MTN11] mountain_luma=<f>"

【必须自己做的测量（用截图）】
- `mountain_luma`：画面 y ∈ [0.10, 0.45]、x ∈ [0.05, 0.95] 区域的平均亮度 → 目标 **0.42–0.60**（现在约 0.22）
- `seam_max_grad`：在该区域内沿 x 方向求逐列平均亮度的相邻差，取 99 分位 → 目标 **≤ 0.035**（硬缝会显著更高）
硬门槛：`mountain_luma ∈ [0.42, 0.60]`、`seam_max_grad ≤ 0.035`、`dark_ratio_lower ≤ 0.10`、`magenta_px = 0`。

【必须自己跑】
`capture_runtime.sh`（重出三张）、`-demo-reveal`（gate=pass 保持）、`-demo-cinema`、`-demo-enter-church`。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`measure_shot.py`。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改教堂/室内/玩法/相机/measure_shot.py。
