只做一件事：把雪地里的石头从"砖块/饼干"改成**不规则巨石**。不改山体、天空、路面、角色、教堂。

必读：`spike/project/Assets/Editor/SpikeScene.cs` 里的 `BuildWinterRock()` 与它的调用处（`BuildProps` 中 `rocks` 循环）。

【问题】
现在是 **2 环棱柱**（底环 + 顶环，顶面是平的），目标尺寸 y=.3–.8 再半埋 → 只露出一块"平板"，读成地砖/饼干。

【要改】
1) 用 **低模 icosphere（subdiv 1，42 顶点 / 80 面）** 作基础，每个顶点沿法线做 **±18% 径向抖动**，再按 `(x,y,z)` 非均匀缩放 `x,z ∈ [0.55,1.25]`、`y ∈ [0.60,1.05]`；**顶面不允许是平面**。
2) 随机旋转三段欧拉角（yaw 0–360、pitch/tilt ±12°），让没有一个面与地面平行。
3) 埋深 30–45%（改成这个区间），尺寸 0.45–1.5 m；总数 40–80、成簇 3–6 块/簇、簇间距 8–25 m（保留现有分布逻辑）。
4) 材质保持三组：岩体 `#6B6157`、背光面 `#4E463E`、朝上薄雪 `#E7EDF4`；雪的覆盖率按面法线 y>0.62 判定，**只盖朝上面**。

【自检打印】
"[ROCK2] count=<n> faces_per_rock=80 radial_jitter=0.18 burial=0.30..0.45 size=0.45..1.5 flat_top=false"
并断言：每块石头的包围盒 `size.y / max(size.x,size.z) ≥ 0.45`（不许再出现扁平板）。

【顺序】SpikeScene.Run → SpikeBuild.Run → capture_runtime.sh → measure_shot.py，四条退出码 0。
硬门槛（不得退化）：snow_ratio ≥ 0.55、white_clip_ratio ≤ 0.05、dark_ratio_lower 0.02–0.08、ground_shadow_gap ≥ 0.06、magenta_px = 0。

不加包、不 commit、不改山体/天空/路面/角色/教堂/measure_shot.py。
