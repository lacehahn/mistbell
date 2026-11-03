只做一件事：修**远山山脚那条水平硬切线**和**几条 1–2 px 的竖向杂线**。不改教堂/玩法/相机/室内。

必读：`Shots/art_runtime.png`（山脚在 y≈0.63 处有一条笔直的水平边，山像放在一块平板上；画面左侧 x≈40 与中部 x≈520 各有一条细竖线）、`MountainBackdrop.cs` 的 `Paint(...)` 与 `streaks`。

【要改】
1) **山脚消隐**：每层贴图**底边 18%** 做垂直渐隐（alpha 1 → 0），并让最下层（L1）额外再淡 0.15；这样山与雪原之间是雾里淡出，而不是一刀切断。
2) **竖向杂线**：`streaks` 宽度下限提到 **4 px**，且 `contrast` 只在 0.86–1.0（现在可能到 0.6，压出黑线）；若一条 streak 的宽度 < 4 px 或长度 < 24 px，直接不画。
3) **雪帽边缘**：白/灰交界处保留 1 px 过渡（已有），但不要在**山脚渐隐区**内画雪，避免出现"浮空白条"。
4) 若层 Quad 的 UV 只覆盖到山体：把 UV 往下多给 0.06，让渐隐区有像素可用（不要留空）。

【自检打印】
"[MTN15] foot_fade_pct=18 min_streak_px=4 max_streak_contrast=1.0 stray_strokes=<n> foot_edge_grad=<f>"
硬门槛：`stray_strokes = 0`、`foot_edge_grad ≤ 0.02`（沿山脚水平扫描线相邻像素亮度梯度的 95 分位）、`mountain_luma 0.42–0.60`、`seam_max_grad ≤ 0.035`、`magenta_px = 0`。

【必须自己跑】
`capture_runtime.sh`（3 张）、`-demo-cinema`、`-demo-reveal`（`gate=pass` 保持）。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-enter-church`、`-demo-journal`、`-demo-trail 10`、`measure_shot.py`。
⚠️ 演示一律用 `bash spike/tools/run_demos.sh "<参数>"` 跑（内部已带 caffeinate 与 `open -n -a`，直接执行二进制会因窗口非激活而静默卡住）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改教堂/玩法/相机/室内/measure_shot.py。
