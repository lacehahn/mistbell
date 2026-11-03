只做一件事：修**远山中景那块深蓝三角硬边**与**过直的棱线**（截图 `Shots/art_runtime.png`：教堂左后方一整块接近 #2F3B4A 的三角面，左上坡面棱线是直线）。不改教堂、不改玩法、不改相机、不改贴图分辨率。

必读：`Shots/art_runtime.png`、`MountainBackdrop.cs` 的 `Rock(...)` / `mix` 计算与「阴面」亮度。

【根因判断（先验证再改）】
1) 阴面亮度下限太低：`Rock(basis,false,...)` 直接压到基色 45–55%，在阴天光下读成近黑的蓝墙。
2) 脊线在低分辨率下由整数坐标直接连线 → 直线段。
3) L1/L2 的山体平铺大三角面没有中途起伏，缺少 8–15 px 的小转折。

【要改】
1) **阴面提亮**：阴面亮度比从 ~0.48 提到 **0.66–0.70**（雪原反射会照亮岩壁），并把阴面色相往 #5F6E80 靠，不要纯冷蓝。
2) **明暗差压缩**：同一座山最亮面与最暗面 Luma 差控制在 **0.18–0.26**（原来可能 >0.4）。
3) **加中途转折**：每条山脊在峰与山脚之间插入 **2–3 个次级转折点**（水平偏移 ±6–18 px、竖直 ±3–8 px），并把脊线用 2 倍超采样后降采样，消除直线段。
4) **大三角面拆解**：任一可见面片面积超过 **200 px²** 时，在其内部切一条明暗过渡（亮度差 ≤0.04），避免"整块塑料板"感。

【自检打印】
"[MTN14] shadow_ratio=<f> shade_luma=<f> face_luma_gap=<f> ridge_breaks=<n> max_face_px=<n> straight_segments=<n>"
硬门槛：`shadow_ratio ≥ 0.66`、`face_luma_gap ≤ 0.26`、`straight_segments = 0`、`mountain_luma 0.42–0.60`、`seam_max_grad ≤ 0.035`。

【必须自己跑】
`capture_runtime.sh`（3 张）、`-demo-cinema`、`-demo-reveal`（gate=pass）。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-enter-church`、`measure_shot.py`（magenta_px=0）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改教堂/玩法/相机/measure_shot.py。
