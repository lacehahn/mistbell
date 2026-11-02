只做一件事：把远山的**纸片感**压下去——现在是三层纯色块 + 细白线雪脊，读成剪纸。不改教堂/室内/玩法/相机。

必读：`Shots/art_runtime.png`、`MountainBackdrop.cs`（三层像素山的生成与贴图）、`art-ref/target-path-hd2d.jpg`（真实参考：山体有岩理、明暗面、雪沟、林线）。

【要加（只改生成像素山的绘制）】
1) **山体不再纯色**：每层山体内部按"上暗下亮"叠 3 阶岩色（#3E4A58 / #4E5A68 / #5F6B78），并叠 **8–14 条竖向岩理**（宽 2–5 px、长度随高度、明度 ±6%），方向随峰形微倾（±8°）。
2) **每座峰分受光/背光面**：以峰顶竖线为界，左/右两侧明度差 **≥ 0.06**（光从左前方来），并在分界处画一条 1–2 px 的脊线高光。
3) **雪帽**：坡缓处厚（12–24 px）、坡陡处薄（5–8 px）；雪帽下缘加 **2–4 px 蓝灰过渡**（#C6D5E3）；从雪帽向下画 **6–12 条雪沟**（宽 2–4 px、长 20–70 px、末端渐隐）。
4) **山脚林线**：L1 底部 0–10% 高度画针叶林剪影（每 4–8 px 一个 3–6 px 高的三角，色 #26313A），L2 淡化 50%，L3 不画。
5) **新增第 4 层远景**：更淡（向 #D6E2EC 混 75%），放在 L3 之后，只画轮廓与雪脊，不用岩理。

【自检打印】
"[MTN10] layers=4 rock_levels=3 rock_streaks=<l1>/<l2> lit_shadow_delta=<f> snowcap_px=5..24 snow_gullies=<l1>/<l2> treeline=yes/half/no silhouette_peaks=<l1>/<l2>/<l3>"
硬门槛：`rock_levels=3`、`lit_shadow_delta ≥ 0.06`、`snow_gullies ≥ 6`（每层）、`snowcap_px` 覆盖 5–24、`layers=4`。

【必须自己跑】
`capture_runtime.sh`（重出 art_runtime/art_close/art_ground）、`-demo-reveal`（三张，`gate=pass` 不变）、`-demo-cinema`、`-demo-enter-church`。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`measure_shot.py`（magenta_px=0、snow_ratio ≥ 0.40）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改教堂/室内/玩法/相机/measure_shot.py。
