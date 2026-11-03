只做一件事：**教堂室内打光**——现在一进门整体偏暗偏灰，长椅两端和墙根几乎看不清，只有烛台附近亮。不改外观/玩法/相机/地形/远山/天空。

必读：`Shots/interior_inside.png`、`Shots/interior_entry.png`、`Shots/interior_lookup.png`、`ChurchInteriorBuilder.cs`（室内灯光与材质）、`ChurchInterior.cs`。

【要改】
1) **主光**：室内现在几乎没有方向光。加一盏从**西侧高窗**斜射进来的暖冷混合光（方向 ~35° 俯角、强度 1.9、色 `#E8D9C4`），让长椅与地面出现清楚的明暗分界。
2) **补光**：在长椅区上方加 **2 盏低强度点光**（强度 0.55、范围 9 m、色 `#C9D6E6`），只提暗部，不加高光。
3) **烛光范围**：烛台点光范围 4 m → **6 m**，强度不变，让地面有更大的暖色光斑。
4) **墙根与长椅端头**：把这两处材质的 `_BaseColor` 提亮 **8–12%**（不要整体提亮，保持有暗部层次）。
5) **地板**：石板 `#B9A78E` 保持，但把粗糙度从当前值下调 0.1，让窗光在地面有一点点反射（不要镜面）。

【自检打印】
"[INT_LIGHT] key=1 fill=2 key_angle=<f> nave_luma=<f> wall_luma=<f> bench_luma=<f> floor_gloss=<f> ceiling_keep=<f>"
硬门槛：
- 站立视角（interior_inside）：`nave_luma 0.16–0.30`、`wall_luma 0.12–0.26`、`bench_luma 0.10–0.22`
- 抬头视角（interior_lookup）：`ceiling_keep ≥ 0.28`（不要为了提亮把穹顶压暗）
- `magenta_px = 0`

【必须自己跑】
`-demo-enter-church`（重出 interior_entry / interior_inside / interior_lookup 三张）。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-cinema`、`-demo-reveal`（gate=pass）、`-demo-journal`、`-demo-trail 10`、`capture_runtime.sh`、`measure_shot.py`。
⚠️ 演示一律用 `bash spike/tools/run_demos.sh "<参数>"`（内含 caffeinate + `open -n -a`）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改外观/玩法/相机/地形/远山/天空/measure_shot.py。
