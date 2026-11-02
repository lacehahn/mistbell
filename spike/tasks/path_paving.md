只做一件事：把"朝圣小道"做成一条**看得见的石板路**。不改山体、天空、角色、教堂。

必读：`spike/project/Assets/Editor/SpikeScene.cs` 的 `BuildStonePath()`、`Assets/Settings/M_Stone.mat`、`Assets/Art/Textures/Stone_Pixel.png`。

【问题】
现在路面是 12 段 3.2m 宽的整块长条，材质与墙体共用，在雪地里**被压成一条白带**，读不出"石板"。

【要改】
1) 每段拆成 **2 排 × 3 块**独立石板（每块 ≈1.5×1.05 m，厚 0.09 m），块间留 **4–8 cm 缝**，每块**随机旋转 ±3°、上下 ±2 cm**，不许整条一个平面。
2) 新材质 `M_Paving`（由 M_Stone 复制）：基色压暗到 `#8C8378`，`_StoneSurface=0`（不叠雪覆盖），粗糙度 0.85；石板上表面保留 15% 积雪斑（用 Snow_Pixel 混合）。
3) 路两侧加**雪堆边**：沿路每 4 m 放一个 0.6×0.25×1.2 m 的柔和白色体块（半埋），让路从雪里"陷"下去，而不是贴在雪面上。
4) 路面高度：石板顶面比地形高 **3.5 cm**（不能悬空，也不能埋没）。

【自检打印】
"[PATH2] tiles=<n> gap_cm=4..8 rotated=true snow_banks=<n> lift_cm=3.5"
并跑 measure_shot.py。

硬门槛（不得退化）：snow_ratio ≥ 0.55、white_clip_ratio ≤ 0.05、dark_ratio_lower 0.02–0.08、ground_shadow_gap ≥ 0.06、magenta_px = 0。

【顺序】SpikeScene.Run → SpikeBuild.Run → capture_runtime.sh → measure_shot.py，四条退出码 0，并同屏打印 `FRAME md5=` 与 `MEASURE`。

不加包、不 commit、不改山体/天空/角色/教堂/measure_shot.py。
